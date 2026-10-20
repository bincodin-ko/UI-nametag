using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;

namespace UiNametag;

// 녹화: 앱 창 영역만 ffmpeg로 찍고, 그동안의 클릭·부품 상태 변화를 표로 남긴다
public class Recorder
{
    Process? _ff;
    Stopwatch _sw = new();
    readonly List<RecEvent> _events = [];
    string _id = "", _video = "", _window = "";
    double[] _rect = [];
    DateTime _start;
    public bool On => _ff != null;
    public double[] Rect => _rect;

    public void Start(IntPtr hwnd)
    {
        var el = AutomationElement.FromHandle(hwnd);
        _window = el.Current.Name;
        _rect = Store.R(el.Current.BoundingRectangle);
        _id = Store.NewRecId();
        _video = Path.Combine(Catalog.DataDir, _id + ".mp4");
        _events.Clear();
        _ff = Process.Start(new ProcessStartInfo("ffmpeg",
            $"-y -loglevel error -f lavfi -i ddagrab=output_idx=0:framerate=15 -vf \"hwdownload,format=bgra,{Store.Crop(_rect)},format=yuv420p\" -c:v libx264 -preset ultrafast \"{_video}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true })!;
        _start = DateTime.Now;
        _sw = Stopwatch.StartNew();
    }

    readonly List<Task> _pending = [];

    // 훅에서 바로 부른다: 시각만 재고, 부품 읽기는 뒤에서 (Stop이 끝날 때까지 기다림)
    public void OnClick(Point pt)
    {
        if (!On) return;
        var t = Math.Round(_sw.Elapsed.TotalSeconds, 2);
        lock (_pending) _pending.Add(Task.Run(() => Log(pt, t)));
    }

    void Log(Point pt, double t)
    {
        var hit = Catalog.Resolve(pt);
        var target = hit == null ? null : Store.ToTarget(hit);
        lock (_events) _events.Add(new RecEvent(t, "Click", target, State(pt)));
        // 클릭 뒤 상태가 바뀌는지 0.3초·1초 뒤 다시 읽는다 (예: 열렸다가 바로 닫힘)
        foreach (var delay in new[] { 300, 1000 })
        {
            var at = t + delay / 1000.0 - _sw.Elapsed.TotalSeconds;
            if (at > 0) Thread.Sleep(TimeSpan.FromSeconds(at));
            var s = State(pt);
            lock (_events) if (s != null) _events.Add(new RecEvent(Math.Round(t + delay / 1000.0, 2), "상태", target, s));
        }
    }

    static string? State(Point pt)
    {
        try
        {
            var el = AutomationElement.FromPoint(pt);
            var parts = new List<string> { $"이름=\"{el.Current.Name}\"" };
            if (el.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var ec)) parts.Add("열림=" + ((ExpandCollapsePattern)ec).Current.ExpandCollapseState);
            if (el.TryGetCurrentPattern(TogglePattern.Pattern, out var tg)) parts.Add("켜짐=" + ((TogglePattern)tg).Current.ToggleState);
            if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var si)) parts.Add("선택=" + ((SelectionItemPattern)si).Current.IsSelected);
            return string.Join(" ", parts);
        }
        catch { return null; }
    }

    public Recording Stop(string memo)
    {
        Task[] wait; lock (_pending) { wait = [.. _pending]; _pending.Clear(); }
        Task.WaitAll(wait, 8000);
        _ff!.StandardInput.Write('q');
        _ff.WaitForExit(10000);
        _ff = null;
        List<RecEvent> ev;
        lock (_events) ev = [.. _events.OrderBy(e => e.T)];
        // 바뀌지 않은 상태 줄은 뺀다 (바로 앞 같은 부품 상태와 같으면)
        var outList = new List<RecEvent>();
        foreach (var e in ev)
            if (!(e.What == "상태" && outList.LastOrDefault(o => o.Target?.Id == e.Target?.Id)?.State == e.State)) outList.Add(e);
        var r = new Recording(_id, _video, _window, _rect, _start, Math.Round(_sw.Elapsed.TotalSeconds, 1), outList, memo);
        Store.SaveRec(r);
        return r;
    }
}
