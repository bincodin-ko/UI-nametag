# CHANGES

## 바뀐 점
- 2026-10-20 · 메인 창 시안 3개(design/) · Open Design 사용: 작업 방식 design-brief+creative-director, 공통 원칙 anti-ai-slop·laws-of-ux·state-coverage·typography-hierarchy · A 마스터-디테일(PowerToys 설정 참고, 디자인 시스템 linear-app) / B 커맨드 팔레트+통합 목록+미리보기 패널(Raycast 참고, raycast) / C 트레이 팝업+점진적 공개(Windows 11 빠른 설정 참고, shadcn) · Open Design 프로젝트 "UiNametag 메인 창"에 올림 · 사용자 고르기 대기
- 2026-10-20 · 바탕화면 바로가기 `UiNametag.lnk`(주황 이름표 아이콘): 꺼져 있으면 켜기, 켜져 있으면 끄기(두 번째 실행이 첫 프로그램에 끄기 신호 + "꺼짐" 알림) · 사용자 요청 · Program.cs, Picker.cs, app.ico
- 2026-10-20 · 고른 부품에 앱 이름·창 제목 저장, 사전 id가 없는 창이면 "⚠ 사전 밖 앱" 이름표 + get_picks 맨 위 경고 · 다른 앱을 골라도 Claude가 알아채게 · Catalog.cs, McpTools.cs
- 2026-10-20 · 메모 창: 그 모니터 작업 영역 안에 배치(아래 자리 없으면 부품 위), 띄울 때 앞으로, 포커스 없어도 Esc로 닫힘(전역 단축키) · 사용자 요청 · Picker.cs
- 2026-10-20 · 트레이 아이콘(주황, 녹화 중 빨강) + 오른쪽 클릭 메뉴(고르기·폴더 열기·끄기), 두 번 실행하면 "이미 켜져 있음" · picker가 켜졌는지 몰라서 · Picker.cs, Program.cs
- 2026-10-20 · 안전 가이드에 "시험 클릭은 글자(Text)에만" 추가 · selftest가 IINA 최근 목록을 눌러 영상이 열리고 재생 기록 순서가 바뀜(되돌림: 백업 `personal/backup/`, 원래 5번째 자리로, 시각은 10-19 21:00로 추정 입력) · CLAUDE.md, Selftest.cs
- 2026-10-20 · Electron은 FromPoint가 바깥 Pane에서 멈춤 → 창 맨 위부터 점을 품은 모든 갈래를 내려가 가장 작은 부품 선택 · 투명 레이어도 FromPoint에 잡혀서 WindowFromPoint로 아래 창부터 · Catalog.cs
- 2026-10-20 · C# WPF 프로그램 하나로 구현(picker·MCP·사전·녹화) · 계획서 결정대로 · src/UiNametag

## 2026-10-20 메인 창 (시안 C 팝업 + A 전체 창)
- 트레이 왼쪽 클릭 = 팝업(TrayPopup.xaml: 상태·고르기/녹화/대기열 타일·내 앱 4개·새 앱 등록·전체 창·설정·끄기). 오른쪽 클릭 메뉴에 "UiNametag 열기".
- 전체 창(MainWindow.xaml): 내 앱(마스터-디테일, 사전 미리보기·검색, 다시 만들기·폴더 열기·두 번 눌러 지우기), 새 앱 등록 3단계(화면에서 창 클릭/창 목록·웹 주소 → 소스 폴더+추천 → 사전 만들기), 대기열(고른 것·녹화 지우기), 설정(단축키 글자, 시작프로그램 폴더 바로가기로 자동 켜기, MCP 명령 복사, 데이터 폴더).
- 등록 목록 apps.json + 앱별 catalogs/{id}.json. 마우스 아래 창 → 실행 파일 같은 앱, 없으면 부품 id가 가장 많이 겹치는 앱의 사전. Hit/Target에 Source(앱 소스 폴더).
- 자기 인식: 첫 실행에 UiNametag 자신(src/UiNametag) 자동 등록, 모든 부품 x:Name → 자기 창도 MainWindow.xaml:줄.
- MCP: register_app, list_apps 추가, build_catalog = 다시 만들기/없으면 등록, catalog_lookup(app 선택) 모든 앱 검색.
- 사전이 .htm/.jsx/.tsx/.vue/.svelte id도 읽음.
- `UiNametag.exe open` = 메인 창 열기.
- 고친 것: 테마의 TextBlock 글자색이 버튼 글자색을 덮음 → 창에서 물려받게. 팝업이 포커스를 못 받으면 바로 사라짐 → 받은 뒤 잃을 때만 닫음.
- 안 넣음: 녹화 화질·최대 길이 설정(녹화기에 그 값이 없음), 밝은 테마.

