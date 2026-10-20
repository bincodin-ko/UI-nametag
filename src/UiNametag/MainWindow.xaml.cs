using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace UiNametag;

public partial class MainWindow : Window
{
    readonly Picker _p;
    string? _sel;            // 고른 앱 Id
    WinRow? _chosen;         // 등록할 창
    bool _deleteArmed;

    public MainWindow(Picker p)
    {
        _p = p;
        InitializeComponent();
        SourceInitialized += (_, _) => Native.DarkTitle(new WindowInteropHelper(this).Handle);
        Activated += (_, _) => { RefreshStatus(); if (NavQueue.IsChecked == true) RefreshQueue(); };
        var tick = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        tick.Tick += (_, _) => { if (IsVisible && NavQueue.IsChecked == true) RefreshQueue(); };
        tick.Start();
        DataDirText.Text = Catalog.DataDir;
        McpCommandBox.Text = Apps.McpCommand;
    }


    // page: apps · register · queue · settings
    public void Go(string page, string? appId = null)
    {
        if (appId != null) _sel = appId;
        RegisterPanel.Visibility = Visibility.Collapsed;
        (page switch { "queue" => NavQueue, "settings" => NavSettings, _ => NavApps }).IsChecked = true;
        Nav_Checked(null!, null!);
        if (page == "register") StartRegister();
    }

    void Nav_Checked(object s, RoutedEventArgs e)
    {
        if (AppsPage == null) return; // InitializeComponent 중
        AppsPage.Visibility = Show(NavApps.IsChecked == true);
        QueuePage.Visibility = Show(NavQueue.IsChecked == true);
        SettingsPage.Visibility = Show(NavSettings.IsChecked == true);
        if (NavApps.IsChecked == true) RefreshApps();
        if (NavQueue.IsChecked == true) RefreshQueue();
        if (NavSettings.IsChecked == true) RefreshSettings();
    }

    static Visibility Show(bool on) => on ? Visibility.Visible : Visibility.Collapsed;

    void RefreshStatus()
    {
        var claude = Apps.ClaudeConnected;
        ClaudeDot.Fill = AppRow.Res(claude ? "Ok" : "Warn");
        ClaudeText.Text = claude ? "Claude 연결됨" : "Claude 연결 안 됨";
        var s = Apps.LoadSettings();
        PickerDot.Fill = AppRow.Res(_p?.Rec.On == true ? "Bad" : "Ok");
        PickerText.Text = _p?.Rec.On == true ? "● 녹화 중" : $"켜짐 · Ctrl+Shift+{s.PickKey}";
        NavAppsCount.Text = Apps.List().Count.ToString();
        NavQueueCount.Text = Store.Picks().Count(x => !x.Delivered).ToString();
    }

    // ── 내 앱 ──────────────────────────────
    void RefreshApps()
    {
        var keep = _sel; // ItemsSource를 바꾸면 선택이 풀리며 SelectionChanged가 _sel을 지운다
        var rows = Apps.List().Select(a => new AppRow(a)).ToList();
        AppsList.ItemsSource = rows;
        RefreshStatus();
        if (RegisterPanel.Visibility == Visibility.Visible) return;
        var pick = rows.FirstOrDefault(r => r.A.Id == keep); // 처음엔 닫힌 채로 — 앱을 눌러야 자세히가 열린다
        AppsList.SelectedItem = pick;
        ShowDetail(pick);
    }

