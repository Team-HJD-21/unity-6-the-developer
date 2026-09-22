# System Re-architecture Charter

작성일: 2026-09-07
수정일: 2026-09-11
상태: Core 경계와 레거시 교체 원칙의 Source of Truth

## Source of Truth

이 문서는 TeamHJD 회의록과 이후 합의된 내용을 제품·시스템 설계의 현재 기준으로 기록한다.
회의록의 Unreal 관련 마이그레이션 문구는 과거 이력으로만 취급한다. 현재 프로젝트의 기술 기준은
Unity 6이다.

아키텍처 계층 및 클래스 명명은 [naming-and-architecture-conventions.md](naming-and-architecture-conventions.md)를 기준으로 한다.
레거시와 목표 구조의 클래스/의존성 대응은 [architecture-rebuild-notes.md](architecture-rebuild-notes.md)를 기준으로 한다.

### 제품 방향

- 우주선은 메인 허브이자 다이아제틱 UI의 공간이다.
- 스토리 모드는 영역 확장형 행성 스테이지와 웨이브 기반 진행을 가진다.
- 연구는 Player 스킬 트리, Weapon/Turret, Control Unit의 세 카테고리로 구성한다.
- 스테이지 실패도 재화를 낮은 비율로 지급한다.
- 싱글, 재화 미니게임, 협동은 하나의 계정 성장/Profile을 공유한다.
- 싱글 Core를 먼저 검증하고 Steam 친구 초대 기반 2인 협동을 다음 단계로 확장한다.
- 랜덤 매칭, SOS, 채팅, Ping은 Co-op MVP 이후 Online 확장 범위다.
- 협동 참가자 이탈은 재접속 유예 후 Ally AI가 슬롯을 대체한다.
- Host Migration에 필요한 Snapshot/Authority 계약은 고려하되 실제 기능은 Deferred다. M4에서 PoC 후 출시 포함 여부를 다시 결정한다.
- 채팅, 미니맵 Ping, 구조 요청(SOS) 난입을 장래 온라인 기능으로 고려한다.
- PvP는 Deferred이며, 현재 방향은 PvE 성장 스펙을 활용하는 것이다.

## 재설계의 목적

기존 Manager를 다른 이름의 Singleton으로 대체하지 않는다. Unity 씬 오브젝트와 게임 규칙,
계정/경제, 네트워크 권한, 표현(UI/Audio)을 분리하여 싱글과 협동이 같은 게임 규칙을 공유하게 한다.

리팩터링 방식은 Strangler 방식이다.

1. 레거시를 동작시키는 동안 새 시스템의 경계를 만든다.
2. 작은 vertical slice 하나만 새 경계로 옮긴다.
3. 같은 경계를 다음 기능에서 재사용한다.
4. 기존 Manager의 책임이 비면 제거한다.

### 현재 프로젝트에 적용하는 방식

현재 레거시는 출시 가능한 기준선으로 유지하는 대상이 아니라 **행동과 콘텐츠의 레퍼런스**다.
새 기능을 레거시 Manager에 추가하지 않는다. 대신 새 운영체제(New Core)를 독립적으로 만들고,
이전할 기능을 하나씩 새 Core의 vertical slice로 다시 구현한다.

```text
Legacy:         기능 동작/수치/연출 참고, 긴급 버그 수정만
New Core:       앞으로의 모든 시스템 기능 개발
Minimal Test:   New Core의 단위·통합 검증 장면
Integration:    New Core가 충분해진 뒤 Hub/Stage 콘텐츠를 연결
Retirement:     기존 Manager의 책임이 완전히 대체되면 삭제
```

전체 레거시 프로젝트가 항상 Play Mode에서 동작하는 것은 전환기의 성공 기준이 아니다.
대신 새 Core의 각 vertical slice는 최소 테스트 씬 또는 테스트 코드에서 독립적으로 실행되어야 한다.
레거시 파일/폴더를 당장 이동하거나 삭제하지 않아 prefab·scene reference를 깨뜨리지 않는다.

## 현재 레거시에서 교체할 책임

| 현재 | 문제 | 목표 책임 |
| --- | --- | --- |
| GeneralManager | 씬 탐색 Service Locator, 영속/로컬 수명 혼합 | AppBootstrap + Scene Composition |
| GameManager | static 전역 상태 | AppState / Profile / MatchState로 분해 |
| SceneController | 씬 전환과 게임 규칙 결합 | ISceneFlow + Mode/Match 진입 흐름 |
| DataManager | 밸런스 정의, 재화, 해금, 업그레이드 혼합 | Content Catalog + Profile/Economy |
| InGameManager | 웨이브, 승패, 대화, UI, 보상 혼합 | Match Flow, Simulation, Presenter로 분해 |
| UI Manager | 직접 상태 접근·폴링 | Presenter가 상태/이벤트를 구독 |

## 목표 계층과 수명주기

```text
App (게임 실행 동안 1개)
  AppBootstrap
  PlatformService        Steam identity, ownership, auth ticket
  BackendGateway         Profile/reward validation API
  ProfileService         verified profile cache + persistence
  ContentCatalog         definitions/versioning
  SceneFlow              hub/lobby/match 화면 흐름

Lobby (온라인 방에 참여한 동안)
  LobbyService           invite, random match, member/ready state
  SocialService          chat, ping, SOS request

Match (스테이지/미니게임 1회 동안)
  MatchSession
  MatchConfig            immutable: mode, stage, difficulty, loadout snapshots
  GameSimulation         rules, command handling, state transitions
  MatchState             players, CU, wave, entities, result
  ModeRules              Story / Dungeon / Coop / future PvP rules
  MatchSnapshot          reconnect / host migration recovery

Scene / Presentation
  Unity adapters          Player, Monster, Tower, Spawner, physics
  Presenters              HUD, minimap, dialogue, audio, camera, diegetic UI
```

