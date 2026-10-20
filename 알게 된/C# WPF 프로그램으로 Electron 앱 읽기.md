# C# WPF 프로그램으로 Electron 앱 읽기 (2026-10-20)

UI 이름표 picker는 C# WPF로 만든다. 그런데 시험 대상 IINA는 WPF가 아니라 Electron 앱이다. 그래도 읽을 수 있는지 직접 확인했다.

## 핵심
- **picker를 무엇으로 만드는지**(WPF)와 **읽을 앱이 무엇으로 만들어졌는지**(Electron)는 상관없다.
- 둘 사이를 이어 주는 건 Windows의 **UI Automation**. 화면 읽기 프로그램(내레이터 등)이 쓰는 그 기능이다.
- Windows 앱은 WPF든 WinForms든 Electron이든 UI Automation에 "내 화면엔 이런 부품이 있다"를 알려 준다.

```
[picker — C# WPF] ──묻기──▶ [Windows UI Automation] ◀──알려 줌── [IINA — Electron(Chromium)]
                     "마우스 아래 부품 뭐야?"            "버튼, id=settings"
```

## Electron에서는 무엇이 넘어오나
- Electron 안에는 Chromium(크롬 엔진)이 들어 있고, Chromium이 HTML 부품을 UI Automation 부품으로 바꿔 준다.
- **HTML `id` → UI Automation의 `AutomationId`** 로 그대로 넘어온다.
- 그래서 `AutomationId`로 소스의 HTML을 찾으면 파일:줄이 나온다.

| WPF 앱 | Electron 앱 |
|---|---|
| XAML `x:Name="AddButton"` | HTML `id="settings"` |
| → AutomationId `AddButton` | → AutomationId `settings` |
| 사전: `.xaml` 파일 읽기 | 사전: `.html` 파일 읽기 |

## 실제 확인 (IINA 시작 창)
PowerShell에서 .NET UI Automation으로 읽음(마우스 안 씀).

| 읽은 AutomationId | 종류 | 소스 |
|---|---|---|
| `settings` | Button | `src/renderer/welcome.html:50` |
| `open` | Button | `welcome.html:52` |
| `openurl` | Button | `welcome.html:52` |
| `recent` | List | `welcome.html:54` |

## 주의
1. **같은 id가 여러 파일에 있을 수 있다.** `open`은 `welcome.html`과 `openurl.html` 둘 다 있음 → 창 제목·페이지로 어느 파일인지 가린다.
2. **id 없는 부품이 많다.** 시작 창 부품 33개 중 id가 있는 건 5개 → 나머지는 종류·글자·가까운 부모의 id로 추정.
3. 부품 이름 글자가 PowerShell 출력에서 깨져 보였다(한글 인코딩 문제, 읽기 자체는 됨). C# 프로그램 안에서는 문제없다.

## 정리
- WPF로 만든 picker 하나로 WPF·WinForms·Electron 앱을 다 읽을 수 있다.
- 다른 건 **사전 만드는 쪽**뿐: 앱 종류에 따라 `.xaml` / `*.Designer.cs` / `.html`을 읽는다.
