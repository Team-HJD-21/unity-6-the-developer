# Team Role Ownership — 5개 제품 영역

기준일: 2026-09-20
상태: 역할·AI·협업 경계의 현행 기준 / Sprint 1 역할 확정

## 1. 팀 구성

| 코드 | 이름 | 역할 | 상태 |
| --- | --- | --- | --- |
| A | 양현석 | Project Lead / Core Systems & UI Architect | 확정 |
| B | 이영빈 | Player, Cooperative UX & Cinematic Director | 확정 |
| C | 김진태 | World, Mission & Agent Experience Designer | 확정 |
| D | 황재동 | Defense, Progression & Balance Designer | 확정 |
| E | 조수빈 | Enemy, Encounter & AI Platform Engineer | 확정 |

C와 D의 역할 매핑은 각각 김진태, 황재동으로 확정됐다. 제품 영역은 다른 역할에 자동 합치지 않으며, 각 Task에는 지원자와 가용 시간을 명시한다.

## 2. 공통 운영 원칙

- Feature마다 최종 Owner는 한 명이다. Owner는 범위·품질·테스트·문서·통합 상태를 끝까지 책임진다.
- A는 Core의 Owner지만 모든 Content와 Feature를 직접 구현하지 않는다.
- Build·CI·QA·Release는 특정 한 사람의 전담 영역이 아니라 각 Feature의 Done 조건이다.
- 인간과 AI는 같은 Player Action/Command 계약을 사용한다.
- AI·Balance·World Content 수치는 코드에 고정하지 않고 Definition/Profile Data로 조정한다.
- 실제 Host Migration, PvP와 `RivalPlayerAgent`는 Deferred다.

## 3. 역할별 책임

### A — 양현석 · Project Lead / Core Systems & UI Architect

**Owner**

- 프로젝트 범위, Priority, Milestone Gate와 Decision Log
- `AppBootstrap`, `AppRoot`, Composition Root와 App lifecycle
- `MatchSession`, `MatchState`, `GameSimulation`, `ModeRules`, Authority 계약
- ID, Command, Event, Result, Snapshot, Error와 Content schema/version
- `PlayerProfile`, Reward transaction, Economy 정책, local binary cache contract
- Steam/Backend/Network adapter 경계와 통합 Architecture
- Mission/Research/Result/Economy UI architecture와 navigation
- Core Test, Architecture Decision Record와 통합 승인

**직접 Owner가 아님**

- Player 조작감·Combat HUD 세부 구현(B)
- Planet/Hub authored Scene와 Mission Content(C)
- Turret/Power 실제 기능과 Balance(D)
- Enemy/Boss/Encounter와 AI runtime(E)

### B — 이영빈 · Player, Cooperative UX & Cinematic Director

**Owner**

- Player 이동·조준·사격·능력·피격·회복·상호작용 Presentation
- Input, Camera, Targeting feedback, Combat HUD, 접근성, Tutorial
- Hub Console의 player-facing interaction pattern
- Lobby/Invite/Random/SOS/Ping/Chat/Reconnect 화면과 입력 흐름
- 짧고 Skip 가능한 실시간 Cutscene, Timeline, UI/Input 전환
- 인간/AI가 공유할 Player Action 계약의 Presentation 측

**경계**

- Match State·Reward·Currency를 직접 변경하지 않고 A의 Command/Use Case를 사용한다.
- 환경과 공간은 C, Turret/Progression 의미는 D, Enemy/Boss 행동은 E와 협업한다.

### C — 김진태 · World, Mission & Agent Experience Designer

**Owner**

- Planet topology, Sector/Expansion, 방어 거점, Collision, Spawn 접근로, Boss arena
- 환경 기믹, 위험 지형, 상호작용 오브젝트, Dynamic Mission/Event
- Hub Interior, 공간 동선, 다이아제틱 UI의 월드 배치
- `PlanetDefinition`, `MissionDefinition`, `SectorDefinition`, `EnvironmentModifier`
- `AllyPlayerAgent`의 목표·지원·생존·Mission 우선순위와 구체 구현
- `RivalPlayerAgent` 정책/구현은 Deferred

**경계**

- C는 적 개체 AI와 Horde Director를 소유하지 않는다.
- Ally Agent는 E의 AI runtime과 B의 Player Action 계약을 재사용한다.
- 전투 수치의 최종 결정은 D의 Profile/Balance를 따른다.

### D — 황재동 · Defense, Progression & Balance Designer

**Owner**

- Cannon/Missile/Laser 터렛, Control Unit, Power 소비·회수, Targeting과 방어 전술
- `설치`는 Scene/Prefab authoring을 의미하며 플레이어의 건설 기능을 의미하지 않는다.
- Engineering/Research, 해금·강화와 Resource 소비 경험
- `TurretDefinition`, `PowerDefinition`, Upgrade/Counter/Reward/Economy Data
- 전투·경제·성장·Wave 난이도·AI profile의 수치 설계와 Simulation

**경계**

- 공용 State/Command/Reward transaction 계약은 A와 합의한다.
- Enemy 행동은 구현하지 않고 E가 사용할 상성·난이도 Data를 제공한다.

### E — 조수빈 · Enemy, Encounter & AI Platform Engineer

**Owner**

- 기본/탱커/원거리/암살자/Elite/Boss의 행동, Targeting, Skill과 Boss phase
- `EncounterDirector`: Wave, Spawn 위치·타이밍, 조합, 압박 곡선과 Boss 투입
- AI framework, behavior/state runtime, 감지·경로·타게팅·action executor
- `EnemyArchetype`, `BossDefinition`의 행동 구현과 Data 연결
- AI 관련 성능 Test와 공통 Debug 도구

