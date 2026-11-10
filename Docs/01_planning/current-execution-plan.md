# 현재 실행 계획 — Stage 1 검증

기준일: 2026-11-10 · 상태: 현행 작업·미결정·검증 경계. 새 Sprint 번호·확정 납기·완료 선언 아님.

## 공통 목표

캠페인 구간별 지역 규칙은 [Product Plan](../00_product/PROJECT_PLAN.md)을 따른다. Stage 1은 CU 거리 기반 해금부터 실제 통합 플레이로 검증한다. 기존 Sprint 1 문서는 당시 time-box 기록으로 보관한다. 팀원 5명 전원 크런치 도입은 확정된 운영 결정이며 세부 근무·휴식·복귀 일정은 별도로 조율한다.

## 담당 작업과 인수

| 담당 | 현재 확인할 작업 | 남은 확인 |
| --- | --- | --- |
| 양현석 | Core Match·시간·정지·저장·게임 사이클·Random·출입 계약, Steam 설계와 검증 지원. PR 리뷰 Markdown 템플릿·프롬프트 공유, 조수빈과 소환시설 실행 경계 확인 | Feature 실제 소비·원본 writer, 전체 Mission/저장 복원·Steam 실계정 인수 |
| 이영빈 | Player 수리 입력, 피해 수신 공통화 협의, LMB/설치/Space 대시와 무기 HUD | 수리 입력 시 모션과 체력 회복이 시작되고, 대상이 수리 범위를 벗어나면 중단되는지 확인. 호스트와 참가자 화면에서 서로의 이동·행동이 보이고 수리 후 터렛 체력이 일치하는지 확인 |
| 황재동 | 파괴/손상 수리 대상, 부분 전력 반환·자동 활성화 순서와 타기팅 계약. 은신 셰이더 추적과 Codex Review 흐름 안내 시험 | 실제 Player 입력·다중 클라이언트 인수. 성장 트리는 검토 초안 |
| 조수빈 | 지원형 두 종·회복 시안·스포너 그룹화·Enemy Docs. 스쿼드 소환형·광부·암살자 행동 구체화, 양현석과 소환시설 실행 경계 확인 | 회복·은신 수치·자폭병 피아 판정·광부 편성, 실제 계약 소비 |
| 김진태 | Core와 Weather/Time 연동, Stage 배치 반복·Level Docs | Host Random/출입/공용 시간의 실제 Feature 연결과 플레이 결과 |

스폰 연계는 Core(양현석)가 주도하고 Enemy(조수빈)가 함께 담당한다.

실제 담당·작업 상태는 GitHub Issue/Project, 병합은 PR이 기준이다. 위 표는 체크 완료나 납기를 생성하지 않는다. 11/3 회의 당시의 리뷰 대기·착수 예상은 그 날짜의 보고이며 현재 상태로 재사용하지 않는다.

## 아직 결정·검증할 항목

- 스테이지별 점유·해금·상실 판정 및 실제 플레이 결과. 보스 처치 승리·CU 파괴 패배 방향의 상세 조건·저장/복귀 계약. 과거 PoC의 승리 조건 미정과 최종 제품 방향을 혼합하지 않는다.
- HUD 오른쪽 상단 미션 Alert의 상세 동작과 갱신 조건. 회복 방식, 암살자 셰이더·거리·알파, 소환시설과 기존 Spawn Executor 경계, 자폭병 피아·수량, 광부 편성, 분열 방벽 채택·탄환 API.
- 점령/해금/활성화 구분과 지역별 스포너 영향, 후방 터렛 역할·실제 반복 배치 결과.
- CU/터렛 성장 트리·재화/수치·제공 장소·저장/네트워크. 현재 반환 기본값을 최종 밸런스 확정으로 읽지 않는다.
- 전체 정지·환경·Spawn·checkpoint 원본 writer·원격 Player·실제 협동 인수. 필수 writer 누락 시 전체 저장 성공으로 표시하지 않는다.

황재동은 터렛·CU 성장 트리 4안을 추가했다고 보고했다. Stage ID 연결·필드 사용·노드 수치 및 검증은 후속이다.

## 11월 10일 후속 확인

- 이영빈이 현재 스킬의 쿨타임·공격력과 강화안을 정리한다. 교체 조건·설치물 유지와 성장 세부는 후속 설계다.
- 처치 주체 식별, 멀티 경험치 배분, 프로필 연결을 확인한다.
- 터렛 탄환의 구조체 관통을 수정하고, 도탄 대상과 조건을 별도로 정해 씬에서 검증한다.

## 근거와 현재 문서

[11/10 주간 점검 근거](../99_archive/2026-11-10/weekly-progress.md) · [교체 전 실행 계획](../99_archive/2026-11-10/planning/current-execution-plan.md) · [교체 전 Product Plan](../99_archive/2026-11-10/product/PROJECT_PLAN.md)

[11/7 정기회의 근거](../99_archive/2026-11-07/regular-meeting.md) · [PR 리뷰 절차](../03_team/TEAM_WORKFLOW.md#review와-approval)

[Product Plan](../00_product/PROJECT_PLAN.md) · [Core API 지도](../02_architecture/01_core/core-lifecycle-and-api-guide.md) · [Player 입력](../02_architecture/06_player/player-primary-input-api.md) · [터렛 API](../02_architecture/07_turrets/api-reference.md) · [검증 실행 규칙](../04_development/unity-runtime-test-guidelines.md)

[11/3 회의 원문](../99_archive/2026-11-05/planning/2026-11-03-weekly-progress.md) · [11/5 기반 통합 증거](../99_archive/2026-11-05/planning/2026-11-05-foundation-main-integration.md) · [11/5 Random/출입 제공 기록](../99_archive/2026-11-05/planning/2026-11-05-core-random-presence-api.md)
