# Player 구조와 Mission 복귀

`MissionRecoveryLedger`는 한 Mission의 팀 구조 시간·안전장치 사용 기록이다. `MatchRecovery`가 Host 원본을 관측해 구조 유지와 실패를 판단한다. Player HP/이동/Scene을 Core에서 복제하지 않고 실제 `IPlayerRescueActor`에 복귀를 요청한다.

## 구조 호출

- Host가 roster ID별 실제 actor를 `Register`하고 수명이 끝나면 `Unregister`한다. 아직 준비되지 않은 원본을 죽었다고 처리하지 않는다.
- 입력 gateway는 실제 송신자를 확인한 뒤 `SetRescueHeld(rescuer, target, held)`로 유지/해제를 보낸다. 클라이언트 좌표나 HP를 판정 원본으로 쓰지 않는다.
- 조율부의 `Pump`는 CU 패배를 먼저 평가한 뒤 실행한다. 기본3초 유지/거리3/HP30%·입력만료0.5초는 검증한 Scene 설정을 복사한다. 해제·이탈·죽음·입력 중단은 옛 유지 시간을 이어 쓰지 않는다.
- 다운 대기 중 팀 전체가60초를 나눠 쓰며 여러 명이라고 빨라지지 않는다. 구조 성공은 남은 예산을 충전하지 않는다.

`PlayerInfo.Rescue`는 현재 Host에서 사망한 actor만 복귀시킨다. 생명 잠금만 풀고 다른 메뉴/입력 잠금은 유지한다. 기존 단일 `IPlayerInteractionActor`와 중복되지 않도록 구조 관측은 별도 `TryCaptureRescue`다.

## 실패와 비환급 기록

`Failure`는 한 번만 발행한다. `TryBeginFailure`는 같은 실패 ID의 기술 재시도에 추가 차감하지 않는다. 실제 복귀 성공 후에만 `CompleteRecovery`를 호출한다. 안전장치3회를 사용한 다음 실패는 실행을 종료한다. CU 파괴는 구조보다 먼저 패배 처리한다.

`MissionRecoveryParticipant`는 Kind Progression으로 이 기록을 성장 파일에 보존한다. 월드 복원 정책은 이 원본을 되감지 않는다. 새 Match ID가 생긴 것과 명시 새 Mission은 구분한다. 정상 종료/복귀에는 대기 쓰기를 Flush하며 강제 종료에는 마지막 성공한 파일만 보장한다.

## 실제 Scene 연결

`InitialStageCompositionRoot.Recovery`가 실제 Host actor와 local 입력을 연결한다. F를 유지하면 가까운 사망 참가자를 찾고 Host 구조 서비스에 heartbeat를 전달한다. 같은 F의 자기 회복은 구조 대상이 있을 때 실행하지 않는다. 원격 actor 등록은 기존 `InitialStageNetworkBinding.RegisterAuthorityPlayerActor` lease와 함께 해제한다. 원격 입력/실제 Player NetworkBehaviour는 별도 연결 대상이다.

`MatchCoordinator.EvaluateProgress`가 CU 패배를 먼저 평가하고 Root가 `Recovery.Pump`를 호출한다. 전멸 실패는 공용 시간을 임대 정지하고 성장 쓰기를 완료한다. 실제 유효 체크포인트가 있으면 전체 writer로 복원하고, 없으면 App이 같은 Mission을 다시 연다. 성공한 새 Scene도 모든 실제 참가자가 살아 있어야 차감된 복귀를 완료한다.

`InitialStageCompositionRoot.RetryRecoveryAsync`는 같은 Scene의 기술 실패 재시도다. Scene 입장 자체가 실패한 경우 UI는 `AppRoot.CanRetryMissionRecovery`/`LastMissionRecoveryError`를 표시하고 `RetryPendingMissionRecoveryAsync`를 사용한다. Game Debugger의 Checkpoint/App Game Flow도 이 API를 호출한다. 기술 재시도는 안전장치를 또 차감하지 않는다.

비환급 기록은 기존 성장 파일의 source 집합을 변경하지 않는 별도 Host/campaign/Stage recovery 슬롯이다. 구조 대기는1초 병합하고 실패·복귀·정상 종료는 Flush한다. 새 Mission만 실행 ID와 예산을 초기화한다. Campaign 월드 section v2는 실행 ID도 검증한다. 옛 v1은 명시 migration 없이 이어갈 수 없으며 자동 삭제하지 않는다.

## 검증 범위

자동 규칙/저장19개 통과. 실제 Stage_1에서1~3회 사망→새 Player/Match와 동일 실행 ID·사용1/2/3,4번째는 사용3 유지·Defeat 종료를 확인했다. 처음 작성한 구조 검수는 실제 Player1명+시험용 아군 port에서 공용3.015초 후0→120/400 HP였다. 물리 F·실제2인 NGO 구조 검증을 대신하지 않는다.

전체 월드 저장/복원은 실제 `world/weather` writer가 아직 없어 실행 불가다. 이 필수 항목을 제거하거나 시험 writer로 성공을 만들지 않는다. 실제 Scene 안전장치 확인은 ‘첫 저장 없는 재진입’ 경로다.