    // 이미 골라진 앱을 한 번 더 누르면 선택을 풀어 자세히 패널을 닫는다
    void AppsList_PreviewMouseLeftButtonDown(object s, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(AppsList, (DependencyObject)e.OriginalSource) is ListBoxItem { IsSelected: true })
        {
            AppsList.SelectedItem = null;
            e.Handled = true;
        }
    }

    void AppsList_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (AppsList.SelectedItem is not AppRow r)
        {
            if (RegisterPanel.Visibility != Visibility.Visible) { _sel = null; ShowDetail(null); }
            return;
        }
        _sel = r.A.Id;
        RegisterPanel.Visibility = Visibility.Collapsed;
        ShowDetail(r);
    }

    // 닫히면 오른쪽 칸을 접고 목록이 창 너비를 다 쓴다
    void SetDetailOpen(bool open)
    {
        DetailScroll.Visibility = Show(open);
        Grid.SetColumnSpan(AppsListPane, open ? 1 : 2);
    }

    void ShowDetail(AppRow? r)
    {
        var none = AppsList.Items.Count == 0;
        EmptyAppsPanel.Visibility = Show(r == null && none);
        AppDetail.Visibility = Show(r != null);
        SetDetailOpen(r != null || none);
        if (r == null) return;
        _deleteArmed = false; DeleteAppButton.Content = "지우기";
        var a = r.A;
        DetailInitial.Text = r.Initial;
        DetailName.Text = a.Name;
        DetailKind.Text = r.Kind;
        DetailStatus.Text = r.St.Text;
        DetailDot.Fill = r.Dot;
        DetailExe.Text = a.Exe.Length > 0 ? a.Exe : "(모름 — 창 안 부품 이름으로 알아봄)";
        DetailSource.Text = a.Source;
        DetailCount.Text = $"{a.Count}개";
        DetailBuilt.Text = a.Built == DateTime.MinValue ? "-" : a.Built.ToString("yyyy-MM-dd HH:mm");
        var help = r.St.Text switch
        {
            "폴더 없음" => "소스 폴더가 없어요. 폴더를 옮겼다면 이 앱을 지우고 새 위치로 다시 등록하세요.",
            "사전 0개" => "이 폴더에서 부품 이름(id·x:Name·Name)을 하나도 못 찾았어요. 소스 폴더가 맞는지, 화면 파일(.html·.tsx·.vue·.xaml 등)이 들어 있는지 확인하세요.",
            "소스 바뀜" => "사전을 만든 뒤 소스가 바뀌었어요. '사전 다시 만들기'를 누르면 새 위치가 반영돼요.",
            _ => null,
        };
        StatusHelp.Visibility = Show(help != null);
        StatusHelpText.Text = help ?? "";
        StatusHelp.BorderBrush = r.Dot;
        EntrySearch.Text = "";
        ShowEntries();
    }

    void ShowEntries()
    {
        if (AppsList.SelectedItem is not AppRow r) return;
        var q = EntrySearch.Text.Trim();
        var all = Catalog.For(r.A).SelectMany(g => g).Where(e => q.Length == 0 || e.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        EntriesList.ItemsSource = all.Take(q.Length == 0 ? 8 : 50).Select(e => new EntryRow(e.Id, $"→ {e.File}:{e.Line}"));
        EntriesCount.Text = q.Length == 0 ? $"{r.A.Count}개 중 {Math.Min(8, all.Count)}개" : $"찾음 {all.Count}개";
        EntriesBox.Visibility = Show(r.A.Count > 0);
    }

    void EntrySearch_TextChanged(object s, TextChangedEventArgs e)
    {
        EntrySearchHint.Visibility = Show(EntrySearch.Text.Length == 0);
        ShowEntries();
    }

    async void RebuildButton_Click(object s, RoutedEventArgs e)
    {
        var id = _sel!;
        BuildingPanel.Visibility = Visibility.Visible; RebuildButton.IsEnabled = false;
        try { await Task.Run(() => Apps.Rebuild(id)); }
        finally { BuildingPanel.Visibility = Visibility.Collapsed; RebuildButton.IsEnabled = true; }
        RefreshApps();
    }

    void OpenSourceButton_Click(object s, RoutedEventArgs e)
    {
        if (AppsList.SelectedItem is AppRow r && Directory.Exists(r.A.Source)) Process.Start("explorer.exe", r.A.Source);
    }

    // 실수로 지우지 않게 두 번 눌러야 지운다 (사전 파일만 지움, 소스는 안 건드림)
    void DeleteAppButton_Click(object s, RoutedEventArgs e)
    {
        if (!_deleteArmed) { _deleteArmed = true; DeleteAppButton.Content = "한 번 더 누르면 지움"; return; }
        Apps.Remove(_sel!);
        _sel = null;
        RefreshApps();
    }

    // ── 새 앱 등록 ──────────────────────────
    void NewAppButton_Click(object s, RoutedEventArgs e) => StartRegister();

    void StartRegister()
    {
        AppsList.SelectedItem = null;
        EmptyAppsPanel.Visibility = AppDetail.Visibility = Visibility.Collapsed;
        RegisterPanel.Visibility = Visibility.Visible;
        SetDetailOpen(true);
        _chosen = null;
        ChosenWindowText.Text = "아직 고른 창 없음";
        AppNameBox.Text = WebUrlBox.Text = SourceBox.Text = "";
        RegisterError.Visibility = Visibility.Collapsed;
        LoadWindows();
        SuggestList.ItemsSource = Apps.Suggest("");
        PickWindowButton.Focus();
    }

    void CancelRegisterButton_Click(object s, RoutedEventArgs e) { RegisterPanel.Visibility = Visibility.Collapsed; RefreshApps(); }

    void RefreshWindowsButton_Click(object s, RoutedEventArgs e) => LoadWindows();

    void LoadWindows()
    {
        WindowsList.ItemsSource = Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.Length > 0 && p.Id != Environment.ProcessId)
            .Select(p => Row(p)).Where(r => r != null).OrderBy(r => r!.Title).ToList();
    }

    static WinRow? Row(Process p)
    {
        try
        {
            string exe = "";
            try { exe = p.MainModule?.FileName ?? ""; } catch { }
            return new WinRow(p.MainWindowTitle, p.ProcessName, exe, Apps.KindOf(exe, p.ProcessName));
        }
        catch { return null; }
    }

    void WindowsList_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (WindowsList.SelectedItem is WinRow w) Choose(w);
    }

    // 화면에서 창 클릭: 이 창을 잠깐 내리고 picker의 창 고르기 모드
    void PickWindowButton_Click(object s, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        _p.PickWindow(h =>
        {
            WindowState = WindowState.Normal; Activate();
            if (h == IntPtr.Zero) return;
            Native.GetWindowThreadProcessId(h, out var pid);
            try { if (Row(Process.GetProcessById((int)pid)) is { } w) Choose(w with { Title = w.Title.Length > 0 ? w.Title : w.Process }); } catch { }
        });
    }

    void Choose(WinRow w)
    {
        _chosen = w;
        ChosenWindowText.Text = $"고른 창: {w.Title} · {w.Kind}" + (w.Exe.Length > 0 ? $" · {w.Exe}" : "");
        var self = w.Exe.Length > 0 && string.Equals(w.Exe, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        AppNameBox.Text = self ? "UiNametag (이 앱)" : w.Kind == "웹사이트" ? "" : w.Process;
        var sug = Apps.Suggest(w.Kind == "웹사이트" ? w.Title.Split(' ', '-', '—')[0] : w.Process);
        if (self && Apps.SelfSource() is { } me) sug.Insert(0, me);
        SuggestList.ItemsSource = sug;
        if (SourceBox.Text.Length == 0 && sug.Count > 0) SourceBox.Text = sug[0];
    }

    void Suggest_Click(object s, RoutedEventArgs e) => SourceBox.Text = (string)((FrameworkElement)s).Tag;

    void SourceBox_TextChanged(object s, TextChangedEventArgs e)
    {
        if (AppNameBox.Text.Length == 0 && Directory.Exists(SourceBox.Text)) AppNameBox.Text = Apps.NameOf(SourceBox.Text);
    }

    void BrowseButton_Click(object s, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = "앱 소스 폴더 고르기" };
        if (Directory.Exists(SourceBox.Text)) d.InitialDirectory = SourceBox.Text;
        if (d.ShowDialog(this) == true) SourceBox.Text = d.FolderName;
    }

    async void BuildButton_Click(object s, RoutedEventArgs e)
    {
        var src = SourceBox.Text.Trim();
        var name = AppNameBox.Text.Trim();
        var url = WebUrlBox.Text.Trim();
        string? err = !Directory.Exists(src) ? "소스 폴더가 없어요. ② 에서 폴더를 골라 주세요."
            : Apps.List().Any(a => Apps.SameDir(a.Source, src)) ? "이 소스 폴더는 이미 등록돼 있어요."
            : null;
        RegisterError.Text = err ?? "";
        RegisterError.Visibility = Show(err != null);
        if (err != null) return;
        if (name.Length == 0) name = Apps.NameOf(src);
        var kind = url.Length > 0 ? "웹사이트" : _chosen?.Kind ?? "";
        var exe = url.Length > 0 ? url : _chosen?.Kind == "웹사이트" ? "" : _chosen?.Exe ?? "";
        BuildButton.IsEnabled = false; RegisterBar.Visibility = Visibility.Visible;
        try
        {
            var a = await Task.Run(() => Apps.Register(name, kind, exe, src));
            _sel = a.Id;
            RegisterPanel.Visibility = Visibility.Collapsed;
            RefreshApps();
        }
        catch (Exception ex) { RegisterError.Text = "사전을 못 만들었어요: " + ex.Message; RegisterError.Visibility = Visibility.Visible; }
        finally { BuildButton.IsEnabled = true; RegisterBar.Visibility = Visibility.Collapsed; }
    }

    // ── 대기열 ──────────────────────────────
    void RefreshQueue()
    {
        var all = Store.Picks();
        _checked.RemoveWhere(n => !all.Any(p => p.No == n && !p.Done)); // 고쳤거나 지운 항목은 체크에서 뺌
        var picks = all.OrderByDescending(p => p.No).Select(p => new PickRow(p, _checked)).ToList();
        var recAll = Store.Recs();
        _checkedRecs.RemoveWhere(id => !recAll.Any(r => r.Id == id && !r.Done));
        var recs = recAll.Select(r => new RecRow(r, _checkedRecs)).ToList();
        PicksList.ItemsSource = picks; PicksEmpty.Visibility = Show(picks.Count == 0);
        RecsList.ItemsSource = recs; RecsEmpty.Visibility = Show(recs.Count == 0);
        AskClaudeButton.IsEnabled = CopyPromptButton.IsEnabled = picks.Any(p => !p.P.Done) || recs.Any(r => !r.R.Done);
        UpdateSendButtons();
        RefreshStatus();
    }

    void Thumb_Click(object s, RoutedEventArgs e)
    {
        if (((PickRow)((FrameworkElement)s).Tag).P.Shot is { } f && File.Exists(f)) Process.Start(new ProcessStartInfo(f) { UseShellExecute = true });
    }

    void RecThumb_Click(object s, RoutedEventArgs e)
    {
        if (((RecRow)((FrameworkElement)s).Tag).R.Video is { } v && File.Exists(v)) Process.Start(new ProcessStartInfo(v) { UseShellExecute = true });
    }

    void DeletePick_Click(object s, RoutedEventArgs e) { Store.RemovePick(((PickRow)((FrameworkElement)s).Tag).P.No); RefreshQueue(); }
    void DeleteRec_Click(object s, RoutedEventArgs e) { Store.RemoveRec(((RecRow)((FrameworkElement)s).Tag).R); RefreshQueue(); }
    void ClearTakenButton_Click(object s, RoutedEventArgs e) { Store.RemoveDone(); RefreshQueue(); }

    // 대기열 → Claude Code 연동: 새 터미널에서 Claude Code를 켜고 가져가 고치게 한다 (대화형이라 사용자가 보고 답할 수 있음)
    // 보낼 항목: 체크한 것(아직 안 고친 것만), 아무것도 안 골랐으면 안 고친 것 전부
    // 보낼 것: 체크한 것(아직 안 고친 것만). 아무것도 안 골랐으면 안 고친 부품·녹화 전부
    public static (List<Pick> Picks, List<Recording> Recs) ToSend(ICollection<int>? sel = null, ICollection<string>? selRecs = null)
    {
        var picks = Store.Picks().Where(p => !p.Done).ToList();
        var recs = Store.Recs().Where(r => !r.Done).ToList();
        var pp = picks.Where(p => sel?.Contains(p.No) == true).ToList();
        var rr = recs.Where(r => selRecs?.Contains(r.Id) == true).ToList();
        return pp.Count + rr.Count > 0 ? (pp, rr) : (picks, recs);
    }

    readonly HashSet<int> _checked = []; // 대기열에서 체크한 번호
    readonly HashSet<string> _checkedRecs = []; // 체크한 녹화

    void PickCheck_Click(object s, RoutedEventArgs e) => UpdateSendButtons();

    void UpdateSendButtons()
    {
        var n = Store.Picks().Count(p => !p.Done && _checked.Contains(p.No)) + Store.Recs().Count(r => !r.Done && _checkedRecs.Contains(r.Id));
        AskClaudeButton.Content = n > 0 ? $"Claude에게 고치게 하기 ({n})" : "Claude에게 고치게 하기";
        CopyPromptButton.Content = n > 0 ? $"지시문 복사 ({n})" : "지시문 복사";
    }

    public static (string Exe, string Args, string Dir)? ClaudeCommand(ICollection<int>? sel = null, ICollection<string>? selRecs = null)
    {
        var (todo, recs) = ToSend(sel, selRecs);
        if (todo.Count + recs.Count == 0) return null;
        // 일할 폴더: 항목들의 앱 소스 폴더가 하나면 거기, 아니면 가장 많은 것 (캡처·녹화는 소스가 없음)
        var dir = todo.Select(p => p.Target.Source).Where(Directory.Exists).GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault()
            ?? Apps.List().Select(a => a.Source).FirstOrDefault(Directory.Exists) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var prompt = ClaudePrompt(todo, recs);
        return ("wt.exe", $"-d \"{dir}\" claude \"{prompt.Replace('"', '\'')}\"", dir);
    }

    // Claude에게 줄 지시문 (새 터미널로 켤 때와 복사할 때 같은 글)
    public static string ClaudePrompt(List<Pick> todo, List<Recording>? recs = null)
    {
        recs ??= [];
        var what = new List<string>();
        if (todo.Count > 0) what.Add($"get_picks로 UiNametag 대기열(번호 {string.Join(",", todo.Select(p => p.No))})");
        if (recs.Count > 0) what.Add($"get_recording으로 녹화({string.Join(",", recs.Select(r => r.Id))})");
        return $"uinametag MCP의 {string.Join("와 ", what)}을 가져와서 고쳐줘. " +
            (todo.Count > 0 ? "Action=shot은 Shot 경로 그림을 열어 보고, 항목마다 Source 폴더와 File:Line을 기준으로 고쳐. " : "") +
            (recs.Count > 0 ? "녹화는 일어난 일 표(클릭한 부품·코드 위치·상태 변화)와 장면 그림을 보고 메모에 적힌 문제를 찾아 고쳐. " : "") +
            "뜻이 애매하면 고치기 전에 나한테 물어봐. 실제로 고친 것만 mark_done으로 표시해(항목은 numbers, 녹화는 recordings).";
    }

    // 이미 열어 둔 터미널(Claude Code)에 붙여 넣을 수 있게 지시문만 복사
    void CopyPromptButton_Click(object s, RoutedEventArgs e)
    {
        var (todo, recs) = ToSend(_checked, _checkedRecs);
        if (todo.Count + recs.Count == 0) { QueueHint.Text = "고칠 항목이 없어요."; return; }
        Clipboard.SetText(ClaudePrompt(todo, recs));
        QueueHint.Text = $"지시문을 복사했어요 (항목 {todo.Count}개, 녹화 {recs.Count}개). 원하는 터미널의 Claude Code에 붙여 넣으세요.";
    }

    void AskClaudeButton_Click(object s, RoutedEventArgs e)
    {
        if (ClaudeCommand(_checked, _checkedRecs) is not { } c) { QueueHint.Text = "고칠 항목이 없어요."; return; }
        try { Process.Start(new ProcessStartInfo(c.Exe, c.Args) { UseShellExecute = true }); QueueHint.Text = $"Claude Code를 켰어요 ({c.Dir}). 고치면 '✓ 고침 완료'로 바뀌어요."; }
        catch (Exception ex) { QueueHint.Text = "Claude Code를 못 켰어요: " + ex.Message; }
    }

    // ── 설정 ──────────────────────────────
    void RefreshSettings()
    {
        var st = Apps.LoadSettings();
        PickKeyBox.Text = st.PickKey; RecKeyBox.Text = st.RecKey; ShotKeyBox.Text = st.ShotKey;
        KeysResult.Text = "";
        AutostartSwitch.IsChecked = Apps.Autostart;
        ClaudeStateText.Text = Apps.ClaudeConnected ? "● 지금 Claude가 연결돼 있어요." : "지금 연결된 Claude가 없어요. 아래 명령으로 한 번 등록하세요.";
        RefreshStatus();
    }

    void SaveKeysButton_Click(object s, RoutedEventArgs e)
    {
        var keys = new[] { PickKeyBox.Text, RecKeyBox.Text, ShotKeyBox.Text }.Select(k => k.Trim().ToUpper()).ToArray();
        if (keys.Any(k => k.Length != 1 || !char.IsLetterOrDigit(k[0])) || keys.Distinct().Count() < 3)
        { KeysResult.Text = "글자나 숫자 하나씩, 서로 다르게 넣어 주세요."; return; }
        var st = Apps.LoadSettings() with { PickKey = keys[0], RecKey = keys[1], ShotKey = keys[2] };
        var bad = _p.ApplyKeys(st);
        if (bad != null) { KeysResult.Text = $"{bad}은(는) 다른 프로그램이 쓰고 있어요. 다른 글자를 골라 주세요."; _p.ApplyKeys(Apps.LoadSettings()); return; }
        Apps.SaveSettings(st);
        KeysResult.Text = "적용됨";
        RefreshStatus();
    }

    void AutostartSwitch_Click(object s, RoutedEventArgs e)
    {
        try { Apps.SetAutostart(AutostartSwitch.IsChecked == true); }
        catch (Exception ex) { AutostartSwitch.IsChecked = Apps.Autostart; ClaudeStateText.Text = "자동 켜기 설정 실패: " + ex.Message; }
    }

    void CopyMcpButton_Click(object s, RoutedEventArgs e) { Clipboard.SetText(McpCommandBox.Text); CopyMcpButton.Content = "복사됨"; }

    void OpenDataDirButton_Click(object s, RoutedEventArgs e) => Process.Start("explorer.exe", Catalog.DataDir);
}
