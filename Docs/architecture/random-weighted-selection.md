# 가중 Random 선택 — 비 50% 설정하기

일반 목록 선택은 `Choose`, 서로 다른 빈도로 한 항목을 고를 때는 **`WeightedChoose`**를 사용한다. 이미 제공하는 API이므로 Weather 전용 난수 서비스나 별도 가중 추첨기를 추가하지 않는다. `WeightedIndex`는 같은 규칙으로 목록의 인덱스만 반환한다.

## 날씨 세 종류 중 하나 선택

아래는 **비 50% / 맑음 30% / 천둥 20%** 예제다. 실제 게임 확률을 이 값으로 변경한다는 뜻은 아니다. 날씨 종류·가중치·호출 시점은 World/Weather가 정하고, Core는 주입된 난수로 선택만 수행한다.

```csharp
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;

public static class WeatherSelectionExample
{
    // 예제 ID다. 실제 Weather enum/정의 목록과 연결하는 것은 호출자의 책임이다.
    private static readonly string[] WeatherIds = { "rain", "sunny", "thunder" };
    private static readonly double[] WeatherWeights = { 50d, 30d, 20d };

    // random은 Host Match에서 BindRandom으로 이미 받은 source다.
    public static string SelectNextWeather(IRandomSource random)
        => random.WeightedChoose(WeatherIds, WeatherWeights);
}
```

기존 `IRandomConsumer.BindRandom`에서 `RandomBinding.Bind(streams, "weather", ref _random)`으로 한 번 연결한 뒤, 실제 날씨 선택 시점에 `WeatherSelectionExample.SelectNextWeather(_random)`을 호출하면 된다. 순수 함수 예제라 새 MonoBehaviour를 Scene에 배치하거나 새 부모 클래스를 상속하지 않는다. 목록과 가중치는 매 추첨마다 생성하지 않고 기존 Feature 데이터/필드로 재사용한다.

Core 연결 전 `Awake`에서 추첨하지 않는다. Client도 별도로 추첨하지 않고 Host가 확정한 날씨를 표시한다. 난수 주입·해제는 [Random API 사용법](core-random-api.md)의 규칙을 따른다.

| 항목 | 가중치 | 선택 확률 |
| --- | ---: | ---: |
| rain | 50 | 50 / 100 = 50% |
| sunny | 30 | 30 / 100 = 30% |
| thunder | 20 | 20 / 100 = 20% |

가중치는 **확률 값 자체가 아니라 상대 비율**이다. `[50, 30, 20]`, `[5, 3, 2]`, `[0.5, 0.3, 0.2]`는 같은 비율이다. 합이 100일 필요는 없으며 각 확률은 `해당 가중치 / 전체 양수 가중치 합`이다. 반대로 `[50, 1, 1]`은 비 50%가 아니라 약 96.15%다.

선택은 매번 독립적인 의사 난수 추첨이다. 비 50%는 **10회 중 정확히 5회 비가 온다는 보장이나 최근 날씨를 고르게 섞는 정책이 아니다.** 같은 날씨가 연속 나올 수 있다. 중복 금지·쿨다운·예고 큐·낮밤별 가중치 변경은 Weather의 별도 정책이며 Core가 몰래 추가하지 않는다. 이전 날씨를 0 가중치로 제외하면 남은 항목으로 확률이 다시 계산된다.

## 두 가지 판정이면 Chance

```csharp
bool shouldRain = random.Chance(0.5); // 비가 온다 / 오지 않는다, 50%
```

`Chance` 입력은 **0~1**이다. `Chance(50)`가 아니다. 맑음/비/천둥 중 **정확히 하나**를 선택하려면 `WeightedChoose`를 한 번 호출한다. 종류별로 `Chance`를 여러 번 호출하면 둘 이상 또는 아무것도 선택되지 않을 수 있다.

## 데이터로 인덱스만 선택

```csharp
int selectedIndex = random.WeightedIndex(weights);
var selectedDefinition = weatherDefinitions[selectedIndex];
```

`weights`와 정의 목록의 순서·개수는 호출자가 맞춘다. 항목 자체를 전달할 수 있으면 `WeightedChoose(weatherDefinitions, weights)`가 개수 불일치도 검사해 준다. enum·ScriptableObject·문자열 등 목록 항목 형식에 제한을 두지 않는다.

## 잘못된 설정의 처리

| 설정 | 결과 |
| --- | --- |
| 어떤 항목의 가중치가 0 | 해당 항목은 선택하지 않음 |
| 빈 목록 또는 모든 가중치가 0 | `ArgumentException` |
| 음수·NaN·무한 가중치 | `ArgumentOutOfRangeException` |
| 항목/가중치 개수가 다름 | `ArgumentException` |
| null 목록 | `ArgumentNullException` |
| 해제된 source 또는 Host 권한이 없음 | 기존 수명/권한 검사에서 거부 |

유효하지 않은 설정을 균등 선택으로 대체하지 않는다. 잘못된 데이터는 추첨 전에 수정한다. 가중치를 최댓값으로 정규화하므로 큰 유한 가중치들의 합이 overflow하는 경우도 처리한다. 가중치를 극단적으로 작게 섞으면 double 표현/난수 해상도의 한계가 있으므로 실제 게임 설정은 읽기 쉬운 비율로 유지한다.

## 상태와 저장 책임

가중 선택도 기존 `weather` 난수 흐름을 한 번 진행시킨다. 같은 seed·흐름 상태·호출 순서·가중치 목록이면 재현할 수 있다. Core의 `core/random` writer는 난수 진행 상태를 저장하고, 실제 날씨·예고 큐·가중치 정책의 실행 상태는 Weather writer가 저장한다. 난수 저장만으로 전체 Weather 저장이 완성되는 것은 아니다.

## 확인한 범위

2026-11-05, Unity 6000.3.23f1의 격리 검사 **13/13, 종료 0**. 위 예제와 같은 선택 함수를 실제 API로 컴파일하고 확률 경계·0 제외·비율 동일성·큰 가중치·잘못된 데이터·권한/해제·저장 복원 후 선택열을 확인했다.

고정 seed 100,000회에서 비 **50,151**, 맑음 **29,807**, 천둥 **20,042**회였다. 이는 이 실행의 분포 확인이며 실제 Weather 콘텐츠가 이 예제 확률을 사용하거나 짧은 구간에서 정확한 비율을 보장한다는 뜻은 아니다. 검사 코드는 개인 검증 자료에만 보관한다.

이 작업은 기존 API 확인과 사용법 보강이다. 김진태의 Weather 선택 코드·예고 큐·날씨 수치를 수정하지 않는다.
