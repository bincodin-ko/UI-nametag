# PLAN (끝까지 모드, 2026-10-20)

C# WPF 프로그램 하나 `src/UiNametag` (.NET 10). 모드: `build-catalog` · 상주 picker(기본) · `mcp`(stdio) · `selftest` · `probe`.
시험 대상: IINA(Electron) + 소스 `Desktop/iina/src`.

| 단계 | 완료 기준 (프로그램이 읽어서 판정) | 결과 |
|---|---|---|
| 1 사전 | `build-catalog iina/src` → catalog.json에 `settings → renderer/welcome.html:50` 포함, html id 항목 수 = 소스 grep 수 | ✅ 216개 = grep 216 |
| 2 picker 이름 찾기 | IINA 켠 상태에서 투명 레이어를 띄운 채 `settings` 버튼 중앙 → "welcome.html:50" | ✅ |
| 3 고르기·MCP | pick 1건 저장 → MCP stdio로 `get_picks` 응답에 그 pick, `catalog_lookup("settings")` → welcome.html:50 | ✅ |
| 4 이동·확인 | move pick 저장, `check_layout(settings, 자기 rect)`=100%, 비켜 간 rect=0%, `preview_plan` 창이 UIA로 보임 | ✅ |
| 5 녹화 | 녹화 시작 → IINA의 글자(Text) 클릭 → 끝 → mp4 크기>0·길이>1초, 표에 Click(Text) 기록, `get_recording`이 표+장면 경로 반환 | ✅ 3.1초 |

결과: `UiNametag.exe selftest C:/Users/gusql/Desktop/iina/src` → **11/11 통과**, IINA 재생 기록 변화 없음 확인.
화면 겉모습(테두리·이름표·메모창 모양)은 사용자 확인(NOTSOLUTION C).

## 미룬 일
| 일 | 선행 조건 | 다시 볼 때 |
|---|---|---|
| 여러 모니터 녹화·캡처 (지금 주 모니터만) | 두 번째 모니터에서 쓸 일 | 다른 모니터 앱을 녹화할 때 |
| 그림(그리기) 선을 PNG로도 저장 (지금 좌표만) | AI가 좌표만으로 이해 못 할 때 | 그리기 써 본 뒤 |

## 다음 묶음: 메인 창 (시안 선택 대기, 2026-10-20)
- 메인 창은 XAML + 모든 부품에 x:Name → UiNametag가 **자기 창도** 사전으로 읽음(자기 src를 앱으로 등록). 완료 기준: 메인 창 버튼을 고르면 `MainWindow.xaml:줄` 표시.
- Resolve는 이미 배경 스레드 + WindowFromPoint(투명 레이어만 건너뜀)라 자기 메인 창은 그대로 잡힘.
- 2026-10-20 구현: C 팝업(트레이 왼쪽 클릭) + A 전체 창(내 앱·대기열·설정, 3단계 등록), apps.json + 앱별 catalogs/, MCP register_app·list_apps, .jsx/.tsx/.vue/.svelte id.
  selftest 8/10: 자기 창 읽기 ✅ `MainWindow.xaml:61`, 화면 전환 ✅. 실패 2건(Esc·팝업 보임)은 사용자가 게임 중이라 포커스를 못 받음 → 사용자 PC 안 쓸 때 다시.
- 2026-10-20 재시험(사용자 PC 안 씀): selftest **12/12** ×3회 (그 전 1회 11/12, 실패 단계 기록 못 함). 메모 Esc는 실제 경로(Esc 단축키)로 시험하게 바꿈. 10 창 고르기 클릭·Esc 취소 추가. 실제 트레이 아이콘 클릭 → 팝업 → "전체 창 열기" → 메인 창 열림·팝업 닫힘 확인(UIA).
