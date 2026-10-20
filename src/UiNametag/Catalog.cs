using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;

namespace UiNametag;

public record Entry(string Id, string Tag, string File, int Line);

public record Hit(string Id, string Kind, string Name, Rect Rect, Entry? Entry, bool Guess, string Label, string App = "", string Window = "", bool Outside = false, string Source = "");

// 부품 사전: 소스에서 id를 뽑아 AutomationId → 파일:줄. 등록한 앱마다 하나(catalogs/{앱 Id}.json)
public static class Catalog
{
    public static string DataDir { get; } = Directory.CreateDirectory(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UiNametag")).FullName;
    public static string PathOf(string appId) => Path.Combine(Directory.CreateDirectory(Path.Combine(DataDir, "catalogs")).FullName, appId + ".json");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    record CatalogFile(string Project, List<Entry> Entries);

    static readonly string[] SkipDirs = ["node_modules", "bin", "obj", ".git", "vendor", "dist"];
    static readonly Regex HtmlId = new(@"<([\w.]+)[^>]*?\sid=[""']([^""'{}]+)[""']", RegexOptions.Compiled);
    static readonly Regex XamlName = new(@"<([\w:.]+)[^>]*?\s(?:x:)?Name=""([^""]+)""", RegexOptions.Compiled);
    static readonly Regex DesignerName = new(@"this\.(\w+)\.Name\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    static readonly string[] Exts = [".html", ".htm", ".jsx", ".tsx", ".vue", ".svelte", ".xaml", ".Designer.cs"];

    public static int Build(string project) => Apps.BuildOrRegister(project).Count;

    public static int BuildTo(string project, string appId)
    {
        var entries = new List<Entry>();
        foreach (var f in Files(project))
        {
            var rel = Path.GetRelativePath(project, f).Replace('\\', '/');
            var (re, idGroup, tagGroup) = f.EndsWith(".xaml") ? (XamlName, 2, 1) : f.EndsWith(".Designer.cs") ? (DesignerName, 2, 1) : (HtmlId, 2, 1);
            var lines = File.ReadAllLines(f);
            for (int i = 0; i < lines.Length; i++)
                foreach (Match m in re.Matches(lines[i]))
                    entries.Add(new Entry(m.Groups[idGroup].Value, m.Groups[tagGroup].Value, rel, i + 1));
        }
        File.WriteAllText(PathOf(appId), JsonSerializer.Serialize(new CatalogFile(project, entries), Json));
        return entries.Count;
    }

    public static IEnumerable<string> Files(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
            if (Exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))) yield return f;
        foreach (var d in Directory.EnumerateDirectories(dir))
            if (!SkipDirs.Contains(Path.GetFileName(d)) && !Path.GetFileName(d).StartsWith('.'))
                foreach (var f in Files(d)) yield return f;
    }

