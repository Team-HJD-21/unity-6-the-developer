# 범용 Random API — 사용법과 기존 호출 교체 위치

기준: 2026-11-05. 범용 난수 함수·주입·권한·저장 상태의 사용법이다. 실제 Feature 연결은 [Feature 통합](core-feature-integration-guide.md)을 함께 확인한다.

**비 50%처럼 항목마다 빈도가 달라야 하면 [가중 Random 선택 예제](random-weighted-selection.md)를 먼저 확인한다.** `WeightedChoose`와 `WeightedIndex`는 이미 제공하며, 두 가지 성공/실패 판정에는 `Chance(0.5)`를 사용한다.

**Random을 사용하는 기능을 구현·교체하기 전에 이 문서의 주입 방법, 게임/연출 구분, 범위 규칙을 먼저 확인한다.** 함수 이름만 치환하면 Client의 독립 추첨이나 저장 상태 누락은 해결되지 않는다.

## 바로 사용하기 — 연결 한 줄, 기존 함수 그대로

기존 컴포넌트에 `IRandomConsumer` 인터페이스와 아래 필드/메서드를 추가한다. 새 부모 클래스를 상속하지 않는다. `RandomBinding`은 Core가 제공하는 **정적 헬퍼**이며 Feature마다 등록·해제용 클래스를 작성할 필요가 없다.

```csharp
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using UnityEngine;

public sealed class WeatherRandomExample : MonoBehaviour, IRandomConsumer
{
    private IRandomSource _random;

    public IDisposable BindRandom(IRandomStreams streams)
        => RandomBinding.Bind(streams, "weather", ref _random);

    public int DrawWeatherRoll(int totalWeight)
        => _random.NextInt(0, totalWeight);
}
```

1. 같은 Match Scene에서 활성화된 `IRandomConsumer`는 Core가 Host 초기화 때 자동으로 찾고 `BindRandom`을 호출한다. `Awake`에서 직접 호출하거나 `RandomService`를 새로 만들지 않는다.
2. `ref _random`은 헬퍼가 해당 필드에 사용할 source를 넣는다는 뜻이다. 위 메서드의 반환값은 Core가 보관한다.
3. 주입 이후 기존 Feature 실행 지점에서 `_random.NextInt(...)`, `_random.Chance(...)` 등을 호출한다. 첫 추첨을 `Awake`에 두지 않는다.
4. Match 종료·Scene 정리·조립 실패 때 Core가 바인딩을 해제한다. 위처럼 Scene 수명을 따르는 소비자에는 별도 `RandomBinding` 클래스나 해제 메서드를 만들지 않는다.

예제는 연결 방법이다. 별도 Weather 컴포넌트를 중복 배치하지 말고 담당자가 기존 컴포넌트에 적용한다. 기존 Weather/Enemy/터렛/UI 파일은 이번 변경에서 수정하지 않았다.

## 순수 함수로도 사용하기

계산 함수는 컴포넌트나 `IRandomConsumer`를 구현할 필요가 없다. 이미 받은 `IRandomSource`를 인자로 전달하면 된다. 확률 판정·목록 선택·셔플은 같은 방식으로 사용할 수 있다.

```csharp
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;

public static class CombatRolls
{
    public static bool IsCritical(IRandomSource random, double chance)
        => random.Chance(chance);
}
```

Feature에서는 `CombatRolls.IsCritical(_random, 0.1)`로 호출한다. 정적 함수인 `RandomBinding.Bind`와 확장 함수들은 공통으로 사용하되, 상태 원본은 전달받은 App/Match에 있다. 숨은 전역 Match를 검색하거나 게임·UI가 하나의 전역 난수 상태를 공유하지 않는다.

## 왜 도입했나

각 기능이 Unity의 전역 Random을 직접 사용하면 날씨, 적 생성, 포탄 오차, UI 흔들림이 같은 난수 진행 상태를 소비한다. 이 경우 HUD 효과의 호출 횟수를 바꾼 것만으로 다음 게임 추첨 결과도 달라질 수 있다. 공통 함수는 유지하면서 `weather`, `enemy/spawn-position`, `combat/cannon`처럼 이름별로 상태를 나눠 이 의존을 끊는다.

