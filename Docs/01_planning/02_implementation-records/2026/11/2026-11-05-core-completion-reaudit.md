# Core 완료 판정 재검토 — 전체 완료 아님

> 최신 보완 상태: [요구·Issue 대응](2026-11-05-core-requirements-and-issue-map.md), [C0–C5 배치 기록](2026-11-05-core-completion-batches.md). 이 문서의 당시 누락/검사 수치는 경위로 보존하며 이후 보완 결과와 구분합니다.


기준: 2026-11-05, 현재 HEAD `5cc57615a`, 원격 main `1a03e9a91` 재확인. 앞서 보고한 **“Core 담당 구현 완료, 수동 인수만 남음” 판정을 정정한다.** 기능 일부의 집중 검사 통과를 스프린트·기획·Issue·이전 배치 전체의 완료로 확대했다.

이번 문서는 코드·현재 본인 담당 GitHub Issue18개·11월2일 Sprint/11월5일 통합 계획·11월3일 회의·stash10개와 복구 기록·이 대화의 담당/제외 합의를 대조한 결과다. 새 PlayMode 성공을 주장하지 않으며, 제품 코드를 수정한 결과도 아니다.

## 수동 확인 전에 PL 구현/검증이 남은 것

| 항목 | 현재 코드/근거 | 아직 해야 하는 것 |
| --- | --- | --- |
| Player 감속 수신·시간/저장 | `ArcaneOrbProjectile`은 `ISlowEffectReceiver`를 조회하지만 현재 Player에 구현체가 없다. 11월5일 통합 전 계획의 Player 항목6에는 연결이 명시돼 있다. `ArcaneSlowEffect`는 표현일 뿐 수신자가 아니다. | 최신 Enemy 계약에 Player 수신을 연결하고 공용 시계·효과 만료·복원 접점을 구현/검증. 중첩 정책은 임의 확정하지 않음 |
| 원거리 적의 발사 진행 저장 | `EnemyProjectileAbility`에는 `_isExecuting`, `_shotPending`, `_pendingDirection`이 있지만 `EnemyNetworkController.Checkpoint.State`는 HP/쿨다운/폭발/경로/Animator만 저장한다. 실제 `PlantLeafer.prefab`은 `fireOnAnimationEvent: 1`이다. | 발사 전/후·종료 전 상태를 저장하고 적용/참조 단계에서 복원. 현재는 해당 공격 도중 저장을 거부하는 검사도 없어 상태 누락을 허용함 |
| 정지 직후 Animation Event 생성 경계 | `EnemyProjectileAbility.OnProjectileFireAnimationEvent`와 `EnemyProjectileSpawner.TrySpawn`은 공용 필드 진행을 검사하지 않는다. Actor의 Animator 동결만으로 이미 전달된/명시 호출된 이벤트까지 보호했는지 검증하지 않았다. | 필드가 정지/종료된 프레임의 이벤트·지연 생성 허용 조건을 보완하고 직접 재현. 정적 검사로 경계 부재를 확인했으며 실제 타이밍 재현은 미수행 |
| 저장 파일로 새 Match 복원 성공 검증 | 현재 검증은 `SaveAsync` 결과의 `Data`를 **같은** `scene.Checkpoints.Restore(saved.Data)`에 두 번 적용한다. `ResumeCheckpointAsync` 검사는 없는 슬롯의 `NotFound` 경로다. | 새 Match/Scene 구성→실제 파일 읽기→복원 성공·실패/정리 검증. Core 경로는 외부 Weather 대역으로 먼저 검증할 수 있고 실제 Weather 인수와 구분해야 함 |
| 협동 Scene 이동 Core | `AppRoot.GetSceneTravelRestriction`은 Host 접속자가2명이면 `Multiplayer Scene travel awaits the shared readiness contract`로 거부한다. `ResumeCheckpointAsync`도 1인 Story만 받는다. | 협동 게임 사이클까지 완료라고 할 수 없음. 공유 Scene 준비/전환은 Core 잔여이고 Player NetworkBehaviour만의 문제로 분류하면 안 됨. 협동 저장 재접속은 별도 범위/계약으로 유지 |
| Core 전체 정리/문서 대조 | asmdef 순환0·서식 토큰 동일과 주요 안내 문서 갱신은 확인했다. 그러나 `core-api-usage.md`에는 Feature 호출부가 미변경이라는 과거 설명이 남아 있고, 운영 책임 문서에는 확정한 우주선 필드 정지 정책을 미결로 적은 부분이 있다. | 기존 팀용 API/운영 문서까지 실제 구현에 맞춰 재대조. 전체 public API·상태 원본·불필요한 중계의 검토 결과/판정을 완료 체크리스트로 남겨야 함 |
| stash 기능별 최종 판정 | stash10개는 보존했다. 복구 report의 applied/conflict는 중간 merge 결과다. 수동 수정/삭제/공용 helper 대체가 있었으므로 이 결과만으로 기능 전부가 현재 코드에 반영됐다고 증명할 수 없다. | 필요한 기능별 반영/최신 main 대체/폐기/진태 제외/미구현을 현재 파일·메서드에 대응. 중간 conflict 기록을 실제 미해결 Git conflict라는 뜻으로 오해하지 않음 |

