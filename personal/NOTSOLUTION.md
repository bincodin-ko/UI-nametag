# NOTSOLUTION

<!-- namunil:begin — 남은일이 자동으로 쓰는 칸. 고칠 때는 남은일 앱이나 namunil MCP로 -->
## 남은일

### 사용자가 직접 할 일
- (없음)

### A 나중에 고칠 것(Claude 남은 일)
- [ ] #508 UiNametag: 옆 모니터 사진에서 재생 중인 영상이 검게 찍힐 수 있음 (화면 복사 한계) — Store.Shot은 주 모니터만 ffmpeg ddagrab, 옆 모니터는 CopyFromScreen. ddagrab output_idx를 DXGI 출력 좌표(IDXGIOutput::GetDesc DesktopCoordinates·Rotation)로 찾아 옆 모니터도 ddagrab으로 찍으면 해결. 세로 모니터는 회전 처리 필요. Recorder.cs 녹화도 output_idx=0 고정이라 같은 문제. 끝나면: 옆 모니터 영상 위 부품을 골라 대기열 그림에 영상이 보이면 정상.

### B 받아들인 한계(타협)
- (없음)
<!-- namunil:end -->


## 사용자가 직접 할 일
| 할 일 | 어떻게 | 시간 | 끝나면 할 말 | 풀어 주는 작업 | 상태 |
|---|---|---|---|---|---|

## A 나중에 고칠 것
- (10-20 해결: IINA 켠 selftest 22/22) 2026-10-20 · IINA 시험 2~5를 이번 수정 뒤 다시 못 돌림 · 사용자가 IINA로 영상 보는 중이라 건너뜀(6~8은 통과) · 다음: IINA가 시작 화면일 때 `UiNametag.exe selftest C:/Users/gusql/Desktop/iina/src`
- 2026-10-20 · get_recording 장면이 클릭 순간 대신 끝 장면 1장만 나올 때가 있음 · 클릭 시각+0.1초 추출이 짧은 영상 앞부분에서 빈 결과 · 다음: 추출 실패 시 시각 0으로 다시

## B 받아들인 한계
- 녹화·캡처는 주 모니터만(ffmpeg ddagrab output_idx=0)
- id 없는 부품(IINA 시작 창 33개 중 28개)은 "코드 위치 모름" 또는 가장 가까운 부모 id로 추정
- 같은 id가 여러 파일에 있으면 같은 창에 보이는 다른 id가 가장 많은 파일로 고름(틀릴 수 있음)

## C 사용자가 눈·귀로 확인할 것
- picker 켜기: `src\UiNametag\bin\Release\net10.0-windows\UiNametag.exe` 더블클릭(창 없음, 상주) → IINA 켜고 `Ctrl+Shift+G` → 마우스 올리면 주황 테두리 + "버튼 · Button · renderer/welcome.html:50" 이름표가 부품을 따라오면 정상 (열기: src/UiNametag/bin/Release/net10.0-windows/UiNametag.exe > !IINA 켜고 Ctrl+Shift+G > @설정 톱니: 이름표에 welcome.html:50이 보이면 정상)
- 클릭 → 메모 창이 부품 아래 뜸 → [저장] → 마우스 옆 "대기열에 저장 (#N)" (열기: - > !Ctrl+Shift+G 후 부품 클릭 > 저장 > @대기열에 저장: 보이면 정상)
- 메모 창 [이동] → 놓을 곳에 마우스 → 파란 점선 칸 + "… · 앞/뒤" + 주황 점선 유령 → 클릭 → 메모 창에 "→ 이동:" 줄 (열기: - > 이동 > @→ 이동: 보이면 정상)
- 메모 창 [그리기] → 드래그로 빨간 선 → 오른쪽 클릭이나 Esc로 끝 → 메모 창에 "+ 그림 있음" (열기: - > 그리기 > @+ 그림 있음: 보이면 정상)
- 녹화: 앱 창 클릭 → `Ctrl+Shift+R` → 빨간 테두리 → 써 보기 → `Ctrl+Shift+R` → 메모 → [보내기] → "녹화 보냄 (rec_…)" (열기: - > !Ctrl+Shift+R 두 번 > 보내기 > @녹화 보냄: 보이면 정상)
- 끄기: 작업 관리자 > UiNametag > 작업 끝내기

## 디자인 오류

## 2026-10-20 미룬 확인 (사용자 PC 사용 중 — 마우스·키 자동 조작 시험 보류)
- selftest 7 메모 창 Esc, 9 트레이 팝업 보임: 전경 창(게임)이 포커스를 안 내줘 실패. 팝업은 "포커스 받은 뒤 잃을 때만 닫기"로 고침, 재시험 필요.
- 창 고르기 모드(등록 ①·팝업 녹화 버튼) 실제 클릭 시험 안 함.
- (10-20 해결) 위 두 건 재시험 통과. 남은 것: selftest 1회 11/12(어느 단계인지 출력 놓침) — 다시 나오면 FAIL 줄 확인.

## 2026-10-20 사고·미룸
- selftest 5단계 클릭 1번이 IINA 대신 League of Legends 게임 창(`League of Legends (TM) Client`)에 들어감(3.33s, 사용자 PC 사용 중). 녹화 기록으로 확인: 맞은 것은 부품이 아니라 게임 창 전체(부품 id 없음), 0.3·1초 뒤 상태 변화 기록 없음. 게임 안에서 무엇이 일어났는지는 게임 화면 상태를 프로그램이 읽을 수 없어 확인 불가(UIA로 창 이름만 읽힘) — 사용자 판단. 조치: 클릭 직전 그 자리 맨 위 창이 IINA가 아니면 클릭 안 하도록 selftest에 가드 추가(빌드는 다음 시험 때).
- IINA windows.json(창 위치) 바뀜 — 시험이 IINA를 켜고 닫으며 생긴 창 위치 저장. history·preferences는 그대로.
- (10-20 해결, selftest 25/25) 미완: Resolve 시작점을 WindowFromPoint로 바꾼 뒤 selftest 2단계(IINA settings) 실패 — 원인 조사 중. 영역 캡처(Ctrl+Shift+S)·메모 창 테마 코드는 들어갔고 빌드됨, 시험 미완.
- (10-20 해결, selftest 25/25) (10-20) Windows Sandbox 시험 포기: 로그온 명령이 안 돌고 세션 오류 반복. 대신 selftest를 "가짜 입력"으로 바꿈(Picker.Fake/FakeEsc/FakeCursor — 실제 마우스·키보드 안 씀). 빌드됨, 아직 안 돌림. 남은 것: 시험 창(메모·메인·팝업)이 앞으로 뜨며 포커스를 가져감 → 게임 중엔 돌리지 않음.
