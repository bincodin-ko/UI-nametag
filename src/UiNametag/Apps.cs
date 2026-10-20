using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace UiNametag;

// Exe = 실행 파일 경로(웹사이트는 주소). 사전은 catalogs/{Id}.json
public record AppInfo(string Id, string Name, string Kind, string Exe, string Source, int Count, DateTime Built);
public record Settings(string PickKey = "G", string RecKey = "R", bool Autostart = false, string ShotKey = "S");

// 등록한 내 앱 목록(apps.json). 트레이 창과 Claude(MCP)가 같은 목록을 쓴다.
public static class Apps
{
    static string ListPath => Path.Combine(Catalog.DataDir, "apps.json");
    static string SettingsPath => Path.Combine(Catalog.DataDir, "settings.json");
    static readonly object Gate = new();
    static List<AppInfo>? _cache;
    static DateTime _cacheAt;

    // 다른 프로세스(MCP)가 고쳐도 보이게 파일 시각이 바뀌면 다시 읽는다
    public static List<AppInfo> List()
    {
        lock (Gate)
        {
            if (!File.Exists(ListPath)) Save(FirstRun());
            var at = File.GetLastWriteTimeUtc(ListPath);
            if (_cache == null || at != _cacheAt)
            {
                _cache = JsonSerializer.Deserialize<List<AppInfo>>(File.ReadAllText(ListPath))!;
                _cacheAt = at;
            }
            return [.. _cache];
        }
    }

    static void Save(List<AppInfo> l)
    {
        lock (Gate) { File.WriteAllText(ListPath, JsonSerializer.Serialize(l, Catalog.Json)); _cache = null; }
    }

    // 첫 실행: 옛 사전(catalog.json) 하나와 이 앱 자신을 등록한다
    static List<AppInfo> FirstRun()
    {
        var l = new List<AppInfo>();
        var old = Path.Combine(Catalog.DataDir, "catalog.json");
        if (File.Exists(old))
        {
            var src = JsonDocument.Parse(File.ReadAllText(old)).RootElement.GetProperty("Project").GetString()!;
            var a = new AppInfo(NewId(), NameOf(src), "Electron", "", src, 0, DateTime.MinValue);
            l.Add(a with { Count = Catalog.BuildTo(src, a.Id), Built = DateTime.Now });
        }
        if (SelfSource() is { } me)
        {
            var a = new AppInfo(NewId(), "UiNametag (이 앱)", "Windows 앱", Environment.ProcessPath ?? "", me, 0, DateTime.MinValue);
            l.Add(a with { Count = Catalog.BuildTo(me, a.Id), Built = DateTime.Now });
        }
        return l;
    }

