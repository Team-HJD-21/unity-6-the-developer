# 명시적 터렛 생성·초기화

`TurretInitializationData`는 새 게임 또는 향후 로드 경로에서 사용할 **해석된 생성 입력**이다. 조회용 `TurretSnapshot`과 다르며, 파일 저장 형식이 아니다. 저장 파일의 Definition/Upgrade ID를 실제 에셋으로 찾는 책임은 향후 로드 담당자에게 있다.

## 공통 생성 경로

```csharp
var initial = TurretInitializationData.CreateDefault(
    canonPrefab.Definition, isLocked: false, shouldActivate: true);

TurretBase turret = TurretFactory.Create(
    canonPrefab, initial, controller, position, rotation, parent);
```

나중에 로드된 값으로 입력을 만들 때도 같은 생성 경로를 사용한다.

```csharp
var initial = new TurretInitializationData(
    prefab.Definition, currentHealth: 37, isLocked: false,
    shouldActivate: true, damageBonus: 0,
    upgrade: resolvedUpgrade, upgradeLevel: 2);
```

- 위치·회전·부모는 생성 함수의 인자다. 부모는 활성 상태여야 한다.
- Definition은 선택한 프리팹의 Definition과 일치해야 한다. 잘못된 체력·업그레이드 단계·호환성은 생성 전에 거부한다.
- 초기 체력 0은 파괴 상태를 뜻한다. 로드가 파괴된 터렛을 자동 복구하지 않는다.
- 상태 적용은 비활성 객체에서 첫 `Awake` 전에 준비한다. `Awake`가 공통 초기화를 수행하고, `OnEnable`은 완성된 상태를 등록한 뒤 활성화 의도를 처리한다.
- 활성화는 다음 프레임의 기존 `RequestActivation(true)` 경로로 요청한다. `ShouldActivate`는 의도이지 예약 성공 보장이 아니다. 잠금·파괴·전력 부족은 기존 결과로 거부되며 `LastInitialActivationResult`에서 확인한다.
- 초기화 직후 명시적인 ON/OFF 요청, 잠금, 비활성화는 대기 중인 초기 활성화를 취소한다. 비활성화 후 다시 켜도 과거 의도를 재실행하지 않는다.
- 체력·업그레이드가 적용된 뒤 전력 비용을 계산한다. `_isActivated`만 복사하거나 과거 예약을 그대로 가져오지 않는다.
- 기존 씬/프리팹 배치는 그대로 지원한다. 직접 배치한 터렛도 초기 상태 적용 → 등록 → 초기 활성화 순서로 동작한다.

## 수명주기를 별도로 조립하는 소비자

새 비활성 인스턴스에 `PrepareInitialization(data, source)`를 한 번 호출한 다음 활성화할 수 있다. 이미 초기화됐거나 활성 상태인 터렛에는 적용할 수 없다. 기본 경로는 `TurretFactory.Create`이며, 구체 터렛의 `Awake`는 반드시 `ConfigureSceneActivation()`을 호출해야 한다. 현재 Canon/Missile은 이 계약을 따른다. 기존 Laser는 이 경로를 지원하지 않는다.

## 다른 복원 동작과의 구분

- `CopyForLevelChange`: 승급/강등용 런타임 ID·체력 비율·업그레이드 이전. 저장 입력 적용으로 대체하지 않는다.
- `Restore()`: 파괴된 터렛을 최대 체력으로 수리하는 게임 명령. 세이브 로드가 아니다.
- `Initialize(data)`: 생성 시 정확한 체력·잠금·업그레이드 적용. 새 런타임 ID가 등록 단계에서 배정된다.

## 아직 구현하지 않은 것

JSON/파일 저장, 슬롯·버전·마이그레이션, 영구 ID, Research/Profile 해금 정책, 전체 Match 복원은 포함하지 않는다. 과열/냉각 타이머·발사체·타깃 예약·전력 반환 대기 상태도 복원하지 않는다. 향후 중간 전투 이어하기가 필요하면 따로 저장 범위를 설계해야 한다.

`RuntimeState`의 `[NonSerialized]`는 그대로 유지한다. 저장할 때는 필요한 의미 있는 값들을 별도 저장 데이터로 추출한다.

## 회귀 테스트

`Assets/Scripts/Tower/Turret/Tests`에 초기 입력 검증, 부분 체력·업그레이드 재계산, 잠금·파괴 거부, 재초기화 방지, 승급 상태 이전, 초기 등록 순서와 자동 활성화 취소를 검사하는 Unity 테스트가 있다. 코루틴/Factory 테스트는 **PlayMode**에서 실행해야 한다.
