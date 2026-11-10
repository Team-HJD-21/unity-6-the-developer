# Match 낮밤 상태 조회 API 사용법

`MatchDayNightQueries.TryGetCurrentDayPhase`는 Core가 확정한 환경 snapshot에서 현재 Match의 낮밤 상태를 읽는다. `MatchDayNightCycle` 컴포넌트를 직접 찾거나 시간을 계산하지 않는다.

## 호출 방법

```csharp
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

// source는 현재 Match의 권위 또는 복제 IMatchEnvironmentSource다.
if (source.TryGetCurrentDayPhase(matchId, out DayNightPhase phase))
{
    bool isNight = phase == DayNightPhase.Night;
    // 확정된 현재 시간대에 맞는 게임 규칙이나 표현을 적용한다.
}
else
{
    // 이 Match의 시간대가 아직 제공되지 않았다. Day라고 가정하지 않는다.
}
```

확장 메서드는 `TeamHJD.Game.Contracts` 네임스페이스에서 사용할 수 있다. `source`는 `IMatchEnvironmentSource`, `matchId`는 조회할 `MatchId`다. 성공하면 `phase`에 `Day` 또는 `Night`가 들어간다.

## 성공·실패 조건

| 입력 또는 snapshot 상태 | 결과 |
| --- | --- |
| 같은 Match의 `TimeOfDayId`가 `day` | `true`, `Day` |
| 같은 Match의 `TimeOfDayId`가 `night` | `true`, `Night` |
| source·snapshot이 없거나 `matchId`가 비어 있음 | `false` |
| snapshot의 MatchId가 다르거나 시간대가 없음 | `false` |
| 시간대 ID가 `day`/`night` 이외의 값 | `false` |

실패하면 `phase`는 `default`로 초기화된다. `default`가 `Day`이므로 **반환값이 `true`일 때만** `phase`를 사용한다.

## 변경 알림을 받을 때

`IMatchEnvironmentSource.Changed`는 날씨나 예고만 바뀌어도 발생할 수 있다. 알림을 받은 뒤 같은 `matchId`로 다시 조회하고, 이전에 읽은 Phase와 비교해 실제 낮밤 변경인지 판정한다. 구독은 소비자의 수명에 맞춰 해제한다.

```csharp
void OnEnvironmentChanged(MatchEnvironmentSnapshot snapshot)
{
    if (!source.TryGetCurrentDayPhase(matchId, out DayNightPhase phase)) return;
    if (hasPhase && phase == previousPhase) return;

    previousPhase = phase;
    hasPhase = true;
    // 낮밤이 바뀐 경우에만 필요한 동작을 실행한다.
}
```

구독 직후에도 한 번 조회해 초기 상태를 반영한다. Host는 권위 source를, Client 표현은 복제 source를 사용한다. 이 API는 확정된 **상태**만 반환하며 `RemainingSeconds`나 전환 진행률은 반환하지 않는다.

구현: [MatchDayNightQueries.cs](../../Assets/Scripts/Core/Contracts/Environment/MatchDayNightQueries.cs) · 관련 계약: [낮밤 주기](day-night-api.md), [Match 환경](match-environment-api.md)
