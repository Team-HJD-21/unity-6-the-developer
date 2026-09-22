# Issue Label Guide — GitHub Issue 분류 규칙

기준일: 2026-09-21
상태: 팀 공용 운영 가이드

[문서 목차](../README.md)

이 문서는 `The Developer V2`의 GitHub Issue에 Type, Priority, Label을 일관되게 지정하기 위한 기준이다. Sprint, Ready/Done, Review 규칙은 [TEAM_WORKFLOW.md](TEAM_WORKFLOW.md)를 따른다.

## 1. Type, Field, Label의 역할

| 구분 | 용도 | 예시 | 기본 규칙 |
| --- | --- | --- | --- |
| Issue Type | 작업의 목적 | `Feature`, `Task`, `Bug` | 정확히 1개 선택 |
| Priority Field | 현재 Milestone에서의 우선순위 | `P0`, `P1`, `P2`, `P3` | Label로 중복 생성하지 않음 |
| Milestone | 작업이 속한 개발 단계 | `M1 New Core`, `M2 Vertical Slice` | 현재 목표와 연결 |
| Assignee | 작업의 주 담당자 | 황재동 | 원칙적으로 1명 지정 |
| Label | 작업 영역·성격·예외 상태 | `domain:turret`, `work:refactor` | 아래 조합 규칙 사용 |

### Issue Type 선택

- `Feature`: 플레이어나 개발 흐름에 새로운 기능·시스템을 추가한다.
- `Task`: Feature를 완성하기 위한 구체적인 구현·설계·정리 작업이다.
- `Bug`: 기존의 의도나 완료 조건과 다르게 작동하는 문제다.

`docs`, `assets`, `refactor`, `ci`는 Issue Type이 아니라 작업 성격을 나타내므로 `work:*` Label을 사용한다.

## 2. Label 조합 규칙

Issue에는 다음 순서로 Label을 선택한다.

1. 주 작업 영역을 나타내는 `domain:*`을 1개 선택한다.
2. 작업 성격을 추가로 설명할 필요가 있으면 `work:*`을 선택한다.
3. 외부 결정이나 선행 작업 때문에 진행할 수 없을 때만 `status:blocked`를 추가한다.

```text
domain:*  1개 권장
work:*    0개 이상
status:*  필요한 동안만 사용
```

실제로 두 영역의 코드를 함께 변경하는 Issue라면 `domain:*`을 2개까지 사용할 수 있다. 단순히 영향을 받을 가능성이 있다는 이유로 여러 Domain을 붙이지 않고, 주 담당 영역을 우선한다.

## 3. Domain Labels

| Label | 사용할 때 |
| --- | --- |
| `domain:core` | 공용 Architecture, Lifecycle, Data Contract, Shared System |
| `domain:economy` | 성장, 보상, 자원, Research 및 Economy |
| `domain:enemy` | Monster, Enemy AI, 행동 및 Enemy Data |
| `domain:map` | Planet, Level Design, Expansion, Environment |
| `domain:network` | Multiplayer, Authority, Replication, Synchronization |
| `domain:player` | Player Character, 조작, 이동, Player Combat |
| `domain:spaceship` | Spaceship 내부, Mission 준비 및 Hub 기능 |
| `domain:turret` | Turret, Power, Defense, Turret Upgrade 및 Balance |
| `domain:ui` | UI, HUD, Menu 및 화면 흐름 |
| `domain:wave` | Wave 진행, Spawn, Encounter 구성 및 Director |

### Domain 선택 예시

- `TurretManager` 구조 개선 → `domain:turret`
- Monster가 Turret 정보를 조회하는 기능 → 주 구현이 Enemy AI라면 `domain:enemy`, Turret 계약도 함께 변경하면 `domain:turret` 추가
- Planet 전투 HUD → 주 작업이 화면이면 `domain:ui`, Map 배치까지 함께 수정하면 `domain:map` 추가
- 여러 시스템이 사용하는 Event Bus → `domain:core`

## 4. Work Labels