온라인 게임에서는 Host가 게임 결과를 결정해야 한다. Core에서 Match의 권한과 수명을 확인하고 Feature에는 필요한 source를 주입한다. Client가 같은 seed로 따로 추첨해 결과가 일치할 것이라고 가정하지 않는다. 로컬 HUD 효과는 App의 PresentationRandom을 사용한다.

안전장치로 게임을 복원할 때 시작 seed만 다시 넣으면 난수도 처음부터 시작한다. 이미 진행한 뒤의 상태를 함께 저장해야 다음 날씨/스폰 추첨부터 이어갈 수 있다. Core가 이름별 난수 원본을 저장하고, 각 Feature는 실제 날씨·예고 큐·생성 개체 등 자신의 원본을 저장한다.

또한 정수/실수 범위와 잘못된 가중치 처리 등을 공통 함수로 정하면 파트마다 다른 추첨 코드를 다시 만들 필요가 줄어든다. 고정 seed와 호출 순서로 재현할 수 있어 검사와 버그 추적에도 사용할 수 있다.

| Core가 맡는 것 | Feature가 맡는 것 |
| --- | --- |
| 난수 함수, seed와 이름별 진행 상태, 권한/수명, 난수 저장 writer | 날씨 가중치, 스폰 규칙, 공격 오차 등 실제 게임 규칙/데이터 |
| 게임/로컬 연출용 서비스 분리 | 전달받은 올바른 source 선택과 호출 시점 |

```mermaid
flowchart LR
    Match["Host Match · RandomService"] --> Weather["weather · 날씨"]
    Match --> Spawn["enemy/spawn-position · 생성 위치"]
    Match --> Cannon["combat/cannon · 발사 오차"]
    App["App · PresentationRandom"] --> HUD["ui/turret-shake · 로컬 연출"]
```

## 무엇을 쓰면 되나

모든 기능은 `IRandomSource`의 같은 함수 세트를 사용한다. 게임 판정용은 Host Match의 `IRandomStreams`, HUD 효과는 App의 `PresentationRandom`에서 받는다. 한 개의 전역 seed/진행 상태를 공유하는 방식은 아니다. 같은 API 아래에서 날씨·스폰·공격·로컬 연출을 분리해야 UI 호출 횟수 때문에 날씨 결과가 달라지지 않는다.

| 할 일 | 호출 | 범위/동작 |
| --- | --- | --- |
| 정수 범위 | `random.NextInt(0, 10)` | 0 이상 10 미만 |
| 실수 범위 | `random.NextFloat(-.5f, .5f)` / `NextDouble(min, max)` | 유한한 하한 이상, 상한 미만 |
| 0~1 실수 | `NextFloat()` / `NextDouble()` | 1 미만; double은 53비트 해상도 |
| 참/거짓 | `NextBool()` | 동일 확률 |
| 확률 판정 | `Chance(.25)` | 25%; 입력 0~1. 0/1은 추첨 없이 확정 |
| 목록 선택 | `Choose(values)` | 빈 목록 거부 |
| 가중치 인덱스 | `WeightedIndex(weights)` | 0 가중치 제외, 음수/NaN/무한/전부 0 거부 |
| 가중치 항목 | `WeightedChoose(values, weights)` | 항목/가중치 개수 동일 |
| 목록 섞기 | `Shuffle(list)` | 호출자 목록을 제자리에서 변경 |
| 원판 좌표 | `InsideUnitCircle()` | XY 원판 면적에 균일한 `(double X, double Y)` |
| 원시 난수 | `NextUInt32()` | 32비트 전체 범위. 일반 Feature는 위 함수 우선 |

