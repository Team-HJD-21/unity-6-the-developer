# The Developer — Project Documentation

수정일: 2026-10-24
상태: 현행 PL 기준 / 몬스터 목표 배정·추적 우선 시험 방향 기록 · 실행 검증 미완료

이 폴더는 Unity 6 기반 `The Developer`의 제품 기획, New Core 아키텍처, 역할, Milestone과 실행 규칙을 보관한다. 레거시 코드는 기능·수치·연출의 참고 자료이며 신규 기능의 기반으로 사용하지 않는다.

## 현재 기준

2026-10-24 수기 회의록은 전력 보충 지연·대기 전력, 시간 기반 수리·복구와 복구 취소 시 진행 유지 방향, 주간 회의 일정을 기록했다. 성장·재화와 확률은 기획 검토 항목이며 구현·완료 증거가 아니다. [회의 검토 기록](planning/2026-10-24-regular-meeting.md)에서 확정 방향과 추가 확인 항목을 구분한다.

2026-10-15 Core 통합 스프린트를 진행한다. 정적·동적 지역 모두 지역 Grid의 상태가 원본이며, 동적 삼각형은 점령 셀을 계산하는 임시 자료다. 동적 모드는 지역 잠금 없이 활성 터렛으로 점령을 갱신하고, 점령 회수로 나머지 터렛을 끄지 않는다. [배치·구현·검증 기록](planning/2026-10-15-core-sprint-implementation.md)에서 실제 구현과 미완료 통합을 구분한다.

2026-10-10 회의에서는 팀이 M1과 M2 사이에 있다고 설명했으며, M1·M2 완료나 새 일정을 승인하지 않았다. 터렛 수리와 잠금 해금의 제품 방향 및 설치 판정, 지역 Grid와 저장 방식의 미결 사항은 [회의 검토 기록](planning/2026-10-10-regular-meeting.md)을 따른다.

2026-10-17 회의는 해금·활성화·점유를 분리하고, CU에서 이어지는 점유 방향과 정적 송신탑 해금 시제품을 검토했다. 점유 후 보스 처치 승리 흐름과 CU 파괴 게임오버 방향도 기록했으며, 상세 규칙·체크포인트와 전제 1·2의 최종 선택은 미정이다. [회의 검토 기록](planning/2026-10-17-regular-meeting.md)에서 회의 방향, 발언 당시 보고와 미검증 범위를 구분한다.

2026-10-23 몬스터 비정기회의에서는 최단거리와 주변 밀집도를 우선 사용한 터렛 목표 배정, 목표 유지, 플레이어 감지 시 추적 동작을 먼저 시험하기로 했다. 초기 Stage의 `ConfigureUnlockZones` 호출 연결도 양현석이 맡기로 했으며, main 정적 검색에서는 호출을 찾지 못했다. 세부 규칙과 실제 구현·실행 결과는 [회의 검토 기록](planning/2026-10-23-monster-meeting.md)을 따른다.

2026-10-09 Legacy 실행 코드와 PoC 경로를 정리했다. [Assets 구조와 Legacy 교체 기준](architecture/asset-layout-and-migration.md) 및 [이관 결과](planning/2026-10-09-asset-migration-results.md)를 따른다. 현재 실행 Scene은 `Assets/Scenes/Gameplay/Initial_Stage.unity`다. 과거 분석의 경로는 당시 위치이며 현재 경로와 구분한다.

2026-10-08 PL이 실제 런타임을 확인해 양현석의 Core 공간 분석·스폰 통합·Game Debugger 기술 PoC를 인수했다. 현재 완료 범위와 Issue/PR 인수는 [PoC 마감 기록](planning/2026-10-08-poc-closeout-and-pr-handoff.md)을 따른다. 아래의 10월 3일 내용은 당시 검토 배경이며 최신 실행 상태로 읽지 않는다. 정식 승패·성장·멀티플레이 완성과 팀 공통 Windows Build 검증은 별도다.

팀원의 호출 규칙은 [Core API 사용 규약](architecture/core-api-usage.md), 도구 조작은 [Game Debugger 사용법](tooling/game-debugger-guide.md)을 참고한다.