Only AppBootstrap may create the App root. Gameplay code must not query an
`Instance` property, find a manager by name, or modify shared profile state directly.

## 공유할 것과 나눌 것

| 공유 | 모드별 구현으로 나눌 것 |
| --- | --- |
| Content definitions, stat formulas, combat/wave/tower rules | authority: Local / Host / Dedicated Server |
| Command and MatchState contracts | connection, lobby, reconnect, migration |
| MatchConfig and loadout snapshot creation | Story/Dungeon/Coop/PvP ModeRules |
| reward receipt contract | reward validation policy |
| UI Presenter contracts | local UI layout and mode-specific panels |
| player slot and Agent contract | Human input / Ally Player Agent / Enemy AI |

`GameSimulation` is shared. A mode selects an authority adapter and a `ModeRules`
implementation; it must not fork the whole gameplay codebase.

```text
Single Story/Dungeon: LocalAuthority -> GameSimulation
Coop:                 HostAuthority / migrated authority -> GameSimulation
Future PvP:           DedicatedServerAuthority -> GameSimulation
```

## 데이터와 경제

### Content definition (versioned, immutable per match)

- `StageDefinition`, `WaveDefinition`, `SectorDefinition`, `WeatherDefinition`
- `PlayerSkillDefinition`, `WeaponDefinition`, `TurretDefinition`, `ControlUnitDefinition`
- `EnemyArchetype`, `EnemyDifficultyProfile`, `AllyPlayerAgentProfile`
- `RewardTable`, `ModeRulesDefinition`

ScriptableObject is appropriate for authoring. At match start, definitions are resolved
into a versioned immutable `MatchConfig`; runtime systems must not read mutable authoring
assets as state.

### Account/profile (backend authoritative)

- SteamID, currencies, unlocks, research levels, inventory/loadout
- profile version and transaction/receipt history
- local binary cache is a signed/versioned cache, never the authority source
- do not use BinaryFormatter; use a versionable format such as MessagePack or Protobuf

### Reward flow

```text
MatchResult -> RewardReceipt -> Backend validation -> Profile transaction
                                         -> verified Profile snapshot -> local cache
```

Clients and hosts may propose a result; only the backend finalizes the shared profile.
The exact anti-cheat strength depends on how much match evidence the backend validates.

## AI and difficulty

AI creates Commands; it does not bypass game rules.

```text
HumanPlayerAgent  ┐
AllyPlayerAgent   ├─> Command -> GameSimulation -> MatchState
EnemyAgent        ┘
```

Difficulty must be data-driven through profiles and encounter definitions, not duplicate
per-scene scripts. Runtime difficulty adjustments belong to `MatchConfig`/`ModeRules`.

## Online-specific boundaries

- `IPlatformService`: Steam initialization, SteamID, ownership, auth tickets.
- `IBackendGateway`: verified profile, match start/end, reward receipt, leaderboard.
- `ILobbyService`: invite/random queue, lobby metadata, membership, ready state.
- `INetworkSession`: transport, connection lifecycle, commands, replicated snapshots.
- `ISocialService`: chat, minimap ping, SOS request. These are network messages, not
  persistent MatchState.
- `IReconnectService`: player identity/slot reservation, reconnect token, AI takeover.
- `IMatchMigrationService`: snapshot publication, authority election, new-host restore.

Host Migration PoC는 testable `MatchSnapshot` serialization과 authority handoff protocol을
검증한다. Relay나 Lobby만으로 gameplay state가 이전되지는 않는다. PoC 성공은 실제 기능의
자동 승인이 아니며 [PROJECT_PLAN.md](../product/PROJECT_PLAN.md) Decision Log에서 별도로 범위를 결정한다.

## Refactoring guardrails

- Do not rewrite all Managers or all scenes at once.
- New code may not add `GeneralManager.Instance`, `GameManager` static state, or
  `GameObject.Find` dependencies.
- Existing UI stays initially, but new presenters receive an interface/state subscription.
- Existing prefabs are adapted at scene composition boundaries before being redesigned.
- Each vertical slice must run in a minimal test scene without Main/GeneralManager.
- The full legacy flow is not a gate during the rebuild; New Core testability is the gate.
- Before replacing a legacy feature, write its observable behavior and data inputs/outputs as
  a migration note. Reference behavior is copied deliberately, not accidentally.
- Each new persistent data type has schema version and migration policy.
- Every network-relevant state mutation is represented as a command/result, not direct UI
  or MonoBehaviour mutation.

## Architecture slices

1. M1 — App/Profile/Content boundary와 Single `MatchSession` Architecture Slice
2. M1 — Human/Enemy Command source와 difficulty Definition
3. M2 — Planet/Hub를 New Core에 연결하는 Single-player Product Vertical Slice
4. M3 — Lobby, player slot, replicated state, reconnect/Ally AI의 Co-op adapter
5. M4 — 두 Client의 host loss, snapshot restore, authority handoff PoC

정확한 Milestone·Priority·Exit Criteria는 [milestones-and-core-design.md](../planning/milestones-and-core-design.md)를 따른다.
