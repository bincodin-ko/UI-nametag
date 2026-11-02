using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace UiNametag;

public record Target(string Id, string Kind, string Name, double[] Rect, string? File, int Line, bool Guess, string Label, string App = "", string Window = "", bool Outside = false, string Source = "");
public record Move(Target Anchor, string Where, double[] Rect);
public record Pick(int No, DateTime Time, string Action, Target Target, string Memo, Move? Move, List<double[]>? Strokes, string? Shot, bool Delivered, bool Done = false);
public record RecEvent(double T, string What, Target? Target, string? State);
public record Recording(string Id, string Video, string Window, double[] Rect, DateTime Start, double Seconds, List<RecEvent> Events, string Memo, bool Done = false);

// 대기열(picks.json)과 녹화 묶음(rec_N.json) 저장
public static class Store
{
    static string PicksPath => Path.Combine(Catalog.DataDir, "picks.json");
    static readonly object Gate = new();

    public static Target ToTarget(Hit h) => new(h.Id, h.Kind, h.Name, R(h.Rect), h.Entry?.File, h.Entry?.Line ?? 0, h.Guess, h.Label, h.App, h.Window, h.Outside, h.Source);
    public static double[] R(Rect r) => [Math.Round(r.X), Math.Round(r.Y), Math.Round(r.Width), Math.Round(r.Height)];

    public static List<Pick> Picks()
    {
        lock (Gate) return File.Exists(PicksPath) ? JsonSerializer.Deserialize<List<Pick>>(File.ReadAllText(PicksPath))! : [];
    }

    static void Save(List<Pick> l) { lock (Gate) File.WriteAllText(PicksPath, JsonSerializer.Serialize(l, Catalog.Json)); }

    public static Pick Add(string action, Target t, string memo, Move? move = null, List<double[]>? strokes = null, string? shot = null)
    {
        var l = Picks();
        var p = new Pick(l.Count == 0 ? 1 : l.Max(x => x.No) + 1, DateTime.Now, action, t, memo, move, strokes, shot, false);
        l.Add(p); Save(l);
        return p;
    }

    public static void RemovePick(int no) { var l = Picks(); l.RemoveAll(p => p.No == no); Save(l); }
    // Claude가 고친 뒤 표시 (가져감 = 읽기만 함, 고침 = 실제로 고침)
    // 녹화도 고친 뒤 '고침 완료'
    public static int MarkRecDone(IEnumerable<string> ids)
    {
        int n = 0;
        foreach (var id in ids)
            if (LoadRec(id) is { } r) { SaveRec(r with { Done = true }); n++; }
        return n;
    }

    public static int MarkDone(IEnumerable<int> nos)
    {
        var set = nos.ToHashSet();
        var l = Picks();
        var n = l.Count(p => set.Contains(p.No));
        Save(l.Select(p => set.Contains(p.No) ? p with { Done = true, Delivered = true } : p).ToList());
        return n;
    }

    public static void RemoveDone() { var l = Picks(); l.RemoveAll(p => p.Done); Save(l); }

    public static List<Recording> Recs() =>
        [.. Directory.GetFiles(Catalog.DataDir, "rec_*.json").Select(f => JsonSerializer.Deserialize<Recording>(File.ReadAllText(f))!).OrderByDescending(r => r.Start)];

    public static void RemoveRec(Recording r)
    {
        foreach (var f in Directory.GetFiles(Catalog.DataDir, r.Id + "*")) File.Delete(f);
        if (Directory.Exists(Path.Combine(Catalog.DataDir, r.Id))) Directory.Delete(Path.Combine(Catalog.DataDir, r.Id), true);
    }

    public static List<Pick> TakeNew(bool all)
    {
        var l = Picks();
        var res = l.Where(p => all || !p.Delivered).ToList();
        Save(l.Select(p => p with { Delivered = true }).ToList());
        return res;
    }

    public static string NewRecId() => "rec_" + DateTime.Now.ToString("MMdd_HHmmss");
    public static void SaveRec(Recording r) => File.WriteAllText(Path.Combine(Catalog.DataDir, r.Id + ".json"), JsonSerializer.Serialize(r, Catalog.Json));
    public static Recording? LoadRec(string? id)
    {
        var f = id != null ? Path.Combine(Catalog.DataDir, id + ".json")
            : Directory.GetFiles(Catalog.DataDir, "rec_*.json").OrderBy(File.GetLastWriteTime).LastOrDefault();
        return f != null && File.Exists(f) ? JsonSerializer.Deserialize<Recording>(File.ReadAllText(f)) : null;
    }

