using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace UiNametag;

// 화면 전체를 덮는 투명 창. 평소엔 클릭이 그대로 통과한다.
public class Overlay : Window
{
    public readonly Canvas Canvas = new();
    public readonly Rectangle Box = Shape("#ff6a00", 2), Slot = Shape("#0a84ff", 2, true), Ghost = Shape("#ff6a00", 2, true), RecBorder = Shape("#e5534b", 4), Area = Shape("#a371f7", 2, true);
    public readonly Border Tag = Label("#ff6a00"), SlotTag = Label("#0a84ff"), Toast = Label("#24292f");
    public readonly Polyline Pen = new() { Stroke = Brush("#e5534b"), StrokeThickness = 3.5, Visibility = Visibility.Collapsed };
    public IntPtr Hwnd { get; private set; }
    public double Scale = 1;

    public Overlay()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; Title = "UiNametag overlay";
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        Content = Canvas;
        foreach (UIElement e in new UIElement[] { Box, Slot, Ghost, RecBorder, Area, Pen, Tag, SlotTag, Toast }) Canvas.Children.Add(e);
        SourceInitialized += (_, _) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            Native.ClickThrough(Hwnd, true);
            Scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        };
    }

    static SolidColorBrush Brush(string c) => (SolidColorBrush)new BrushConverter().ConvertFrom(c)!;
    static Rectangle Shape(string c, double w, bool dashed = false) => new()
    {
        Stroke = Brush(c), StrokeThickness = w, Visibility = Visibility.Collapsed,
        Fill = new SolidColorBrush(Color.FromArgb(0x18, Brush(c).Color.R, Brush(c).Color.G, Brush(c).Color.B)),
        StrokeDashArray = dashed ? [4, 3] : null, RadiusX = 3, RadiusY = 3,
    };
    static Border Label(string c) => new()
    {
        Background = Brush(c), CornerRadius = new(4), Padding = new(8, 3, 8, 3), Visibility = Visibility.Collapsed,
        Child = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold, FontFamily = new("Malgun Gothic") },
    };

    // 물리 픽셀 → 이 창의 좌표
    Point P(double x, double y) => new((x - SystemParameters.VirtualScreenLeft * Scale) / Scale, (y - SystemParameters.VirtualScreenTop * Scale) / Scale);
    public Point ToPx(Point dip) => new(dip.X * Scale + SystemParameters.VirtualScreenLeft * Scale, dip.Y * Scale + SystemParameters.VirtualScreenTop * Scale);

    public void Place(FrameworkElement e, double[]? r, double pad = 0)
    {
        if (r == null) { e.Visibility = Visibility.Collapsed; return; }
        var a = P(r[0], r[1]);
        Canvas.SetLeft(e, a.X - pad); Canvas.SetTop(e, a.Y - pad);
        e.Width = r[2] / Scale + 2 * pad; e.Height = r[3] / Scale + 2 * pad;
        e.Visibility = Visibility.Visible;
    }

    public void Say(Border b, string? text, double x = 0, double y = 0)
    {
        if (text == null) { b.Visibility = Visibility.Collapsed; return; }
        ((TextBlock)b.Child).Text = text;
        var a = P(x, y);
        Canvas.SetLeft(b, a.X); Canvas.SetTop(b, Math.Max(0, a.Y - 26));
        b.Visibility = Visibility.Visible;
    }

    public void ToastAt(string text)
    {
        var c = Native.Cursor();
        Say(Toast, text, c.X + 16, c.Y + 40);
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        t.Tick += (_, _) => { Toast.Visibility = Visibility.Collapsed; t.Stop(); };
        t.Start();
    }
}

