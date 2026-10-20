# UI 이름표 프로그램 계획 (2026-10-19)

## 목표
하나의 프로그램을 만들어 MCP로 Claude와 연결한다. 그 안에서 웹·APK·데스크톱 앱·게임을 실행하고, 내 프로젝트 폴더를 연동하면 부품마다 표준 이름(한국어+영어)과 코드 위치를 붙인다. 아코디언·스크롤 체이닝 같은 효과·동작 이름도 예시와 함께 보여 준다.

그림: `ui-nametag-구상.png`(번호 이름표 예시), `ui-nametag-전체흐름.png`(전체 흐름)

## 흐름
0. **부품 사전 만들기** (처음 한 번 + 코드 바뀌면 다시)
   - 소스코드에서 화면·부품 전부 뽑기 (아코디언 안쪽·모달·탭·오류 상태 포함: 실행 화면이 아니라 코드라 안 놓침)
   - 표준 이름(한/영) + 코드 위치 → 사전 HTML
1. **내가 직접 가리키기**
   - 제품 실행 → 고칠 화면·상태까지 내가 이동 → 단축키(`Ctrl+Shift+G`) → 투명 선택 레이어 → 마우스 올리면 테두리 + 이름 → 클릭 + 메모 → 대기열
   - 선택 레이어 = 별도 작은 프로그램(AI 아님). 운영체제가 주는 부품 정보 + 사전으로 이름 표시
2. **AI가 고치기**
   - `get_picks()` → 사전에서 코드 위치 → 수정 → 다시 실행·`capture()` → 전후 비교 그림

## MCP 도구
이름표 MCP 자체에는 클릭·입력 도구를 넣지 않는다. 앱 실행·클릭·다시 찍기는 Windows-MCP(설치됨, 2026-10-19)에 맡긴다.
- `build_catalog(project)` 사전 만들기
- `get_picks()` 내가 고른 것(메모·이동 요청·그림 포함) 가져오기
- `catalog_lookup(name 또는 AutomationId)` → 파일:줄
- `preview_plan(plan)` AI가 이해한 결과를 앱 위에 보라 점선으로 그려 보여 줌 → 내가 OK해야 코드 수정
- `check_layout(element, target_rect)` 고친 뒤 실제 위치와 내가 그린 목표 칸의 겹침 % (기준 80%)
- `get_recording(id)` 녹화 묶음 가져오기(표 + 핵심 장면 몇 장, 영상 전체는 안 읽음)
- ~~`capture(target)`~~ → Windows-MCP `Screenshot(region)`으로 대체

## 만드는 방식 (2026-10-20 결정: 전부 C#)
- **C# (WPF, .NET 10) 프로그램 하나** = picker + 이름표 MCP 서버 + 사전 생성기 + 녹화. Python 안 씀.
- WPF를 고른 이유: 마우스가 통과하는 투명 창(선택 레이어)이 WinUI 3보다 쉽다.
- MCP 서버: 공식 C# SDK(`ModelContextProtocol` NuGet)로 같은 프로그램 안에 둔다.
- 한 프로그램이라 좋은 점: `preview_plan`이 호출되면 바로 화면에 그림 · `check_layout`이 UI Automation을 바로 읽음 · 녹화는 Windows 기본 화면 캡처(Windows.Graphics.Capture, 앱 창만, GPU 화면도 안 검게) · exe 하나만 켜면 끝
- 사전 생성기: XAML(`x:Name`)·WinForms(`*.Designer.cs`의 `Name`) 읽기 → catalog.json
- 버린 안: picker만 C# + 나머지 Python(파일로 주고받음) → 설치 두 벌·그리기 지연·좌표 어긋남 위험
- 준비: .NET 10 SDK(winget `Microsoft.DotNet.SDK.10`), 스킬 `winui-app`, MCP `microsoft-learn`(Windows API 문서)

## 시험 대상: IINA (2026-10-20 확인)
- 실행: `C:\Users\gusql\Desktop\IINA.lnk` → `AppData\Local\Programs\IINA\IINA.exe` · 소스: `C:\Users\gusql\Desktop\iina` (Electron 44, 화면은 `src/renderer/*.html`, id 991개)
- **확인됨:** Electron(Chromium)도 UI Automation의 AutomationId = HTML `id` → 사전으로 파일:줄 바로 찾기 가능
  - 예: 시작 창에서 읽은 `settings`·`open`·`openurl`·`recent` → `welcome.html:50·52·54`
- 주의 1: 같은 id가 여러 파일에 있음(`open`은 welcome.html·openurl.html) → 창 제목·페이지로 어느 파일인지 가려야 함
- 주의 2: 시작 창 부품 33개 중 id 있는 건 5개 → id 없는 부품은 종류·글자·부모 id로 추정
- 그래서 사전 생성기는 XAML뿐 아니라 HTML(`id`) 읽기도 넣는다