- 장르: 2D 탑다운 액션 타워디펜스
- 핵심 경험: 제한된 전력으로 미리 배치된 터렛을 선택해 전선을 운영하고, 플레이어가 무너지는 전선을 찾아가 보조한다.
- Meta Loop: Spaceship에서 Mission·Research를 준비하고 Planet에 출격한 뒤 결과를 반영해 귀환한다.
- 현재 검증: Stage 1 PoC는 전제 1(동적 전선)과 전제 2(지역 해금)의 재미를 시험한다. 10월 3일 회의 시점에는 통합·플레이 결과가 확인되지 않아 주말 마무리와 전선 형성 관찰이 남았다. 기존 웨이브 진행은 보류하고, 두 안의 구현 순서·병행 범위와 최종 선택은 미정이다. 연구·결과 성장·다음 Mission은 M2 목표이며, PoC의 승리 조건도 미정이다. [Sprint 기준](planning/SPRINT_1_STAGE_1_POC.md) · [10월 3일 후속 검토](planning/2026-10-03-poc-followup.md)
- 개발 순서: New Core Foundation → Single-player Vertical Slice → 2-player Co-op → Online/Alpha → Release
- Deferred: 실제 Host Migration, PvP, `RivalPlayerAgent`, 대형 우주선, 두 번째 Planet

## 현재 팀

| 이름 | 제품 영역 |
| --- | --- |
| 양현석 | Project Lead / Core Systems & UI Architecture |
| 이영빈 | Player / Combat·Co-op UX / Cinematic |
| 김진태 | World / Mission / Ally Player Agent |
| 황재동 | Defense / Progression / Balance |
| 조수빈 | Enemy / Encounter / AI Platform |

김진태와 황재동의 담당 영역을 포함해 각 역할은 자신의 제품 영역과 관련 Task의 구현·검증·인수 기준을 끝까지 책임진다.

## 폴더 안내

새 문서의 배치·이동·보관 방법은 [Docs 정리 규칙](DOCS_ORGANIZATION_RULES.md)을 따른다.

- `product/` — 게임의 정체성, 제품 범위, Decision Log
- `architecture/` — 새 Core의 경계, 코드 명명 규칙, 레거시 이식 참고
- `planning/` — Milestone, 전체 개발 흐름, 현재 Sprint 실행 계획, 아직 진행 중인 회의 검토 기록
- `team/` — 담당 영역, 협업 규칙, GitHub Issue 분류
- `archive/` — 현행 기준에 반영한 회의 요약, 종료된 Sprint, 모집 자료와 레거시 분석. 현재 규칙으로 사용하지 않음
- `tooling/` — 팀 공통 Unity 패키지·CLI 설치 및 오류 구분 절차
- `Local/` — 개인 메모·초안. `.gitignore` 대상이며 팀 공유 문서가 아님

Unity 패키지 복원과 CLI 연결은 [Unity Tooling Setup](tooling/UNITY_TOOLING_SETUP.md)을 따른다.

## 읽는 순서

터렛을 사용하는 다른 영역은 [터렛 연동 가이드](architecture/turrets/index.md)에서 빠른 시작과 API 및 스냅샷을 따로 확인할 수 있다. 내부 실행 흐름은 기존 Turret System Guide의 Mermaid를 유지한다.

플레이어 스킬의 책임 분리와 신규 네트워크 몬스터 연동·테스트 범위는 [Player skill review](architecture/player-skill-review.md)를 참고한다.

스킬 Canvas를 씬에 미리 배치하고 실행 중 생성되는 로컬 플레이어에게 연결하는 방법은 [Player Skill HUD API](architecture/player-skill-hud-api.md)를 참고한다.

Initial 씬의 플레이어 체력·전력 표시와 터렛 선택·레벨 변경 UI는 [Player UI API](architecture/player-ui-api.md)를 참고한다.

플레이어 무기 전환과 스패너 공격·수리는 [Player Weapon API](architecture/player-weapon-api.md)를 참고한다.

LMB 공격과 포탑 선택·설치·UI 클릭의 우선순위는 [Player Primary Input API](architecture/player-primary-input-api.md)를 참고한다.

Space 대시, 미리 배치한 잔상과 하단 쿨타임 표시는 [Player Dash API](architecture/player-dash-api.md)를 참고한다.

플레이어 8방향 대기·이동·공격·수리 프레임은 [Player Motion API](architecture/player-motion-api.md)를 참고한다.

기존 모션을 유지하며 몸체와 총기·스패너를 나눈 표시 에셋과 편집 방법은 [Player Weapon Appearance API](architecture/player-weapon-appearance-api.md)를 참고한다.

플레이어 기본 무기·총구·탄환 초기화 계약과 이동속도 독립 사격은 [Player Combat API](architecture/player-combat-api.md)를 참고한다.

