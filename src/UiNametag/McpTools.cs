using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ModelContextProtocol.Server;

namespace UiNametag;

[McpServerToolType]
public static class McpTools
{
    [McpServerTool(Name = "build_catalog"), Description("소스 폴더에서 부품 사전(AutomationId → 파일:줄)을 다시 만든다. 그 폴더가 등록 안 된 앱이면 새로 등록한다. .html/.jsx/.tsx/.vue/.svelte id, .xaml x:Name, *.Designer.cs Name을 읽는다.")]
    public static string BuildCatalog([Description("프로젝트 소스 폴더 절대 경로")] string project) { var a = Apps.BuildOrRegister(project); return $"[{a.Name}] 사전 항목 {a.Count}개"; }

    [McpServerTool(Name = "register_app"), Description("고칠 앱을 등록하고 사전을 만든다. 트레이 창의 '내 앱' 목록과 같은 목록에 들어간다.")]
    public static string RegisterApp([Description("보일 이름")] string name, [Description("소스 폴더 절대 경로")] string source,
        [Description("실행 파일 경로(선택). 웹사이트면 주소")] string exe = "", [Description("Windows 앱 / Electron / 웹사이트 (선택)")] string kind = "")
    {
        if (!Directory.Exists(source)) return $"소스 폴더 없음: {source}";
        if (Apps.List().FirstOrDefault(a => Apps.SameDir(a.Source, source)) is { } old) return $"이미 등록됨: {old.Name} (사전 {old.Count}개). 다시 만들려면 build_catalog";
        var n = Apps.Register(name, kind, exe, source);
        return $"등록: {n.Name} · 사전 {n.Count}개" + (n.Count == 0 ? " — id/x:Name을 하나도 못 찾음. 폴더 확인" : "");
    }

    [McpServerTool(Name = "list_apps"), Description("등록한 앱 목록(이름·종류·실행 파일·소스 폴더·사전 항목 수·상태).")]
    public static string ListApps() => string.Join("\n", Apps.List().Select(a => $"{a.Name}\t{a.Kind}\t{a.Exe}\t{a.Source}\t사전 {a.Count}개\t{Apps.Status(a).Text}")) is { Length: > 0 } s ? s : "등록한 앱 없음";

    [McpServerTool(Name = "get_picks"), Description("사용자가 화면에서 고른 부품 대기열(메모·이동 목표·그림·캡처 경로 포함)을 가져온다. 기본은 아직 안 가져간 것만. Action=shot은 부품이 아니라 사용자가 드래그로 찍은 화면 캡처이니 Shot 경로의 그림을 꼭 열어 볼 것. 읽으면 '가져감'이 되고, 실제로 고친 뒤에는 mark_done으로 '고침'을 표시할 것.")]
    public static string GetPicks([Description("true면 이미 가져간 것도 전부")] bool all = false)
    {
        var picks = Store.TakeNew(all);
        var warn = string.Join("\n", picks.Where(p => p.Target.Outside || p.Move?.Anchor.Outside == true)
            .Select(p => $"⚠ #{p.No}: 사전 밖 앱 [{p.Target.App}] \"{p.Target.Window}\" 에서 고름 — 이 프로젝트 코드가 아닐 수 있음. 사용자에게 확인할 것."));
        return (warn.Length > 0 ? warn + "\n\n" : "") + JsonSerializer.Serialize(picks, Catalog.Json);
    }

    [McpServerTool(Name = "mark_done"), Description("get_picks로 받은 항목을 실제로 고친 뒤 번호들을 '고침 완료'로 표시한다. 사용자 대기열 화면에 보인다.")]
    public static string MarkDone([Description("고친 항목 번호들 (No)")] int[] numbers) => $"고침 표시 {Store.MarkDone(numbers)}건";

    [McpServerTool(Name = "catalog_lookup"), Description("AutomationId나 이름 일부로 등록한 앱들의 사전에서 파일:줄을 찾는다. 파일은 그 앱 소스 폴더 기준.")]
    public static string CatalogLookup(string query, [Description("앱 이름 일부(선택)")] string? app = null) =>
        string.Join("\n", Catalog.Lookup(query, app).Take(30).Select(x => $"[{x.App.Name}] {x.E.Id}\t<{x.E.Tag}>\t{x.E.File}:{x.E.Line}\t({x.App.Source})")) is { Length: > 0 } s ? s : "없음";

    [McpServerTool(Name = "check_layout"), Description("실제 화면에서 AutomationId 부품의 위치를 읽어 목표 사각형(물리 픽셀 x,y,w,h)과 겹침 비율(IoU, 0~100%)을 낸다. 80% 이상이면 통과.")]
    public static string CheckLayout(string automationId, double x, double y, double w, double h)
    {
        var el = Catalog.FindById(automationId);
        if (el == null) return $"{automationId} 부품을 화면에서 못 찾음";
        var r = el.Current.BoundingRectangle;
        var t = new Rect(x, y, w, h);
        var i = Rect.Intersect(r, t);
        var inter = i.IsEmpty ? 0 : i.Width * i.Height;
        var iou = inter / (r.Width * r.Height + w * h - inter) * 100;
        return $"실제 위치 [{string.Join(", ", Store.R(r))}] · 목표 [{x}, {y}, {w}, {h}] · 겹침 {iou:F0}% → {(iou >= 80 ? "통과" : "다름")}";
    }

