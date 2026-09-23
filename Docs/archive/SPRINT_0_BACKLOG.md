# Sprint 0 Backlog — M0 Alignment & Baseline

> 과거 Sprint 0 계획 기록이다. 현재 실행 기준은 [Sprint 1 Stage 1 PoC](../planning/SPRINT_1_STAGE_1_POC.md)를 따른다.

기간: 2026-09-14 ~ 2026-09-20, 1주 가안
목표: M1 New Core Foundation을 시작할 수 있도록 팀 기준, Owner, Build/CI와 입력 자료를 확정한다.

> Sprint 0는 M0 전용 1주 Sprint다. 완성된 Gameplay를 만드는 주가 아니라, M1 작업이 `Ready`가 되도록 만드는 주다.

## 1. Sprint Goal

> 모든 팀원이 같은 M1 범위와 A~E 경계를 이해하고, 필수 CI가 성공하는 저장소에서 자신의 첫 Architecture Slice를 시작할 수 있다.

## 2. 진행 순서

1. Baseline: `M0-001`, `M0-002`, `M0-003`
2. 역할별 P0 입력: `M0-A01`, `M0-B01`, `M0-C01`, `M0-D01`, `M0-E01`
3. 역할별 P1 설계: P0가 검토된 영역부터 병렬 진행
4. Gate Review: `M0-004`

C와 D는 각각 김진태, 황재동으로 확정됐다. 모든 Task는 실명 Owner의 가용 시간을 확인한 뒤 `Ready`로 전환한다.

## 3. P0 — M0 Gate

| ID | Owner | 지원 | 예상 | 선행 | 작업·가치 | 완료 조건 | 증거 |
| --- | --- | --- | ---: | --- | --- | --- | --- |
| M0-001 | A · 양현석 | 전체 | 3h | 없음 | Product Plan 0.2와 M0~M5 승인: 서로 다른 범위 해석 방지 | Decision Log·Deferred·Milestone 이름에 팀 이견 반영 | 승인 회의 기록과 Docs PR |
| M0-002 | A · 양현석 | B, E | 3h | 없음 | Repository Baseline 확인: 깨진 기반 위 개발 방지 | Compile Error 0, Main/Test 진입, 필수 CI 성공 | CI Run, Editor/Build 결과 |
| M0-003 | A · 양현석 | C · 김진태, D · 황재동 | 1h | M0-001 | C/D 역할 수락과 가용 시간 확인 | C/D Task에 이름·가용 시간·첫 P0 착수일 기록 | 역할 확인 기록과 Issue |
| M0-A01 | A · 양현석 | B, E | 6h | M0-001 | M1 Core Backlog와 asmdef 방향 확정 | App/Profile/Content/Match Slice의 Interface·Test·의존성 정의 | Architecture checklist/Issue |
| M0-B01 | B · 이영빈 | A | 4h | M0-001 | Player/Input/HUD 레거시 행동과 M1 경계 작성 | 입력·피격·HP 표시의 현재 동작, Command/Event, Test Case 기록 | Migration note와 Test 목록 |
| M0-C01 | C · 김진태 | A, B, E | 6h | M0-003 | Planet/Hub topology와 Expansion 요구사항 작성 | 50/75/100, CU·Spawn·Turret Slot·동선이 한 Graybox에 표시 | Graybox 이미지/Scene과 설명 |
| M0-D01 | D · 황재동 | A, B, E | 6h | M0-003 | Turret/Power 역할표와 Paper Simulation | 3 Turret 역할·Cost·Counter, 서로 다른 Clear 조합 2개 | 역할표와 계산 Sheet |
| M0-E01 | E · 조수빈 | A, C, D | 5h | M0-001 | Enemy/Wave 레거시 행동과 M1 최소 AI 범위 작성 | Enemy 1종·Director의 Command/Event, Target, Spawn/Defeat 조건 정의 | Migration note와 AI flow |
| M0-004 | A · 양현석 | 전체 | 2h | 모든 P0 | M0 Gate Review | 아래 Sprint 통과 기준을 항목별 통과/미통과 판정 | 회의 기록, 다음 Sprint Backlog |

