# Team Workflow — 팀 협업 규칙

기준일: 2026-09-12
상태: Sprint·Git·Review·Ready/Done의 Source of Truth

역할 경계는 [TEAM_ROLE_OWNERSHIP.md](TEAM_ROLE_OWNERSHIP.md), Milestone별 Priority 업무는 [milestones-and-core-design.md](../planning/milestones-and-core-design.md)를 따른다.

## 1. PL 운영 원칙

A — 양현석은 `Player-Coach` 형태의 PL로, 방향을 결정하면서 Core를 직접 구현한다.

- 현재 Milestone과 Sprint Goal을 한 문장으로 유지한다.
- 팀원당 동시에 진행하는 핵심 Task를 1개, 작은 Task를 1개 이하로 제한한다.
- Task 착수 전에 범위·선행 작업·완료 조건·검증 방법을 확인한다.
- Blocker와 역할 간 in-between 문제를 해결하고 필요한 결정을 기록한다.
- 자신의 Core 변경도 영향 범위가 크면 다른 Owner의 Review를 받는다.
- C 김진태와 D 황재동의 업무를 A가 자동으로 떠안지 않고, 가용 시간에 맞춰 범위 또는 일정을 조정한다.
- Milestone Exit Criteria 전에는 다음 단계의 Content 대량 제작을 허용하지 않는다.

## 2. Priority

| Priority | 의미 | 착수 규칙 |
| --- | --- | --- |
| P0 | 없으면 Milestone을 통과할 수 없는 Gate/Blocker | 먼저 처리하며 다른 Milestone P0보다 우선 |
| P1 | 현재 Milestone의 핵심 플레이 결과 | P0 기반이 준비된 뒤 진행 |
| P2 | 품질·사용성·성능·도구 개선 | P0/P1 완료 가능성이 확보된 뒤 진행 |
| P3 | 현재 범위 밖 아이디어·Deferred | Product Backlog에만 기록, 별도 승인 전 착수 금지 |

Priority는 중요도의 감정적 표현이 아니라 현재 Milestone과의 관계다. P3를 올리려면 같은 크기의 작업을 빼거나 일정을 다시 산정한다.

## 3. Backlog 구조

```text
Milestone: M2 Single-player Vertical Slice
└─ Epic: Planet 1 Expansion
   └─ Feature: 제한 전력 Turret 선택
      └─ Task: Laser를 Power Command에 연결
         └─ Bug: 비활성화 후 Power가 두 번 반환됨
```

### Task 필수 항목

- Milestone과 Priority
- Player 가치 또는 해결할 문제
- 주 담당 한 명과 지원자
- 예상 시간
- 선행 작업과 Blocker
- 포함/제외 범위
- 완료 조건
- 확인 방법과 남길 증거
- 영향받는 Scene, Prefab, asmdef 또는 Data

이 항목이 없으면 `Ready`가 아니며 Sprint에 넣지 않는다.

## 4. Sprint 운영

- Sprint 0만 1주, 이후 기본 2주
- 계획에는 개인 가용 시간의 70%만 배정
- 주 1회 같은 통합 Build로 Playtest
- 각 Sprint마다 Release Captain을 한 명 지정하되, Feature 품질 책임은 각 Owner에게 유지

### Sprint Planning

1. 최신 `main` Build와 CI를 확인한다.
2. 현재 Milestone의 가장 큰 Risk 하나를 고른다.
3. Sprint Goal을 한 문장으로 정한다.
4. P0와 Goal에 직접 연결된 P1만 먼저 선택한다.
5. 각 Task가 Definition of Ready를 충족하는지 확인한다.
6. C/D Task는 각각 김진태·황재동의 가용 시간과 지원자가 기록돼야 `Ready`로 전환한다.

### 작업일 공유

```text
Done: 완료한 결과와 증거
Next: 다음 작업
Blocker: 필요한 결정·사람·선행 작업
```

### 주간 Build/Playtest

- 개인 Editor 화면이 아니라 같은 Build를 실행한다.
- 최소 한 명은 자신이 만들지 않은 기능을 Test한다.
- Bug에 재현 순서, 기대/실제 결과, Build/Commit을 기록한다.
- 다음 Build에서 반드시 고칠 항목은 최대 3개로 제한한다.

## 5. Task 상태

| 상태 | 의미 | 다음 상태 조건 |
| --- | --- | --- |
| Idea | 검토 전 제안 | 문제와 기대 효과 기록 |
| Backlog | 아직 약속하지 않은 작업 | Milestone/Priority 지정 |
| Ready | 착수 정보가 준비됨 | Sprint에서 선택 |
| In Progress | 구현·제작 중 | Local Test와 Self-review 완료 |
| Review | PR 또는 산출물 검토 중 | 필요한 Review 의견 해결, CI 성공 |
| Integrated | `main`에서 연결됨 | 통합 Build 성공 |
| Verify | Playtest/완료 조건 검증 중 | 증거와 조건 충족 |
| Done | 검증까지 완료 | 없음 |
| Blocked | 결정·담당·선행 작업이 없음 | 해결 Owner와 목표일 지정 |