**경계**

- Ally/Rival Player Agent의 목표 정책은 C가 소유하며 E는 공통 runtime을 제공한다.
- Network Authority와 Match State를 직접 우회하지 않고 A의 Command 계약을 사용한다.
- Difficulty 수치는 D가 소유하고 E는 행동 구현과 검증을 담당한다.

## 4. AI 책임 분해

| AI | 최종 Owner | 협업 |
| --- | --- | --- |
| Enemy Actor AI | E · 조수빈 | D: 수치/상성, C: 공간, A: Command |
| Encounter/Horde Director | E · 조수빈 | D: 난이도, C: Spawn layout, A: ModeRules |
| Ally Player Agent | C | E: runtime, B: Player Action, D: profile, A: authority |
| Rival Player Agent | C, Deferred | E/B/D/A |

AI는 `GameCommand`를 생성하고 A의 Authority/Simulation이 검증·처리한다. EventBus는 로컬 전달 수단이지 Network 동기화 수단이 아니다.

## 5. 기능 연결 지점

| 연결 지점 | Owner | 지원 | 완료 판단 |
| --- | --- | --- | --- |
| Spaceship → Planet Launch/Return | A | B, C | Mission/Match/Result/Profile이 정확히 왕복 |
| Player Action → Match Command | B | A, E | 인간/AI/향후 Network 입력이 같은 계약 사용 |
| Expansion → Mission/UI | C | A, B | Sector 상태와 World/Minimap/Mission UI 일치 |
| Upgrade → Turret/Power | D | A, B | 다음 MatchConfig와 전투 수치에 반영 |
| Enemy/Director → World/Balance | E | C, D, A | Spawn·행동·수치·권한이 각 Owner 경계 준수 |
| 이탈 Player → Ally AI → 복귀 | C | A, B, E | Slot·권한·표현·Agent 행동이 끊기지 않음 |
| 통합 Build/Playtest | Sprint Release Captain | 전체 팀 | 같은 Build로 Core Flow 완주 |

## 6. Review 조합

| 작성자 | 기본 검토자 | 주요 검토 관점 |
| --- | --- | --- |
| A | D 또는 E | 계약이 Feature 구현을 과도하게 제한하지 않는가 |
| B | A 또는 D | State/Command와 UI·전투 의미가 일치하는가 |
| C | A 또는 E | World Data, Spawn, AI 경계가 맞는가 |
| D | A 또는 E | State/Reward 계약과 Enemy 난이도가 맞는가 |
| E | A 또는 D | Authority와 Balance Data를 우회하지 않는가 |

GitHub의 Required Approval 수는 0으로 유지한다. 작은 독립 변경은 Self-review와 CI 통과 후 Merge할 수 있다. 공용 계약, Save/Profile, Scene 구조, Build 설정, Network 변경은 표의 검토자에게 Review를 요청하고 의견을 해결한 뒤 Merge한다.

## Sprint 1 Stage 1 PoC 역할 초점

| 담당 | 핵심 책임 | 협업 경계 |
| --- | --- | --- |
| A · 양현석 | PoC 범위·계약·임시 승패 조건·10월 2일 통합 | B/C/D/E 결과를 한 Build에 연결하고 범위 결정을 기록 |
| B · 이영빈 | Unity Netcode 계열 조사와 Player/Combat 지원 | 실시간 협동은 구현하지 않고 RPC·동기화·권한 위험을 문서화 |
| C · 김진태 | 전선·거점 확장 Graybox와 Spaceship 공간 조사 | D의 터렛 상태와 E의 공격 결과를 World 변화에 연결 |
| D · 황재동 | 터렛 인스턴스 관리와 AI 평가 데이터 | 위치·활성·체력·화력·거리·위협도 계약을 E와 합의 |
| E · 조수빈 | 터렛 점수 기반 목표 선택, 경로·Spawn 실험 | brute-force를 먼저 검증하고 필요 시 region 방식과 비교 |

Sprint 1의 연결 순서는 `D 터렛 데이터 → E 목표 선택 AI → C 전선 확장 → A 통합`을 기본으로 한다. 이 표는 이번 PoC의 업무 초점이며 장기 제품 영역의 Owner를 바꾸지 않는다.

## 7. Milestone 적용

Milestone별 P0/P1/P2/P3 업무는 [milestones-and-core-design.md](milestones-and-core-design.md)가 기준이다. 역할 자체는 Milestone마다 바뀌지 않으며, 작업량만 달라진다.

- M0/M1: A의 Core 부담이 가장 크고 B/C/D/E가 각 Feature의 계약·최소 Slice를 병렬 제공한다.
- M2: C/D/E의 Content·Gameplay 비중이 커지고 B가 Player/UX, A가 왕복 통합을 책임진다.
- M3: A가 Authority/Network, B가 Co-op UX, C가 Ally Agent, D가 Co-op Balance, E가 AI runtime을 맡는다.
- M4/M5: Feature Owner가 Content·품질을 마감하고 A는 운영·통합·Release Gate를 관리한다.

## 8. C/D 확정 이후 운영 규칙

1. C는 김진태, D는 황재동을 기본 Owner로 지정한다.
2. Sprint Planning에서 두 사람의 실제 가용 시간과 지원자를 확인한다.
3. 개인별 동시 핵심 Task는 하나를 넘기지 않는다.
4. 가용 시간이 부족하면 A가 자동 대행하지 않고 Task 범위를 줄이거나 Milestone을 조정한다.
5. 역할 변경이 필요하면 Decision Log와 Ownership 문서를 같은 PR에서 갱신한다.