    // 앱 사전 읽기 (파일 시각이 바뀌면 다시 읽음 — MCP 쪽에서 다시 만들어도 반영)
    static readonly Dictionary<string, (DateTime At, ILookup<string, Entry> L)> Cache = [];
    static readonly ILookup<string, Entry> Empty = Array.Empty<Entry>().ToLookup(e => e.Id);
    public static ILookup<string, Entry> For(AppInfo a)
    {
        var f = PathOf(a.Id);
        if (!File.Exists(f)) return Empty;
        var at = File.GetLastWriteTimeUtc(f);
        lock (Cache)
        {
            if (Cache.TryGetValue(a.Id, out var c) && c.At == at) return c.L;
            var l = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(f))!.Entries.ToLookup(e => e.Id);
            Cache[a.Id] = (at, l);
            return l;
        }
    }

    public static IEnumerable<(AppInfo App, Entry E)> Lookup(string q, string? app = null)
    {
        var apps = Apps.List().Where(a => app == null || a.Name.Contains(app, StringComparison.OrdinalIgnoreCase) || a.Id == app);
        var all = apps.SelectMany(a => For(a).SelectMany(g => g).Select(e => (a, e))).ToList();
        var exact = all.Where(x => x.e.Id == q).ToList();
        return exact.Count > 0 ? exact : all.Where(x => x.e.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    // 마우스 아래 창이 어느 등록 앱인지: 실행 파일이 같으면 그 앱, 아니면 창 안 id가 사전과 가장 많이 겹치는 앱
    static readonly Dictionary<int, string> ExeOf = [];
    static AppInfo? MatchApp(int pid, HashSet<string> ids)
    {
        string? exe;
        lock (ExeOf)
            if (!ExeOf.TryGetValue(pid, out exe))
            {
                try { exe = System.Diagnostics.Process.GetProcessById(pid).MainModule?.FileName ?? ""; } catch { exe = ""; }
                ExeOf[pid] = exe;
            }
        var apps = Apps.List();
        return apps.FirstOrDefault(a => exe.Length > 0 && a.Exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && string.Equals(a.Exe, exe, StringComparison.OrdinalIgnoreCase))
            ?? apps.Select(a => (a, n: ids.Count(id => For(a)[id].Any()))).Where(x => x.n > 0).OrderByDescending(x => x.n).Select(x => x.a).FirstOrDefault();
    }

    // 같은 id가 여러 파일에 있으면 같은 창에 보이는 다른 id가 가장 많이 들어 있는 파일을 고른다
    static readonly Dictionary<int, HashSet<string>> WindowIds = [];
    static readonly Dictionary<int, DateTime> WindowIdsAt = [];
    static Entry? Pick(string id, AutomationElement el, ILookup<string, Entry> cat)
    {
        var c = cat[id].ToList();
        if (c.Count <= 1) return c.FirstOrDefault();
        var ids = WindowIdSet(TopWindow(el));
        return c.OrderByDescending(e => cat.SelectMany(g => g).Count(o => o.File == e.File && ids.Contains(o.Id))).First();
    }

    // 창 안의 AutomationId 모음 (창마다 한 번만 읽고 기억)
    static HashSet<string> WindowIdSet(AutomationElement? top)
    {
        var key = top?.Current.NativeWindowHandle ?? 0;
        lock (WindowIds)
            if (WindowIds.TryGetValue(key, out var hit) && WindowIdsAt.TryGetValue(key, out var at) && DateTime.Now - at < TimeSpan.FromSeconds(10)) return hit;
        var ids = new HashSet<string>();
        if (top != null)
            foreach (AutomationElement d in top.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
                if (d.Current.AutomationId is { Length: > 0 } a) ids.Add(a);
        lock (WindowIds) { WindowIds[key] = ids; WindowIdsAt[key] = DateTime.Now; }
        return ids;
    }

    public static AutomationElement? TopWindow(AutomationElement el)
    {
        var w = TreeWalker.ControlViewWalker;
        AutomationElement? last = null;
        for (var p = el; p != null && p != AutomationElement.RootElement; p = w.GetParent(p)) last = p;
        return last;
    }

    // 화면 좌표(물리 픽셀) → 부품 + 이름 + 코드 위치
    public static Hit? Resolve(Point pt)
    {
        AutomationElement el;
        // FromPoint는 클릭이 통과하는 투명 창(내 레이어, NVIDIA Overlay 같은 화면 전체 오버레이)도 잡는다
        // → 클릭이 실제로 가는 창(WindowFromPoint)에서 찾아 내려간다
        try { el = UnderWindow(pt) ?? AutomationElement.FromPoint(pt); } catch { return null; }
        if (el == null) return null;
        // Electron(Chromium)은 FromPoint가 바깥 Pane에서 멈춘다 → 자식으로 직접 내려간다
        el = Descend(TopWindow(el) ?? el, pt);
        var c = el.Current;
        var w = TreeWalker.ControlViewWalker;
        var top = TopWindow(el);
        string proc = "";
        try { proc = System.Diagnostics.Process.GetProcessById(c.ProcessId).ProcessName; } catch { }
        var win = top?.Current.Name ?? "";
        // 등록한 앱 중 어느 것에도 안 맞으면 사전 밖 앱
        var app = MatchApp(c.ProcessId, WindowIdSet(top));
        var cat = app != null ? For(app) : Empty;
        var outside = app == null;
        var src = app?.Source ?? "";
        var p = el;
        for (int depth = 0; p != null && depth < 12; depth++, p = w.GetParent(p))
        {
            var id = p.Current.AutomationId;
            if (id.Length > 0 && cat[id].Any())
            {
                var entry = Pick(id, p, cat);
                return new Hit(id, Kind(c.ControlType), c.Name, c.BoundingRectangle, entry, depth > 0, Label(c.ControlType, entry, depth > 0, id), proc, win, false, src);
            }
        }
        return new Hit(c.AutomationId, Kind(c.ControlType), c.Name, c.BoundingRectangle, null, true,
            outside ? $"[{proc}] {StdName(c.ControlType)} · ⚠ 사전 밖 앱 (코드 위치 없음)" : Label(c.ControlType, null, true, ""), proc, win, outside, src);
    }

    // WindowFromPoint는 WS_EX_TRANSPARENT 창을 건너뛴다. 그 창에서 점을 품은 가장 작은 부품까지 내려간다.
    static AutomationElement? UnderWindow(Point pt)
    {
        var h = Native.GetAncestor(Native.WindowFromPoint(new Native.POINT { X = (int)pt.X, Y = (int)pt.Y }), 2);
        return h == IntPtr.Zero ? null : AutomationElement.FromHandle(h);
    }

    // 점을 품은 모든 갈래를 내려가 가장 작은(가장 안쪽) 부품을 고른다. 같은 크기 빈 Pane이 겹쳐 있어도 안 막힌다.
    static AutomationElement Descend(AutomationElement cur, Point pt, int depth = 0)
    {
        var w = TreeWalker.ControlViewWalker;
        AutomationElement best = cur;
        double area = Area(cur);
        if (depth > 25) return best;
        for (var c = w.GetFirstChild(cur); c != null; c = w.GetNextSibling(c))
        {
            var r = c.Current.BoundingRectangle;
            if (r.IsEmpty || !r.Contains(pt)) continue;
            var d = Descend(c, pt, depth + 1);
            if (Area(d) <= area) { best = d; area = Area(d); }
        }
        return best;
    }
    static double Area(AutomationElement e) { var r = e.Current.BoundingRectangle; return r.IsEmpty ? double.MaxValue : r.Width * r.Height; }

    public static AutomationElement? FindById(string id)
    {
        var cond = new PropertyCondition(AutomationElement.AutomationIdProperty, id);
        foreach (AutomationElement win in AutomationElement.RootElement.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition))
            if (win.FindFirst(TreeScope.Descendants, cond) is { } found) return found;
        return null;
    }

    public static string Kind(ControlType t) => t.ProgrammaticName.Replace("ControlType.", "");

    static readonly Dictionary<string, string> Names = new()
    {
        ["Button"] = "버튼 · Button", ["List"] = "목록 · List", ["ListItem"] = "목록 항목 · List Item",
        ["Edit"] = "입력칸 · Text Field", ["Text"] = "글자 · Label", ["CheckBox"] = "체크박스 · Checkbox",
        ["ComboBox"] = "드롭다운 · Dropdown", ["Tab"] = "탭 · Tabs", ["TabItem"] = "탭 항목 · Tab",
        ["Slider"] = "슬라이더 · Slider", ["Menu"] = "메뉴 · Menu", ["MenuItem"] = "메뉴 항목 · Menu Item",
        ["Image"] = "그림 · Image", ["Hyperlink"] = "링크 · Link", ["Group"] = "묶음 · Group",
        ["Document"] = "문서 영역 · Document", ["Pane"] = "영역 · Pane", ["Window"] = "창 · Window",
        ["ToolBar"] = "도구 막대 · Toolbar", ["ScrollBar"] = "스크롤바 · Scrollbar", ["ProgressBar"] = "진행 막대 · Progress Bar",
        ["Tree"] = "트리 뷰 · Tree View", ["TreeItem"] = "트리 항목 · Tree Item", ["RadioButton"] = "라디오 버튼 · Radio Button",
        ["Spinner"] = "숫자 조절 · Spinner", ["DataGrid"] = "표 · Data Grid", ["Header"] = "머리글 · Header",
    };
    public static string StdName(ControlType t) => Names.TryGetValue(Kind(t), out var n) ? n : Kind(t);

    static string Label(ControlType t, Entry? e, bool guess, string id) =>
        StdName(t) + (e == null ? " · 코드 위치 모름" : $" · {(guess ? "(" + id + " 안) " : "")}{e.File}:{e.Line}");
}