1. [PROJECT_PLAN.md](product/PROJECT_PLAN.md) — 게임 정체성, 제품 범위, Vertical Slice와 Decision Log
2. [system-rearchitecture-charter.md](architecture/system-rearchitecture-charter.md) — 레거시 교체 원칙과 변경할 수 없는 시스템 경계
3. [milestones-and-core-design.md](planning/milestones-and-core-design.md) — M0~M5, Priority별 역할, 통과 기준
4. [naming-and-architecture-conventions.md](architecture/naming-and-architecture-conventions.md) — 계층, asmdef 의존성, 타입 명명
5. [turret-system-guide.md](architecture/turret-system-guide.md) — Canon·Missile 터렛의 상태·전력·Enemy 연동, 세부 업그레이드·레벨 승급 및 검증 방법
6. [TEAM_ROLE_OWNERSHIP.md](team/TEAM_ROLE_OWNERSHIP.md) — 양현석~조수빈 제품 영역과 협업 경계
7. [TEAM_WORKFLOW.md](team/TEAM_WORKFLOW.md) — Sprint, Git, Review, Ready/Done 규칙
8. [DEVELOPMENT_FLOW.md](planning/DEVELOPMENT_FLOW.md) — Milestone 안에서 실제 작업이 흘러가는 순서
9. [SPRINT_1_STAGE_1_POC.md](planning/SPRINT_1_STAGE_1_POC.md) — 2026-10-02 Stage 1 PoC 범위, 역할, 완료 기준과 통합 순서
10. [2026-10-02 AI 구조 검토](planning/2026-10-02-ai-architecture-review.md) — 현재 점수 기반 몬스터 AI 우선 시험, 미채택 경로안, 상위 AI 구조·라이벌 AI 미결정 범위
11. [2026-10-03 PoC 후속 검토](planning/2026-10-03-poc-followup.md) — 전선 통합·몬스터 분대·해금·복귀 방향, 시험 기간 운영과 미확정 사항
12. [2026-10-05 몬스터·맵 스폰 연동 검토](planning/2026-10-05-monster-map-spawn-review.md) — 세 전선의 스폰 계획, 생성 확인 범위와 문·통로 피드백. 실제 생성·이동 결과는 미확인
13. [2026-10-07 Stage 1 PoC 통합 준비표](planning/2026-10-07-stage1-poc-integration-prep.md) — 최신 main·Issue 기준, 로컬 1차 패치와 2·3차 통합 범위, 동일 Build 검증·기록 양식. 통합 완료 판정은 아님
14. [2026-10-08 Stage 1 PoC 2차 통합·시각 진단 설계](planning/2026-10-08-stage1-phase2-integration-and-visual-diagnostics.md) — B0~B5와 Gate T, Core/Encounter·타깃 경계와 시각 진단 설계. 양현석의 B0~B3는 구현, 조수빈의 후속 계약은 합의 전
15. [2026-10-08 2차 배치 결과](planning/2026-10-08-phase2-batch-results.md) — 커밋 경계, 구현 파일, CLI 검증 증거, 사용 순서와 팀원 후속 작업
16. [2026-10-08 Game Debugger 확장 배치](planning/2026-10-08-game-debugger-batch-plan.md) — G0~G8 계획, 기존 스폰 경로와 Scene 통합 정책
17. [Game Debugger 배치 결과](planning/2026-10-08-game-debugger-batch-results.md) — 구현 경계·CLI 검사·실제 Scene 검증과 미실행 항목
18. [Game Debugger 사용/API](tooling/game-debugger-guide.md) — 통합 창, 시간/통계/타임라인·조건부 정지·CSV/JSON·A/B, 관측 계약과 제한
19. [Spatial Spawn Distribution](architecture/spatial-spawn-distribution.md) — Grid 셀 공유 부하·Match별 실제 생성 이력과 전선 보조 기준, 반복 스폰 편중 수정
20. [2026-10-10 정기회의 검토 기록](planning/2026-10-10-regular-meeting.md) — 터렛 수리·잠금/해금 방향, 설치 판정, 지역 Grid 후속 작업과 미정 사항
21. [2026-10-17 정기회의 검토 기록](planning/2026-10-17-regular-meeting.md) — 해금·점유 구분, 송신탑 시제품, 보스 승리 흐름과 후속 문서화
22. [2026-10-23 몬스터 비정기회의 검토 기록](planning/2026-10-23-monster-meeting.md) — 최단거리·밀집도 기반 타깃 시험, 플레이어 추적, 넉백 검토와 터렛 해금 초기화 연결 요청

