using System.Windows;
using System.Windows.Media;

namespace UiNametag;

// 화면 목록에 보이는 한 줄들
public class AppRow(AppInfo a)
{
    public AppInfo A { get; } = a;
    public (string Text, string Level) St { get; } = Apps.Status(a);
    public string Name => A.Name;
    public string Initial => A.Name.Length > 0 ? A.Name[..1].ToUpper() : "?";
    public string Kind => A.Kind.Length > 0 ? A.Kind : "앱";
    public string Source => A.Source;
    public string Sub => $"{St.Text} · 사전 {A.Count}개";
    public Brush Dot => Res(St.Level switch { "ok" => "Ok", "warn" => "Warn", _ => "Bad" });
    public static Brush Res(string key) => (Brush)Application.Current.FindResource(key);
}

public class PickRow(Pick p, HashSet<int>? sel = null)
{
    public Pick P { get; } = p;
    public bool Sel { get => sel?.Contains(P.No) == true; set { if (sel == null) return; if (value) sel.Add(P.No); else sel.Remove(P.No); } }
    public Visibility SelVis => P.Done ? Visibility.Hidden : Visibility.Visible;
    public string Head => $"#{P.No} · {(P.Target.Outside ? "사전 밖 " : "")}{P.Target.App} · {Act}";
    public string Label => P.Target.Label;
    public string Memo => P.Memo.Length > 0 ? P.Memo : "(메모 없음)";
    public string Act => P.Action switch { "move" => "이동", "draw" => "그리기", "shot" => "캡처", _ => "메모" };
    public System.Windows.Media.ImageSource? Thumb
    {
        get
        {
            if (P.Shot == null || !System.IO.File.Exists(P.Shot)) return null;
            var b = new System.Windows.Media.Imaging.BitmapImage();
            b.BeginInit(); b.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; // 파일을 잡고 있지 않게
            b.DecodePixelWidth = 280; b.UriSource = new Uri(P.Shot); b.EndInit(); b.Freeze();
            return b;
        }
    }
    public Visibility ThumbVis => Thumb != null ? Visibility.Visible : Visibility.Collapsed;
    public string Taken => P.Done ? "✓ 고침 완료" : P.Delivered ? "Claude가 읽음 (아직 안 고침)" : "대기 중";
    public Brush TakenBrush => AppRow.Res(P.Done ? "Ok" : P.Delivered ? "Muted" : "Accent");
}

public class RecRow(Recording r, HashSet<string>? sel = null)
{
    public Recording R { get; } = r;
    public bool Sel { get => sel?.Contains(R.Id) == true; set { if (sel == null) return; if (value) sel.Add(R.Id); else sel.Remove(R.Id); } }
    public Visibility SelVis => R.Done ? Visibility.Hidden : Visibility.Visible;
    public string Taken => R.Done ? "✓ 고침 완료" : "대기 중";
    public Brush TakenBrush => AppRow.Res(R.Done ? "Ok" : "Accent");
    public string Head => $"{R.Id} · {R.Seconds:F1}초";
    public string Memo => R.Memo.Length > 0 ? R.Memo : "(메모 없음)";
    public string Window => $"창 \"{R.Window}\" · {R.Start:MM-dd HH:mm}";
}

public record EntryRow(string Id, string Where);

public record WinRow(string Title, string Process, string Exe, string Kind)
{
    public string Sub => $"{Process} · {Kind}";
}