// 메모 창: 고른 부품 아래에 뜬다
public class MemoWindow : Window
{
    public readonly TextBox Box = new() { FontSize = 13, Margin = new(0, 8, 0, 10), MinHeight = 34, AcceptsReturn = false };
    public string Result = "cancel";
    public MemoWindow(string head, string[] buttons)
    {
        Title = "UiNametag 메모"; WindowStyle = WindowStyle.ToolWindow; SizeToContent = SizeToContent.WidthAndHeight;
        Topmost = true; ResizeMode = ResizeMode.NoResize; FontFamily = new("Malgun Gothic");
        if (Application.Current.TryFindResource("Bg") is Brush bg) { Background = bg; Foreground = (Brush)Application.Current.FindResource("Ink"); }
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons)
        {
            var btn = new Button { Content = b, Padding = new(12, 5, 12, 5), Margin = new(6, 0, 0, 0), IsDefault = b == buttons[0] };
            if (Application.Current.TryFindResource(b == buttons[0] ? "Primary" : "Btn") is Style st) btn.Style = st;
            btn.Click += (_, _) => { Result = b; Close(); };
            row.Children.Add(btn);
        }
        Content = new StackPanel
        {
            Margin = new(14), Width = 340,
            Children = { new TextBlock { Text = head, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold }, Box, row },
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        SourceInitialized += (_, _) => Native.DarkTitle(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) => { FitOnScreen(); Activate(); Box.Focus(); };
    }

    // 부품(물리 픽셀) 아래에 띄우되, 그 모니터 작업 영역 밖으로 나가면 부품 위로 / 안쪽으로 당긴다
    public double[]? Anchor;
    void FitOnScreen()
    {
        var s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var a = Anchor ?? [Left * s, Top * s, 0, 0];
        var wa = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)a[0], (int)a[1])).WorkingArea;
        double L = wa.Left / s, T = wa.Top / s, R = wa.Right / s, B = wa.Bottom / s, w = ActualWidth, h = ActualHeight;
        var top = (a[1] + a[3]) / s + 10;
        if (top + h > B) top = a[1] / s - h - 10;          // 아래 자리 없으면 위로
        Top = Math.Clamp(top, T, Math.Max(T, B - h));
        Left = Math.Clamp(a[0] / s, L, Math.Max(L, R - w));
    }
}

