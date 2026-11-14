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

`InitialStageCompositionRoot.RetryRecoveryAsync`는 같은 Scene의 실제 복원을 수행한다. UI/외부 호출자는 Scene의 생존 여부를 나누지 말고 `AppRoot.CanRetryMissionRecovery`를 확인한 뒤 `RetryPendingMissionRecoveryAsync`를 사용한다. App이 현재 Scene 복원 또는 닫힌 Scene의 재입장을 선택한다. `LastMissionRecoveryError` 원문은 진단용이다. Game Debugger의 Checkpoint/App Game Flow도 이 API를 호출한다. 기술 재시도는 안전장치를 또 차감하지 않는다.

비환급 기록은 기존 성장 파일의 source 집합을 변경하지 않는 별도 Host/campaign/Stage recovery 슬롯이다. 구조 대기는1초 병합하고 실패·복귀·정상 종료는 Flush한다. 새 Mission만 실행 ID와 예산을 초기화한다. Campaign 월드 section v2는 실행 ID도 검증한다. 옛 v1은 명시 migration 없이 이어갈 수 없으며 자동 삭제하지 않는다.

## 검증 범위

자동 규칙/저장19개 통과. 실제 Stage_1에서1~3회 사망→새 Player/Match와 동일 실행 ID·사용1/2/3,4번째는 사용3 유지·Defeat 종료를 확인했다. 처음 작성한 구조 검수는 실제 Player1명+시험용 아군 port에서 공용3.015초 후0→120/400 HP였다. 물리 F·실제2인 NGO 구조 검증을 대신하지 않는다.

전체 월드 저장/복원은 실제 `world/weather` writer가 아직 없어 실행 불가다. 이 필수 항목을 제거하거나 시험 writer로 성공을 만들지 않는다. 실제 Scene 안전장치 확인은 ‘첫 저장 없는 재진입’ 경로다.


## 유효 저장과 기술 실패

안전장치 후보 캡처는 모든 실제 참가자가 생존한 시점만 허용한다. 사망 직후 Root Pump 전의 짧은 구간도 검사한다. 개별 HP0 writer의 표현 가능 여부와 안전한 복귀 지점은 같은 뜻이 아니다.

복귀는 저장된 slot뿐 아니라 마지막 성공한 CheckpointId도 확인한다. 파일 누락/교체·읽기 실패는 같은 실패의 기술 오류로 남고, 조용히 초기 전장으로 바꾸지 않는다. 기술 실패 뒤 외부 호출자는 App.RetryPendingMissionRecoveryAsync를 사용한다. 같은 Scene의 실제 작업은 App이 Root.RetryRecoveryAsync에 전달하고, Scene 소유권이 닫혔으면 같은 복귀 의도로 재입장한다. App 이동/실제 준비는30초와 App 종료 취소로 제한하며 이전 Scene의 취소 토큰을 사용하지 않는다. 처음 저장 링크 자체가 없을 때에만 초기 전장을 다시 연다.

실제 검수에서는 없는 저장 링크를 주입해 사망/재시도/Scene 소유권 종료 뒤 App 재시도를 확인했다. 안전장치는1회만 차감됐고 정지·이유·원본을 유지했다. 이는 실제 Weather writer 전체 rollback 실패의 인수를 대신하지 않는다.


## 실제 게임 메뉴에서 재시도

복귀에 기술 오류가 남으면 기존 공용 메뉴 프리팹을 열고 제목을 `복귀 오류`, 주 버튼을 `같은 복귀 재시도`로 표시한다. 기존 폰트/색/배치/전환을 재사용하며 새 HUD/Prefab은 만들지 않는다. 원본 오류는 App/실행 기록에 보존하고 화면에는 추가 차감이 없다는 짧은 안내를 보여준다.

- **재시도:** 같은 실행/실패 ID와 저장 slot/CheckpointId를 다시 검사한다. 없거나 바뀐 파일은 계속 거부하며 새 게임으로 바꾸지 않는다. 처리 중에는 기존 로딩/입력 잠금을 쓰고 중복 클릭을 막는다.
- **우주선으로:** 기존 명시 귀환 API를 사용한다. 실제 허브 도착이 성공해야 이전 기술 오류 표시를 닫는다. 실패한 이동은 오류와 재시도 의도를 유지한다. 임시 허브 방문이나 자동 저장을 추가한 기능은 아니다.
- **설정:** 기존 메뉴의 설정 화면을 그대로 사용할 수 있다. 오류 상태에서 Esc로 메뉴를 닫아 정지된 전장만 보이게 하지는 않는다.

직접 Stage_1 실행과 Startup→메인→허브 문→Stage_1 경로를 나눠 실제 메뉴 Button.onClick을 확인했다. 둘 다 Scene 생존/해제 뒤 재시도에서 사용1/1/1, 같은 실행, 누락 저장의 초기 전장 대체 없음, 허브 성공/오류 닫힘을 확인했다. Startup 경로의 최종 Player/Camera는 각각1개다. 실제 OS 포인터 입력 인수나 전체 Weather 복원을 뜻하지 않는다.

직접 호출 예시:

```csharp
// 실패 화면/개발 도구가 App을 받아 사용하는 같은 진입점입니다.
if (app.CanRetryMissionRecovery)
{
    try { await app.RetryPendingMissionRecoveryAsync(); }
    catch (Exception) { /* 원본은 app.LastMissionRecoveryError로 진단합니다. 새 Mission으로 대체하지 않습니다. */ }
}
```

이전 읽기 작업이 늦게 실패하더라도 그 사이 새 Mission 의도가 시작됐으면 이전 오류로 새 UI를 덮지 않는다. 실패 ID 검증은 App과 Scene 양쪽에 적용한다. 이전 읽기는 Scene 이동 전 시도 소유권도 검사하며, 이전 작업의 finally가 새 재시도 잠금을 해제하지 않는다. 자동 회귀2개와 실제 BinaryCheckpointStore 순서 검수로 확인했다.
