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

현재 규칙/codec·Player 생명 port의 실제 검수까지 완료했고, Coordinator/Scene 실패 이동·첫 저장 없는 초기 전장 재시도·물리 입력/원격 전송은 다음 조립 단계다. 실제 검수는 Player1명+시험용 아군 port에서 공용3.015초 뒤0→120/400 HP와 입력잠금 해제를 확인했다. 실제2인 검증 완료를 뜻하지 않는다.
