using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace UiNametag;

// 완료 기준을 프로그램이 직접 읽어 판정한다 (personal/PLAN.md 표와 같음)
public static class Selftest
{
    static readonly List<(string Step, bool Ok, string Detail)> Rows = [];
    static void Row(string s, bool ok, string d) { Rows.Add((s, ok, d)); Console.WriteLine($"{(ok ? "PASS" : "FAIL")} | {s} | {d}"); }

    public static async Task<int> Run(string project)
    {
        try { await Steps(project); }
        catch (SkipException e) { Console.WriteLine($"SKIP | 2~5 IINA 시험 | {e.Message}"); }
        catch (Exception e) { Row("예외", false, e.GetType().Name + ": " + e.Message); }
        finally { Clean(null); } // 중간에 멈춰도 시험 흔적·사용자 읽음 표시를 되돌린다
        Console.WriteLine($"\n{Rows.Count(r => r.Ok)}/{Rows.Count} 통과");
        return Rows.All(r => r.Ok) ? 0 : 1;
    }

    class SkipException(string m) : Exception(m);

    static Dictionary<int, (bool Delivered, bool Done)> _userState = [];

    static async Task Steps(string project)
    {
        _userState = Store.Picks().ToDictionary(p => p.No, p => (p.Delivered, p.Done));
        // 1 사전
        var n = Catalog.Build(project);
        var s = Catalog.Lookup("settings").Select(x => x.E).FirstOrDefault(e => e.File.EndsWith("welcome.html"));
        Row("1 사전", s?.Line == 50, $"항목 {n}개 · settings → {s?.File}:{s?.Line}");

        var picker = new Picker();
        var exe = Environment.ProcessPath!;
        await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new() { Command = exe, Arguments = ["mcp"], Name = "self" }));

        // 사용자가 IINA로 영상을 보고 있으면 IINA를 건드리는 시험(2~5)은 건너뛴다
        if (Process.GetProcessesByName("IINA").Any(x => x.MainWindowHandle != IntPtr.Zero && x.MainWindowTitle != "IINA"))
        {
            Console.WriteLine("SKIP | 2~5 IINA 시험 | 사용자가 IINA 사용 중 — 건드리지 않음");
            await Extra(mcp, picker);
            Clean(null);
            return;
        }

        // 2 picker 이름 찾기 (투명 레이어를 띄운 채로 마우스 아래 부품을 읽는다)
        var win = await IinaWindow();
        var settings = win.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "settings"));
        var c = Center(settings.Current.BoundingRectangle);
        Front(new IntPtr(win.Current.NativeWindowHandle));
        picker.FakeCursor = c;
        picker.TestSelect();
        await Task.Delay(800);
        var hit = picker.Current;
        if (hit == null) try { await Task.Run(() => Catalog.Resolve(c)); } catch (Exception ex) { Console.WriteLine("DEBUG " + ex); }
        Row("2 picker", hit?.Entry?.Line == 50 && hit.Entry.File.EndsWith("welcome.html"), hit?.Label ?? "못 읽음");
        picker.Exit();

        // 3 고르기 → MCP
        var pick = Store.Add("note", Store.ToTarget(hit!), "selftest 간격 좁게");
        var picks = await Call(mcp, "get_picks", new() { ["all"] = true });
        var look = await Call(mcp, "catalog_lookup", new() { ["query"] = "settings" });
        Row("3 get_picks", picks.Contains("selftest 간격 좁게") && picks.Contains("welcome.html"), $"pick #{pick.No} 받음={picks.Contains("selftest")}");
        Row("3 catalog_lookup", look.Contains("welcome.html:50"), look.Split('\n')[0]);

        // 4 이동 · check_layout · preview_plan
        var recent = win.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "recent"));
        var rr = Store.R(recent.Current.BoundingRectangle);
        var anchor = Catalog.Resolve(Center(recent.Current.BoundingRectangle))!;
        var mv = Store.Add("move", Store.ToTarget(hit!), "selftest 이동", new Move(Store.ToTarget(anchor), "앞", rr));
        var sr = Store.R(settings.Current.BoundingRectangle);
        var same = await Call(mcp, "check_layout", new() { ["automationId"] = "settings", ["x"] = sr[0], ["y"] = sr[1], ["w"] = sr[2], ["h"] = sr[3] });
        var off = await Call(mcp, "check_layout", new() { ["automationId"] = "settings", ["x"] = sr[0] + sr[2], ["y"] = sr[1], ["w"] = sr[2], ["h"] = sr[3] });
        Row("4 move 저장", Store.Picks().Any(p => p.No == mv.No && p.Move?.Anchor.Id == "recent"), $"anchor={anchor.Id} {anchor.Entry?.File}:{anchor.Entry?.Line}");
        Row("4 check_layout 같음", same.Contains("겹침 100%"), same);
        Row("4 check_layout 비킴", off.Contains("겹침 0%"), off);
        var prev = Call(mcp, "preview_plan", new() { ["x"] = rr[0], ["y"] = rr[1], ["w"] = sr[2], ["h"] = sr[3], ["label"] = "selftest", ["timeoutSeconds"] = 5 });
        await Task.Delay(2500);
        var pw = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "UiNametag preview"));
        var prevRes = await prev;
        Row("4 preview_plan 창", pw != null && prevRes.Contains("시간 초과"), $"창 보임={pw != null} · 답={prevRes}");

        await Extra(mcp, picker);

        // 5 녹화: IINA 창을 녹화하며 눌러도 아무 일 없는 글자(Text)를 실제로 클릭 (클릭은 picker 훅이 기록)
        // 목록·버튼은 누르면 영상이 열리거나 기록이 바뀐다 → 절대 누르지 않는다 (2026-10-20 사고)
        var hwnd = new IntPtr(win.Current.NativeWindowHandle);
        picker.Rec.Start(hwnd);
        await Task.Delay(1000);
        Front(hwnd);
        var label = win.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        var rc = Center(label.Current.BoundingRectangle);
        if (Catalog.Resolve(rc)?.Kind != "Text") throw new InvalidOperationException("클릭 지점이 글자가 아님 — 클릭 시험 중단");
        // 클릭 바로 직전: 그 자리 맨 위 창이 IINA가 아니면(게임 등 다른 창이 덮음) 누르지 않는다
        Native.GetWindowThreadProcessId(Native.GetAncestor(Native.WindowFromPoint(new Native.POINT { X = (int)rc.X, Y = (int)rc.Y }), 2), out var topPid);
        if (Process.GetProcessById((int)topPid).ProcessName != "IINA") throw new SkipException("클릭 자리에 IINA가 아닌 창이 있음");
        picker.Fake(Native.WM_LBUTTONDOWN, rc); // 가짜 클릭: 녹화 기록 길로만 들어가고 IINA엔 안 감
        await Task.Delay(1800);
        var rec = picker.Rec.Stop("selftest 녹화");
        var size = File.Exists(rec.Video) ? new FileInfo(rec.Video).Length : 0;
        var dur = Probe(rec.Video);
        Row("5 녹화 파일", size > 0 && dur > 1, $"{Path.GetFileName(rec.Video)} {size / 1024}KB {dur:F1}초");
        Row("5 클릭 기록", rec.Events.Any(e => e.What == "Click" && e.Target?.Kind == "Text"), string.Join(" / ", rec.Events.Select(e => $"{e.T}s {e.What} {e.Target?.Id} {e.State}")));
        var gr = await Call(mcp, "get_recording", new() { ["id"] = rec.Id });
        var paths = gr.Split("장면: ").Last().Trim().Split(" | ", StringSplitOptions.RemoveEmptyEntries);
        var shots = paths.Where(File.Exists).ToList();
        // 클릭 장면(t*.png)과 끝 장면(end.png)이 둘 다 있어야 함
        Row("5 get_recording", shots.Any(f => Path.GetFileName(f).StartsWith('t')) && shots.Any(f => f.EndsWith("end.png")), $"장면 {shots.Count}장: {string.Join(", ", paths.Select(Path.GetFileName))}");

        // 녹화 미리보기: 대기열 녹화 칸에 영상 가운데 장면 그림이 뜨고(▶), 그림 파일이 만들어진다
        picker.ShowMain("queue"); await Task.Delay(800);
        var qw = Application.Current.Windows.OfType<MainWindow>().First();
        qw.Go("queue"); qw.UpdateLayout();
        var rrow = ((IEnumerable<RecRow>)qw.RecsList.ItemsSource).FirstOrDefault(x => x.R.Id == rec.Id);
        var thumbFile = Path.Combine(Catalog.DataDir, rec.Id + "_thumb.png");
        var shown = FindAll<System.Windows.Controls.Image>(qw.RecsList).Any(i => i.IsVisible && i.Source != null && i.ActualWidth > 30 && i.ActualHeight > 60);
        int tw = 0; bool notBlack = false;
        if (File.Exists(thumbFile)) { using var tb = new System.Drawing.Bitmap(thumbFile); tw = tb.Width; for (int yy = 0; yy < tb.Height && !notBlack; yy += 4) for (int xx = 0; xx < tb.Width; xx += 4) { var px0 = tb.GetPixel(xx, yy); if (px0.R + px0.G + px0.B > 60) { notBlack = true; break; } } }
        Row("5 녹화 미리보기", rrow?.Thumb != null && shown && tw == 280 && notBlack, $"미리보기 그림 {(File.Exists(thumbFile) ? Path.GetFileName(thumbFile) : "없음")} 폭 {tw} · 화면에 보임 {shown} · 검은 화면 아님 {notBlack}");
        qw.Close();

        Clean(rec);
    }

    static async Task Extra(McpClient mcp, Picker picker)
    {
        // 6 사전 밖 앱: 작업 표시줄(explorer)을 고르면 경고가 붙는다
        var tb = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        var tbr = tb.Current.BoundingRectangle;
        var outHit = Catalog.Resolve(new Point(tbr.X + tbr.Width / 2, tbr.Y + tbr.Height / 2));
        Row("6 사전 밖 앱 표시", outHit?.Outside == true && outHit.Label.Contains("사전 밖"), $"{outHit?.App} · {outHit?.Label}");
        var outPick = Store.Add("note", Store.ToTarget(outHit!), "selftest 밖");
        var gp = await Call(mcp, "get_picks", new() { ["all"] = false });
        Row("6 get_picks 경고", gp.Contains($"⚠ #{outPick.No}: 사전 밖 앱"), gp.Split('\n')[0]);

        // 7 메모 창: 화면 맨 아래 부품이어도 화면 안에 뜨고, Esc로 닫힌다
        var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var memo = new MemoWindow("selftest", ["저장", "취소"]) { Anchor = [wa.Left + 100, wa.Bottom - 30, 80, 24] };
        picker.TestMemo(memo);
        await Task.Delay(500);
        var ds = System.Windows.Media.VisualTreeHelper.GetDpi(memo).DpiScaleX;
        var inside = memo.Top >= wa.Top / ds - 1 && memo.Top + memo.ActualHeight <= wa.Bottom / ds + 1;
        Row("7 메모 창 화면 안", inside, $"창 {memo.Top:F0}~{memo.Top + memo.ActualHeight:F0} · 작업 영역 {wa.Top / ds:F0}~{wa.Bottom / ds:F0}");
        // 버튼이 창 안쪽에 다 들어오는지 (잘려 보이던 문제)
        var lastBtn = FindAll<System.Windows.Controls.Button>(memo).Last();
        var bBottom = lastBtn.TranslatePoint(new Point(0, lastBtn.ActualHeight), memo).Y;
        var bRight = lastBtn.TranslatePoint(new Point(lastBtn.ActualWidth, 0), memo).X;
        var content = (FrameworkElement)memo.Content;
        Row("7 메모 창 버튼 안 잘림", bBottom <= content.ActualHeight + 20 && bRight <= memo.ActualWidth, $"버튼 아래 {bBottom:F0} · 내용 높이 {content.ActualHeight:F0} · 버튼 오른쪽 {bRight:F0} · 창 폭 {memo.ActualWidth:F0}");
        var closed = false; memo.Closed += (_, _) => closed = true;
        picker.FakeEsc();
        await Task.Delay(500);
        Row("7 메모 창 Esc", closed, closed ? "닫힘" : "안 닫힘");
        if (!closed) memo.Close();
        Row("8 트레이 아이콘", picker.Tray.Visible && picker.Tray.Text.Contains("켜짐"), picker.Tray.Text);

        // 9 메인 창: 자기 앱이 등록돼 있고, 자기 창 부품을 MainWindow.xaml:줄로 읽는다
        var self = Apps.List().FirstOrDefault(a => a.Name.StartsWith("UiNametag"));
        Row("9 자기 앱 등록", self?.Count > 0, $"{self?.Name} · 사전 {self?.Count}개 · {self?.Source}");
        picker.ShowMain("apps");
        await Task.Delay(1000);
        var mw = Application.Current.Windows.OfType<MainWindow>().First();
        var btn = mw.NewAppButton;
        var bp = btn.PointToScreen(new Point(btn.ActualWidth / 2, btn.ActualHeight / 2));
        var meHit = await Task.Run(() => Catalog.Resolve(bp)); // 내 창 UI 스레드를 막지 않게 다른 스레드에서
        var want = self == null ? 0 : File.ReadAllLines(Path.Combine(self.Source, "MainWindow.xaml")).ToList().FindIndex(l => l.Contains("x:Name=\"NewAppButton\"")) + 1;
        Row("9 자기 창 읽기", meHit?.Entry?.File == "MainWindow.xaml" && meHit.Entry.Line == want, $"{meHit?.Label} (기대 MainWindow.xaml:{want})");
        mw.Go("queue"); var q = mw.QueuePage.IsVisible;
        var withShot = ((IEnumerable<PickRow>)mw.PicksList.ItemsSource).Count(r => r.Thumb != null);
        var cc = MainWindow.ClaudeCommand();
        Row("9 Claude 연동 명령", cc == null ? !Store.Picks().Any(p => !p.Done) : cc.Value.Args.Contains("get_picks") && cc.Value.Args.Contains("mark_done") && Directory.Exists(cc.Value.Dir), cc?.Args.Length > 0 ? $"wt {cc.Value.Args[..Math.Min(90, cc.Value.Args.Length)]}…" : "고칠 항목 없음");
        var oldClip = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        mw.CopyPromptButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        var clip = Clipboard.ContainsText() ? Clipboard.GetText() : "";
        Row("9 지시문 복사", !Store.Picks().Any(p => !p.Done) || (clip.Contains("uinametag MCP") && clip.Contains("mark_done")), clip.Length > 50 ? clip[..50] + "…" : clip);

        // 체크한 것만 보내기: 첫 체크 칸을 켜고 복사 → 그 번호 하나만 들어가야 함
        mw.UpdateLayout();
        var boxes = FindAll<System.Windows.Controls.CheckBox>(mw.PicksList).Where(c => c.IsVisible).ToList();
        Row("9 체크 칸 보임", boxes.Count == Store.Picks().Count(p => !p.Done), $"체크 칸 {boxes.Count}개");
        if (boxes.Count > 0)
        {
            var row = (PickRow)boxes[0].DataContext;
            boxes[0].IsChecked = true;
            boxes[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            mw.CopyPromptButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            var one = Clipboard.ContainsText() ? Clipboard.GetText() : "";
            Row("9 체크한 것만", one.Contains($"(번호 {row.P.No})") && (mw.CopyPromptButton.Content as string)!.Contains("(1)"), $"체크 #{row.P.No} → {(one.Length > 50 ? one[..50] + "…" : one)} · 버튼 \"{mw.CopyPromptButton.Content}\"");
            boxes[0].IsChecked = false;
        }

        // 녹화도 체크해서 보내기 → MCP mark_done(recordings)로 '고침 완료'
        var fake = new Recording(Store.NewRecId() + "_st", "", "selftest", [0, 0, 10, 10], DateTime.Now, 1, [], "selftest 녹화 보내기");
        Store.SaveRec(fake);
        mw.Go("queue"); mw.UpdateLayout();
        var recBox = FindAll<System.Windows.Controls.CheckBox>(mw.RecsList).FirstOrDefault(c => c.DataContext is RecRow rr && rr.R.Id == fake.Id);
        if (recBox != null) { recBox.IsChecked = true; recBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
        mw.CopyPromptButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        var rc = Clipboard.ContainsText() ? Clipboard.GetText() : "";
        var md = await Call(mcp, "mark_done", new() { ["recordings"] = new[] { fake.Id } });
        var done = Store.LoadRec(fake.Id)?.Done == true;
        Row("9 녹화 보내기", recBox != null && rc.Contains("get_recording") && rc.Contains(fake.Id) && !rc.Contains("get_picks") && done, $"체크 칸 {recBox != null} · {(rc.Length > 60 ? rc[..60] + "…" : rc)} · {md}");
        File.Delete(Path.Combine(Catalog.DataDir, fake.Id + ".json"));

        // 버튼 위치·반응형: 글자가 바뀌어도 옆 버튼이 안 움직이고, 좁은 창에선 줄바꿈되어 안 잘리고, 넓은 창에선 넓어진다
        mw.Go("queue"); mw.Width = 960; mw.UpdateLayout();
        Point Pos(FrameworkElement e) => e.TranslatePoint(new Point(0, 0), mw.QueueBody);
        var btnBefore = Pos(mw.ClearTakenButton);
        var listBefore = Pos(mw.PicksList);
        mw.CopyPromptButton.Content = "지시문 복사 (12)"; mw.AskClaudeButton.Content = "Claude에게 고치게 하기 (12)";
        mw.QueueHint.Text = new string('가', 300); mw.UpdateLayout();
        var still = Pos(mw.ClearTakenButton) == btnBefore && Pos(mw.PicksList) == listBefore;
        mw.Width = 720; mw.UpdateLayout();
        var fits = new FrameworkElement[] { mw.AskClaudeButton, mw.CopyPromptButton, mw.ClearTakenButton, mw.OpenQueueFolderButton }
            .All(b => Pos(b).X + b.ActualWidth <= mw.QueueBody.ActualWidth + 1);
        var narrow = mw.QueueBody.ActualWidth;
        mw.Width = 1400; mw.UpdateLayout();
        var wide = mw.QueueBody.ActualWidth;
        Row("9 대기열 반응형", still && fits && wide > narrow + 300, $"글자 바뀌어도 그대로 {still} · 720폭 버튼 안 잘림 {fits} · 본문 폭 {narrow:F0}→{wide:F0}");
        mw.Width = 960;
        if (oldClip != null) Clipboard.SetText(oldClip); else Clipboard.Clear(); // 사용자 클립보드 되돌림
        Row("9 대기열 그림 미리보기", withShot == Store.Picks().Count(p => p.Shot != null && File.Exists(p.Shot)), $"그림 칸 {withShot}개");
        mw.Go("settings"); var st = mw.SettingsPage.IsVisible && mw.McpCommandBox.Text.Contains(" mcp");
        mw.Go("register"); await Task.Delay(300);
        var reg = mw.RegisterPanel.IsVisible && mw.WindowsList.Items.Count > 0;
        Row("9 메인 창 화면 전환", q && st && reg, $"대기열 {q} · 설정 {st} · 등록(창 목록 {mw.WindowsList.Items.Count}개) {reg}");
        mw.Close();
        picker.TogglePopup(); await Task.Delay(500);
        var pop = Application.Current.Windows.OfType<TrayPopup>().FirstOrDefault();
        var popApps = pop?.PopupApps.Items.Count ?? 0;
        Row("9 트레이 팝업", pop?.IsVisible == true && popApps == Math.Min(4, Apps.List().Count), $"보임 {pop?.IsVisible} · 앱 {popApps}개 · {pop?.StatusText.Text}");
        pop?.Hide();

        // 10 창 고르기 모드: 실제 클릭 한 번 → 그 자리 창을 돌려주고, 클릭은 아래 앱에 안 감(훅이 막음)
        var wp = new Point(wa.Left + wa.Width / 2, wa.Top + wa.Height / 2);
        var got = new TaskCompletionSource<IntPtr>();
        picker.PickWindow(h => got.TrySetResult(h));
        await Task.Delay(400);
        // 클릭 직전에 그 자리 창을 읽는다 (앞 단계에서 앞에 오는 창이 바뀔 수 있음)
        var expect = Native.GetAncestor(Native.WindowFromPoint(new Native.POINT { X = (int)wp.X, Y = (int)wp.Y }), 2);
        picker.Fake(Native.WM_LBUTTONDOWN, wp);
        var res = await Task.WhenAny(got.Task, Task.Delay(3000)) == got.Task ? got.Task.Result : IntPtr.Zero;
        Native.GetWindowThreadProcessId(res, out var wpid);
        string wname = ""; try { wname = Process.GetProcessById((int)wpid).ProcessName; } catch { }
        Row("10 창 고르기 클릭", res != IntPtr.Zero && res == expect, $"고른 창 {res} ({wname}) · 기대 {expect}");
        got = new TaskCompletionSource<IntPtr>();
        picker.PickWindow(h => got.TrySetResult(h)); await Task.Delay(200);
        picker.FakeEsc();
        var esc = await Task.WhenAny(got.Task, Task.Delay(2000)) == got.Task && got.Task.Result == IntPtr.Zero;
        Row("10 창 고르기 Esc 취소", esc, esc ? "취소됨(빈 값)" : "취소 안 됨");

        // 11 영역 캡처: 맨 위에 뜬 창(보라색)까지 찍혀 대기열에 shot으로 들어간다
        var x0 = (int)(wa.Left + wa.Width / 2 - 150); var y0 = (int)(wa.Top + wa.Height / 2 - 100);
        var top = new Window { WindowStyle = WindowStyle.None, Topmost = true, ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.Magenta };
        top.SourceInitialized += (_, _) => { var s2 = System.Windows.Media.VisualTreeHelper.GetDpi(top).DpiScaleX; top.Left = (x0 + 100) / s2; top.Top = (y0 + 60) / s2; top.Width = 100 / s2; top.Height = 80 / s2; };
        top.Show(); await Task.Delay(400);
        picker.StartArea(); await Task.Delay(300);
        var before = Store.Picks().Count;
        picker.Fake(Native.WM_LBUTTONDOWN, new Point(x0, y0));
        for (int k = 1; k <= 5; k++) { picker.Fake(Native.WM_MOUSEMOVE, new Point(x0 + 60 * k, y0 + 40 * k)); await Task.Delay(30); }
        picker.Fake(Native.WM_LBUTTONUP, new Point(x0 + 300, y0 + 200));
        MemoWindow? cap = null;
        for (int i = 0; i < 60 && cap == null; i++) { await Task.Delay(200); cap = Application.Current.Windows.OfType<MemoWindow>().FirstOrDefault(m => m.Title.Length > 0 && m.IsVisible); }
        if (cap != null) { cap.Box.Text = "selftest 캡처"; cap.Result = "보내기"; cap.Close(); }
        await Task.Delay(500);
        top.Close();
        var shotPick = Store.Picks().LastOrDefault(p => p.Action == "shot");
        var magenta = false; var size = "";
        if (shotPick?.Shot is { } sp && File.Exists(sp))
        {
            using var bmp = new System.Drawing.Bitmap(sp);
            size = $"{bmp.Width}×{bmp.Height}";
            var px = bmp.GetPixel(150, 100);
            magenta = px.R > 200 && px.G < 60 && px.B > 200;
        }
        Row("11 영역 캡처", cap != null && Store.Picks().Count == before + 1 && shotPick != null, $"메모 창 {(cap != null)} · {shotPick?.Target.Label} · {shotPick?.Shot}");
        Row("11 위에 뜬 창도 찍힘", magenta && size == "300×200", $"그림 {size} · 가운데 보라색 {magenta}");

        // 12 그리기: 부품 밖에 그린 선도 사진에 들어간다 (부품+선을 감싸게, 선이 보이는 채로 찍음)
        // 작은 부품(100×40 창)을 띄워 고르고, 그 밖으로 멀리(오른쪽 아래 500·300px) 선을 긋는다
        var part = new Point(wa.Left + 250, wa.Top + 220);
        var far = new Point(wa.Left + 700, wa.Top + 500);
        var small = new Window { WindowStyle = WindowStyle.None, Topmost = true, ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.SteelBlue, ShowActivated = false };
        small.SourceInitialized += (_, _) => { var s3 = System.Windows.Media.VisualTreeHelper.GetDpi(small).DpiScaleX; small.Left = (part.X - 50) / s3; small.Top = (part.Y - 20) / s3; small.Width = 100 / s3; small.Height = 40 / s3; };
        small.Show(); await Task.Delay(400);
        picker.FakeCursor = part; picker.TestSelect(); await Task.Delay(800);
        var partRect = picker.Current?.Rect;
        picker.Fake(Native.WM_LBUTTONDOWN, part);
        MemoWindow? m1 = null;
        for (int i = 0; i < 30 && m1 == null; i++) { await Task.Delay(200); m1 = Application.Current.Windows.OfType<MemoWindow>().FirstOrDefault(m => m.IsVisible); }
        if (m1 != null) { m1.Result = "그리기"; m1.Close(); }
        await Task.Delay(400);
        picker.Fake(Native.WM_LBUTTONDOWN, far);
        for (int k = 0; k <= 10; k++) { picker.Fake(Native.WM_MOUSEMOVE, new Point(far.X + 15 * k, far.Y + 8 * k)); await Task.Delay(20); }
        picker.Fake(Native.WM_LBUTTONUP, new Point(far.X + 150, far.Y + 80));
        await Task.Delay(100);
        picker.Fake(Native.WM_LBUTTONDOWN, new Point(far.X, far.Y + 200)); // 손 떼고 다른 곳에 한 번 더
        for (int k = 0; k <= 5; k++) { picker.Fake(Native.WM_MOUSEMOVE, new Point(far.X + 20 * k, far.Y + 200)); await Task.Delay(20); }
        picker.Fake(Native.WM_LBUTTONUP, new Point(far.X + 100, far.Y + 200));
        await Task.Delay(200);
        Row("12 뗐다 그리면 새 선", picker.PenLines == 2, $"선 {picker.PenLines}개 (기대 2)");
        picker.Fake(Native.WM_RBUTTONDOWN, far);
        MemoWindow? m2 = null;
        for (int i = 0; i < 30 && m2 == null; i++) { await Task.Delay(200); m2 = Application.Current.Windows.OfType<MemoWindow>().FirstOrDefault(m => m.IsVisible); }
        if (m2 != null) { m2.Box.Text = "selftest 그리기"; m2.Result = "저장"; m2.Close(); }
        Pick? dp = null;
        for (int i = 0; i < 40 && dp == null; i++) { await Task.Delay(250); dp = Store.Picks().LastOrDefault(p => p.Action == "draw" && p.Memo == "selftest 그리기"); }
        var red = 0; var dsize = "";
        if (dp?.Shot is { } dsp && File.Exists(dsp))
        {
            using var bmp = new System.Drawing.Bitmap(dsp);
            dsize = $"{bmp.Width}×{bmp.Height}";
            // 부품 오른쪽 끝(사진 안 위치)보다 바깥쪽에 있는 빨간 점만 센다 = 부품 밖 선
            var outX = (int)((partRect?.Width ?? 0) + 40 + 10);
            for (int yy = 0; yy < bmp.Height; yy += 2) for (int xx = outX; xx < bmp.Width; xx += 2) { var px = bmp.GetPixel(xx, yy); if (px.R > 200 && px.G < 110 && px.B < 110) red++; }
        }
        small.Close();
        // 사진이 부품(100×40)보다 훨씬 커져서 선 끝(far+150, far+200)까지 덮어야 함
        var small1 = partRect is { } pr && pr.Width <= 120 && pr.Height <= 60;
        var covers = dp != null && dsize.Length > 0 && int.Parse(dsize.Split('×')[0]) >= far.X + 150 - part.X && int.Parse(dsize.Split('×')[1]) >= far.Y + 200 - part.Y;
        Row("12 부품 밖 선까지 사진에", dp != null && small1 && covers && red > 20, $"부품 {Store.R(partRect ?? Rect.Empty).Aggregate("", (a, v) => a + v + " ")}· 사진 {dsize} · 부품 밖 빨간 점 {red}개");
    }

    static IEnumerable<T> FindAll<T>(DependencyObject d) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = System.Windows.Media.VisualTreeHelper.GetChild(d, i);
            if (c is T t) yield return t;
            foreach (var x in FindAll<T>(c)) yield return x;
        }
    }

    // 시험 흔적 지우기
    static void Clean(Recording? rec)
    {
        var all = Store.Picks();
        // 시험이 get_picks로 읽으며 사용자 것까지 '가져감'으로 바꾼 것을 되돌린다
        var keep = all.Where(p => !p.Memo.StartsWith("selftest"))
            .Select(p => _userState.TryGetValue(p.No, out var st) ? p with { Delivered = st.Delivered, Done = st.Done } : p).ToList();
        // 지우는 그림은 시험이 만든 것(메모가 selftest로 시작)만 — 사용자 캡처는 절대 안 지움 (2026-10-20 사고)
        foreach (var p in all.Where(p => p.Memo.StartsWith("selftest") && p.Shot != null)) File.Delete(p.Shot!);
        File.WriteAllText(Path.Combine(Catalog.DataDir, "picks.json"), System.Text.Json.JsonSerializer.Serialize(keep, Catalog.Json));
        if (rec != null)
        {
            foreach (var f in Directory.GetFiles(Catalog.DataDir, rec.Id + "*")) File.Delete(f);
            if (Directory.Exists(Path.Combine(Catalog.DataDir, rec.Id))) Directory.Delete(Path.Combine(Catalog.DataDir, rec.Id), true);
        }
        Console.WriteLine($"시험 흔적 지움: selftest pick {all.Count - keep.Count}건" + (rec != null ? $", {rec.Id} 영상·표·장면" : ""));
    }

    // 시험 대상이 맨 앞이 아니면 다른 앱을 누르게 되므로 멈춘다
    static void Front(IntPtr h)
    {
        for (int i = 0; i < 4 && Native.GetForegroundWindow() != h; i++)
        {
            if (i >= 2) { Native.ShowWindow(h, 6); Thread.Sleep(300); } // 최소화했다가
            Native.ShowWindow(h, 9);                                      // 되살리기
            Native.SetForegroundWindow(h);
            try { AutomationElement.FromHandle(h).SetFocus(); } catch { }
            Thread.Sleep(500);
        }
        if (Native.GetForegroundWindow() != h) throw new SkipException("IINA 창을 맨 앞으로 못 올림(다른 앱 사용 중)");
    }

    static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    static async Task<string> Call(McpClient c, string tool, Dictionary<string, object?> a) =>
        string.Join("\n", (await c.CallToolAsync(tool, a)).Content.OfType<TextContentBlock>().Select(t => t.Text));

    static async Task<AutomationElement> IinaWindow()
    {
        if (!Process.GetProcessesByName("IINA").Any(x => x.MainWindowHandle != IntPtr.Zero))
            Process.Start(Environment.GetEnvironmentVariable("IINA_EXE") ?? @"C:\Users\gusql\AppData\Local\Programs\IINA\IINA.exe");
        // Electron은 처음 띄운 프로세스가 아닌 다른 프로세스가 창을 가진다 → 창이 있고 settings 버튼까지 그려진 프로세스를 기다린다
        var cond = new PropertyCondition(AutomationElement.AutomationIdProperty, "settings");
        for (int i = 0; i < 80; i++)
        {
            foreach (var x in Process.GetProcessesByName("IINA").Where(x => x.MainWindowHandle != IntPtr.Zero))
            {
                var w = AutomationElement.FromHandle(x.MainWindowHandle);
                if (w.FindFirst(TreeScope.Descendants, cond) != null) { Front(x.MainWindowHandle); return w; }
            }
            await Task.Delay(250);
        }
        throw new SkipException("IINA 창(설정 버튼)이 20초 안에 안 뜸");
    }

    static double Probe(string f)
    {
        var p = Process.Start(new ProcessStartInfo("ffprobe", $"-v error -show_entries format=duration -of csv=p=0 \"{f}\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var o = p.StandardOutput.ReadToEnd(); p.WaitForExit();
        return double.TryParse(o.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
    }
}