확장 함수는 `using TeamHJD.Game.Application;`, 계약은 `using TeamHJD.Game.Contracts;`가 필요하다. 정수/실수 모두 `min < max`여야 한다. 잘못된 입력은 추첨 전에 거부한다. Unity의 float `Random.Range`는 양 끝을 포함하므로 **상한을 포함해야 하는 게임 규칙은 교체 시 별도로 검토**한다. [Unity 공식 Range 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Random.Range.html)

## Core가 연결·해제하는 시점

기존 Weather 컴포넌트에 `IRandomConsumer`를 구현하면 `InitialStageCompositionRoot`가 Host Match 조립 시 자동으로 `BindRandom`을 호출한다. 활성화된 같은 Scene의 소비자만 대상이다. 이때 바인딩은 환경 Feature의 `BindAuthority`보다 먼저 이루어진다.

- `Awake`에서 먼저 날씨를 뽑지 않는다. 난수 주입 뒤, 권위 환경 바인딩이 준비된 시점에 최초 선택/예고 큐를 생성한다.
- Unity의 `Start`도 난수 주입 완료를 보장하는 신호가 아니다. `BindRandom`은 source 연결만 담당하고, Weather의 최초 선택은 이후 Core가 호출하는 환경 `BindAuthority` 등 실제 Feature 시작 지점에서 한다.
- Client는 권위 난수를 받지 않는다. Client의 `Start`에서 별도로 날씨를 뽑지 않고 Host 환경 snapshot을 표시한다.
- `"weather"`는 초기화 때 한 번 받는다. 난수마다 새 seed를 만들거나 프레임/InstanceId로 이름을 만들지 않는다.
- Match 종료/조립 실패 시 Core가 반환 lease를 Dispose한다. 이미 보관한 source도 권한 상실/Match 종료 후에는 사용할 수 없다.
- 나중에 생성된 동적 Feature는 Scene 조립부가 `BindRandom`을 직접 호출하고 그 lease를 소유한다. 기존 Scene 검색이 뒤늦게 생성된 객체를 자동 연결하지는 않는다.
- 직접 조회가 필요한 Core 조립부에는 `InitialStageCompositionRoot.Random` / `MatchSession.Random`이 있다. 초기화 전과 Client에서는 null이다. Feature 안에 매 프레임 전역 검색을 넣지 않는다.

### 해제·재연결 규칙

`RandomBinding.Bind`는 `IDisposable`을 반환한다. Scene 자동 연결에서는 Core가, 직접 생성한 소비자에서는 해당 조립부가 이 값을 보관하고 해제한다.

- 해제된 필드는 **null로 바뀌지 않는다.** 남은 참조의 `IsAvailable`은 false가 되고 추첨은 거부된다. 상태를 확인해야 하면 `_random != null && _random.IsAvailable`을 사용한다. `_random != null`만으로 준비 여부를 판단하지 않는다.
- 다른 곳에 복사한 참조도 함께 무효화한다. 해제한 바인딩으로 새 Match의 난수를 소비할 수 없다.
- 같은 필드가 연결 중이면 중복 등록을 거부한다. 이전 연결을 해제한 뒤에는 같은 필드로 다시 `Bind`할 수 있다. 예전 반환값을 다시 Dispose해도 새 연결에는 영향이 없다.
- 등록 실패 시 기존 필드는 바뀌지 않는다. 반환된 바인딩의 해제는 해당 연결만 닫으며 공용 RandomService나 다른 소비자를 종료하지 않는다.

### 직접 조립하는 경우

순수 C# 객체나 Match 중간에 생성한 Feature는 자동 Scene 검색 대상이 아니다. 소유 조립부가 같은 정적 헬퍼를 호출하고 반환값을 자신의 Scene/객체 수명에 맞춰 해제한다. 아래는 짧은 작업의 예이며, 계속 살아 있는 객체에 전달했다면 작업 직후가 아닌 객체 해제 시점까지 보관한다.

```csharp
IRandomSource random = null;
using (IDisposable binding = RandomBinding.Bind(streams, "enemy/spawn-position", ref random))
{
    var offset = random.InsideUnitCircle();
    // offset을 해당 작업의 위치 계산에 사용한다.
}
// random.IsAvailable은 false. 다른 소비자의 source와 streams는 계속 사용할 수 있다.
```

