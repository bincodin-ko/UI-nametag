# UI-nametag 안전 가이드

## 건드려도 되는 것
- 이 폴더 안 파일, `src/` 빌드·실행
- NuGet 패키지 받기(`dotnet add package`), `%LOCALAPPDATA%\UiNametag` 데이터 폴더
- 시험 클릭은 눌러도 아무 일 없는 글자(Text)에만. 목록 항목·버튼은 누르지 않는다(2026-10-20 최근 목록을 눌러 영상이 열리고 재생 기록이 바뀐 사고)
- 시험 대상은 앱 안 시험 전용 창 `src/UiNametag/SelftestTarget.xaml`(selftest가 띄우고 닫음). 다른 앱(IINA 등)은 시험에 켜지 않는다(2026-11-02 사용자)

## 절대 하지 않는 것
- IINA 소스(`C:\Users\gusql\Desktop\iina`) 수정 — 읽기만
- IINA 설정·재생 기록 등 실제 데이터 바꾸기, 파일 열기 대화상자에서 파일 선택
- 전역 설치(`npm i -g`, `dotnet tool install -g`), 레지스트리 수정

## 멈출 때
- 관리자 권한·로그인 요구, 실제 데이터가 바뀌었을 때

## 되돌리는 법
- 데이터: `%LOCALAPPDATA%\UiNametag` 폴더 삭제
- 빌드물: `src/*/bin`, `src/*/obj` 삭제
- 켜진 프로그램: 작업 관리자에서 `UiNametag` 끄기
