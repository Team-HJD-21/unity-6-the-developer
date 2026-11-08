# 현재 실행 계획 — Stage 1 검증

기준일: 2026-11-07 · 상태: 현행 작업·미결정·검증 경계. 새 Sprint 번호·확정 납기·완료 선언 아님.

## 공통 목표

전제 1(동적 전선)과 전제 2(지역 해금)의 재미를 실제 통합 플레이에서 비교한다. 기존 Sprint 1 문서는 당시 time-box 기록으로 보관한다. 11월 안에 전제를 선택한다는 회의 목표와 실제 Gate 통과를 구분한다. 팀원 5명 전원 크런치 도입은 확정된 운영 결정이며 세부 근무·휴식·복귀 일정은 별도로 조율한다.

## 담당 작업과 인수

| 담당 | 현재 확인할 작업 | 남은 확인 |
| --- | --- | --- |
| 양현석 | Core Match·시간·정지·저장·게임 사이클·Random·출입 계약, Steam 설계와 검증 지원. PR 리뷰 Markdown 템플릿·프롬프트 공유, 조수빈과 소환시설 실행 경계 확인 | Feature 실제 소비·원본 writer, 전체 Mission/저장 복원·Steam 실계정 인수 |
| 이영빈 | Player 수리 입력, 피해 수신 공통화 협의, LMB/설치/Space 대시와 무기 HUD | 실제 입력·모션/거리, 원격 actor·상태 복제와 전체 협동 검증 |
| 황재동 | 파괴/손상 수리 대상, 부분 전력 반환·자동 활성화 순서와 타기팅 계약. 은신 셰이더 추적과 Codex Review 흐름 안내 시험 | 실제 Player 입력·다중 클라이언트 인수. 성장 트리는 검토 초안 |
| 조수빈 | 지원형 두 종·회복 시안·스포너 그룹화·Enemy Docs. 스쿼드 소환형·광부·암살자 행동 구체화, 양현석과 소환시설 실행 경계 확인 | 회복·은신 수치·자폭병 피아 판정·광부 편성, 실제 계약 소비 |
| 김진태 | Core·Weather/Time·Enemy 스폰 연계, Stage 배치 반복·Level Docs | Host Random/출입/공용 시간의 실제 Feature 연결과 플레이 결과 |

실제 담당·작업 상태는 GitHub Issue/Project, 병합은 PR이 기준이다. 위 표는 체크 완료나 납기를 생성하지 않는다. 11/3 회의 당시의 리뷰 대기·착수 예상은 그 날짜의 보고이며 현재 상태로 재사용하지 않는다.

## 아직 결정·검증할 항목

- 동적 전선/지역 해금 최종 선택과 실험 결과. 보스 처치 승리·CU 파괴 패배 방향의 상세 조건·저장/복귀 계약. 과거 PoC의 승리 조건 미정과 최종 제품 방향을 혼합하지 않는다.
- HUD 오른쪽 상단 미션 Alert의 상세 동작과 갱신 조건. 회복 방식, 암살자 셰이더·거리·알파, 소환시설과 기존 Spawn Executor 경계, 자폭병 피아·수량, 광부 편성, 분열 방벽 채택·탄환 API.
- 점령/해금/활성화 구분과 지역별 스포너 영향, 후방 터렛 역할·실제 반복 배치 결과.
- CU/터렛 성장 트리·재화/수치·제공 장소·저장/네트워크. 현재 반환 기본값을 최종 밸런스 확정으로 읽지 않는다.
- 전체 정지·환경·Spawn·checkpoint 원본 writer·원격 Player·실제 협동 인수. 필수 writer 누락 시 전체 저장 성공으로 표시하지 않는다.

터렛 성장 트리는 안을 검토한 뒤 선택한다. 추가 터렛과 Stage 개방을 포함한 4안은 황재동의 제안이며 팀 채택·Stage 수·노드 값은 미정이다.

## 근거와 현재 문서

[11/7 정기회의 근거](../archive/2026-11-07/regular-meeting.md) · [PR 리뷰 절차](../team/TEAM_WORKFLOW.md#review와-approval)

[Product Plan](../product/PROJECT_PLAN.md) · [Core API 지도](../architecture/core-lifecycle-and-api-guide.md) · [Player 입력](../architecture/player-primary-input-api.md) · [터렛 API](../architecture/turrets/api-reference.md) · [검증 실행 규칙](../development/unity-runtime-test-guidelines.md)

[11/3 회의 원문](../archive/2026-11-05/planning/2026-11-03-weekly-progress.md) · [11/5 기반 통합 증거](../archive/2026-11-05/planning/2026-11-05-foundation-main-integration.md) · [11/5 Random/출입 제공 기록](../archive/2026-11-05/planning/2026-11-05-core-random-presence-api.md)
