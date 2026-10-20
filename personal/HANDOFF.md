# HANDOFF (2026-10-20)

## 지금 상태
- `src/UiNametag` C# WPF(.NET 10) 완성, `selftest` 11/11 통과(IINA 대상).
- 실행: `UiNametag.exe`(상주 picker) · `build-catalog <폴더>` · `mcp` · `selftest <시험 앱 소스>` · `probe x y`(진단)
- 데이터: `%LOCALAPPDATA%\UiNametag` (catalog.json, picks.json, pick_*.png, rec_*.mp4/json)
- uinametag MCP 전역 등록(user scope), Connected 확인

## 다음 할 일
1. 사용자 C 항목 확인(테두리·메모 창·이동·그리기·녹화 겉모습)
2. 실제 흐름: Ctrl+Shift+G로 고르기 → Claude에 "고른 거 봐줘"
3. NOTSOLUTION A: get_recording 장면 추출

## 주의
- dotnet은 이 세션 PATH에 없음 → `"C:\Program Files\dotnet\dotnet.exe"`
- 빌드: `dotnet build -c Release` (src/UiNametag). picker가 켜져 있으면 exe 잠김 → 먼저 끄기
- 시험 클릭은 글자(Text)에만(CLAUDE.md)
- 사전은 `iina\src`처럼 소스 폴더를 줘야 함(루트엔 mimic/spike 복사본이 있어 같은 id 중복)

## 참고 파일
- 계획 `ui-nametag-계획.md` · 완료 기준 `personal/PLAN.md` · 확인 목록 `personal/NOTSOLUTION.md`