    // 실행 파일 위쪽 폴더에서 UiNametag.csproj를 찾는다 (소스로 빌드해 쓰는 경우)
    public static string? SelfSource()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "UiNametag.csproj"))) return d.FullName;
        return null;
    }

    static string NewId() => Guid.NewGuid().ToString("N")[..8];
    static string Norm(string p) => Path.GetFullPath(p).TrimEnd('\\', '/');
    public static bool SameDir(string a, string b) => string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);

    // src·app 같은 흔한 이름이면 한 단계 위 폴더 이름
    public static string NameOf(string source)
    {
        var d = new DirectoryInfo(Norm(source));
        return d.Name.ToLower() is "src" or "app" or "source" or "client" && d.Parent != null ? d.Parent.Name : d.Name;
    }

    public static AppInfo Register(string name, string kind, string exe, string source)
    {
        var a = new AppInfo(NewId(), name, kind, exe, Norm(source), 0, DateTime.MinValue);
        a = a with { Count = Catalog.BuildTo(a.Source, a.Id), Built = DateTime.Now };
        var l = List(); l.Add(a); Save(l);
        return a;
    }

    public static AppInfo Rebuild(string id)
    {
        var l = List();
        var i = l.FindIndex(x => x.Id == id);
        l[i] = l[i] with { Count = Catalog.BuildTo(l[i].Source, id), Built = DateTime.Now };
        Save(l);
        return l[i];
    }

    // 같은 소스 폴더 앱이 있으면 다시 만들고, 없으면 새로 등록 (build-catalog 명령·MCP용)
    public static AppInfo BuildOrRegister(string source) =>
        List().FirstOrDefault(a => SameDir(a.Source, source)) is { } a ? Rebuild(a.Id) : Register(NameOf(source), "", "", source);

    public static void Remove(string id)
    {
        var l = List(); l.RemoveAll(a => a.Id == id); Save(l);
        File.Delete(Catalog.PathOf(id));
    }

    // (글, 수준 ok/warn/bad). ponytail: 바뀜 확인은 소스 파일 시각 전체 훑기, 아주 큰 폴더면 배경으로
    public static (string Text, string Level) Status(AppInfo a)
    {
        if (!Directory.Exists(a.Source)) return ("폴더 없음", "bad");
        if (a.Count == 0) return ("사전 0개", "bad");
        try { if (Catalog.Files(a.Source).Any(f => File.GetLastWriteTime(f) > a.Built)) return ("소스 바뀜", "warn"); } catch { }
        return ("정상", "ok");
    }

    static readonly string[] Browsers = ["msedge", "chrome", "firefox", "whale", "brave", "opera", "vivaldi"];
    public static string KindOf(string exe, string process)
    {
        if (Browsers.Contains(process.ToLower())) return "웹사이트";
        var dir = Path.GetDirectoryName(exe) ?? "";
        if (File.Exists(Path.Combine(dir, "resources", "app.asar")) || Directory.Exists(Path.Combine(dir, "resources", "app"))) return "Electron";
        return "Windows 앱";
    }

    // 소스 폴더 추천: 바탕화면·repos 아래 폴더 중 앱 이름이 들어간 것 먼저, 그다음 최근 고친 것
    public static List<string> Suggest(string name)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Path.Combine(home, "source", "repos"), Path.Combine(home, "Documents", "GitHub") };
        var key = new string(name.ToLower().Where(char.IsLetterOrDigit).ToArray());
        return roots.Where(Directory.Exists)
            .SelectMany(r => { try { return Directory.GetDirectories(r); } catch { return []; } })
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
            .OrderByDescending(d => key.Length > 1 && new string(Path.GetFileName(d).ToLower().Where(char.IsLetterOrDigit).ToArray()) is var f && (f.Contains(key) || key.Contains(f)))
            .ThenByDescending(Directory.GetLastWriteTime)
            .Take(5).ToList();
    }

    public static Settings LoadSettings() =>
        File.Exists(SettingsPath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath))! : new();
    public static void SaveSettings(Settings s) => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, Catalog.Json));

    // Windows 시작 시 켜기 = 시작프로그램 폴더 바로가기 (레지스트리 안 건드림)
    static string StartupLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "UiNametag.lnk");
    public static void SetAutostart(bool on)
    {
        if (!on) { File.Delete(StartupLink); return; }
        dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        var l = sh.CreateShortcut(StartupLink);
        l.TargetPath = Environment.ProcessPath; l.Save();
    }
    public static bool Autostart => File.Exists(StartupLink);

    // 바탕화면 바로가기에 마우스를 올리면 보이는 설명을 지금 상태로 (켜짐/꺼짐)
    public static void ShortcutState(bool on)
    {
        var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "UiNametag.lnk");
        if (!File.Exists(lnk)) return;
        try
        {
            dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            var l = sh.CreateShortcut(lnk);
            l.Description = on ? "UiNametag ● 켜짐 · 누르면 끄기" : "UiNametag ○ 꺼짐 · 누르면 켜고 전체 창";
            l.Save();
        }
        catch { }
    }

    // ponytail: 나 말고 UiNametag 프로세스가 있으면 Claude가 MCP로 붙어 있다고 본다 (시험 실행 중이면 잘못 셈)
    public static bool ClaudeConnected => Process.GetProcessesByName("UiNametag").Any(p => p.Id != Environment.ProcessId);

    public static string McpCommand => $"claude mcp add -s user uinametag -- \"{Environment.ProcessPath}\" mcp";
}