| Label | 사용할 때 |
| --- | --- |
| `work:assets` | Sprite, Tile, Audio, Font, Animation, VFX 등 Asset 추가·수정 |
| `work:ci` | GitHub Actions, 자동 검사, Build Pipeline 및 CI 설정 |
| `work:design` | 게임 규칙, 데이터 규격, 시스템 흐름 및 Level Design 문서화 |
| `work:docs` | 프로젝트 문서, 가이드, README 추가·수정 |
| `work:refactor` | 동작을 의도적으로 바꾸지 않고 코드 구조와 유지보수성을 개선 |

`work:*`은 필요할 때만 사용한다. 일반적인 기능 구현에 별도의 `work:code` Label은 붙이지 않는다.

## 5. Blocked Label

`status:blocked`는 다음 중 하나 때문에 담당자가 스스로 작업을 계속할 수 없을 때 사용한다.

- 선행 Issue 또는 PR이 완료되지 않음
- 필요한 기획 결정이나 Interface가 확정되지 않음
- 다른 Owner의 Asset, Data 또는 검토가 필요함
- 외부 SDK, 계정 또는 권한 문제로 진행할 수 없음

Label을 붙일 때 Issue 댓글이나 본문에 아래 내용을 함께 기록한다.

```text
Blocked by: #이슈번호 또는 필요한 결정
Owner: 해결 담당자
Next check: 다시 확인할 날짜
```

원인이 해결되면 즉시 `status:blocked`를 제거한다. 단순히 우선순위가 낮거나 아직 착수하지 않은 Issue에는 사용하지 않는다.

## 6. 작성 예시

### Turret 리팩터링

```text
Type: Task
Priority: P1
Labels: domain:turret, work:refactor
Milestone: 현재 개발 Milestone
Assignee: 황재동
```

### CI의 `.meta` 검사 오류

```text
Type: Bug
Priority: P0
Labels: domain:core, work:ci
Milestone: 현재 개발 Milestone
Assignee: CI 수정 담당자
```

### Spaceship Mission 선택 화면

```text
Type: Feature
Priority: P1
Labels: domain:spaceship, domain:ui
Milestone: M2 Single-player Vertical Slice
Assignee: Feature Owner
```

### Turret Power 규칙 문서화

```text
Type: Task
Priority: P2
Labels: domain:turret, work:docs, work:design
Milestone: 현재 개발 Milestone
Assignee: Defense Owner
```

## 7. 피해야 할 사용법

- `bug`, `feature`, `task` Label을 별도로 만들어 Issue Type과 중복하지 않는다.
- `P0`, `high`, `urgent` 같은 Priority Label을 만들지 않는다.
- 담당자 이름이나 A~E 역할을 Label로 만들지 않는다.
- Sprint와 Milestone 이름을 Label로 중복 관리하지 않는다.
- PR Merge 자체를 작업으로 분류하는 `type:merge`를 사용하지 않는다.
- 관련 있어 보이는 Domain을 모두 붙이지 않는다.
- 막히지 않은 미착수 Issue에 `status:blocked`를 붙이지 않는다.

## 8. 새 Label 추가 기준

새 Label은 다음 조건을 모두 확인한 뒤 추가한다.

1. 기존 Type, Field, Milestone 또는 Label로 표현할 수 없다.
2. 한 번만 사용할 임시 분류가 아니라 반복해서 검색·필터링할 필요가 있다.
3. 이름이 기존 `domain:*`, `work:*`, `status:*` 체계에 들어맞는다.
4. 팀이 의미와 제거 조건을 한 문장으로 설명할 수 있다.

새 Label을 즉석에서 만들기보다 Issue 댓글이나 회의에서 먼저 제안한다. 채택했다면 이 문서의 표도 함께 갱신한다.

## 9. Issue 생성 전 확인

- [ ] `Feature`, `Task`, `Bug` 중 하나를 선택했는가?
- [ ] Milestone과 Priority를 지정했는가?
- [ ] 주 담당자 한 명을 지정했는가?
- [ ] 주 작업 영역의 `domain:*` Label을 선택했는가?
- [ ] 필요한 경우에만 `work:*` Label을 추가했는가?
- [ ] Blocked라면 원인, 해결 담당자, 재확인 날짜를 기록했는가?
- [ ] 완료 조건과 Test Evidence가 구체적인가?

현재 사용 가능한 Label 목록은 [GitHub Repository Labels](https://github.com/Team-HJD-21/unity-6-the-developer/labels)에서 확인한다.