위의 첫 두 항목은 소스에서 확인한 **실제 구현 누락**이다. Animation Event 항목은 보호 경계와 전용 검증이 빠진 것으로, 관찰한 런타임 버그라고 표현하지 않는다. 새 Match 복원은 **구현 코드가 있지만 성공 경로 검증이 빠진 상태**다. 모두 사용자 수동 확인만으로 완료 처리할 수는 없다.

## 다른 담당 제공을 기다리는 것

- 김진태의 실제 날씨 선택 Random 교체, 날씨/예고 큐·다음 전환 writer, 실제 포털 이동 후 보고, 미션 콘솔 호출은 기존 합의대로 김진태 담당이다. Core CountTick/환경 전달 구현과 구분한다.
- 따라서 현재 전체 Save는 `world/weather` 누락으로 차단된다. 다만 이 외부 의존을 위의 **Core 자체 구현/검증 누락을 덮는 이유로 사용하면 안 된다.**
- Player 전체 NetworkBehaviour와 실제 원격 입력·상태 복제는 Player 담당이다. Host actor 등록/해제 접점은 Core가 제공한다. 협동 Scene 준비 계약은 Core 잔여와 구분한다.

## 이번에 새로 구현할 일로 확대하지 않을 것

- 자동 저장/복원 발동 사건: 사용자가 API/관찰자 경계까지만 만들기로 했다. 임의 사건을 만들지 않은 것은 누락이 아니다.
- 승리 목표·보상·부활/감속 중첩의 미정 제품 정책: 사용자/기획 결정 없이 확정하지 않는다.
- Steam SDK/Backend/초대/매칭: 현재 Sprint의 Steam 작업은 조사·도입 설계다. 실제 SDK 미설치를 이번 연결 코드의 버그로 취급하지 않는다.
- GroundEmergence/AirDrop 실제 연출과 동적 source 개체의 고유 writer: 현재 계약은 미지원 시 명확히 거부한다. 실제 Enemy/Level 콘텐츠 구현과 담당 경계를 확인해 별도 완성 범위를 정한다.
- Host Migration·PvP·Enemy Bot 등 초기 대화의 장기 목표/Deferred를 이번 주 필수 구현으로 소급하지 않는다.

## 통과한 검사와 증명하지 못한 것

- 집중12개·마지막 권한 경로2개·터렛 기존85개·Repository Python50개와 metadata 오류0이라는 기록은 유지한다.
- 하지만 이 검사에는 Player 감속 수신, 원거리 발사 대기 중 snapshot, 새 프로세스/새 Match 복원 성공, 실제2인 게임 사이클 전체가 들어 있지 않다.
- 기존 전체 회귀 실패도 남아 있다. main baseline과 비교한 것은 회귀 원인을 구분하는 근거이며 “전체 회귀 해결”을 뜻하지 않는다.
- 따라서 **새 기능의 일부 검사 통과**와 **전체 요구사항 완료**는 다른 판정이다. 앞선 목표 완료 보고는 성급했다.

## 올바른 현재 상태

시간/수명·Host 요청·환경/스폰·월드 저장의 주요 연결과 문서는 상당 부분 구현됐지만, **구현 누락·Core 자체 검증 누락·기존 문서/stash 추적 정리가 남은 진행 중 상태**다. 단순 수동 인수 대기라고 하면 안 된다.

PR/push/merge와 Issue 종료는 수행하지 않는다. 우선 위 Core 잔여를 보완하고 검증 범위를 맞춘 뒤, 그때 사용자 수동 확인 항목만 남았는지 다시 판정해야 한다.
