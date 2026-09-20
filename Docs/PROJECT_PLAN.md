# The Developer — Product Plan

버전: 0.3
기준일: 2026-09-19
상태: 현행 PL 기준 / Sprint 1 Stage 1 PoC 실행 중

이 문서는 `무엇을 왜 만드는가`를 정의한다. 구현 순서와 일정은 [milestones-and-core-design.md](milestones-and-core-design.md), 담당 경계는 [TEAM_ROLE_OWNERSHIP.md](TEAM_ROLE_OWNERSHIP.md)를 따른다.

## 1. 게임 정체성

> 선발대가 행성에 미리 설치한 터렛 거점을 제한된 전력으로 운용해 전선을 밀고 당기며, 플레이어가 위험한 전선을 찾아가 지원하는 2D 탑다운 액션 타워디펜스.

터렛은 플레이어가 건설하거나 이동하지 않는다. `설치`는 제작자가 Scene/Prefab에 미리 배치한다는 뜻이며, 플레이어 행동은 터렛 선택·가동·정지, 전력 재배치와 현장 지원이다. 플레이어는 혼자 적을 쓸어버리는 주 화력보다 수리·보조 공격·군중 제어로 무너지는 전선을 복구하는 기동 지원 유닛에 가깝다.

## 2. 플레이어 경험 목표

1. 모든 터렛을 켤 수 없다는 긴장감을 느낀다.
2. 적 조합과 공격 방향을 읽고 전력을 배분한다.
3. 전력이 없는 방어선으로 직접 이동해 빈틈을 메운다.
4. 새 Sector가 열리면서 새로운 선택지를 발견한다.
5. 확장과 함께 전선·이동 거리·위협도 커지는 부담을 감수한다.
6. 마지막에는 자신이 구성한 방어망 전체로 행성을 지켜냈다는 성취감을 얻는다.

## 3. Core Loop와 Meta Loop

```text
Spaceship에서 Mission·행성 상태 확인
→ Research·Power·Turret 준비
→ Planet으로 Launch
→ 초기 적 제거와 첫 터렛 거점 확보
→ 제한 전력 안에서 터렛 활성화·강화
→ 약한 전선으로 이동해 수리·보조 전투·군중 제어
→ 새 거점 확보와 전장 확장 또는 거점 상실과 전선 후퇴
→ 임시 승리·실패 조건 확인
→ Spaceship으로 Return
→ Result 확인·성장 적용·다음 Mission 선택
```

### 승리·실패 조건

- 최종 승리 조건은 마지막 거점 점령, Boss 처치, 별도 목표 점령 중에서 아직 결정하지 않았다.
- 최종 실패 조건은 Player 사망, Spaceship·Control Unit 파괴, 핵심 거점 상실 중에서 아직 결정하지 않았다.
- Stage 1 PoC에서는 한 사이클을 끝낼 수 있는 임시 승리·실패 조건만 구현하고 2026-10-02 플레이 테스트 결과로 결정한다.
- 실패 보상 비율과 `RewardReceipt` 반영 규칙은 최종 조건이 정해진 뒤 `ModeRules`와 Balance Data에서 결정한다.

## 4. 핵심 게임 규칙

### 4.1 Power Budget

- 최대 전력은 공간에 펼쳐진 Loadout Cost다.
- Pre-Wave에는 자유롭게 조합을 변경한다.
- In-Wave 변경은 가능하지만 전력 회수 지연 또는 횟수 제한을 둔다.
- 하나의 터렛 조합이 모든 Wave의 정답이 되어서는 안 된다.

초기 Paper Simulation 기준값은 최대 100, Cannon 20, Missile 30, Laser 50이다. 이는 확정 Balance가 아니다.

### 4.2 Map Expansion

확장마다 아래 두 항목을 모두 제공한다.

- 새 이점: 신규 터렛, 외곽 요격선, 지름길, Radar, 보조 시설 중 하나 이상
- 새 위협: 신규 Spawn 방향, 긴 이동 거리, 다중 전선, 특수 Enemy 중 하나 이상

면적과 배경만 늘어나는 확장은 Content로 인정하지 않는다. 이전 Sector의 터렛은 확장 후에도 내곽 방어선으로 유효해야 한다.

Stage 1 PoC에서는 여러 짧은 맵보다 하나의 확장형 전장을 먼저 검증한다. 터렛 거점을 확보하면 안개가 걷히고 다음 터렛·목표가 드러나며, 어느 방향을 먼저 공략할지 선택할 수 있는 비선형 확장을 후보로 둔다.

