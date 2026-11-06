# The Developer 문서 안내

최근 기획: [11월 6일 기획 보강 근거·후속](planning/2026-11-06-planning-review.md). 현행 우주선 출입·점유·안전장치 정책은 [Product Plan](product/PROJECT_PLAN.md)을 따른다. 아래 이전 회의 안내의 정지 정책은 당시 기록이며 현행을 대체하지 않는다.

Core 기반 main 통합 검토: [11월 5일 범위·현행 Feature 호환·검증 기록](planning/2026-11-05-foundation-main-integration.md) · [실제 호출·수명·API 지도](architecture/core-lifecycle-and-api-guide.md) · [Core 코드 안내](../Assets/Scripts/Core/README.md). 기존 회의록과 파트별 문서는 유지하며 이 통합으로 미연결 Feature의 완료를 선언하지 않는다.

**Core 연동 전 필독:** [Random API 사용법](architecture/core-random-api.md)에서 도입 이유, Host/로컬 연출 구분, 주입·가중치 선택·저장 예제와 기존 6개 파일의 교체 위치를 확인한다. [우주선 출입 보고 API](architecture/core-presence-report-api.md)는 실제 Host 이동 후 Core에 결과를 보고하는 계약이다. Feature 호출부는 이 PR에서 교체하지 않았다.

