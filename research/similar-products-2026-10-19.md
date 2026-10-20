# UI 이름표 — 비슷한 제품 조사 (2026-10-19)

대상: `ui-nametag-계획.md`. 라이선스·별·갱신일은 `gh api`로 직접 확인(LICENSE 파일 열어 봄).

## 결론
- 웹/Electron + Windows + Android + 게임 + 부품 사전 + 효과 사전을 한 프로그램으로 묶은 제품은 못 찾음.
- 영역별로는 이미 같은 기능이 있음. 특히 웹 "가리키기 → 코드 위치 → AI 수정"은 포화 상태.

## 영역별
| 영역 | 계획 | 판정 | 추천 | 라이선스 | 이유 |
|---|---|---|---|---|---|
| 웹 가리키기+코드 위치 | 직접 만듦 (참고 React Grab, Agentation) | 교체(가져다 씀) | react-grab (aidenybai/react-grab, ★7.7k) | MIT ✅ | 요소 복사 → 에이전트에 컴포넌트·파일 위치 전달 |
| 웹 가리키기+메모+MCP 대기열 | get_picks() | 교체 | mcp-pointer (etsd-tech, ★597) | MIT ✅ | Option+Click → MCP로 Claude Code에 전달 = get_picks와 같음 |
| 웹 소스 매핑+수정+핫리로드 | 흐름 2 | 참고 | Frontman (frontman-ai/frontman, ★718) | Apache-2.0 (+COMMERCIAL-LICENSE.md 별도 있음) ⚠️ | 요소→소스 파일·줄 매핑. Next/Astro/Vite만 |
| 웹 비주얼 편집기 | — | 참고 | Onlook (★27k) | Apache-2.0 ✅ | 통째 포크 후보지만 React 편집기 쪽, 범위 다름 |
| 웹 리뷰 패널 | 메모 칸 | 참고 | agnt (standardbeagle, ★22) | Apache-2.0 ✅ | 프록시 주입 + 요소 선택 + 접근성 검사 |
| Android | uiautomator dump | 유지(+추가) | mobile-mcp (mobile-next, ★9.8k) | Apache-2.0 ✅ | 화면·부품 트리 읽기 이미 있음. 사람이 고르는 picker는 없음 |
| Windows 앱 | pywinauto UIA | 유지(+추가) | Windows-MCP (CursorTouch, ★9.5k) | MIT ✅ | UIA 트리 읽기. 단 클릭·입력 도구 포함 → 읽기 전용 원칙과 충돌 |
| 소스 없는 화면 | OmniParser | 유지 | microsoft/OmniParser (★25.5k) | 저장소 CC-BY-4.0, 아이콘 모델 가중치 AGPL 여부 미확인 | 계획대로 |
| 효과·부품 이름 사전 | 직접 만듦 | 참고 | NameThatUI(웹 서비스), Storybook Component glossary | 미확인(콘텐츠라 복사 불가 가정) | 이름 사전은 있으나 코드 위치·자동 감지 연결은 없음 |

## 제외
- Agentation: PolyForm Shield 1.0.0 → 경쟁 제품에 쓰면 금지 ❌(우리 제품은 경쟁 제품).
- Stagewise: AGPL-3.0 ⚠️(배포 시 소스 공개).
- LocatorJS, claude-in-mobile, itk-dev browser-feedback: 라이선스 파일 없음 → 사용 불가.
- MobAI: 유료(월 $5~10), 사람이 고르는 picker 있는지 미확인.
- ui-ticket-mcp: 라이선스 "Other", 내용 미확인.

## 조사 범위
- 본 곳: 웹 검색(dev.to, glama.ai, mcpservers.org, mcpmarket, AlternativeTo 목록), GitHub API.
- 못 본 곳: Product Hunt·HN·Reddit 직접 검색, 게임 엔진 MCP 비교(계획에 이미 있음), 한국 커뮤니티.
