# Core 실제 Feature 통합 배치

> 최신 보완 상태: [요구·Issue 대응](2026-11-05-core-requirements-and-issue-map.md), [C0–C5 배치 기록](2026-11-05-core-completion-batches.md). 이 문서의 당시 누락/검사 수치는 경위로 보존하며 이후 보완 결과와 구분합니다.


> **완료 판정 정정(2026-11-05): 수동 확인만 남은 상태가 아닙니다. Player 감속 수신, 원거리 적 발사 진행 저장, Animation Event 생성 경계, 새 Match 복원 성공 검증 및 문서/stash 최종 대조가 남았습니다. [완료 재검토](2026-11-05-core-completion-reaudit.md)를 현재 상태로 우선 읽습니다.**


기준: 2026-11-05 / main `1a03e9a91` (#613·615·616·617 포함).
작업 브랜치: `codex/core-feature-integration-20261105`.

## 목표와 경계

이미 제공한 Core를 Player/UI·FieldTurret/CU·Enemy/Encounter의 실제 원본에 연결한다. 기존 스프린트 #598~602, #442/#444, #495/#496/#528, #555~560을 같은 구현으로 해결하고, 부팅·Mission·결과/귀환과 명시 저장/복원까지 이어간다. Issue 종료는 실제 완료 조건과 검증을 대조한 뒤 판단한다.

- 새 거대 Manager나 공통 부모 클래스를 만들지 않는다. 기존 scope와 작은 명시적 API를 우선한다.
- main의 최근 입력 우선순위, 터렛 자동 활성화 복구(#617), Enemy Variant/Runtime 프리팹(#616)을 보존한다. stash 전체를 적용하지 않는다.
- 김진태 전달 문서의 1·2번인 **CountTick의 Core 시계/예약 연결과 Weather 환경 전달**은 Core가 담당한다. 날씨 선택 규칙/Random 호출 교체·Weather 고유 writer·포털·콘솔은 김진태가 담당한다.
- 기존 HUD/폰트/Asset을 재사용한다. 개인 메뉴와 필드 정지, UI 표현과 상태 원본을 구분한다.
- 자동 저장/복원 발동 사건과 미정 보상/승리/감속 중첩 정책은 임의로 확정하지 않는다. 필요한 요청 API와 명시 실행 경계까지 제공한다.
- 코드 상단은 한국어로 역할·수명을 7줄 이내에 설명한다. 공개 함수에는 호출자·시점·반환/해제 책임을 쓴다.
- PR을 자동 생성·병합하지 않는다. 복구 가능한 목적별 커밋으로 남기고 최종 수동 테스트/외부 의존을 기록한다.

## 배치와 인수

| 배치 | 구현 목적 | 상태 | 직접 영향 확인 |
| --- | --- | --- | --- |
| B0 | 실제 프로젝트를 최신 main 기반으로 전환, 개인 파일/4개 전달본/stash 기준 보존, 계획 확정 | 완료 | main 1a03e9a91, 개인 3개 파일 유지, stash 10개 유지 |
| B1 | 공용 시간·입력과 Feature 자식 수명 | 부분 구현 / 이벤트·효과 보완 필요 | 실제 필드 정지/재개·허브 이동 허용, 터렛 기존 PlayMode 85개 통과 |
| B2 | Host 경계·환경·origin·실제 Encounter 연결 | 구현·집중 검증 완료 | 생산 EnemySpawnRoot 명시 선택, 초기 자동 요청·공용 Random/시간·환경 v2 연결 |
| B3 | 실제 Player·CU/터렛·Enemy/Encounter writer와 논리 참조 | 부분 구현 / 원거리 발사 진행 누락 | 적/터렛·비행 탄환·미사일·예약·분산 이력을 포함한 두 번 복원 통과. Weather 대역은 검증용으로만 사용 |
| B4 | 저장 scope·누락 gate·새 Mission resume·게임 사이클/UI | 경로 구현 / 새 Match 복원 성공 미검증 | 실제 게임 이동·누락 거절 통과. 실제 전체 Save/Resume는 진태 Weather writer 누락으로 차단 |
| B5 | Core 문서/Issue·4개 전달본 대비 변경·수동 확인 | 진행 중 / 기존 문서·stash 최종 대조 필요 | [팀 안내](../architecture/core-feature-integration-guide.md), [결과/남은 인수](2026-11-05-core-feature-integration-results.md) |


각 파일마다 반복 테스트하지 않는다. 연결된 배치 단위로 필요한 컴파일·집중 검증을 수행하고, 최종 통합과 사람 확인을 구분한다. 테스트 소스를 추가해야 하면 검증용 임시 자료로 보관하고 요청한 PR 범위에 불필요한 테스트를 넣지 않는다.

## 소유권과 저장

실제 HP·전력·회복·스킬·투사체·AI 상태는 Feature 원본이다. Core는 시간/권위/실행·복원 순서·파일 IO를 조율한다. 복원용 DTO·상태 적용은 논리 ID와 저장 세대를 사용하며 Unity InstanceId/NetworkObjectId/delegate/coroutine을 영속 원본으로 저장하지 않는다.

Weather writer 등 김진태가 제공할 실제 원본이 필요한 경우 등록 계약과 누락 결과를 남긴다. 필요한 writer 없이 전체 월드 저장 성공으로 표시하지 않는다. 자동 발동 시점은 선택하지 않는다.

## 작업 기록과 전달

- 개인 복구 자료: Git 제외 `Docs/Local/Work/CoreIntegration-20261105`.
- 원본 네 명 전달 문서와 사용자 파일 hash를 보관한다. 현재 요청과 달라진 추가 API/파일/사용 시점은 인수인계 문서에 파트별로 작성한다.
- 기존 paused 목표는 PL이 삭제했고 이번 배치를 기준으로 새 목표를 active로 등록했다. 이전 목표를 허위 완료 처리하지 않았다.
- 최종 문서는 구현/검증/수동 확인/미정 기획/담당자 제공 대기를 따로 적는다. 기본 UI나 Fixture 성공을 실제 제품 통합 완료로 옮기지 않는다.

### 중간 검증 기록

- Unity 6000.3.23f1 격리 worktree의 통합 컴파일 0 종료 확인. 실제 작업 폴더의 개인 3개 파일과 stash 10개는 유지한다.
- 첫 회귀: 72개 중 58 통과/14 실패. 같은 main의 원본 소스로 대조한 64개 중 53 통과/11 실패. 공통 11개와 추가 3개를 구분해 기록했다.
- 설치된 EditMode runner에서 일반 WaitForSeconds/WaitForFixedUpdate를 실제 게임 시각·물리 프레임 대기로 취급하지 않는 기존 환경 기록을 확인했다. 검증용 복사본만 실제 시각을 기다리도록 보정한 실행은 55개 중 52 통과/3 실패. 저장소의 팀원 테스트와 assertion은 바꾸지 않았다.
- 추가 Scene 스폰 검사에서 Active Scene 검색 문제를 확인했다. 현재 Scene에는 이전 Debug Spawner와 현행 `EnemySpawnRoot.prefab` 인스턴스가 함께 있으므로, 현재 제작 Root를 명시 설정에 연결했다. 검증용 Scene에 먼저 적용했으며 실제 Scene 반영/스모크 결과는 다음 기록으로 확정한다.
- 환경 snapshot은 공용 시각의 Epoch를 포함한다. 같은 세대의 시각 역행은 거부하고 명시 복원으로 바뀐 세대의 새 시각은 수용한다. 환경 wire/channel은 v2이므로 서로 같은 변경의 빌드를 사용해야 한다.

### 최종 집중 검증

- 원격 main 재확인: `1a03e9a91`, 로컬 main 대비 추가 커밋0.
- 최소 Scene 반영: Native 저장에서 실제 추가된 컴포넌트2개와 root 참조만 반영(28줄 추가). 무관한 Scene 정규화는 제외.
- `final-feature-smoke`: **12/12**, Unity 종료0. 실제 시작→허브→Mission→귀환, 자동 적10/전선, 출입 정지·재개/정리, missing writer 거절, 빈 슬롯 처리, Core 시간/epoch/물리/예약 연결 및 실제 own-world 반복 복원.
- 반복 복원에는 실제 제작 무기로 만든 살아 있는 탄환·미사일과 대기 요청도 넣었다. 외부 Weather writer는 **검증 대역**이며 제품 코드에는 추가하지 않았다.
- 새 partial/writer60개는 formatter 전후 Roslyn 실행 토큰 동일. 기존 팀원 테스트 소스와 assertion은 수정하지 않았다.
- 기존 전체 회귀 실패는 baseline 비교 자료로 남긴다. 최종 집중 통과를 전체 회귀·2인 빌드·제품 Save 완료로 확대하지 않는다.
