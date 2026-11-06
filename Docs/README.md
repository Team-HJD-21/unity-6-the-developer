# The Developer 문서 안내

기준일: 2026-11-05 · 공식 열람 창구: [docs.teamhjd.com](https://docs.teamhjd.com/docs/projects/the-developer)

GitHub main Docs에서 작성·승인하고 사이트에서 읽는다. Notion은 목표·담당 작업·최근 합의와 링크를 제공한다. 현재 사이트는 공개 기술 문서 범위이며 내부 기획의 사이트 반영은 비공개 소스·배포 경계가 마련된 뒤 진행한다. 접근 제한된 도메인과 공개 저장소의 권한은 별개다.

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
| 날씨 변경·예고·화면 효과 | [날씨 시스템 설계와 API](architecture/weather-system-guide.md) |
| 담당·진행 상태 | GitHub Issue/Project. [역할](team/TEAM_ROLE_OWNERSHIP.md)은 책임 기준 |
| 실제 동작과 인수 | 해당 코드 버전과 실행/PL 인수 증거. 계획으로 완료를 추정하지 않음 |

## 찾기

- [공식 문서 검색](https://docs.teamhjd.com/docs/projects/the-developer/api-search): 선택한 프로젝트의 현행 문서만 검색한다.
- [공식 아카이브 검색](https://docs.teamhjd.com/docs/projects/the-developer/archive-search): 사이트에 게시된 해당 프로젝트의 과거 문서만 별도로 검색한다.
- [정리 규칙](DOCS_ORGANIZATION_RULES.md) · [개발 흐름](planning/DEVELOPMENT_FLOW.md) · [기술 명명](architecture/naming-and-architecture-conventions.md) · [에셋 구조](architecture/asset-layout-and-migration.md)
- [도구 사용법](tooling/game-debugger-guide.md) · [Unity 검사 규칙](development/unity-runtime-test-guidelines.md)
- [교체·보관 목차](archive/2026-11-05/index.md). 과거 문서의 당시 경로·테스트·미정은 현재 계약이 아니다.

현행 문서를 교체하기 전 버전은 보존하고 기존 링크는 보관 안내로 유지한다. 팀원 이름을 기술 식별자의 부분 문자열에 치환하지 않는다.