## 다른 기능에서도 같은 함수 사용하기

모든 예제의 `streams`는 조립부가 주입한 `IRandomStreams`다. `RandomService`를 Feature마다 새로 만들지 않는다. 확장 함수를 쓰는 파일에는 아래 두 using을 추가한다.

```csharp
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;

// 아래 streams는 조립부가 주입한 Match 난수.
IRandomSource spawn = streams.GetStream("enemy/spawn-position");
var offset = spawn.InsideUnitCircle();
Vector3 position = point.Position
    + new Vector3((float)offset.X, (float)offset.Y, 0f) * point.SpawnRadius;

IRandomSource cannon = streams.GetStream("combat/cannon");
float scatter = cannon.NextFloat(-.5f, .5f);
bool critical = cannon.Chance(.1);
```

목록/가중치/셔플은 다음처럼 사용한다. 가중치는 상대 비율이며 아래 예제는 Sunny:Rain:Thunder가 3:1:1이다. 실제 날씨 가중치 데이터는 Weather가 소유한다.

예제 배열은 설명용이다. 반복 추첨에서는 Feature의 기존 데이터/필드 목록을 전달해 재사용한다.

```csharp
IRandomSource random = streams.GetStream("feature/example");
string[] weatherIds = { "sunny", "rain", "thunder" };
double[] weights = { 3d, 1d, 1d };

string uniformChoice = random.Choose(weatherIds);
string weightedChoice = random.WeightedChoose(weatherIds, weights);
bool succeeded = random.Chance(0.25); // 25%

int[] order = { 0, 1, 2, 3 };
random.Shuffle(order); // 원본 배열의 순서를 변경
```

HUD 흔들림은 Core/UI 조립부가 AppRoot 인스턴스 `app`의 `PresentationRandom`에서 source를 받아 전달한다. 정적/Singleton 호출이 아니다.

```csharp
// app은 조립부가 소유한 AppRoot 인스턴스.
IRandomSource uiRandom = app.PresentationRandom.GetStream("ui/turret-shake");
float offsetX = uiRandom.NextFloat(-1f, 1f) * shakeAmount;
float offsetY = uiRandom.NextFloat(-1f, 1f) * shakeAmount;
```

App 연출 난수는 App 수명이며 게임 체크포인트에 저장하지 않는다. 게임 판정은 `MatchConfig.RandomSeed`로 생성된 Match 서비스를 사용한다. 동일 이름은 같은 상태를 공유하므로 기능 안의 호출 순서는 결과에 영향을 준다.

### 교체 전 확인

- 게임 판정인가, 로컬 연출인가? 게임은 Host Match, 연출은 App 서비스를 사용한다.
- source를 초기화 때 `RandomBinding.Bind`로 연결했는가? 매 추첨마다 재등록/전역 검색하지 않는다.
- 최초 추첨이 BindRandom 이후인가? Client에서 권위 결과를 다시 추첨하지 않는다.
- 범위의 상한 제외와 가중치 0 처리에 기존 게임 규칙이 맞는가?
- 저장 대상이면 Core 난수 writer와 해당 Feature 원본 writer를 함께 등록했는가?
- Match 종료/교체 시 바인딩을 해제하고 이전 source를 계속 사용하지 않는가?

### 호출이 안 될 때 확인하기

| 증상 | 확인할 것 |
| --- | --- |
| `_random`이 null | Core의 BindRandom 이전인지, 소비자가 같은 Match Scene에서 활성화돼 있는지 확인한다. 중간 생성 객체는 소유 조립부가 직접 연결한다. |
| `IsAvailable`이 false | 해제된 연결이거나 Host 권한/Match 수명이 끝난 상태다. Client가 로컬 난수로 게임 판정을 대신하지 않는다. |
| 이미 연결됐다는 오류 | 자동 연결 외에 Awake/Start 등에서 중복 등록하지 않았는지 확인한다. 기존 반환값은 소유 조립부에서 해제한다. |
| Chance/Choose가 보이지 않음 | `using TeamHJD.Game.Application;`을 추가한다. |
| 범위/가중치 오류 | `min < max`, 빈 목록 아님, 가중치 개수 동일, 0보다 큰 가중치가 하나 이상인지 확인한다. |