// 상주 프로그램: 단축키 → 고르기·이동·그리기·녹화
public class Picker
{
    enum Mode { Idle, Select, Move, Draw, Busy, Window }
    const int HK_PICK = 1, HK_REC = 2, HK_ESC = 3, HK_SHOT = 4;
    public readonly Overlay O = new();
    public readonly Recorder Rec = new();
    Mode _mode = Mode.Idle;
    MemoWindow? _memo;
    Hit? _cur, _target, _anchor;
    double[]? _ghost;
    string _where = "";
    readonly List<double[]> _strokes = [];
    IntPtr _hook;
    readonly Native.HookProc _proc;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(70) };
    int _busy;
    Action<IntPtr>? _onWindow;   // 창 고르기 모드에서 클릭한 창을 받을 곳
    IntPtr _winUnder;
    TrayPopup? _popup;
    MainWindow? _main;
    bool _area;                  // 영역 캡처: 드래그 중
    Point? _areaFrom;
    double[]? _areaRect;

    public Picker()
    {
        _proc = HookCb;
        O.Show();
        HwndSource.FromHwnd(O.Hwnd).AddHook(WndProc);
        ApplyKeys(Apps.LoadSettings());
        _hook = Native.MouseHook(_proc);
        Tray = new System.Windows.Forms.NotifyIcon { Icon = MakeIcon(System.Drawing.Color.FromArgb(0xff, 0x6a, 0x00)), Text = TrayText, Visible = true };
        // 왼쪽 클릭 = 빠른 창(팝업), 오른쪽 클릭 = 메뉴
        Tray.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) O.Dispatcher.BeginInvoke(TogglePopup); };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("UiNametag 열기", null, (_, _) => O.Dispatcher.BeginInvoke(() => ShowMain("apps")));
        menu.Items.Add("부품 고르기", null, (_, _) => O.Dispatcher.BeginInvoke(StartPick));
        menu.Items.Add("화면 영역 캡처", null, (_, _) => O.Dispatcher.BeginInvoke(StartArea));
        menu.Items.Add("대기열·녹화 폴더 열기", null, (_, _) => System.Diagnostics.Process.Start("explorer.exe", Catalog.DataDir));
        menu.Items.Add("끄기", null, (_, _) => O.Dispatcher.BeginInvoke(Quit));
        Tray.ContextMenuStrip = menu;
        Tray.ShowBalloonTip(3000, "UiNametag 켜짐", $"Ctrl+Shift+{_keys.PickKey} 부품 고르기 · Ctrl+Shift+{_keys.RecKey} 녹화 · 주황 아이콘을 누르면 창", System.Windows.Forms.ToolTipIcon.None);
        _timer.Tick += (_, _) => Track();
    }

    // 단축키 다시 등록. 못 쓰는 글자가 있으면 그 글자를 돌려준다
    public string? ApplyKeys(Settings st)
    {
        Native.UnregisterHotKey(O.Hwnd, HK_PICK); Native.UnregisterHotKey(O.Hwnd, HK_REC);
        if (!Native.RegisterHotKey(O.Hwnd, HK_PICK, Native.MOD_CONTROL | Native.MOD_SHIFT | Native.MOD_NOREPEAT, st.PickKey[0])) return st.PickKey;
        if (!Native.RegisterHotKey(O.Hwnd, HK_REC, Native.MOD_CONTROL | Native.MOD_SHIFT | Native.MOD_NOREPEAT, st.RecKey[0])) return st.RecKey;
        Native.UnregisterHotKey(O.Hwnd, HK_SHOT);
        if (!Native.RegisterHotKey(O.Hwnd, HK_SHOT, Native.MOD_CONTROL | Native.MOD_SHIFT | Native.MOD_NOREPEAT, st.ShotKey[0])) return st.ShotKey;
        _keys = st;
        if (Tray != null) Tray.Text = TrayText;
        return null;
    }
    Settings _keys = new();

    public void TogglePopup()
    {
        _popup ??= new TrayPopup(this);
        // 아이콘을 다시 누르면 닫힘 (팝업은 아이콘 클릭 때 이미 포커스를 잃고 숨었음)
        if (_popup.IsVisible || DateTime.Now - _popup.HiddenAt < TimeSpan.FromMilliseconds(300)) { _popup.Hide(); return; }
        _popup.Open();
    }

    public void ShowMain(string page, string? appId = null)
    {
        if (_main == null) { _main = new MainWindow(this); _main.Closed += (_, _) => _main = null; }
        _main.Go(page, appId);
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
        _main.Topmost = true; _main.Topmost = false; // 다른 프로그램이 앞을 안 내줘도 맨 위로
    }

    public void StartPick() { if (_mode == Mode.Idle) Enter(Mode.Select); }

    // 화면에서 창 하나를 클릭해 고른다 (Esc·오른쪽 클릭 = 취소 → IntPtr.Zero)
    public void PickWindow(Action<IntPtr> done)
    {
        if (_mode != Mode.Idle) Exit();
        _onWindow = done;
        Enter(Mode.Window);
        O.ToastAt("고를 앱 창을 클릭하세요 (Esc 취소)");
    }

    // 트레이 창의 녹화 버튼: 녹화 중이면 끝, 아니면 녹화할 창을 클릭해 고른다
    public void RecFromUi()
    {
        if (Rec.On) { ToggleRec(); return; }
        PickWindow(h => { if (h != IntPtr.Zero) StartRec(h); });
    }

    IntPtr WndProc(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg != Native.WM_HOTKEY) return IntPtr.Zero;
        handled = true;
        switch ((int)w)
        {
            case HK_PICK: if (_mode == Mode.Idle) Enter(Mode.Select); else if (_mode != Mode.Busy) Exit(); break;
            case HK_REC: ToggleRec(); break;
            case HK_SHOT: StartArea(); break;
            case HK_ESC: if (_area) AreaEnd(false); else if (_memo != null) _memo.Close(); else if (_mode == Mode.Draw) DrawDone(); else if (_mode != Mode.Busy) Exit(); break;
        }
        return IntPtr.Zero;
    }

    void Enter(Mode m)
    {
        _mode = m;
        Native.RegisterHotKey(O.Hwnd, HK_ESC, Native.MOD_NOREPEAT, 0x1B);
        Native.ClickThrough(O.Hwnd, m != Mode.Draw);
        O.Background = m == Mode.Draw ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(0x10, 0, 0x67, 0xc0));
        if (m == Mode.Draw) { O.Activate(); O.Cursor = Cursors.Pen; }
        _timer.Start();
    }

    public void Exit()
    {
        var cancel = _onWindow; _onWindow = null; _winUnder = IntPtr.Zero;
        _mode = Mode.Idle; _timer.Stop();
        Native.UnregisterHotKey(O.Hwnd, HK_ESC);
        O.Background = Brushes.Transparent; O.Cursor = null;
        Native.ClickThrough(O.Hwnd, true);
        foreach (var e in new FrameworkElement[] { O.Box, O.Slot, O.Ghost, O.Tag, O.SlotTag, O.Pen }) e.Visibility = Visibility.Collapsed;
        _cur = _target = _anchor = null; _ghost = null; _strokes.Clear(); O.Pen.Points.Clear();
        foreach (var l in _lines) O.Canvas.Children.Remove(l);
        _lines.Clear();
        cancel?.Invoke(IntPtr.Zero);
    }

    // 마우스 아래 부품을 계속 읽는다 (UI Automation은 다른 스레드에서)
    void Track()
    {
        if (_mode == Mode.Window) { TrackWindow(); return; }
        if (_mode is not (Mode.Select or Mode.Move) || Interlocked.Exchange(ref _busy, 1) == 1) return;
        var pt = Cursor();
        Task.Run(() => Catalog.Resolve(pt)).ContinueWith(t =>
        {
            Interlocked.Exchange(ref _busy, 0);
            var h = t.Result;
            if (_mode == Mode.Select && h != null)
            {
                _cur = h;
                O.Place(O.Box, Store.R(h.Rect), 2);
                O.Say(O.Tag, h.Label, h.Rect.X, h.Rect.Y);
            }
            else if (_mode == Mode.Move && h != null && _target != null)
            {
                _anchor = h;
                var a = h.Rect;
                _where = pt.Y < a.Y + a.Height / 2 ? "앞" : "뒤";
                O.Place(O.Slot, Store.R(a), 3);
                O.Say(O.SlotTag, $"{h.Label} · {_where}", a.X, a.Y);
                var tw = _target.Rect.Width; var th = _target.Rect.Height;
                _ghost = [Math.Round(pt.X - tw / 2), Math.Round(pt.Y - th / 2), Math.Round(tw), Math.Round(th)];
                O.Place(O.Ghost, _ghost);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // 마우스 아래 창 전체에 테두리 + "프로세스 · 제목"
    void TrackWindow()
    {
        var c = Cursor();
        var h = Native.GetAncestor(Native.WindowFromPoint(new Native.POINT { X = (int)c.X, Y = (int)c.Y }), 2);
        if (h == IntPtr.Zero || h == _winUnder) return;
        _winUnder = h;
        Native.GetWindowRect(h, out var r);
        Native.GetWindowThreadProcessId(h, out var pid);
        string name = "";
        try { var p = System.Diagnostics.Process.GetProcessById((int)pid); name = $"{p.ProcessName} · {p.MainWindowTitle}"; } catch { }
        double[] rect = [r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top];
        O.Place(O.Box, rect, 0);
        O.Say(O.Tag, "이 창 고르기: " + name, rect[0], rect[1] + 28);
    }

    // 영역 캡처: 이름표로 안 잡히는 것(잘린 창·그림·겹친 창)을 드래그로 찍어 대기열에. 메모 창이 떠 있어도 된다
    public void StartArea()
    {
        if (_area) return;
        _area = true; _areaFrom = null; _areaRect = null;
        Native.RegisterHotKey(O.Hwnd, HK_ESC, Native.MOD_NOREPEAT, 0x1B);
        O.ToastAt("찍을 곳을 드래그하세요 (Esc 취소)");
    }

    void AreaMove(Point px)
    {
        if (_areaFrom is not { } a) return;
        _areaRect = [Math.Min(a.X, px.X), Math.Min(a.Y, px.Y), Math.Abs(px.X - a.X), Math.Abs(px.Y - a.Y)];
        O.Place(O.Area, _areaRect, 0);
    }

    async void AreaEnd(bool ok)
    {
        _area = false;
        if (_memo == null && _mode == Mode.Idle) Native.UnregisterHotKey(O.Hwnd, HK_ESC);
        O.Area.Visibility = Visibility.Collapsed;
        var r = _areaRect;
        if (!ok || r == null || r[2] < 8 || r[3] < 8) { if (ok) O.ToastAt("너무 작아요 — 다시 Ctrl+Shift+" + _keys.ShotKey); return; }
        await Task.Delay(120); // 보라 점선이 화면에서 사라진 뒤 찍는다
        var c = new Native.POINT { X = (int)(r[0] + r[2] / 2), Y = (int)(r[1] + r[3] / 2) };
        Native.GetWindowThreadProcessId(Native.GetAncestor(Native.WindowFromPoint(c), 2), out var pid);
        string app = "", win = "";
        try { var p = System.Diagnostics.Process.GetProcessById((int)pid); app = p.ProcessName; win = p.MainWindowTitle; } catch { }
        var shot = await Task.Run(() => Store.Shot(r, $"shot_{DateTime.Now:MMdd_HHmmss}", 0));
        if (shot == null) { O.ToastAt("캡처 실패 (ffmpeg)"); return; }
        var w = new MemoWindow($"📷 화면 캡처 {r[2]:F0}×{r[3]:F0} · [{app}] — 무엇이 문제인지 적어 주세요", ["보내기", "취소"]) { Anchor = r };
        var outer = _memo;
        _memo = w;
        Native.RegisterHotKey(O.Hwnd, HK_ESC, Native.MOD_NOREPEAT, 0x1B);
        w.ShowDialog();
        _memo = outer;
        if (_memo == null && _mode == Mode.Idle) Native.UnregisterHotKey(O.Hwnd, HK_ESC);
        if (w.Result != "보내기") { System.IO.File.Delete(shot); return; }
        var t = new Target("", "Screen", "화면 캡처", r, null, 0, false, $"화면 캡처 {r[2]:F0}×{r[3]:F0}", app, win, false);
        var pk = Store.Add("shot", t, w.Box.Text, shot: shot);
        O.ToastAt($"캡처 보냄 (#{pk.No})");
    }

    IntPtr HookCb(int code, IntPtr w, IntPtr l)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(l);
            if (Handle((int)w, new Point(info.pt.X, info.pt.Y))) return 1; // 아래 앱으로 안 보냄
        }
        return Native.CallNextHookEx(_hook, code, w, l);
    }

    // 마우스 신호 하나 처리. true = 아래 앱으로 보내지 않고 막음
    bool Handle(int m, Point pt)
    {
        if (_area)
        {
            if (m == Native.WM_LBUTTONDOWN) { _areaFrom = pt; return true; }
            if (m == Native.WM_MOUSEMOVE && _areaFrom != null) O.Dispatcher.BeginInvoke(() => AreaMove(pt));
            if (m == Native.WM_LBUTTONUP && _areaFrom != null) { O.Dispatcher.BeginInvoke(() => { AreaMove(pt); AreaEnd(true); }); return true; }
            if (m == Native.WM_RBUTTONDOWN) { O.Dispatcher.BeginInvoke(() => AreaEnd(false)); return true; }
            if (m == Native.WM_RBUTTONUP) return true;
        }
        if (Rec.On && m == Native.WM_LBUTTONDOWN && _mode == Mode.Idle) Rec.OnClick(pt);
        if (_mode is Mode.Select or Mode.Move or Mode.Window)
        {
            if (m is Native.WM_LBUTTONDOWN or Native.WM_RBUTTONDOWN)
            {
                O.Dispatcher.BeginInvoke(() => { if (m == Native.WM_LBUTTONDOWN) Clicked(); else Exit(); });
                return true; // 아래 앱으로 클릭이 가지 않게 막는다
            }
            if (m is Native.WM_LBUTTONUP or Native.WM_RBUTTONUP) return true;
        }
        if (_mode == Mode.Draw)
        {
            if (m == Native.WM_RBUTTONDOWN) { O.Dispatcher.BeginInvoke(DrawDone); return true; }
            if (m == Native.WM_RBUTTONUP) return true;
            if (m == Native.WM_LBUTTONDOWN) { _drawing = true; _newLine = true; }
            if (m == Native.WM_LBUTTONUP) { _drawing = false; _strokes.Add([-1, -1]); }
            if (m == Native.WM_MOUSEMOVE && _drawing) O.Dispatcher.BeginInvoke(() => AddPen(pt));
            if (m is Native.WM_LBUTTONDOWN or Native.WM_LBUTTONUP) return true;
        }
        return false;
    }

    // 시험용 가짜 입력: 실제 마우스·키보드를 안 쓰고 같은 처리 길로 넣는다 (다른 앱엔 아무것도 안 감)
    public Point? FakeCursor;
    Point Cursor() => FakeCursor ?? Native.Cursor();
    public void Fake(int msg, Point pt) { FakeCursor = pt; Handle(msg, pt); }
    public void FakeEsc() => Native.PostMessage(O.Hwnd, Native.WM_HOTKEY, (IntPtr)HK_ESC, IntPtr.Zero);

    bool _drawing, _newLine;
    readonly List<Polyline> _lines = [];
    public int PenLines => _lines.Count;

    void AddPen(Point px)
    {
        _strokes.Add([px.X, px.Y]);
        var s = O.Scale;
        if (_newLine || _lines.Count == 0)
        {
            var l = new Polyline { Stroke = O.Pen.Stroke, StrokeThickness = O.Pen.StrokeThickness, StrokeLineJoin = PenLineJoin.Round };
            O.Canvas.Children.Add(l); _lines.Add(l); _newLine = false;
        }
        _lines[^1].Points.Add(new Point(px.X / s - SystemParameters.VirtualScreenLeft, px.Y / s - SystemParameters.VirtualScreenTop));
    }

    void Clicked()
    {
        if (_mode == Mode.Window)
        {
            // 마우스를 옮기자마자 누르면 테두리(70ms마다 갱신)가 아직 옛 창 → 클릭한 자리에서 다시 읽는다
            var c = Cursor();
            var h = Native.GetAncestor(Native.WindowFromPoint(new Native.POINT { X = (int)c.X, Y = (int)c.Y }), 2);
            var done = _onWindow;
            _onWindow = null; _winUnder = IntPtr.Zero;
            Exit();
            done?.Invoke(h);
            return;
        }
        if (_mode == Mode.Select && _cur != null) { _target = _cur; _mode = Mode.Busy; _timer.Stop(); ShowMemo(); }
        else if (_mode == Mode.Move && _anchor != null) { _mode = Mode.Busy; _timer.Stop(); ShowMemo(); }
    }

    void DrawDone() { _mode = Mode.Busy; _timer.Stop(); Native.ClickThrough(O.Hwnd, true); O.Background = Brushes.Transparent; O.Cursor = null; ShowMemo(); }

    async void ShowMemo()
    {
        var t = _target!;
        var head = t.Label + (_anchor != null ? $"\n→ 이동: {_anchor.Label} {_where}" : "") + (_strokes.Count > 0 ? "\n+ 그림 있음" : "");
        var w = new MemoWindow(head, ["저장", "이동", "그리기", "취소"]);
        var b = Store.R(t.Rect);
        w.Anchor = b; w.Left = b[0] / O.Scale; w.Top = (b[1] + b[3]) / O.Scale + 10;
        _memo = w;
        Native.RegisterHotKey(O.Hwnd, HK_ESC, Native.MOD_NOREPEAT, 0x1B); // 메모 창에 포커스가 없어도 Esc로 닫히게
        w.ShowDialog();
        _memo = null;
        switch (w.Result)
        {
            case "저장":
                var target = Store.ToTarget(t);
                var move = _anchor != null && _ghost != null ? new Move(Store.ToTarget(_anchor), _where, _ghost) : null;
                var strokes = _strokes.Count > 0 ? new List<double[]>(_strokes) : null;
                var memo = w.Box.Text;
                // 사진: 부품 + 그린 선 + 이동 자리를 다 감싸게, 주황 테두리·빨간 선이 화면에 보이는 채로 찍는다
                var area = Store.Union(target.Rect, strokes, move?.Rect, move?.Anchor.Rect);
                await Task.Delay(150); // 메모 창이 화면에서 사라진 뒤
                var shot = await Task.Run(() => Store.Shot(area, $"pick_{DateTime.Now:MMdd_HHmmss}"));
                Exit();
                var p = Store.Add(move != null ? "move" : strokes != null ? "draw" : "note", target, memo, move, strokes, shot);
                O.ToastAt($"대기열에 저장 (#{p.No})");
                break;
            case "이동": _anchor = null; Enter(Mode.Move); break;
            case "그리기": O.Place(O.Box, b, 2); Enter(Mode.Draw); break;
            default: Exit(); break;
        }
    }

    void ToggleRec()
    {
        if (!Rec.On)
        {
            var fg = Native.GetForegroundWindow();
            if (fg == O.Hwnd || fg == IntPtr.Zero) { O.ToastAt("녹화할 앱 창을 먼저 눌러 주세요"); return; }
            StartRec(fg);
            return;
        }
        StopRec();
    }

    void StartRec(IntPtr win)
    {
        Rec.Start(win);
        Tray.Icon = MakeIcon(System.Drawing.Color.FromArgb(0xe5, 0x53, 0x4b)); Tray.Text = TrayText;
        O.Place(O.RecBorder, Rec.Rect, 0);
        O.ToastAt($"● 녹화 시작 — 다시 Ctrl+Shift+{_keys.RecKey} 하면 끝");
    }

    void StopRec()
    {
        var w = new MemoWindow("■ 녹화 끝 — 한 줄 메모 (선택)", ["보내기", "취소"]);
        w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        O.RecBorder.Visibility = Visibility.Collapsed;
        Tray.Icon = MakeIcon(System.Drawing.Color.FromArgb(0xff, 0x6a, 0x00));
        var r = Rec.Stop("");
        _memo = w;
        Native.RegisterHotKey(O.Hwnd, HK_ESC, Native.MOD_NOREPEAT, 0x1B);
        w.ShowDialog();
        _memo = null;
        Native.UnregisterHotKey(O.Hwnd, HK_ESC);
        Tray.Text = TrayText;
        if (w.Result == "보내기") { Store.SaveRec(r with { Memo = w.Box.Text }); O.ToastAt($"녹화 보냄 ({r.Id}, {r.Seconds}초)"); }
        else { System.IO.File.Delete(r.Video); System.IO.File.Delete(System.IO.Path.Combine(Catalog.DataDir, r.Id + ".json")); }
    }


    public System.Windows.Forms.NotifyIcon Tray = null!;
    string TrayText => Rec.On ? $"UiNametag ● 녹화 중 (Ctrl+Shift+{_keys.RecKey}로 끝)" : $"UiNametag 켜짐 · 클릭하면 창 · Ctrl+Shift+{_keys.PickKey} 고르기";

    // 주황 이름표 모양 아이콘 (녹화 중엔 빨강)
    static System.Drawing.Icon MakeIcon(System.Drawing.Color c)
    {
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var br = new System.Drawing.SolidBrush(c);
            g.FillRectangle(br, 2, 8, 28, 16);
            g.FillEllipse(System.Drawing.Brushes.White, 6, 13, 6, 6);
        }
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    // 끄기 전에 마우스 옆에 "꺼짐"을 잠깐 보여 준다
    public async void QuitWithToast()
    {
        Exit();
        O.ToastAt("UiNametag 꺼짐");
        await Task.Delay(1500);
        Quit();
    }

    public void Quit()
    {
        if (Rec.On) Rec.Stop("");
        Tray.Visible = false; Tray.Dispose();
        Apps.ShortcutState(false);
        _main?.Close(); _popup?.Close();
        Native.UnhookWindowsHookEx(_hook);
        System.Windows.Application.Current.Shutdown();
    }

    // 시험용: 단축키 없이 같은 동작
    public void TestSelect() => Enter(Mode.Select);
    public bool AreaActive => _area;
    // 시험용: 메모 창을 실제와 같이 띄운다 (Esc 단축키 등록 포함, 포커스 없어도 Esc로 닫힘)
    public void TestMemo(MemoWindow w)
    {
        _memo = w;
        Native.RegisterHotKey(O.Hwnd, HK_ESC, Native.MOD_NOREPEAT, 0x1B);
        w.Closed += (_, _) => { _memo = null; Native.UnregisterHotKey(O.Hwnd, HK_ESC); };
        w.Show();
    }
    public void TestToggleRec() => ToggleRec();
    public Hit? Current => _cur;
}
