> 보관 사본. [교체 전 main 원문](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/01_planning/current-execution-plan.md)에 당시 검증 근거를 보존한다.

# 현재 실행 계획 — Stage 1 검증

기준일: 2026-11-10 · 상태: 현행 작업·미결정·검증 경계.

## 공통 목표

Stage 1 통합 플레이에서 해금·점유와 터렛 운용을 검증한다. 제품은 1–3 CU 거리 기반 해금, 4–6 송전탑 해금, 7–8 동적 점유를 적용한다. 구간별 맵과 판정 검증을 이어간다. 팀원 5명 전원 크런치 도입에 따라 세부 근무·휴식·복귀 일정은 별도로 조율한다.

## 담당 작업과 인수

| 담당 | 현재 확인할 작업 | 남은 확인 |
| --- | --- | --- |
| 양현석 | Core Match·시간·정지·저장·게임 사이클·Random·출입 계약, Steam 설계와 검증 지원. PR 리뷰 Markdown 템플릿·프롬프트 공유, 조수빈과 소환시설 실행 경계 확인 | Feature 실제 소비·원본 writer, 전체 Mission/저장 복원·Steam 실계정 인수 |
| 이영빈 | Player 수리 입력·전투 연동, 스킬 및 강화안과 현행 수치 정리·공유 | 수리 입력 시 모션·회복 시작과 거리 이탈 시 중단 확인. 협동에서 서로의 이동·수리 행동과 터렛 체력이 양쪽 화면에 일치하는지 확인. 스킬 교체 조건·경험치 배분 세부 검토 |
| 황재동 | 해금 전후 수리·부분 전력 반환·자동 활성화 확인, 송전탑 후속, 터렛 탄환 구조체 관통 수정 확인 | 실제 플레이 인수, 도탄 조건, 성장 트리의 Stage ID와 필드 사용 범위 |
| 조수빈 | 지원형 두 종·회복 시안·스포너 그룹화·Enemy Docs. 스쿼드 소환형·광부·암살자 행동 구체화, 양현석과 소환시설 실행 경계 확인 | 회복·은신 수치·자폭병 피아 판정·광부 편성, 실제 계약 소비 |
| 김진태 | Core와 Weather/Time 연동, Stage 배치 반복·Level Docs | Host Random/출입/공용 시간의 실제 Feature 연결과 플레이 결과 |

스폰 연계는 Core(양현석)가 주도하고 Enemy(조수빈)가 함께 담당한다.

실제 담당·작업 상태는 GitHub Issue/Project, 병합은 PR이 기준이다. 위 표는 체크 완료나 납기를 생성하지 않는다. 11/3 회의 당시의 리뷰 대기·착수 예상은 그 날짜의 보고이며 현재 상태로 재사용하지 않는다.

## 아직 결정·검증할 항목

- 구간별 점유 판정과 실제 맵 시험. 보스 처치 승리·CU 파괴 패배 방향의 상세 조건과 저장·복귀 계약.
- HUD 오른쪽 상단 미션 Alert의 상세 동작과 갱신 조건. 회복 방식, 암살자 셰이더·거리·알파, 소환시설과 기존 Spawn Executor 경계, 자폭병 피아·수량, 광부 편성, 분열 방벽 채택·탄환 API.
- 점령/해금/활성화 구분과 지역별 스포너 영향, 후방 터렛 역할·실제 반복 배치 결과.
- CU/터렛 성장 트리·재화/수치·제공 장소·저장/네트워크. 현재 반환 기본값을 최종 밸런스 확정으로 읽지 않는다.
- 전체 정지·환경·Spawn·checkpoint 원본 writer·원격 Player·실제 협동 인수. 필수 writer 누락 시 전체 저장 성공으로 표시하지 않는다.

터렛 성장 트리 4안은 문서에 추가됐다. 팀 채택·Stage ID·필드 사용 범위는 검토한다. [11/10 회의 근거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/99_archive/2026-11-10/weekly-progress.md)

## 근거와 현재 문서

[11/7 정기회의 근거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/99_archive/2026-11-07/regular-meeting.md) · [PR 리뷰 절차](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/03_team/TEAM_WORKFLOW.md#review와-approval)

[Product Plan](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/00_product/PROJECT_PLAN.md) · [Core API 지도](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/02_architecture/01_core/core-lifecycle-and-api-guide.md) · [Player 입력](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/02_architecture/06_player/player-primary-input-api.md) · [터렛 API](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/02_architecture/07_turrets/api-reference.md) · [검증 실행 규칙](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/04_development/unity-runtime-test-guidelines.md)

[11/3 회의 원문](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/99_archive/2026-11-05/planning/2026-11-03-weekly-progress.md) · [11/5 기반 통합 증거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/99_archive/2026-11-05/planning/2026-11-05-foundation-main-integration.md) · [11/5 Random/출입 제공 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/06e834cc3eefded31b6abdc0f7671e13706f9846/Docs/99_archive/2026-11-05/planning/2026-11-05-core-random-presence-api.md)