## 저장/복원 연결

난수 생성기와 진행 상태는 Core가 소유한다. Weather는 실제 날씨·예고 큐·남은 전환 시간만 자신의 writer로 저장한다.

저장 조립부는 `scene.CreateRandomCheckpointParticipant()` 또는 `session.CreateRandomCheckpointParticipant()`를 실제 Feature writer 목록에 추가하고, 필수 목록에도 `"core/random"`을 포함한다. 기존 `AppRoot.CreateCheckpointScope(runtime, participants, requiredKeys)`로 조립한다. **이 writer 하나로 전체 게임 저장이 되는 것은 아니다.** Weather/Enemy/Player 등 필요한 원본 writer도 함께 등록한다.

```csharp
ICheckpointParticipant randomWriter = scene.CreateRandomCheckpointParticipant();
participants.Add(randomWriter);
requiredKeys.Add(randomWriter.Key); // core/random
// 이후 기존 CreateCheckpointScope 호출에 participants/requiredKeys 전달
```

난수 writer는 schema 1 / World / RestoreOrder 0이다. 알고리즘 버전·seed·이름별 상태를 기록한다. 준비 단계에서 입력 전체를 검증하고 적용/백업 복구 단계에서만 변경한다. 같은 seed의 새 Match로 이어갈 수 있고, 이미 받은 source 참조도 복원된 상태를 따른다. 저장 이후 생성된 흐름은 초기 상태로 되돌아간다. 오래된 저장에 필수 `core/random`이 없으면 조용히 새 난수를 시작하지 않고 기존 체크포인트 검증에서 거부한다.

정해진 자동 저장 사건은 추가하지 않았다. PCG32는 게임용 의사 난수이며 인증 토큰/백엔드 보상 검증용 난수가 아니다. 전체 AI/물리의 결정적 리플레이를 제공한다는 의미도 아니다.

## 기존 호출부 위치와 연결 참고

아래는 최초 API 도입 시 조사한 **6개 파일, 7개 호출부**의 위치/목적이다. 이후 캐논은 `combat/field-turret`, Enemy 배치는 `encounter/placement`로 연결했다. 표의 과거 줄 번호·이름을 현행 구현 상태로 읽지 않고 [실제 Feature 통합](core-feature-integration-guide.md)과 현재 소스를 함께 확인한다. Weather의 난수·예고 큐 정책은 World/Weather 담당 범위로 남는다.

| 담당/파일 | 현재 사용 위치와 목적 | 이후 교체 |
| --- | --- | --- |
| World/Weather · `Assets/Scripts/Map/WeatherManager.cs:115` | `GetWeather()`의 정수 추첨. sunnyWeight에 따른 날씨/예고 큐 선택 | Match `weather` 흐름의 `NextInt(0, sunnyWeight + (int)Weather.WEATHER_MAX)`. 기존 가중치 해석 유지 |
| Enemy/Encounter · `Assets/Scripts/Features/Enemy/Encounter/Spawning/EnemySpawnExecutor.cs:289` | 각 적을 SpawnPoint 반경의 원판 안에 분산. `Random.insideUnitCircle` | Host Match `enemy/spawn-position` 흐름의 `InsideUnitCircle()`을 Vector2/3로 변환. 기존 반경·스폰 지점 배분 유지 |
| FieldTurret · `Assets/Scripts/Features/FieldTurrets/Cannon/FieldCanonTurretLv1.cs:29` | `Shoot()`에서 발사 기준점 X에 -0.5~0.5 가산 | Match `combat/cannon` 흐름의 `NextFloat(-.5f, .5f)` |
| FieldTurret · `Assets/Scripts/Features/FieldTurrets/Cannon/FieldCanonTurretLv2.cs:34` | `Shoot()` 루프에서 각 발사 방향 Transform X에 오차 가산 | 같은 API. 포탄마다 추첨하는 기존 호출 횟수/순서 확인 |
| FieldTurret · `Assets/Scripts/Features/FieldTurrets/Cannon/FieldCanonTurretLv3.cs:31` | Lv2와 같은 발사 방향 오차 | 같은 API. 공유 흐름 또는 안정적 논리 ID별 흐름은 해당 파트와 결정 |
| Player/UI · `Assets/Scripts/UI/Turrets/TurretInteractionUI.cs:233–234` | UI 흔들림의 X/Y 두 차례 실수 추첨 | App `ui/turret-shake` 흐름의 `NextFloat(-1f, 1f)` 두 번. Match 난수 사용 금지 |