## 6. Git Workflow

### Branch

- `main`: 필수 CI가 성공하고 Release/Test 진입점이 실행 가능한 상태
- `feature/<issue>-<name>`: 기능
- `fix/<issue>-<name>`: Bug
- `content/<issue>-<name>`: Scene, Prefab, Definition, Art
- `chore/<issue>-<name>`: 설정, 문서, 자동화

공유 `main`에는 force push하지 않는다. 잘못 Merge한 변경은 Revert PR로 복구한다.

### Pull Request

- 하나의 PR은 하나의 목적만 갖는다.
- Summary는 짧은 영어, Description은 주로 한국어로 작성한다.
- 변경 이유, 포함/제외 범위, 확인 방법, 영향 Scene/Module을 적는다.
- Gameplay/UI 변경은 Screenshot 또는 짧은 Video를 권장한다.
- 필수 CI가 실패하면 Merge하지 않는다.

### Review와 Approval

- GitHub Required Approval 수: `0`
- 작은 독립 변경: 작성자 Self-review + CI 성공으로 Merge 가능
- 다음 변경은 관련 Owner에게 Review를 요청하고 의견을 해결한 뒤 Merge한다.
  - Core/공용 계약 또는 asmdef 의존성
  - Save/Profile/Economy schema
  - 공용 Scene/Prefab 구조
  - Build/CI/ProjectSettings
  - Network/Authority/Snapshot
- Review 요청은 GitHub의 강제 Approval 규칙과 다르다. 댓글이 없다는 이유로 모든 작은 PR을 막지 않는다.
- Merge 책임은 PR 작성자와 해당 Feature Owner에게 있다. E가 모든 PR을 최종 승인하지 않는다.

## 7. Unity Asset 규칙

- Version Control Mode: Visible Meta Files
- Asset Serialization Mode: Force Text
- Asset과 `.meta`를 항상 함께 Commit
- 이동·Rename은 Unity Editor 안에서 수행
- `Library`, `Temp`, `Logs`, `obj`, 사용자 IDE 파일은 Commit 금지
- 같은 `.unity` Scene 동시 수정 금지
- 반복 배치물은 Prefab, Expansion은 Sector Prefab/Additive Scene 우선
- 대형 Binary는 Git LFS 필요성을 확인

## 8. Definition of Ready

- 현재 Milestone/Priority와 연결됨
- 문제·가치·포함/제외 범위를 설명할 수 있음
- 주 담당, 지원, 예상 시간, 선행 작업이 있음
- 수정할 Scene/Prefab/Module/Data가 알려져 있음
- 완료 조건과 검증 증거가 구체적임
- 필요한 기획·Definition·Interface가 준비됨

## 9. Definition of Done

- Task 완료 조건 충족
- Compile Error와 새 Console Error 0
- Missing Script, Broken Prefab, Missing `.meta` 0
- 관련 Scene/Test에서 직접 검증
- 기존 Core Flow 회귀 확인
- 필수 CI 성공
- 영향이 큰 변경은 요청된 Review 의견 해결
- 필요한 Docs/Definition/Decision Log 갱신
- Issue/PR에 Build, Test, Screenshot, Log 중 필요한 증거 첨부

## 10. 변경 관리

```text
변경 제안:
해결할 Player 문제:
현재 Milestone/Priority:
추가 작업:
제거·연기할 작업:
일정·Owner 영향:
검증 방법:
결정 담당 / 날짜:
```

Core Rule·제품 범위는 [PROJECT_PLAN.md](../product/PROJECT_PLAN.md), Milestone/Exit Criteria는 Core Design, 역할은 Team Role Ownership에 기록한다. 회의·Discord·Notion은 결정 근거일 수 있지만 Source of Truth 변경을 대신하지 않는다.

## 11. PL 주간 점검

- Sprint Goal이 한 문장인가?
- 모든 진행 Task가 Ready였는가?
- 사람별 핵심 Task가 1개 이하인가?
- C/D Task에 김진태·황재동의 가용 시간과 지원자가 기록됐는가?
- `main` 필수 CI와 통합 Build가 정상인가?
- 막힌 결정이 이틀 넘게 방치되지 않았는가?
- P3가 몰래 P0/P1 범위에 들어오지 않았는가?
- 다음 Milestone이 아니라 현재 Exit Criteria를 먼저 보고 있는가?
