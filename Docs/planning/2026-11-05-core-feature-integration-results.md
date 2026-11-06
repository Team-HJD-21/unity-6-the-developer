# 11월5일 Core 실제 통합 결과와 인수

> 최신 보완 상태: [요구·Issue 대응](2026-11-05-core-requirements-and-issue-map.md), [C0–C5 배치 기록](2026-11-05-core-completion-batches.md). 이 문서의 당시 누락/검사 수치는 경위로 보존하며 이후 보완 결과와 구분합니다.


> **보관 기록:** 아래의 1차 완료 판정 정정·당시 누락/검증 결과는 그 시점의 기록입니다. 감속·원거리 발사·새 Match 파일 복원·2인 Scene 및 구조 정리의 현재 결과는 위 최신 링크를 따릅니다.


기준 main: `1a03e9a91` (#615·616·617 포함). 원격 재확인 결과 추가 main 커밋은 없다. 작업은 `codex/core-feature-integration-20261105`에 남기며 **PR/push/merge하지 않는다**. [실제 API·코드·수동 확인](../architecture/core-feature-integration-guide.md)이 팀용 첫 문서다.

## 완료한 Core 작업

| 묶음 | 결과 |
| --- | --- |
| 공용 시간/수명 | `FieldSimulationTime`과 실제 Player·터렛·적·소유 탄환/장치 연결. 우주선 이동과 필드 공격을 분리. Host/Client 생성기 소유·해제 범위 제한 |
| 터렛 검증 | 기존 Host 관리 요청 경계를 유지하고 실제 전력/수리/과열·자동 복구 원본을 연결. 렌치도 같은 Scene/Match·Host·필드 진행·도달 조건을 검사. #617 보존 |
| Weather/Encounter | CountTick의 공용 예약, 초기/변경 환경 전달, Epoch가 있는 환경 복제 v2, 명시 Executor, World origin·게임 시간 예약 |
| 실제 저장 원본 | Player/CU/필드 터렛/적/Encounter writer, 공용 난수/공간, 전력/분대/목표·비행 투사체·예약·분산 이력. 전체 actor 적용 뒤 논리 참조 연결 |
| 실행/재진입 | 실제 필수 writer gate와 Debugger 수동 요청, `AppRoot.ResumeCheckpointAsync`의 메타데이터→새 1인 Mission→복원 연결. 기존 시작→허브→출격→귀환 경로 유지 |
| 화면/설정 | 기존 NEXON OTF·HUD panel/button을 공용 AppScreenStyle로 재사용. Build 첫 Scene은 Startup. Initial_Stage 직접 Play 유지. Scene에는 실행기 명시 설정만 28줄 추가 |
| 인수 문서 | 실제 수명/API guide와 사용 문서 갱신. 기존 네 명 전달본의 원본을 보존하고, 추가한 파일/이유/Core 접점을 파트별로 기록 |

새 게임 Manager로 상태를 다시 복제하지 않았다. 기존 Feature에 시간·권한·저장 접점을 붙였고 같은 클래스의 partial로 분리했다. 실제 HP/무기·전력·AI는 Feature 원본이다. MatchState의 기초 필드는 이를 대체하지 않는다.

## 아직 전체 게임 완료라고 하면 안 되는 것

- **전체 Save/새 Mission 복원 인수:** 현재 Scene은 `world/weather`가 없다. 김진태가 실제 날씨/예고 큐·다음 전환 writer를 제공해야 한다. 운영용 가짜 writer를 넣지 않았다. Core 담당 writer의 성공과 별개다.
- **진태 3번째 이후:** 날씨 선택 Random 교체·Weather writer·포털 실제 이동 후 보고·콘솔/미션 UI 연결은 기존 역할대로 진태가 맡는다. Core 담당 CountTick/환경 전달은 연결했다.
- **당시 2인 실제 실행/Player 원격:** 이 절 작성 시 전체 Player NetworkBehaviour·원격 공격/수리·재접속과 2인 Scene 이동 준비 계약은 추가 인수가 필요했다. 이후 Core 공동 Scene/준비 구현·두 프로세스 검증은 최신 결과에 기록했다. Core의 Host 명령/actor 등록 port와 같은 빌드 조건을 제공했다.
- **추가 생성 연출/개체:** GroundEmergence/AirDrop 어댑터와 나무 등 동적 source의 고유 writer/등록 세대 복원은 미제공이다. 미지원 요청/저장을 명확히 거부한다.
- **제품 규칙/서비스:** 자동 저장 사건·승패 목표/부활·보상·감속 중첩, 제품 경제/프로필 Backend·실제 Steam SDK/온라인 인증은 임의 구현하지 않았다. Steam은 별도 도입 설계 문서가 있다.
- **시각 인수:** 기존 에셋을 재사용했지만 최종 HUD 폰트/레이아웃, 포털/콘솔을 포함한 사람의 플레이 확인은 별도다.

## 검증 근거

| 검사 | 결과 / 실제 의미 |
| --- | --- |
| 실제 Scene와 공용 접점 최종 집중 | `final-feature-smoke` 12/12, Unity 종료0. 시작→허브→Mission→귀환·한 AppRoot, 자동 적10/전선, 출입 정지·재개/종료 정리, 없는 저장 슬롯·writer 누락 거절, 시간/epoch/물리/예약 |
| Core 담당 월드 반복 복원 | 실제 Scene에서 적의 ID/HP/위치와 살아 있는 캐논 탄환·미사일·예약을 저장한 뒤 두 번 복원. **Weather는 검증용 외부 대역**이며 실제 큐 복원을 증명하지 않음 |
| 터렛 기존 PlayMode | 전력 파괴/수리/초기화 85/85, Unity 종료0 |
| Repository Python | 기존 unittest 50/50. 외부 API/알림은 테스트의 mock 경로이며 실제 메시지 발송 아님 |
| Unity metadata | 새 Resources/Core 폴더 meta 누락을 수정한 뒤 오류0 |
| 서식/보존 | 새 partial/writer60개 formatter 전후 Roslyn 토큰 동일. 팀원 테스트 소스 변경0, 개인 파일3개 hash·stash10개 보존 |

마지막 Host/정지 피해 방어 보완도 `final-authority-smoke` 실제 Scene 두 경로 **2/2**, 종료0으로 확인했다. 전체 회귀를 무조건 성공이라고 표시하지 않는다. 초기 전체 72개는58통과/14실패였고, 동일 main 원본64개는53통과/11실패였다. 설치 runner의 물리/게임 시각 대기를 검증 복사본에서만 보정한55개는52통과/3실패였다. 기존 prefab 경로/기대값과 대기 문제를 product source에서 임의 수정하지 않았다. 새 통합에서 확인한 Scene 실행기 선택과 fixture closure 문제는 구분해서 해결했다.

로그·검증 코드·baseline·개인 복구 자료는 Git 제외 `Docs/Local/Work/CoreIntegration-20261105`에 있다. 격리 validation worktree를 사용했고 사용자의 실제 Scene/개인 설정을 테스트로 재작성하지 않았다.

## Issue 판단

| Issue | 이번 결과 / 유지 이유 |
| --- | --- |
| #598 | 실제 시간/정지·CountTick 연결을 제공하고 집중 검증. 최종 수동/2인 인수와 PR 전이므로 열린 상태 유지 |
| #558, #559, #496 | 실제 환경/origin/예약/요청 경로 연결. 미지원 전달/source writer는 명시. 담당자 실제 정책·연출/통합 인수 필요 |
| #599 | Core 실행·binary IO·실제 own-world writer 제공. **Weather writer와 실제 전체 Save 인수 때문에 종료 불가** |
| #600, #555~557 | 게임 흐름·UI/Scene/진행 기반 제공. 콘솔/포털·최종 화면/제품 승패 정책과 PR 인수는 별도 |
| #495, #528 | 실제 D 원본/초기화/관리 요청·읽기/HUD 계약 제공. 최종 실제 HUD·2인 동작은 사람이 확인 |
| #442 | Host 기반·공용 port는 유지/보강. 실제 2인 Player/Scene/종료-재시작 빌드 인수 없이 닫지 않음 |
| #444, #602 | 책임·현재 API·실제 원본/외부 의존과 읽는 순서 갱신. PL 이해/PR 검토가 남음 |
| #601 | Steam SDK 설치가 아니라 도입 설계 범위. 래퍼/transport·인증 도입 선택은 PL 확인 필요 |
| #560 | 팀 Milestone/일정·분담 결정은 기술 구현만으로 종료하지 않음 |
| #420 | 과거 기술 PoC 인수와 전체 팀 Stage1 승패/동일 Build 인수는 구분. 팀 통합 결과 없이는 종료 불가 |
| #403 | 과거 Animator 버그 재현/자산 증거를 이번 시간·저장 검사로 대체할 수 없어 유지 |

이번에는 Issue를 임의 종료하거나 브랜치/stash를 삭제하지 않는다. 변경은 목적별 커밋으로 분리하며, 나중에 PR을 나눌 때 공용 시간/계약→파트 통합→월드 저장→Scene/App 조립→UI/문서 의존 순서를 지킨다. 각 PR에 포함된 실제 완료 조건과 수동 인수만 Closes로 전환한다.

## PL 확인 순서

[팀 안내의 8단계](../architecture/core-feature-integration-guide.md#수동으로-볼-순서)를 그대로 따른다. 특히 다음 세 가지를 먼저 보면 된다.

1. `InitialStageCompositionRoot`의 ComposeAuthority→시간/Feature scope→BindCheckpoints. 생성/해제의 소유자가 납득되는지 확인.
2. `FieldSimulationTime`→PlayerControlState / FieldTurretController.Simulation / EnemySpawnExecutor.Simulation. 같은 시간과 실제 원본이 어떻게 연결됐는지 확인.
3. `CheckpointService`→Controller.Persistence / EnemySpawnExecutor.Checkpoint→전체 Apply 후 ResolveReferences. Weather 누락 gate와 검증 대역의 차이를 확인.

최종 기록: 본인 담당 Issue14개 본문 갱신과14/14 읽기 일치를 확인했다. Issue 종료0, PR/push/merge0. 개인3파일 SHA256과 stash10개를 보존했다.
