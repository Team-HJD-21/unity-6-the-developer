> 보관 사본 · [교체 전 원문](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/current-execution-plan.md) · [현행 문서](../../../planning/current-execution-plan.md)
>
> 당시 결정과 검증 설명은 원문 시점에 해당한다. 상대 링크는 같은 커밋의 원본을 가리킨다.

# 현재 실행 계획 — Stage 1 검증

기준일: 2026-11-05 · 상태: 현행 작업·미결정·검증 경계. 새 Sprint 번호·확정 납기·완료 선언 아님.

## 공통 목표

전제 1(동적 전선)과 전제 2(지역 해금)의 재미를 실제 통합 플레이에서 비교한다. 기존 Sprint 1 문서는 당시 time-box 기록으로 보관한다. 11월 안에 전제를 선택한다는 회의 목표와 실제 Gate 통과를 구분한다. 팀원 5명 전원 크런치 도입은 확정된 운영 결정이며 세부 근무·휴식·복귀 일정은 별도로 조율한다.

## 담당 작업과 인수

| 담당 | 현재 확인할 작업 | 남은 확인 |
| --- | --- | --- |
| 양현석 | Core Match·시간·정지·저장·게임 사이클·Random·출입 계약, Steam 설계와 검증 지원 | Feature 실제 소비·원본 writer, 실제 전체 Mission/저장 복원·Steam 실계정 인수 |
| 이영빈 | Player 수리 입력, 피해 수신 공통화 협의, LMB/설치/Space 대시와 무기 HUD | 실제 입력·모션/거리, 원격 actor·상태 복제와 전체 협동 검증 |
| 황재동 | 파괴/손상 수리 대상, 부분 전력 반환·자동 활성화 순서와 타기팅 계약 | main 반영과 실제 Player 입력·다중 클라이언트 인수 구분. 성장 트리는 검토 초안 |
| 조수빈 | 지원형 소환/회복 두 종, 회복 방식 시안, 타기팅 요구·스포너 그룹화·Enemy Docs | 안전 위치·치료 방식·암살자 반투명/탐지 강화 정책, 실제 계약 소비 |
| 김진태 | Core·Weather/Time·Enemy 스폰 연계, Stage 배치 반복·Level Docs | Host Random/출입/공용 시간의 실제 Feature 연결과 플레이 결과 |

실제 담당·작업 상태는 GitHub Issue/Project, 병합은 PR이 기준이다. 위 표는 체크 완료나 납기를 생성하지 않는다. 11/3 회의 당시의 리뷰 대기·착수 예상은 그 날짜의 보고이며 현재 상태로 재사용하지 않는다.

## 아직 결정·검증할 항목

- 동적 전선/지역 해금 최종 선택과 실험 결과. 보스 처치 승리·CU 파괴 패배 방향의 상세 조건·저장/복귀 계약. 과거 PoC의 승리 조건 미정과 최종 제품 방향을 혼합하지 않는다.
- 지원형의 안전 위치·회복 방식, 암살자 표현과 탐지 업그레이드, 분열 방벽의 정식 채택·탄환 API.
- 점령/해금/활성화 구분과 지역별 스포너 영향, 후방 터렛 역할·실제 반복 배치 결과.
- CU/터렛 성장 트리·재화/수치·제공 장소·저장/네트워크. 현재 반환 기본값을 최종 밸런스 확정으로 읽지 않는다.
- 전체 정지·환경·Spawn·checkpoint 원본 writer·원격 Player·실제 협동 인수. 필수 writer 누락 시 전체 저장 성공으로 표시하지 않는다.

## 근거와 현재 문서

[Product Plan](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/product/PROJECT_PLAN.md) · [Core API 지도](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/architecture/core-lifecycle-and-api-guide.md) · [Player 입력](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/architecture/player-primary-input-api.md) · [터렛 API](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/architecture/turrets/api-reference.md) · [검증 실행 규칙](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/development/unity-runtime-test-guidelines.md)

[11/3 회의 원문](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/archive/2026-11-05/planning/2026-11-03-weekly-progress.md) · [11/5 기반 통합 증거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/archive/2026-11-05/planning/2026-11-05-foundation-main-integration.md) · [11/5 Random/출입 제공 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/archive/2026-11-05/planning/2026-11-05-core-random-presence-api.md)