    [McpServerTool(Name = "preview_plan"), Description("코드를 고치기 전에, AI가 이해한 결과를 앱 위에 보라 점선 사각형+설명으로 그려 사용자에게 보여 주고 [맞아]/[다시] 답을 기다린다. 좌표는 물리 픽셀.")]
    public static async Task<string> PreviewPlan(double x, double y, double w, double h, [Description("보여 줄 설명")] string label,
        [Description("옮기기 전 위치(선택) x,y,w,h — X 표시")] double[]? from = null, int timeoutSeconds = 120) =>
        await Ui.Invoke(() => Preview.Show([x, y, w, h], from, label, timeoutSeconds));

    [McpServerTool(Name = "get_recording"), Description("사용자가 보낸 녹화 묶음을 가져온다: 일어난 일 표(시각·클릭한 부품·코드 위치·상태 변화) + 그 순간 장면 그림 경로. 영상 전체는 보내지 않는다.")]
    public static string GetRecording([Description("rec_ id, 비우면 가장 최근")] string? id = null)
    {
        var r = Store.LoadRec(id);
        if (r == null) return "녹화 없음";
        var dir = Directory.CreateDirectory(System.IO.Path.Combine(Catalog.DataDir, r.Id)).FullName;
        var sb = new StringBuilder($"{r.Id} · 창 \"{r.Window}\" · {r.Seconds}초 · 영상 {r.Video}\n메모: {r.Memo}\n\n");
        var shots = new List<string>();
        foreach (var e in r.Events)
        {
            var loc = e.Target == null ? "" : $"{e.Target.Label} id={e.Target.Id}";
            sb.AppendLine($"{e.T,6:F2}s {e.What,-5} {loc} {e.State}");
            if (e.What == "Click" && shots.Count < 6)
            {
                var f = System.IO.Path.Combine(dir, $"t{e.T:F2}.png");
                Store.Ffmpeg($"-ss {e.T + 0.1:F2} -i \"{r.Video}\" -frames:v 1 \"{f}\"");
                // 영상이 클릭보다 늦게 시작해 그 시각 장면이 없으면 앞에서 가장 가까운 장면
                if (!File.Exists(f)) Store.Ffmpeg($"-sseof -0.5 -i \"{r.Video}\" -frames:v 1 \"{f}\"");
                if (File.Exists(f)) shots.Add(f);
            }
        }
        var last = System.IO.Path.Combine(dir, "end.png");
        Store.Ffmpeg($"-sseof -0.3 -i \"{r.Video}\" -frames:v 1 \"{last}\"");
        if (File.Exists(last)) shots.Add(last);
        sb.AppendLine("\n장면: " + string.Join(" | ", shots));
        return sb.ToString();
    }
}

// MCP 모드에서 화면을 그리기 위한 WPF 스레드
public static class Ui
{
    static Dispatcher? _d;
    public static Task<T> Invoke<T>(Func<Task<T>> f)
    {
        if (_d == null)
        {
            var ready = new ManualResetEventSlim();
            var th = new Thread(() => { _d = Dispatcher.CurrentDispatcher; ready.Set(); Dispatcher.Run(); }) { IsBackground = true };
            th.SetApartmentState(ApartmentState.STA); th.Start(); ready.Wait();
        }
        return _d!.InvokeAsync(f).Task.Unwrap();
    }
}

// AI가 이해한 결과를 보라 점선으로 보여 주는 창
public static class Preview
{
    public static Task<string> Show(double[] r, double[]? from, string label, int timeout)
    {
        var tcs = new TaskCompletionSource<string>();
        var o = new Overlay { Title = "UiNametag preview" };
        var purple = (SolidColorBrush)new BrushConverter().ConvertFrom("#a371f7")!;
        var box = new Rectangle { Stroke = purple, StrokeThickness = 2.5, StrokeDashArray = [5, 3], Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xa3, 0x71, 0xf7)) };
        o.Canvas.Children.Add(box);
        o.Loaded += (_, _) =>
        {
            Native.ClickThrough(o.Hwnd, false);
            o.Place(box, r, 3);
            o.Say(o.Tag, "AI 이해: " + label, r[0], r[1]);
            o.Tag.Background = purple;
            if (from != null)
            {
                var x = new TextBlock { Text = "✕", FontSize = 30, FontWeight = FontWeights.Black, Foreground = purple };
                o.Canvas.Children.Add(x); o.Place(x, [from[0] + from[2] / 2 - 12, from[1] + from[3] / 2 - 22, 30, 44]);
            }
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (t, c, v) in new[] { ("맞아 (OK)", "#1a7f37", "ok"), ("아니야, 다시", "#888888", "retry") })
            {
                var b = new Button { Content = t, Foreground = Brushes.White, Background = (Brush)new BrushConverter().ConvertFrom(c)!, Padding = new(12, 5, 12, 5), Margin = new(0, 0, 8, 0), FontFamily = new("Malgun Gothic"), FontWeight = FontWeights.Bold };
                b.Click += (_, _) => { tcs.TrySetResult(v); o.Close(); };
                row.Children.Add(b);
            }
            o.Canvas.Children.Add(row);
            Canvas.SetLeft(row, Canvas.GetLeft(box)); Canvas.SetTop(row, Canvas.GetTop(box) + box.Height + 8);
        };
        o.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(timeout) };
        timer.Tick += (_, _) => { timer.Stop(); tcs.TrySetResult("timeout"); o.Close(); };
        timer.Start();
        return tcs.Task.ContinueWith(t => t.Result switch
        {
            "ok" => "사용자: 맞아 (OK) → 코드 고쳐도 됨",
            "retry" => "사용자: 아니야, 다시 → 계획을 고치거나 사용자에게 다시 물어볼 것",
            _ => "답 없음 (시간 초과)",
        });
    }
}
