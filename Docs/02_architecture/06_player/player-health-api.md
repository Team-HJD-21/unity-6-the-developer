# 플레이어 체력과 자동 회복

2026-11-15 사용자 지정: 마지막 실제 피격 후 5초가 지나면 초당 최대 체력의 2%를 100%까지 회복한다. 추가 피격은 대기 시간을 다시 시작한다.

## 실행과 조정

- `PlayerInfo.ApplyDamage`가 실제 감소한 HP를 계산한 뒤 자동 회복을 예약한다. 0 이하·권한 없음·필드 정지 등으로 거부된 피해는 대기 시간을 변경하지 않는다.
- `PlayerInfo.FixedUpdate` → `AdvanceAutomaticRecovery`가 공용 `FieldSimulationTime.Seconds`의 절대 Match 시각을 읽는다. FixedUpdate 횟수나 실제 벽시계로 회복 속도를 계산하지 않는다.
- 기본 최대 체력 400에서는 초당 8 HP다. 정수 HP에 반영하지 못한 소수점은 누적하며, 최대 체력을 넘기지 않는다. 회복 중 다시 맞으면 소수점 누적과 대기를 다시 시작한다.
- `PlayerInfo` Inspector의 `_automaticRecoveryDelay`(5), `_automaticRecoveryFractionPerSecond`(0.02)로 조절한다. 기존 Player 프리팹은 새 필드의 기본값을 사용한다.
- 기존 `onHpChange`가 회복된 체력을 HUD에 전달한다. 회복으로 `Damaged`나 사망 이벤트를 발행하지 않는다.

## 수명과 권한

일시정지·시계 미가용·Match 종료 취소 상태에서는 회복을 적용하지 않는다. 대기 시간도 기존 Match 시계의 정지를 따른다. NGO Client는 체력을 직접 변경하지 않는다. 사망과 Match 해제는 자동 회복 예약을 지운다.

명시적인 `RecoverHp()` API의 기존 회복은 유지한다. 이 API를 호출하면 자동 회복 예약을 지워 두 회복이 중복되지 않게 한다. 현재 실제 게임 코드에서 이 API를 호출하는 경로는 없으며 피격 연출 회귀 테스트가 사용한다.

## 저장·복원

`/actor` section은 v2로 올리고 기존 생존·물리·수동 회복 자료 뒤에 자동 회복 예약, 남은 대기 시간, 미반영 HP, 설정값을 추가한다. 저장 시점의 대기와 부분 HP를 보존하고 복원 시 현재 Match 시각에 다시 연결한다. Apply/Rollback은 피해 이벤트를 만들지 않는다.

v1은 계속 읽는다. v1의 살아 있는 부상 actor가 수동 회복 중이 아니면 새로운 5초 대기에서 자동 회복을 시작한다. 잘못된 대기·누적 HP·설정 불일치는 적용 전에 거부한다.

## 검증

`PlayerAutomaticRecoveryTests`는 대기 경계·재피격·최대 HP별 비율·소수점·만피 상한·정지·권한·사망·체크포인트와 v1 호환을 검사한다. `PlayerAutomaticRecoveryRuntimeTests`는 실제 Player 프리팹의 FixedUpdate와 체력 통지를 검사한다. 실제 게임 Scene 수동 플레이·Host/Client 동기화 인수는 별도다.

이번 실행에서 자동 회복 11개(실제 Player 프레임 1개 포함)와 기존 `PlayerHitFeedbackTests` 7개가 통과했다. Unity 컴파일과 변경 범위의 diff 검사도 통과했다.