## 2026-10-20 빌드해도 주황 아이콘 항상 새 버전
- csproj 대상 FreeRunningExe(BeforeBuild)·RestartPicker(AfterBuild) → `tools/swap.ps1`: 켜진 picker만 끄고 기억, 잠긴 exe/dll은 이름만 `.old-*`로 비킴(MCP 안 끊김), 빌드 끝나면 picker 다시 켬. 옛 파일은 다음 빌드 때 지움(잠겨 있으면 남김).
- 버그: 창 고르기에서 마우스 옮기자마자 누르면 옛 창을 고름(테두리 갱신 70ms) → 클릭 순간 위치로 다시 읽음.
- IINA 켠 전체 selftest **22/22**, IINA 설정·기록 json 변화 없음(md5).

## 2026-10-20 영역 캡처·메모 창·NVIDIA·가짜 입력 시험
- Ctrl+Shift+S(설정에서 바꿈, 트레이 메뉴 "화면 영역 캡처"): 드래그 영역을 ffmpeg ddagrab(DXGI)으로 찍음 — 위에 뜬 창 포함. 메모 붙여 대기열 Action=shot, get_picks 설명에 "Shot 그림을 볼 것". 메모 창이 떠 있어도 됨.
- 메모 창: 앱 테마(어두운 바탕·주황 저장 버튼)로, 버튼 잘림 검사 추가.
- Resolve 시작점 = WindowFromPoint(클릭이 실제로 가는 창) → NVIDIA Overlay 같은 화면 전체 투명 창을 안 잡음.
- get_recording: 클릭 장면 못 뽑으면 끝 부근으로 다시. 시험의 장면 세기 오류(마지막 줄바꿈) 고침.
- selftest가 실제 마우스·키보드를 안 씀: Picker.Fake/FakeEsc/FakeCursor로 같은 처리 길에 신호를 넣음. IINA가 맨 앞이 아니면 건너뜀. 25/25, IINA json 그대로.
- Windows Sandbox 시험 시도는 폐기(로그온 명령 안 돎, 세션 오류) — sandbox 폴더·bin/sandbox 지움.
- 전역 훅: no_hidden_assumption.py 걸리는 말 넓힘(할순없어·가능·되게·바꿔…) + 검증 방법 문장 추가, 새 Stop 훅 simplest_first.py(무거운 환경을 권하는 답이면 가벼운 길을 따졌는지 한 번 막음).

## 2026-10-20 대기열 #2~#4
- 사고: selftest 정리가 사용자 캡처(#2 shot_1020_223933.png, #3 shot_1020_224028.png)를 지움 — keep을 `with`로 바꾼 뒤 `Except`가 사용자 항목도 지울 것으로 봄. 고침: 메모가 selftest로 시작하는 것만 지움. 시험 전후 사용자 파일 목록 비교로 확인(사라진 파일 없음). 지운 두 그림은 되살릴 수 없음.
- 재발 방지: 시험 시작 때 사용자 항목의 읽음/고침 상태 기억 → 끝나면(중간에 멈춰도) 되돌림.
- 상태 3개: 대기 중 / Claude가 읽음(아직 안 고침) / ✓ 고침 완료. MCP mark_done 추가.
- #2 대기열 왼쪽에 찍힌 그림 미리보기(누르면 크게). #3 그리기에서 손 떼고 다시 그리면 새 선. #4 대기열 [Claude에게 고치게 하기] → 새 터미널(wt)에서 Claude Code를 항목 소스 폴더로 켜고 get_picks→고침→mark_done 지시. 대기열 화면은 3초마다 새로 읽음. "가져간 것 지우기" → "고친 것 지우기".
- selftest 29/29.
