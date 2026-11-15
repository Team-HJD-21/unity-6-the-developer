# 플레이어 체력과 자동 회복

2026-11-15 후속 사용자 지정: 마지막 실제 HP 피격 후 **5초에 한 번**, 이후 **3초마다 최대 HP의 2%**를 회복한다. 기본 최대 체력 400이면 8 HP씩 5/8/11초에 올라온다. 연속 초당 회복에서 간격을 둔 회복으로 변경했다.

## 실행과 조정

`PlayerInfo.ApplyDamage`의 실제 HP 감소가 대기시간을 다시 시작한다. 0 이하·권한 없음·필드 정지·개발용 피해 보호로 거부된 공격은 변경하지 않는다. `FixedUpdate` → `AdvanceAutomaticRecovery`가 `FieldSimulationTime.Seconds`의 절대 Match 시각으로 다음 회복 시점을 검사한다.

한 번 처리하면 실제 현재 시각에서 3초 뒤로 예약한다. 긴 프레임·시간 점프의 누락 횟수를 한꺼번에 지급하지 않는다. 최대 체력 상한을 지키고, 1 HP 미만인 회복량은 회복 횟수마다 소수점을 누적한다. 피격은 대기와 누적 소수점을 초기화한다. 회복은 `onHpChange`만 전달하고 `Damaged`/사망 이벤트를 발행하지 않는다.

Inspector 필드는 `_automaticRecoveryDelay`(5), `_automaticRecoveryInterval`(3), `_automaticRecoveryFractionPerPulse`(.02)다. 기존 초당 필드는 `FormerlySerializedAs`로 기존 0.02 값을 보존한다. 명시적 `RecoverHp()`는 기존 경로를 유지하고 자동 회복 예약을 지워 중복을 막는다.

일시정지·시계 미준비·Match 종료·비권위 Client·사망은 회복하지 않는다. 사망과 Match 해제는 예약을 지운다.

## 포털 보호막과 체력바

포털 이동 성공 시 20 HP의 보호막을 2초간 제공하며 생성은 15초마다 한 번 가능하다. 강화는 23/26 HP와 2.3/2.6초다. 같은 쿨타임 중 재이동은 보호량과 지속시간을 갱신하지 않는다. 보호량을 넘는 피해만 HP를 감소시킨다. 전부 흡수한 공격은 처리 성공을 반환하지만 HP 피격 이벤트·자동 회복 대기 갱신은 발생하지 않는다.

`StagePlayerVitalsView`가 HP 통지마다 보호량도 읽고 `CombatVitalsBar.ShieldFill`에 연결한다. 빨간 실제 HP 위에 파란 보호 구간을 추가하며, 만피에서는 표시 용량만 HP+보호량으로 확장한다. 실제 최대 HP는 바꾸지 않는다. HP와 보호량을 함께 보면 파란 부분이 보이고, 호버 숫자는 `400 / 400 +20`처럼 구분한다. 캐릭터 주변 보호 테두리는 제거했다. 세부 수명과 저장은 [포털 API](player-portal-api.md)를 따른다.

## 저장·복원

`/actor` section v3은 기존 HP/물리/수동 회복 자료와 다음 회복까지의 시간·미반영 소수 HP·회복 설정·회복 간격을 저장한다. 복원 시 현재 Match 시각에 남은 대기를 연결하며 Apply/Rollback은 피해 이벤트를 발생시키지 않는다. main의 현재 성장 최대 HP와 저장된 전투 HP 비율을 유지한다.

v1/v2는 계속 읽는다. 연속 회복의 누적 HP는 새 회복으로 이월하지 않고, 남은 대기와 생존/HP 상태만 새 간격 방식으로 연결한다. NaN·음수·상한 초과 시간과 설정 불일치는 적용 전에 거부한다.

## 이번 검증

새 간격 회복의 경계·재피격·거부된 피해·소수점·긴 프레임·상한·권한·정지·저장 복원/롤백·구형 v1/v2 호환을 검사한 `PlayerAutomaticRecoveryTests` 13/13 통과. `PlayerPortalSkillTests` 8/8과 실제 제작 HUD의 파란 보호 구간·보호량 소모·쿨타임·만료를 보는 `PortalShieldUsesBlueHealthBarSegmentAndUpdatesWithoutHealthLoss` 1/1 통과. 실제 게임 Scene 사람의 조작과 협동 전송·빌드는 별도다.

실제 Player 프리팹의 FixedUpdate에서 첫 5초 회복, 다음 간격 사이 정지, 재피격 후 대기 재시작과 HP 통지를 보는 `PlayerAutomaticRecoveryRuntimeTests` 1/1도 통과했다. 별도 Unity 렌더에서 체력바의 빨간 HP 위에 20 HP 파란 구간이 보이는 것을 확인했다.