Random 등록·해제는 공용 정적 함수 `RandomBinding.Bind` 한 줄로 연결한다. [바로 사용하기](architecture/core-random-api.md#바로-사용하기--연결-한-줄-기존-함수-그대로)에서 완성된 컴포넌트 예제와 순수 함수 사용법을 확인한다. 별도 부모 클래스나 파트별 해제 클래스를 만들지 않는다.

최근 기획·후속 검토: [10월 30일 기획 보강](planning/2026-10-30-planning-review.md) · [10월 31일 정기회의](planning/2026-10-31-regular-meeting.md) — 전체 전사·수기 대조로 자동 활성화·성장 방향을 보강하고 우주선 필드 정지, 점령/스폰·그룹화, 재방문·실패 보상과 후속 작업을 구분했다. 세부 설계·실제 구현·재미 검증은 별도다.

최근 진행 공유: [11월 3일 주간 보고](planning/2026-11-03-weekly-progress.md) — 지원형 소환/회복 두 종 분리, 암살자 비탐지 우선 대상, 수리 검증용 터렛 배치와 파트별 후속. 분열 방벽은 실험 후보이며 구현·실행 검증 완료나 새 Sprint 확정이 아니다.

수정일: 2026-11-03
상태: 현행 PL 기준 / 몬스터 목표 배정·추적 우선 시험 방향 기록 · 실행 검증 미완료

이 폴더는 Unity 6 기반 `The Developer`의 제품 기획, New Core 아키텍처, 역할, Milestone과 실행 규칙을 보관한다. 레거시 코드는 기능·수치·연출의 참고 자료이며 신규 기능의 기반으로 사용하지 않는다.

## 현재 기준






2026-11-01 사용자 확인: 10월 31일 회의의 **전원 크런치 모드 도입은 확정된 팀 공통 운영 결정**이다. 적용 대상 미확인이라는 기존 해석을 정정했다. 작업·휴식 기간 발언과 후속은 [회의 기록](planning/2026-10-31-regular-meeting.md)을 따른다.

현행 게임 규칙은 [Product Plan](product/PROJECT_PLAN.md)을 따른다. 회의별 설명은 당시 근거이며 11월 6일 후반 정정과 구분한다. 새 Sprint·Build/Gate 통과를 뜻하지 않는다.

2026-10-26 Core 후속을 최신 main에 통합하고 Match 운영 책임을 분리하는 배치를 시작했다. [Match 운영 책임과 수명 계약](architecture/match-management-responsibilities.md)은 Feature 상태 원본, MatchCoordinator / SceneFlowController / ScreenManager의 역할과 완료·입력·환경·추가 생성 계약을 설명한다. [이번 구현 기록](planning/2026-10-26-core-sprint-implementation.md)에서 CU 명시 초기화와 실제 Scene 검사 결과·다음 배치를 확인한다. 목표 구조와 실제 구현/검증 완료를 구분한다. 아래 10/15 검증은 당시 source의 증거다.

2026-10-24 회의는 수기와 완성된 자동 전사를 대조했다. 가동 준비 지연 없음, 당시 정지·전력 반환 검토, 일부 파괴 전력 예약과 키 유지 수리·복구 방향을 기록했다. 일반 OFF 순서는 위 10월 30일 후속을 우선한다. 후반의 경험치 유지·토큰/Blueprint 성장·터렛 레벨별 Minor Upgrade 상한, 분대 목표 유지와 전체 스폰량 조절 방향도 기록했다. 확률·시간·일정 세부와 구현·성능 검증은 별도다. [회의 검토 기록](planning/2026-10-24-regular-meeting.md)에서 확정 방향과 추가 확인 항목을 구분한다.

2026-10-15 Core 통합 스프린트를 진행한다. 정적·동적 지역 모두 지역 Grid의 상태가 원본이며, 동적 삼각형은 점령 셀을 계산하는 임시 자료다. 동적 모드는 지역 잠금 없이 활성 터렛으로 점령을 갱신하고, 점령 회수로 나머지 터렛을 끄지 않는다. [배치·구현·검증 기록](planning/2026-10-15-core-sprint-implementation.md)에서 실제 구현과 미완료 통합을 구분한다.

최신 Core 검증은 Windows Host/Client 두 프로세스의 명령·지역/HUD 상태·종료/재시작·실행 중 Client 연결 해제까지 수행했다. [이슈별 감사·직접 확인 안내](planning/2026-10-15-core-sprint-review-handoff.md)에서 개발 actor fixture의 한계와 Player/UI·FieldTurret·Enemy/Encounter 연계·PL 수동 인수를 구분한다. 이 결과로 전체 협동 완성이나 이슈 자동 종료를 선언하지 않는다.

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

Enemy 컨트롤러·능력 구조, 네트워크 행동 실행, 런타임 프리팹 추가 방법은 [Enemy 시스템 가이드](architecture/enemy-system-guide.md)를 참고한다.

터렛을 사용하는 다른 영역은 [터렛 연동 가이드](architecture/turrets/index.md)에서 빠른 시작과 API 및 스냅샷을 따로 확인할 수 있다. 내부 실행 흐름은 기존 Turret System Guide의 Mermaid를 유지한다.

플레이어 스킬의 책임 분리와 신규 네트워크 몬스터 연동·테스트 범위는 [Player skill review](architecture/player-skill-review.md)를 참고한다.

스킬 Canvas를 씬에 미리 배치하고 실행 중 생성되는 로컬 플레이어에게 연결하는 방법은 [Player Skill HUD API](architecture/player-skill-hud-api.md)를 참고한다.

Initial 씬의 플레이어 체력·전력 표시와 터렛 선택·레벨 변경 UI는 [Player UI API](architecture/player-ui-api.md)를 참고한다.

플레이어 무기 전환과 스패너 공격·수리는 [Player Weapon API](architecture/player-weapon-api.md)를 참고한다.

LMB 공격과 포탑 선택·설치·UI 클릭의 우선순위는 [Player Primary Input API](architecture/player-primary-input-api.md)를 참고한다.

Space 대시, 미리 배치한 잔상과 하단 쿨타임 표시는 [Player Dash API](architecture/player-dash-api.md)를 참고한다.

Player 클래스 이름과 책임 구분은 [Player 클래스 이름과 역할](architecture/player-class-responsibilities.md)을 참고한다.

대각선 대기 호흡과 방향별 독립 스프라이트, 전체 그림 검토는 [Player 스프라이트 후속 검토](art/player-idle-breathing-and-sprite-review.md)를 참고한다.

2026-10-30 독립 좌우 연결과 가방 하단 불꽃 보완은 [독립 좌우 자세와 제트팩 분사구](art/player-independent-facing-review.md)를 참고한다.

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

| 질문 | 기준 |
| --- | --- |
| 게임 방향·범위·확정 제품 규칙 | [Product Plan](product/PROJECT_PLAN.md) |
| 현재 목표·미정·검증할 항목 | [현재 실행 계획](planning/current-execution-plan.md) |
| 장기 Milestone·기술 Gate | [Milestone](planning/milestones-and-core-design.md) |
| 책임·수명과 공용 호출 | [Core API 지도](architecture/core-lifecycle-and-api-guide.md), [Match 책임](architecture/match-management-responsibilities.md), 각 영역 API |
| Player 입력·설치 | [입력 계약](architecture/player-primary-input-api.md), [스킬](architecture/player-skill-review.md) |
| 터렛 상태·전력·공개 API | [터렛 API](architecture/turrets/api-reference.md), [구조와 초안](architecture/turrets/index.md) |
| Enemy 구조·생성 | [Enemy 가이드](architecture/enemy-system-guide.md) |
| 담당·진행 상태 | GitHub Issue/Project. [역할](team/TEAM_ROLE_OWNERSHIP.md)은 책임 기준 |
| 실제 동작과 인수 | 해당 코드 버전과 실행/PL 인수 증거. 계획으로 완료를 추정하지 않음 |

## 찾기

- [공식 문서 검색](https://docs.teamhjd.com/docs/projects/the-developer/api-search): 선택한 프로젝트의 현행 문서만 검색한다.
- [공식 아카이브 검색](https://docs.teamhjd.com/docs/projects/the-developer/archive-search): 사이트에 게시된 해당 프로젝트의 과거 문서만 별도로 검색한다.
- [정리 규칙](DOCS_ORGANIZATION_RULES.md) · [개발 흐름](planning/DEVELOPMENT_FLOW.md) · [기술 명명](architecture/naming-and-architecture-conventions.md) · [에셋 구조](architecture/asset-layout-and-migration.md)
- [도구 사용법](tooling/game-debugger-guide.md) · [Unity 검사 규칙](development/unity-runtime-test-guidelines.md)
- [교체·보관 목차](archive/2026-11-05/index.md). 과거 문서의 당시 경로·테스트·미정은 현재 계약이 아니다.

현행 문서를 교체하기 전 버전은 보존하고 기존 링크는 보관 안내로 유지한다. 팀원 이름을 기술 식별자의 부분 문자열에 치환하지 않는다.
