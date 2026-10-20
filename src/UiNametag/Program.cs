using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UiNametag;

public static class Program
{
    // UiNametag.exe                 상주 picker (Ctrl+Shift+G 고르기, Ctrl+Shift+R 녹화)
    // UiNametag.exe build-catalog D  사전 만들기
    // UiNametag.exe mcp              Claude용 MCP 서버 (stdio)
    // UiNametag.exe selftest P       자동 확인 (P = 시험 앱 소스 폴더)
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.FirstOrDefault() != "mcp") Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        switch (args.FirstOrDefault())
        {
            case "build-catalog":
                var built = Apps.BuildOrRegister(args[1]);
                Console.WriteLine($"[{built.Name}] 사전 항목 {built.Count}개");
                return 0;
            case "probe": // probe x y : 그 점의 부품과 부모 사슬 출력
                var pt = new System.Windows.Point(double.Parse(args[1]), double.Parse(args[2]));
                var el = System.Windows.Automation.AutomationElement.FromPoint(pt);
                for (var p = el; p != null; p = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(p))
                    Console.WriteLine($"pid={p.Current.ProcessId} {Catalog.Kind(p.Current.ControlType)} id='{p.Current.AutomationId}' name='{p.Current.Name}' rect={p.Current.BoundingRectangle}");
                Console.WriteLine("resolve: " + Catalog.Resolve(pt)?.Label);
                return 0;
            case "snap": // snap 페이지 폭 높이 파일.png : 메인 창을 화면 밖에 띄워 그림으로 (겉모습·반응형 확인용)
                var sa = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                LoadTheme(sa);
                sa.Startup += (_, _) =>
                {
                    var w = new MainWindow(null!) { Left = -4000, ShowActivated = false, ShowInTaskbar = false, Width = double.Parse(args[2]), Height = double.Parse(args[3]) };
                    w.Show(); w.Go(args[1]); w.UpdateLayout();
                    sa.Dispatcher.BeginInvoke(() =>
                    {
                        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        var dv = new System.Windows.Media.DrawingVisual(); // 창 바탕색까지 (Content만 그리면 바탕이 흰색으로 나옴)
                        using (var dc = dv.RenderOpen()) { dc.DrawRectangle(w.Background, null, new Rect(0, 0, w.ActualWidth, w.ActualHeight)); dc.DrawRectangle(new System.Windows.Media.VisualBrush((System.Windows.Media.Visual)w.Content), null, new Rect(0, 0, ((FrameworkElement)w.Content).ActualWidth, ((FrameworkElement)w.Content).ActualHeight)); }
                        bmp.Render(dv);
                        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                        using (var f = System.IO.File.Create(args[4])) enc.Save(f);
                        sa.Shutdown();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                };
                sa.Run();
                return 0;
            case "mcp":
                var b = Host.CreateApplicationBuilder();
                b.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
                b.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
                b.Build().Run();
                return 0;
            case "selftest":
                var t = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                LoadTheme(t);
                int code = 1;
                t.Startup += async (_, _) => { code = await Selftest.Run(args[1]); t.Shutdown(); };
                t.Run();
                return code;
            default:
                using (var single = new Mutex(true, "UiNametag.picker", out var first))
                {
                    // 이미 켜져 있으면 다시 실행 = 끄기 (바탕화면 아이콘 한 번 더 더블클릭)
                    using var quit = new EventWaitHandle(false, EventResetMode.AutoReset, "UiNametag.quit");
                    // UiNametag.exe open = 켜면서 전체 창 열기 (바탕화면 바로가기). 켜져 있으면 끄기
                    var wantOpen = args.FirstOrDefault() == "open";
                    if (!first) { quit.Set(); return 0; }
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    LoadTheme(app);
                    app.Startup += (_, _) =>
                    {
                        var p = new Picker();
                        ThreadPool.RegisterWaitForSingleObject(quit, (_, _) => p.O.Dispatcher.BeginInvoke(p.QuitWithToast), null, -1, true);
                        Apps.ShortcutState(true);
                        p.O.ToastAt("UiNametag 켜짐"); // 끌 때 "꺼짐"처럼 켤 때도 마우스 옆에 알림
                        if (wantOpen) p.ShowMain("apps");
                    };
                    app.Run();
                }
                return 0;
        }
    }

    // 색·버튼 모양(Theme.xaml)을 앱 전체에
    static void LoadTheme(Application app) =>
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/UiNametag;component/Theme.xaml", UriKind.Relative)));
}