    // ffmpeg ddagrab(화면 복제 API)으로 찍는다 — GPU 화면도 검게 안 나옴. ponytail: 주 모니터(output_idx=0)만, 여러 모니터면 좌표→모니터 번호 계산 추가
    public static string Ffmpeg(string args, int waitMs = 30000)
    {
        var p = Process.Start(new ProcessStartInfo("ffmpeg", "-y -loglevel error " + args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true })!;
        var err = p.StandardError.ReadToEndAsync();
        p.WaitForExit(waitMs);
        return err.Result;
    }

    public static string Crop(double[] r) => $"crop={(int)r[2] / 2 * 2}:{(int)r[3] / 2 * 2}:{(int)Math.Max(0, r[0])}:{(int)Math.Max(0, r[1])}";

    // 화면 사진. 영역을 모니터마다 잘라 찍고 한 장으로 합친다.
    // 예전엔 주 모니터(ddagrab output_idx=0)만, 화면 밖까지 나간 영역 그대로 잘라서 ffmpeg가 실패 → 그림 없음(2026-11-03 #4·#5, 듀얼 모니터 #426)
    public static string? Shot(double[] rect, string name, double pad = 40)
    {
        var path = Path.Combine(Catalog.DataDir, name + ".png");
        var r = Pad(rect, pad);
        var area = System.Drawing.Rectangle.Intersect(new((int)r[0], (int)r[1], (int)r[2], (int)r[3]), System.Windows.Forms.SystemInformation.VirtualScreen);
        if (area.Width < 2 || area.Height < 2) return null;
        using var bmp = new System.Drawing.Bitmap(area.Width, area.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Black);
            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                var part = System.Drawing.Rectangle.Intersect(area, s.Bounds);
                if (part.Width < 2 || part.Height < 2) continue;
                // 주 모니터는 ddagrab(GPU 영상도 찍힘). 옆 모니터는 ffmpeg 출력 번호를 크기로 못 가려(둘 다 2560×1440으로 보고) 화면 복사로
                // ponytail: 옆 모니터의 GPU 영상은 검게 찍힐 수 있음, 필요하면 DXGI 출력 좌표로 output_idx를 찾아 ddagrab
                using var piece = (s.Primary ? Grab(part, s.Bounds) : null) ?? Copy(part);
                g.DrawImage(piece, part.X - area.X, part.Y - area.Y, piece.Width, piece.Height);
            }
        }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    static System.Drawing.Bitmap? Grab(System.Drawing.Rectangle part, System.Drawing.Rectangle screen)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"uinametag_{Guid.NewGuid():N}.png");
        Ffmpeg($"-f lavfi -i ddagrab=output_idx=0:framerate=5 -vf \"hwdownload,format=bgra,{Crop([part.X - screen.X, part.Y - screen.Y, part.Width, part.Height])}\" -frames:v 1 \"{tmp}\"");
        if (!File.Exists(tmp)) return null;
        try { using var f = new System.Drawing.Bitmap(tmp); return new System.Drawing.Bitmap(f); } finally { File.Delete(tmp); }
    }

    static System.Drawing.Bitmap Copy(System.Drawing.Rectangle part)
    {
        var b = new System.Drawing.Bitmap(part.Width, part.Height);
        using var g = System.Drawing.Graphics.FromImage(b);
        g.CopyFromScreen(part.Location, System.Drawing.Point.Empty, part.Size);
        return b;
    }

    // 사각형들과 점들(그림 선, [-1,-1]은 끊김 표시)을 모두 감싸는 사각형
    public static double[] Union(double[] r, List<double[]>? pts, params double[]?[] more)
    {
        double x1 = r[0], y1 = r[1], x2 = r[0] + r[2], y2 = r[1] + r[3];
        foreach (var p in pts ?? []) if (p[0] >= 0) { x1 = Math.Min(x1, p[0]); y1 = Math.Min(y1, p[1]); x2 = Math.Max(x2, p[0]); y2 = Math.Max(y2, p[1]); }
        foreach (var m in more) if (m != null) { x1 = Math.Min(x1, m[0]); y1 = Math.Min(y1, m[1]); x2 = Math.Max(x2, m[0] + m[2]); y2 = Math.Max(y2, m[1] + m[3]); }
        return [x1, y1, x2 - x1, y2 - y1];
    }

    public static double[] Pad(double[] r, double p) => [r[0] - p, r[1] - p, r[2] + 2 * p, r[3] + 2 * p]; // 화면 밖은 Shot이 잘라냄
}