### 4.3 Player와 Turret

- 터렛만 켜고 기다리는 플레이와 Player 화력만으로 전력 규칙을 무시하는 플레이를 모두 피한다.
- Player는 이동 가능한 지원 전력이며 터렛 사각, 긴급 Enemy, 전력이 없는 구역을 담당한다.
- Stage 1 PoC에서는 수리, 보조 공격, 군중 제어 중 어떤 행동이 주 역할인지 비교한다.
- Turret Cost·Range·역할·현재 상태와 Enemy의 주요 특성은 전투 중 읽을 수 있어야 한다.

### 4.4 Spaceship Hub

Spaceship은 장식 Lobby가 아니라 Mission 준비와 성장 결과를 이해하는 Gameplay Space다.

- Navigation: 행성 상태·Expansion·Mission 선택
- Engineering/Research: 전력 또는 터렛 성장 적용
- Communications: 선발대 기록과 Mission 정보
- Launch Point: 선택한 Mission 확인 후 출격

Vertical Slice에서는 기능이 있는 작은 Interior만 만든다. 빈 복도를 오래 이동하거나 다층 함선을 구현하지 않는다.

### 4.5 Enemy와 전선

- Enemy의 1차 목표는 Player 추격이 아니라 터렛 거점과 핵심 시설 공격이다.
- Stage 1 PoC는 위치, 활성 상태, 체력, 화력, 거리와 위협도를 사용해 터렛별 점수를 비교하는 brute-force 타게팅부터 검증한다.
- 결과가 불명확하면 터렛 주변을 region으로 묶는 지역 단위 점수 방식과 비교한다.
- Spawn 위치와 주기는 약한 전선과 Player 위치를 고려하되, 구체 규칙은 PoC 결과로 결정한다.

### 4.6 낮과 밤 실험

- 낮에는 전선을 밀고 밤에는 시야 감소·Enemy 증가 등 불리함을 주어 Spaceship 복귀와 정비를 유도하는 방향을 검토한다.
- 밤의 정확한 길이와 효과는 확정하지 않았으며 Stage 1 필수 범위가 아니다.

## 5. M2 Product Vertical Slice

M1 New Core Foundation이 통과된 뒤 M2에서 제작한다. 2026-10-02 Stage 1 PoC는 핵심 재미와 구현 가능성을 빠르게 확인하는 time-box 실험이며, 그 자체로 M1 또는 M2 Gate 통과를 뜻하지 않는다.

### 플레이 범위

- 목표 플레이 시간: 15~20분
- Planet 1 Full Map 1개
- 검증 상태: 50% → 75% → 100%
- 완성형 확장 계획: 50% → 65% → 75% → 90% → 100%
- Player Weapon 1종, Control Unit 1개
- Cannon, Missile, Laser 터렛
- Default, Tanker, Assassin Enemy와 Boss 1종
- Power HUD와 개별 터렛 ON/OFF
- 소형 Spaceship Interior와 필수 Console
- Spaceship → Planet → Result → Spaceship 왕복
- Upgrade Choice 1개, Clear·Fail·Retry
- 최소 Tutorial과 Context 설명

### M2 제외 범위

- 실제 Online Co-op, Random Match, SOS
- 두 번째 Planet과 다수 Player Weapon
- 플레이어의 터렛 건설·이동
- Sector 단위 일괄 전원 제어
- 대형 Research Tree와 장기 Economy
- Random/Procedural Map
- Multi-deck Spaceship, Ship Combat, Crew/Maintenance Simulation
- 전체 Story와 장편 Cutscene

## 6. 출시 방향

- M2까지는 Single-player Core와 Product Vertical Slice를 검증한다.
- M3에서 Steam 친구 초대 기반 2-player Co-op MVP를 검증한다.
- M4에서 Random Match, SOS, Reconnect, Chat/Ping과 Content Alpha를 범위별로 검증한다.
- 실제 Host Migration은 Deferred다. M4에서 Snapshot/Authority 이전 PoC만 수행하고 출시 포함 여부는 별도 결정한다.
- PvP와 `RivalPlayerAgent`는 Release 이후 별도 Product Track이다.
- Profile은 Single·Co-op에서 공유하며 Backend authoritative를 목표로 한다. Local versioned binary는 캐시다.

## 7. Product Vertical Slice 통과 지표

