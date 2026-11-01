# Match 공용 시간·예약 API

갱신: 2026-11-02. 상태: 공용 모듈·권위 Scene 이관 구현. 순수 시간/Match 검사 18개와 실제 Initial_Stage Play Mode 1개를 별도 실행해 통과했다. **참가자 출입에 따른 필드 정지·Client 시간 복제·Weather 예약 이관은 후속**이다. [전체 배치](../planning/2026-11-02-core-sprint-scope.md)를 따른다.

## 역할과 호출 순서

| 타입 | 소유자·소비자 | 역할 |
| --- | --- | --- |
| `ITimestampSource` | Infrastructure가 구현, `MatchClock`이 소비 | 준비 여부와 원본 double 초 제공 |
| `NgoTimestampSource` | 조립부가 NGO 관리자를 주입 | 연결 중 SDK ServerTime을 읽음. SDK 시간을 정지하지 않음 |
| `UnityTimestampSource` | standalone/Editor 조립부 | Unity double 실시간. TimeScale을 게임 배속으로 직접 누적하지 않음 |
| `MatchClock` / `IMatchTime` | 권위 Match가 소유, Feature는 읽기 | 원본 timestamp와 anchor로 경과 초·정지·배속 계산 |
| `MatchTimeSnapshot` | 네트워크 표현/진단 읽기 자료 | Match·epoch·revision·원본/게임 anchor·배속·준비 상태. 제어 변경 사이에는 같은 anchor 유지 |
| `MatchScheduler` / `IMatchScheduler` | Match가 소유, Weather 등 Feature가 등록 | 게임 초 deadline의 단발/반복 실행·취소·처리 상한 |
| `MatchTimeService` | Scene 조립부가 소유 | 현재 Match의 clock/scheduler와 완료·폐기 시 예약 차단을 함께 관리 |

```text
권위 Match 조립 → timestamp source 주입 → MatchClock 생성 → MatchScheduler 생성
매 프레임: clock.Pump() → scheduler.Pump() → Feature 진행/표현
해제: Feature 예약 lease/구독 해제 → scheduler.Dispose() → clock.Dispose()
```

Domain/Application은 Unity·NGO에 의존하지 않는다. SDK 어댑터만 원본 시각을 읽는다. `InitialStageCompositionRoot`는 권위 Match에 `MatchTimeService`를 만들고, 네트워크 Match에는 `NgoTimestampSource`, standalone에는 `UnityTimestampSource`를 주입한다. `MatchCoordinator`의 IMatchClock은 이를 전달한다. Scene Update는 `MatchTimeService.Pump(Time.timeScale)`을 호출하며 정식 경로에서 deltaTime을 다시 누적하지 않는다.

`InitialStageCompositionRoot.MatchTime` / `MatchTimers`는 조립부가 Feature에 주입할 읽기/예약 경계다. Feature가 Scene을 전역 검색하는 용도로 사용하지 않는다. Match 완료·폐기 시작에는 예약을 취소하고 추가 등록을 차단하며, 소비자 바인딩 해제 후 시간 서비스를 Dispose한다. 기존 `MatchCoordinator.Tick`은 시계를 주입하지 않은 독립 검사/호환 실행에서만 유효하다.

## 권위 시간과 준비·복원

권위 조율부가 `SetPaused(bool)`, `SetRate(double)`를 호출한다. 변경 전 시각을 먼저 확정하고 anchor를 교체한다. `ElapsedSeconds`는 마지막 Pump의 게임 시각이고, snapshot의 `GameSecondsAtAnchor`는 계산 기준이다. 표현에서 현재 원본 시각을 `EstimateElapsedSeconds`에 넣을 수 있다.

원본 미준비 상태는 0초와 구분한다. 미준비 중에는 게임 시각을 보존하고 준비가 돌아오면 그 시점에 다시 anchor를 잡는다. 미준비 구간을 게임 시간으로 따라잡지 않는다. 원본 역행·계산 범위 초과는 `RequiresReset=true`, `IsAvailable=false`로 동결한다. 권위 조율부는 원인을 표시하고 저장 시각 복원 또는 scope 재조립으로 처리한다.

`Restore(gameSeconds)`는 현재의 준비된 원본 시각을 기준으로 게임 초를 복원하고 새 epoch를 발행한다. scheduler는 이전 epoch 예약을 취소한다. Feature는 복원한 상태에 맞춰 다시 등록한다. 체크포인트에는 원본 NGO timestamp나 callback delegate를 저장하지 않는다.

개인 메뉴는 공유 Match 시간을 변경하지 않는다. 우주선 정지는 Host의 참가자 판정에 연결하며 필드 정지 중에도 네트워크·허브 UI/이동은 계속해야 한다. 이 정책의 실제 Scene 연결은 후속 구현이다.

## Feature 예약 예

아래 clock/scheduler는 조립부가 주입한 객체다. 전역 검색이나 별도 NetworkManager를 만들지 않는다.

```csharp
// Feature 바인딩 때 등록하고 해제 때 lease를 Dispose한다.
IDisposable weatherTimer = scheduler.ScheduleRepeating(intervalSeconds, tick =>
{
    // 긴 프레임의 밀린 회차는 tick.MissedIntervals로 받는다.
    // 현재 상태를 다시 평가하고 무제한 과거 사건을 생성하지 않는다.
    EvaluateWeather(tick.ExecutedAt);
}, gameplayCancellation);

weatherTimer.Dispose();
```

- 지연/간격 단위는 게임 초다. 반복 간격은 양수이며 첫 호출은 한 간격 뒤다. 등록 도중에는 callback을 실행하지 않는다.
- 긴 프레임의 반복은 한 번의 통지로 합치고 예정 시각·실행 시각·미실행 회차를 제공한다. 다음 기한은 원래 간격 위상을 유지한다.
- 기본 상한은 예약 4096개, Pump당 callback 64개이며 조립 시 조정할 수 있다. 이는 보존/처리 상한이며 게임 밸런스 값이 아니다.
- 같은 기한은 등록 순서대로 실행한다. callback 중 추가한 예약과 처리 상한을 넘는 예약은 다음 Pump부터 실행한다.
- lease 또는 토큰 취소는 실행을 차단한다. 등록/Pump/해제는 소유 스레드에서 수행하며 외부 스레드는 lease·토큰으로 취소할 수 있다.
- callback이 정지·복원·Dispose를 실행하면 뒤의 예약도 그 상태를 따른다. 한 callback의 예외가 다른 기한 작업을 누락시키지 않도록 처리한 뒤 AggregateException으로 보고한다.
- Feature의 이벤트 판단과 공용 예약 실행은 구분한다. 이 서비스를 추가하며 자동 저장·복원 이벤트를 선정하지 않는다.

## 검증 상태

`MatchTimeTests` 11개와 `MatchCoordinatorTests` 7개가 통과했다. 최초 결합 실행의 Scene fixture가 실패하여 수정한 뒤 `MatchTimeSceneTests` 1개만 별도 실행해 통과했다. 실제 Initial_Stage Host에서 시간 진행·예약 실행·완료 후 차단을 확인했으며 초기 실패 로그도 보존한다. 두 실행을 하나의 19/19 실행으로 표시하지 않는다. 참가자 우주선 필드 정지·Client 시각 복제·Weather 예약 이관 완료를 뜻하지 않는다.