## Windows-MCP 활용 (Windows 앱)
그림: `ui-nametag-windows-mcp.png`
- 코드 위치: XAML `x:Name` / WinForms `Name` = 실행 중 UIA `AutomationId` → 사전 키로 쓰면 파일:줄이 바로 나옴
- Windows-MCP가 하는 것: `App` 실행 · `Snapshot` 부품 트리 · `Screenshot(region)` 다시 찍기 · `Click`/`WaitFor`로 고친 뒤 같은 화면·상태까지 이동
- 직접 만들 것: 아래 "만드는 방식"의 C# 프로그램 하나
- picker 만들기 전 임시 방법: 마우스를 부품에 올리고 "이거" → `Screenshot`의 커서 위치 둘레를 `Snapshot(region)`으로 읽기
- 주의: Windows-MCP의 Click·Type은 실제 마우스·키보드를 움직임 → AI가 쓰는 동안 PC 안 쓰기

## "어디로 옮겨"를 그림으로 (이동·그리기)
말로는 "위로"가 목록 위인지 제목 옆인지 안 통함 → 말 대신 끌어 놓기와 그림으로 보낸다.
1. 부품 고르면 도구 막대: [메모] [이동] [크기] [그리기]
2. [이동] → 원하는 곳에 끌어다 놓기. 놓을 수 있는 칸이 파랗게 뜨고, 목표가 구조로 바뀜: "TitleRow 안 · TitleText 뒤 · 오른쪽 끝"
3. [그리기]로 동그라미·화살표 덧붙이기 + 메모
4. picks.json에 저장: 동작(move) · 대상(AddButton, Main.xaml:48) · 목표 부품·정렬(Main.xaml:12) · 목표 좌표 · 그림 파일
5. AI는 `preview_plan()`으로 이해한 결과를 먼저 그림 → OK 후 수정 → `check_layout()`으로 숫자 확인

## 녹화해서 보내기
버그든 "이런 움직임으로" 같은 참고든, 말보다 영상이 정확하다.
1. `Ctrl+Shift+R` 녹화 시작 → 앱 창에 빨간 테두리 + 녹화 시간 (앱 창만 찍음)
2. 보여 주고 싶은 걸 그대로 해 보기
3. `Ctrl+Shift+R` 다시 → 녹화 끝 → 메모 한 줄(선택) → [보내기]
4. 묶음: 영상 + 일어난 일 표(시각 · 클릭한 부품 이름·코드 위치 · 부품 상태 변화(예: 열림 → 0.1초 뒤 닫힘) · 오류 로그)
5. AI: `get_recording()` → Windows-MCP로 같은 클릭 재생해 재현 확인 → 수정 → 같은 재생으로 "안 생김" 확인 → 전후 비교
- "방금 그거 뭐야?"(효과 이름 묻기)도 이 녹화를 그대로 쓴다

## 데모 영상 (가짜 앱, 흐름만)
- `ui-nametag-demo-v2.mp4` (86초: 기본 흐름 → 이동·그리기 → 녹화 보내기)
- 원본 HTML: `demo/demo.html`, `demo/demo2.html`, `demo/demo3.html`

## 대상별 방법

| 대상 | 실행 | 부품 읽기 | 비고 |
|---|---|---|---|
| 웹 / Electron | 프로그램 안 Chromium | DOM·CSS | 가장 정확. 참고: React Grab, Agentation |
| APK | Android Studio 에뮬레이터(APK 끌어다 설치) 또는 실제 폰 + scrcpy | `adb shell uiautomator dump`, 캡처 `screencap` | Flutter APK는 부품 목록 거의 없음. 참고: MobAI Element Picker. WSA는 2025-03 지원 종료 |
| Windows 앱 | 별도 창 | UI Automation (`pywinauto`, backend="uia") | WPF·WinForms·WinUI 잘 됨, Qt 일부. 참고: Inspect.exe, Accessibility Insights |
| 내 게임 | 엔진 에디터에서 Play | **엔진 MCP** (Unity 공식 MCP·CoplayDev unity-mcp, Godot MCP, Unreal MCP) | 게임에 넣을 것 없음. AltTester·Poco(빌드에 SDK 넣음)는 불필요 |
| 남의 게임 / 소스 없는 화면 | 별도 창 | 그림 인식 (Microsoft OmniParser) | 정확도 낮음, 코드 위치 없음. 라이선스 확인 필요(아이콘 모델 AGPL로 알려짐, 미확인) |
| iOS | ❌ Windows 불가 | — | Mac 필요. 같은 코드면 Android로 대신 확인 |

## 효과·동작 이름 기능
1. **효과 사전**: 아코디언·콜랩서블·트리 뷰·드롭다운·스크롤 체이닝·고정 헤더(sticky)·토스트·스켈레톤·패럴랙스 등. 항목마다 움직이는 예시 + 헷갈리는 것과 차이 + 요청할 때 쓰는 말
2. **자동 감지**: 부품 클릭 시 성질을 읽어 관련 효과 표시 (웹: `overflow`·`position: sticky` 등 CSS / APK: `ExpandableListView` 등 종류)
3. **"방금 그거 뭐야?"**: 아래 "녹화해서 보내기"로 찍어 보냄 → 이름·비슷한 것·요청 말투 → 사전에 추가

## 남은 약점
- Flutter·게임(엔진 MCP 없이)·소스 없는 앱: 클릭 좌표 + 그림으로 추정 → 정확도 낮음(추정 70~80%, 측정 안 함)
- iOS: Windows만으로 불가

## 상태
구상만. 아직 아무것도 만들지 않음. (그림·데모 영상만 있음)
+ 고칠거 메모해놓는 칸 → 도구 막대 [메모] + 대기열(picks.json)로 반영
