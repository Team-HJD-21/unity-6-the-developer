# The Developer — Project Documentation

수정일: 2026-09-24
상태: 현행 PL 기준 / Sprint 1 Stage 1 PoC 실행 중

이 폴더는 Unity 6 기반 `The Developer`의 제품 기획, New Core 아키텍처, 역할, Milestone과 실행 규칙을 보관한다. 레거시 코드는 기능·수치·연출의 참고 자료이며 신규 기능의 기반으로 사용하지 않는다.

## 현재 기준

- 장르: 2D 탑다운 액션 타워디펜스
- 핵심 경험: 제한된 전력으로 미리 배치된 터렛을 선택해 전선을 운영하고, 플레이어가 무너지는 전선을 찾아가 보조한다.
- Meta Loop: Spaceship에서 Mission·Research를 준비하고 Planet에 출격한 뒤 결과를 반영해 귀환한다.
- 현재 검증: 2026-10-02 Stage 1 PoC는 터렛 중심 전선 운영을 검증한다. 2026-09-22 대표 회의에서 진행 방식 3안을 비교하기로 했으며 최종 선택은 9월 26일 논의 대상이다.
- 개발 순서: New Core Foundation → Single-player Vertical Slice → 2-player Co-op → Online/Alpha → Release
- Deferred: 실제 Host Migration, PvP, `RivalPlayerAgent`, 대형 우주선, 두 번째 Planet

## 현재 팀

| 코드 | 이름 | 제품 영역 |
| --- | --- | --- |
| A | 양현석 | Project Lead / Core Systems & UI Architecture |
| B | 이영빈 | Player / Combat·Co-op UX / Cinematic |
| C | 김진태 | World / Mission / Ally Player Agent |
| D | 황재동 | Defense / Progression / Balance |
| E | 조수빈 | Enemy / Encounter / AI Platform |

C와 D의 담당은 각각 김진태, 황재동으로 확정됐다. 각 역할은 자신의 제품 영역과 관련 Task의 구현·검증·인수 기준을 끝까지 책임진다.

## 폴더 안내

새 문서의 배치·이동·보관 방법은 [Docs 정리 규칙](DOCS_ORGANIZATION_RULES.md)을 따른다.

- `product/` — 게임의 정체성, 제품 범위, Decision Log
- `architecture/` — 새 Core의 경계, 코드 명명 규칙, 레거시 이식 참고
- `planning/` — Milestone, 전체 개발 흐름, 현재 Sprint 실행 계획
- `team/` — 담당 영역, 협업 규칙, GitHub Issue 분류
- `archive/` — 지난 Sprint와 모집 자료. 현재 기준으로 사용하지 않음
- `tooling/` — 팀 공통 Unity 패키지·CLI 설치 및 오류 구분 절차
- `Local/` — 개인 메모·초안. `.gitignore` 대상이며 팀 공유 문서가 아님

Unity 패키지 복원과 CLI 연결은 [Unity Tooling Setup](tooling/UNITY_TOOLING_SETUP.md)을 따른다.

## 읽는 순서

1. [PROJECT_PLAN.md](product/PROJECT_PLAN.md) — 게임 정체성, 제품 범위, Vertical Slice와 Decision Log
2. [system-rearchitecture-charter.md](architecture/system-rearchitecture-charter.md) — 레거시 교체 원칙과 변경할 수 없는 시스템 경계
3. [milestones-and-core-design.md](planning/milestones-and-core-design.md) — M0~M5, Priority별 역할, 통과 기준
4. [naming-and-architecture-conventions.md](architecture/naming-and-architecture-conventions.md) — 계층, asmdef 의존성, 타입 명명
5. [turret-system-guide.md](architecture/turret-system-guide.md) — Canon·Missile 터렛의 상태·전력·Enemy 연동, 세부 업그레이드·레벨 승급 및 검증 방법
6. [TEAM_ROLE_OWNERSHIP.md](team/TEAM_ROLE_OWNERSHIP.md) — A~E 제품 영역과 협업 경계
7. [TEAM_WORKFLOW.md](team/TEAM_WORKFLOW.md) — Sprint, Git, Review, Ready/Done 규칙
8. [DEVELOPMENT_FLOW.md](planning/DEVELOPMENT_FLOW.md) — Milestone 안에서 실제 작업이 흘러가는 순서
9. [SPRINT_1_STAGE_1_POC.md](planning/SPRINT_1_STAGE_1_POC.md) — 2026-10-02 Stage 1 PoC 범위, 역할, 완료 기준과 통합 순서

Issue를 작성할 때는 [ISSUE_LABEL_GUIDE.md](team/ISSUE_LABEL_GUIDE.md)를 함께 본다. [architecture-rebuild-notes.md](architecture/architecture-rebuild-notes.md)는 레거시 분석과 이식 대응표를 담은 참고 문서다. [SPRINT_0_BACKLOG.md](archive/SPRINT_0_BACKLOG.md)와 [team-recruitment-proposal.md](archive/team-recruitment-proposal.md)는 역사적 자료이며 현재 일정·역할의 기준이 아니다.

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