- 외부 Tester 5명 중 4명이 설명 없이 터렛을 켜고 Power Limit을 이해한다.
- 5명 중 4명이 Expansion과 Power Allocation을 게임의 특징으로 기억한다.
- 5명 중 4명이 Spaceship에서 다음 행동과 Launch 방법을 찾는다.
- 서로 다른 두 Turret Build로 Clear할 수 있다.
- Player의 직접 전투가 최소 한 번 이상 승패에 의미 있게 영향을 준다.
- Expansion 뒤 Attack Direction과 Power Allocation이 실제로 달라진다.
- 이전 Sector가 최종 방어에서도 사용된다.
- Progression Blocker, Crash, Save Corruption이 없다.

## 8. 미결정 항목

다음은 정해진 기능이 아니라 검증할 질문이다.

- 모든 Sub-stage 후 Spaceship으로 귀환할지, 주요 Mission 뒤에만 귀환할지
- 이전 Mission의 터렛 상태·피해를 다음 Mission에 유지할지
- In-Wave 전력 전환의 지연과 횟수 제한
- 터렛 파괴·수리 규칙
- 미확장 구역 접근 방식
- 최종 Planet 수와 전체 플레이 시간
- Sector 일괄 전원 제어의 필요성
- Single-player Offline 정책과 Backend 장애 시 동작
- Host Migration의 출시 포함 여부
- Player의 주 지원 행동과 공격 조작 방식
- 터렛 단위와 region 단위 전선 계산 중 채택할 방식
- 낮/밤 주기와 위험 효과
- 한 행성의 목표 플레이 시간. 20~25분 의견은 나왔으나 확정하지 않음

미결정 항목은 현재 Milestone의 P0/P1을 대체하지 않는다. 실험 결과와 함께 Decision Log에 승인된 뒤 Backlog로 이동한다.

## 9. Decision Log

| 날짜 | 결정 | 이유 | 영향 |
| --- | --- | --- | --- |
| 2026-09-10 | 첫 Product 목표를 Planet 1 Vertical Slice로 제한 | Core Fun 검증 전 Content 확장 방지 | Online, Planet 2, 대형 Progression 보류 |
| 2026-09-10 | 터렛은 선발대가 미리 설치 | 건설보다 제한 전력 운용에 집중 | 플레이어의 건설·이동 기능 제외 |
| 2026-09-10 | 초기 검증은 개별 터렛 조작 | 현재 Turret 수와 UI 복잡도에 적합 | Sector Control은 Playtest 후 재검토 |
| 2026-09-10 | 소형 Spaceship 왕복을 Product Vertical Slice에 포함 | Combat만으로 최종 경험을 대표할 수 없음 | M2에 Hub·Launch·Return 포함 |
| 2026-09-11 | M1을 New Core Foundation으로 정의 | 레거시 Manager 위에 신규 기능이 쌓이는 것을 방지 | 실제 Planet/Hub 통합은 M2에서 수행 |
| 2026-09-11 | Profile은 Backend authoritative, Local Binary는 cache | Single·Co-op의 공정한 공유 성장 유지 | M1은 Port/Fake, 운영 Backend는 M4 |
| 2026-09-11 | M3의 Online 목표를 친구 초대 기반 2인 Co-op으로 제한 | Online 범위 팽창 방지 | Random/SOS/Chat/Ping은 M4 |
| 2026-09-11 | Host Migration 실제 기능과 PvP를 Deferred로 유지 | M2/M3 핵심 검증 우선 | M4는 Migration PoC까지만 기본 범위 |
| 2026-09-19 | 직접 전투보다 터렛 중심 전선 운영을 Stage 1 핵심으로 검증 | 기존 플레이가 Player 화력에 치우치는 위험을 줄이고 고유한 전략성을 확인 | Player는 기동 지원 역할, Enemy는 터렛·시설을 우선 고려 |
| 2026-09-19 | 여러 짧은 맵보다 하나의 확장형 전장을 우선 검증 | 거점 확보와 전선 변화가 한 사이클에서 보이는지 확인 | 거점 기반 Expansion, 전진·후퇴와 비선형 공략 후보 포함 |
| 2026-09-19 | 2026-10-02까지 기능 중심 Stage 1 PoC 통합 | 아트보다 핵심 재미와 구현 가능성을 먼저 판정 | `SPRINT_1_STAGE_1_POC.md`를 현재 실행 기준으로 사용 |
| 2026-09-19 | 실시간 협동은 PoC 범위에서 제외하되 network-aware 계약 유지 | 구현 범위는 줄이고 향후 전면 재작성 위험은 낮춤 | B가 Netcode/RPC/권한 구조를 조사하고 현재 코드 위험을 기록 |
