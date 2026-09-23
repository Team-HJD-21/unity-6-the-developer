# Docs 정리 규칙

기준일: 2026-09-23
상태: 팀 공용 문서 배치·유지 가이드

이 문서는 **문서를 어디에 두고 어떻게 연결·관리할지** 정한다. 게임 규칙, 일정, 역할의 결정권은 [문서 목차의 Source of Truth](README.md#source-of-truth) 표를 따른다.

## 1. 폴더별 기준

| 위치 | 넣을 문서 | 예시 |
| --- | --- | --- |
| `product/` | 게임 정체성, 플레이 경험, 제품 범위와 결정 기록 | [Product Plan](product/PROJECT_PLAN.md) |
| `architecture/` | Core 경계, 코드·계약 명명, 레거시 이식 참고 | [System Charter](architecture/system-rearchitecture-charter.md) |
| `planning/` | Milestone, 개발 흐름, **현재 진행 중인** Sprint 계획 | [Sprint 1 PoC](planning/SPRINT_1_STAGE_1_POC.md) |
| `team/` | 담당 영역, 협업 절차, Issue 분류·Review 규칙 | [Team Workflow](team/TEAM_WORKFLOW.md) |
| `archive/` | 종료된 Sprint, 모집 자료 등 현재 기준이 아닌 기록 | [Sprint 0 Backlog](archive/SPRINT_0_BACKLOG.md) |
| `Local/` | 개인 메모와 승인 전 초안. Git에서 무시하며 팀 기준으로 사용하지 않음 | 로컬 전용 |

`Docs/` 최상위에는 탐색용 [README.md](README.md)와 이 정리 규칙을 둔다. 새 문서는 목적에 맞는 하위 폴더에 배치한다.

## 2. 새 문서를 만들 때

1. 먼저 [README.md](README.md)에서 같은 주제를 다루는 기준 문서가 있는지 찾는다. 내용이 겹치면 새 파일보다 기존 기준 문서 수정을 우선한다.
2. 문서 첫머리에 제목, 기준일 또는 수정일, 상태를 적는다. 초안·현행 기준·참고 자료·과거 기록을 구분한다.
3. 파일명은 내용을 알 수 있게 짓고, 새 문서는 영문 `kebab-case.md`를 기본으로 한다. 이미 사용 중인 대문자 파일명은 정리만을 위해 변경하지 않는다.
4. 다른 문서를 인용할 때는 파일명만 적지 말고 상대경로 Markdown 링크를 사용한다. `Docs/` 최상위에서의 예: [역할 기준](team/TEAM_ROLE_OWNERSHIP.md). 하위 폴더의 문서에서는 필요에 따라 `../`로 상위 폴더를 가리킨다.
5. 팀원이 찾아야 할 새 문서는 [README.md](README.md)의 폴더 안내 또는 읽는 순서에 추가한다.

## 3. 내용의 기준을 유지할 때

- 같은 결정은 한 기준 문서에서 관리하고, 다른 문서는 그 문서로 연결한다. 충돌 시 [Source of Truth 표](README.md#source-of-truth)의 기준 문서가 우선한다.
- 게임 범위·Core 계약·역할·Milestone을 바꾸면 해당 기준 문서와 영향을 받는 계획·Backlog를 함께 갱신한다. 자세한 협업 절차는 [Team Workflow](team/TEAM_WORKFLOW.md)를 따른다.
- `archive/` 문서를 다시 현재 기준처럼 고치지 않는다. 필요하면 현행 기준 문서에 결정을 반영하고, 과거 문서에는 현재 문서로 가는 안내만 둔다.
- `Local/`은 원격에 올라가지 않는다. 팀에 공유할 내용은 검토한 뒤 적절한 폴더로 옮기고 README에서 연결한다. 비밀키·토큰은 로컬 문서에도 기록하지 않는다.

## 4. 문서를 이동하거나 보관할 때

- 원본 내용을 보존하면서 파일을 이동한다. 이름과 경로를 동시에 바꿔야 한다면 한 번에 한 주제씩 처리한다.
- 문서 내부 링크, [Docs 목차](README.md), 저장소 최상단 README 등 옛 경로를 참조하는 곳을 같은 변경에서 갱신한다. 외부에 공유한 직접 링크도 확인한다.
- `archive/`로 옮긴 문서에는 과거 기록임을 표시하고, 있다면 현재 실행 문서로 연결한다. 별도 요청 없이 삭제하지 않는다.
- 변경 후 상대경로 링크가 실제 파일을 가리키는지 검사한다. Git에서 이동이 삭제·신규 파일로 표시될 수 있으므로 커밋 전 변경 범위를 확인한다.

이 규칙은 폴더와 링크의 관리 기준이며, 기존 문서의 제품·기술 결정을 새로 정의하지 않는다.