## 4. P1 — M1 준비

| ID | Owner | 지원 | 예상 | 선행 | 작업·가치 | 완료 조건 | 증거 |
| --- | --- | --- | ---: | --- | --- | --- | --- |
| M0-A02 | A · 양현석 | B, C, D, E | 4h | M0-A01 | Minimal Test Scene Integration Plan | Player/CU/Wave/Reward의 생성·종료·Test 순서 명시 | Sequence/Dependency diagram |
| M0-B02 | B · 이영빈 | A, D | 3h | M0-B01 | Minimal HUD wireframe | Player HP, CU HP/Power, Wave/Result가 한 화면에서 읽힘 | 흑백 Wireframe |
| M0-C02 | C · 김진태 | A, E | 3h | M0-C01 | Test World authoring 규칙 | Control Unit, Spawn Point, Sector marker와 Scene 담당 범위 정의 | Scene/Prefab ownership note |
| M0-D02 | D · 황재동 | A, E | 3h | M0-D01 | M1 Cannon/Power 최소 Data | `TurretDefinition/PowerDefinition` 필드와 유효성 규칙 정의 | Data schema 초안 |
| M0-E02 | E · 조수빈 | A, C, D | 3h | M0-E01 | Enemy/Encounter 최소 Data | `EnemyArchetype`와 Director 최소 필드·성능 목표 정의 | Data schema 초안 |
| M0-B03 | B · 이영빈 | 전체 | 2h | M0-B01, M0-C01, M0-D01, M0-E01 | M1/M2 Playtest 질문 | 이해도·전략 변화·오류 발견 질문 5~7개 | Test checklist |
| M0-L01 | A · 양현석 | 전체 | 2h | M0-001 | Third-party Asset inventory 시작 | 기존 음악·주요 Asset의 출처와 상업 사용 상태 기록 | License 표 |

P1은 P0 완료 가능성을 해치지 않는 범위에서만 수행한다.

## 5. P2 — 여유 시 수행

| ID | Owner | 지원 | 예상 | 선행 | 작업 | 완료 조건 |
| --- | --- | --- | ---: | --- | --- | --- |
| M0-A03 | A · 양현석 | E | 2h | M0-A01 | Architecture Test 자동화 후보 조사 | 금지 의존성·asmdef 검사 후보와 도입 비용 기록 |
| M0-B04 | B · 이영빈 | A | 2h | M0-B02 | Input/접근성 Risk 정리 | 키보드/패드/마우스 기준과 Blocker 기록 |
| M0-C03 | C · 김진태 | D, E | 2h | M0-C01 | 환경 기믹 후보 | Gameplay를 바꾸는 후보 3개와 제외 이유 기록 |
| M0-D03 | D · 황재동 | A | 2h | M0-D01 | Balance 데이터 양식 | 변경값·가설·결과를 비교할 표 작성 |
| M0-E03 | E · 조수빈 | C, D | 2h | M0-E01 | AI 성능 기준 | Target FPS와 동시 Enemy 수 초안 작성 |

## 6. P3 — Sprint 0에서 하지 않음

- 실제 Steamworks/Backend/Netcode 도입
- 완성된 Spaceship/Planet Scene과 Final Art
- 3종 Turret·3종 Enemy·Boss 전체 구현
- Online Co-op, Random Match, SOS, Host Migration
- Planet 2와 대형 Progression

## 7. Sprint 0 통과 기준

- 모든 P0 Task에 담당자·시간·선행 작업·검증 증거가 있다.
- C 김진태와 D 황재동을 포함한 전원의 역할·가용 시간이 확인됐다.
- Compile Error 0, 필수 CI 성공, 진입점 정상 실행이 확인됐다.
- Product Plan 0.2와 M0~M5, Deferred 범위가 팀에 승인됐다.
- M1 Core/App/Profile/Content/Match, Player, World, Defense, Enemy Slice가 각각 Ready다.
- asmdef 의존 방향과 Minimal Test Scene 통합 순서가 승인됐다.
- P3 작업이 별도 Backlog로 분리됐다.

하나라도 충족하지 못하면 M1을 시작하지 않고 미통과 P0만 재계획한다.
