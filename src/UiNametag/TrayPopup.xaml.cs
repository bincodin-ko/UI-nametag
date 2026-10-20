using System.Windows;
using System.Windows.Input;

namespace UiNametag;

public partial class TrayPopup : Window
{
    readonly Picker _p;
    public DateTime HiddenAt;

    public TrayPopup(Picker p)
    {
        _p = p;
        InitializeComponent();
        // 포커스를 실제로 받은 뒤 잃을 때만 닫는다 (다른 창이 포커스를 안 내주면 뜨자마자 사라지던 문제)
        Activated += (_, _) => _active = true;
        Deactivated += (_, _) => { if (_active) Close2(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close2(); };
        SizeChanged += (_, _) => Place();
    }

    bool _active;
    void Close2() { if (IsVisible) { Hide(); HiddenAt = DateTime.Now; _active = false; } }

    public void Open()
    {
        Refresh();
        Show(); Place(); Activate();
        PickTile.Focus();
    }

    // 작업 표시줄 오른쪽 위 (주 모니터 작업 영역)
    void Place()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth;
        Top = wa.Bottom - ActualHeight;
    }

    public void Refresh()
    {
        var s = Apps.LoadSettings();
        var claude = Apps.ClaudeConnected;
        StatusText.Text = (_p.Rec.On ? "● 녹화 중" : "켜짐") + " · Claude " + (claude ? "연결됨" : "연결 안 됨");
        StatusDot.Fill = AppRow.Res(_p.Rec.On ? "Bad" : claude ? "Ok" : "Warn");
        PickKeyText.Text = $"Ctrl+Shift+{s.PickKey}";
        RecKeyText.Text = $"Ctrl+Shift+{s.RecKey}";
        RecTileText.Text = _p.Rec.On ? "녹화 끝" : "녹화";
        var picks = Store.Picks();
        QueueCount.Text = picks.Count(x => !x.Delivered).ToString();
        QueueSub.Text = $"전체 {picks.Count}개";
        var apps = Apps.List().Select(a => new AppRow(a)).ToList();
        PopupApps.ItemsSource = apps.Take(4);
        PopupEmpty.Visibility = apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MoreApps.Text = $"외 {apps.Count - 4}개 — 전체 창에서 보기";
        MoreApps.Visibility = apps.Count > 4 ? Visibility.Visible : Visibility.Collapsed;
    }

    void PickTile_Click(object s, RoutedEventArgs e) { Close2(); _p.StartPick(); }
    void RecTile_Click(object s, RoutedEventArgs e) { Close2(); _p.RecFromUi(); }
    void QueueTile_Click(object s, RoutedEventArgs e) { Close2(); _p.ShowMain("queue"); }
    void AddAppButton_Click(object s, RoutedEventArgs e) { Close2(); _p.ShowMain("register"); }
    void AppRow_Click(object s, RoutedEventArgs e) { Close2(); _p.ShowMain("apps", ((AppRow)((FrameworkElement)s).Tag).A.Id); }
    void OpenMainButton_Click(object s, RoutedEventArgs e) { Close2(); _p.ShowMain("apps"); }
    void SettingsButton_Click(object s, RoutedEventArgs e) { Close2(); _p.ShowMain("settings"); }
    void QuitButton_Click(object s, RoutedEventArgs e) { Close2(); _p.QuitWithToast(); }
}