`FieldLaserTurret.cs:79`의 난수 코드는 이미 주석이라 현재 실행 교체 대상이 아니다. `Guid.NewGuid()`로 만드는 Match/요청/등록 ID와 seed를 전달·저장하는 필드도 게임 추첨 함수가 아니므로 이번 교체 목록에 포함하지 않았다. Core 난수 검사의 Unity Random 사용은 전역 상태를 훼손하지 않는지 확인하기 위한 검사이며 시작 상태를 복구한다.

현재 Cannon은 Transform의 기존 X에 오차를 누적한다. **난수 API를 바꾸면서 이 조준 동작까지 임의로 고치지 않는다.** 해당 파트가 기준점/발사 방향 의도를 확인한 뒤 별도 변경한다. 원판 좌표로 흩뿌리는 것과 어떤 SpawnPoint를 선택/배분하는지도 별개 책임이다.

## 구현 위치와 검증

- `Contracts/Randomness`: IRandomSource / IRandomStreams / IRandomConsumer.
- `Application/Randomness`: 정적 연결 헬퍼 RandomBinding / RandomService / RandomExtensions / RandomConsumerScope / PCG32 내부 구현.
- `Domain/Randomness`: 버전·seed·이름별 불변 상태.
- `Application/Persistence/RandomCheckpointParticipant.cs`: 기존 저장 실행기 연결.
- `InitialStageCompositionRoot`: Match 서비스 생성/Feature 주입. `AppServices`: 로컬 연출 서비스 생성.

PCG32 초기화·상태 전이·정수 rejection sampling은 [공식 minimal 구현](https://www.pcg-random.org/download.html)을 C#으로 이식했다. 이름은 고정 FNV-1a로 해시하여 실행별 string.GetHashCode 차이를 피한다. 출처와 Apache-2.0 전문은 [라이선스 파일](../licenses/pcg32-LICENSE.txt)에 포함했다. stream 이름은 소문자 ASCII 1~128자, scope당 최대 256개다.

삭제 전 검수 커밋 `e87bdf91`에서 고정 참조 수열, 음수/전체 int 폭, 인접 실수의 상한 제외, 가중치 검증, 원판 분포, 이름별 독립성, 권한/Dispose, snapshot 복원/백업, 기존 저장 실행기의 왕복을 확인했다. 이후 PL 요청으로 이번 API에 추가했던 임시 테스트 7개와 .meta는 최종 PR에서 제외했다. 실행 결과는 삭제 전 검증 기록이며, 실제 Weather/Enemy/터렛/HUD 교체 검증은 각 파트가 위 연결을 적용한 뒤 진행한다.

11월 5일 공용 바인딩 헬퍼: 함수 전달 호환, 중복 연결 차단, 복사한 이전 참조 해제, 같은 필드 재연결, Host 권한/서비스 종료와 조립 실패 정리를 10/10 검증했다. 이 문서와 README의 C# 예제 8개는 실제 Unity/Core DLL을 참조해 컴파일했다. 검증용 테스트는 개인 자료에 보관하며 최종 PR에 추가하지 않는다.