9월 22일 [진행 방식 비교](archive/2026-09-22-progression-options.md)와 9월 26일 [Stage 1 회의 요약](archive/2026-09-26-stage1-planning.md)은 현재 Product Plan·Sprint 기준에 반영한 회의 근거로 보관한다. 10월 2일 [AI 구조 검토](planning/2026-10-02-ai-architecture-review.md), 10월 3일 [PoC 후속 검토](planning/2026-10-03-poc-followup.md), 10월 5일 [스폰 연동 검토](planning/2026-10-05-monster-map-spawn-review.md)는 실행·설계 확인이 남아 `planning/`에 둔다. [레거시 몬스터 흐름 분석](archive/legacy-monster-flow-and-dependencies.md)은 현재 Enemy AI 규칙이 아닌 이전 코드의 참고 자료다.

Issue를 작성할 때는 [ISSUE_LABEL_GUIDE.md](team/ISSUE_LABEL_GUIDE.md)를 함께 본다. [architecture-rebuild-notes.md](architecture/architecture-rebuild-notes.md)는 레거시 분석과 이식 대응표를 담은 참고 문서다. [SPRINT_0_BACKLOG.md](archive/SPRINT_0_BACKLOG.md)와 [team-recruitment-proposal.md](archive/team-recruitment-proposal.md)는 역사적 자료이며 현재 일정·역할의 기준이 아니다.

[2026-10-24 정기회의 검토 기록](planning/2026-10-24-regular-meeting.md) — 전력·수리·복구 방향, 재화 검토와 회의 운영 일정

## Source of Truth

| 질문 | 기준 문서 | 보조 문서 |
| --- | --- | --- |
| 무엇을 만들고 무엇을 미룰 것인가? | [Product Plan](product/PROJECT_PLAN.md) Decision Log | [System Charter](architecture/system-rearchitecture-charter.md) |
| 레거시를 어떻게 교체하고 어떤 의존성을 허용하는가? | [System Charter](architecture/system-rearchitecture-charter.md) | [Naming](architecture/naming-and-architecture-conventions.md), [Architecture Notes](architecture/architecture-rebuild-notes.md) |
| M0~M5에서 무엇을 언제 검증하는가? | [Milestones and Core Design](planning/milestones-and-core-design.md) | [Development Flow](planning/DEVELOPMENT_FLOW.md) |
| 누가 무엇을 소유하는가? | [Team Role Ownership](team/TEAM_ROLE_OWNERSHIP.md) | [Team Workflow](team/TEAM_WORKFLOW.md) |
| Sprint·Git·Review·Ready·Done은 어떻게 운영하는가? | [Team Workflow](team/TEAM_WORKFLOW.md) | [Development Flow](planning/DEVELOPMENT_FLOW.md) |
| 현재 무엇을 하는가? | [Sprint 1 Stage 1 PoC](planning/SPRINT_1_STAGE_1_POC.md) | GitHub Issue/Project |

충돌할 경우 위 표의 기준 문서가 우선한다. 기준을 변경할 때는 보조 문서와 현재 Backlog를 같은 PR에서 함께 갱신한다.

## 용어 구분

- `Product Vertical Slice`: Spaceship 준비부터 Planet 전투·귀환까지 플레이어가 경험하는 M2 검증판
- `Architecture Slice`: M1에서 한 계약을 Domain부터 Test Scene까지 연결하는 작은 기술 단위
- `P0`: 해당 Milestone의 통과를 막는 필수 작업
- `P1`: Milestone의 핵심 결과를 완성하는 작업
- `P2`: 품질·사용성·도구 개선 작업
- `P3`: 현재 Milestone 밖의 아이디어 또는 Deferred 작업

## 문서 변경 규칙

- 게임 규칙·범위·Deferred 변경: [PROJECT_PLAN.md](product/PROJECT_PLAN.md) Decision Log
- Core lifecycle·data contract·외부 SDK 경계 변경: System Charter와 Core Design
- 역할·AI Owner 변경: Team Role Ownership과 Team Workflow
- Milestone 또는 Priority 변경: Core Design, Development Flow, 현재 Sprint Backlog
- 승인되지 않은 초안은 Source of Truth와 같은 표현을 사용하지 않는다.

- [Player Input Guide](team/player-input-guide.md): Player V2 입력, Match 연결과 임시 포탑 체력·재충전 설정
