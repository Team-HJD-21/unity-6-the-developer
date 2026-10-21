# Initial Stage Player HUD API

최종 갱신: 2026-10-15
상태: 프리팹 기반 UI 구현. 최종 PlayMode 외형·조작 확인은 별도.

API 기준: main `8d08001a`의 PR #525 냉각·수동 조작 제한 계약이다.
PR #522 리뷰에 맞춰 HUD를 연결했다. 스크립트와 프리팹은
`InitialStagePlayerHUD`, UI 약어는 `UI`로 표기를 통일한다.

[문서 목차](../README.md) · [터렛 API](turrets/api-reference.md)

## 10월 15일 CU·지역 표시 추가

`StageControlUnitView`를 기존 HUD 프리팹 우측 하단에 추가했다. 정적/동적 정책과 점령 셀 수를 `TurretController.Regions`에서 읽는다. Dynamic은 지역 잠금이 없고 활성화로 점령한다는 안내를 표시한다. 터렛 선택창에도 동적 점령 안내를 표시한다.

살아 있는 Player가 CU와 interactionDistance(현재 10) 이내에 있고 조작 가능한 상태이면 자동 활성화 버튼을 사용할 수 있다. 버튼은 D의 `TryStartAutoActivation()`을 호출하며 진행 중/재실행 대기/전력·냉각 규칙을 새로 구현하지 않는다. 프리팹·실제 Scene Button.onClick 검사는 통과했다. 사람의 마우스 조작·해상도별 외형은 별도다.

현재 이 입력은 로컬 경로다. 원격 Client 전송과 하드코딩된 local-player를 실제 참가자/actor 주입으로 바꾸는 작업은 아직 남아 있다. [Core 연결 API](core-sprint-region-and-request-api.md)를 함께 확인한다.

## 적용 위치와 편집 방법

`Assets/Scenes/Gameplay/Initial_Stage.unity`에서
`Assets/Prefabs/UI/InitialStagePlayerHUD.prefab`을 사용한다.
원본 Stage 1과 `TurretInteractionUI`는 변경하지 않는다.

상태 UI, 터렛 창, 텍스트, 버튼과 화살표는 모두 프리팹에 미리 배치되어 있다.
런타임에 UI 오브젝트를 만들거나 이름으로 UI 참조를 찾지 않는다.
위치·크기·색상·폰트·스프라이트·앵커는 Prefab Mode에서 편집한다.
체력 칸의 위치·크기도 프리팹에 저장하며 코드에서 Layout을 끄거나 재배치하지 않는다.

```text
InitialStagePlayerHUD (Canvas)
├─ UIPlayerHp
│  ├─ PImg
│  ├─ PHpBar / AlignSet / Cell...
│  └─ PlayerHealthText
├─ UICUHp
├─ UICUPower
│  └─ PowerDetailsText
└─ TurretControlPanel
   ├─ NameText
   ├─ LevelControl
   │  ├─ LevelText
   │  ├─ UpButton / Arrow / Left Stroke, Right Stroke
   │  └─ DownButton / Arrow / Left Stroke, Right Stroke
   ├─ ActivationButton
   ├─ StatsText
   ├─ StatusText
   ├─ FeedbackText
   ├─ LowPowerText / LowPowerMinus / LowPowerPlus
   ├─ HighFirepowerText / HighFirepowerMinus / HighFirepowerPlus
   ├─ UpgradeRestrictionText
   └─ CloseButton
```

Canvas는 편집 중 숨기도록 프리팹에서 비활성화한다. 디자인을 미리 볼 때 Prefab Mode에서
Canvas를 잠시 켜고, 터렛 창은 해당 GameObject를 잠시 켠다. 미리 보기 후 초기 비활성 상태로
돌려둔다. 플레이 시 `InitialStagePlayerHUD.OnEnable`이 Canvas를 켜며 터렛 창은 선택 시에만
표시한다. CanvasScaler는 프리팹에서 1920×1080, 화면 비율 혼합 0.5로 설정한다.
현재 Scene의 EventSystem과 InputSystem UI 모듈을 사용한다.

## Inspector 연결

- Canvas의 `InitialStagePlayerHUD`: `vitalsView`, `turretView`, `levelCatalog`,
  `interactionDistance`(기본 10 Unity 단위).
- Canvas의 `StagePlayerVitalsView`: 체력 칸 Image 배열, 플레이어 체력 Text,
  CU 체력 Fill/Text, 전력 Fill/Percent/Details Text 및 체력 칸 색상.
- TurretControlPanel의 `StageTurretControlView`: 제목·레벨·수치·상태·결과·활성화 Text,
  Activate·Up·Down·Close Button, `upgradeView`, `upgradeRestrictionText`와 활성화 상태 색상.
- 같은 패널의 `StageTurretUpgradeView`: 두 강화 분기의 Text·Plus·Minus Button,
  `lowPowerUpgrades`와 `highFirepowerUpgrades`. 기본 배열에는 Canon/Missile의 기존
  UpgradeDefinitions 에셋을 연결한다. 선택 터렛 Definition ID와 호환되는 에셋을 사용한다.
- Arrow의 `StageLevelArrowGraphic`: 왼쪽·오른쪽 Stroke RectTransform과 위/아래 방향.
  두 Stroke는 프리팹에 배치된 기본 Image다.

Button의 Inspector OnClick에 요청 메서드를 중복 연결하지 않는다. View가 OnEnable에서
구독하고 OnDisable에서 해제한다. 필수 참조가 누락되면 조립 진입점이 오류를 남기고
해당 기능만 중단한다. `TryInitializeVitals`와 `TryInitializeTurretControls`가
각각 참조를 검사한다. 터렛 창·강화 참조·레벨 카탈로그가 빠져도 체력·전력 표시는
동작하며, 상태 View가 잘못 연결돼도 정상 연결된 터렛 조작은 동작한다.
둘 다 사용할 수 없으면 Canvas를 숨긴다. 오류 로그에는 누락된 필드 이름과
해당 컴포넌트가 포함되며, `OnValidate`에서도 편집 중 누락을 경고한다.
참조를 수정한 뒤 컴포넌트를 다시 활성화하면 초기화 검사를 다시 수행한다.

## 스크립트별 책임

파일 위치: `Assets/Scripts/UI/Turrets/`

| 스크립트 | 책임 / 호출 조건 |
| --- | --- |
| `InitialStagePlayerHUD` | Canvas·CanvasScaler·GraphicRaycaster 필수. 상태 표시와 터렛 조작을 개별 초기화하고 정상 기능만 갱신한다. 플레이어 참조는 View와 독립적으로 관리한다. 생성이 늦은 데이터는 0.5초마다 재탐색한다. |
| `StagePlayerVitalsView` | 상태 표시 MonoBehaviour. `BindPlayer`, `BindControlUnit`, `BindPower`로 이벤트를 연결하고 `Unbind`로 해제한다. 데이터 값이 바뀐 경우에만 체력 칸·전력 텍스트를 갱신한다. |
| `StageTurretSelection` | 클릭·빈 공간·Escape 입력, 씬 소속 터렛 ID 유지와 사거리 표시. `CanSelect`는 거리와 무관하게 같은 씬의 활성·등록 터렛을 허용한다. `CanInteractWith`는 조작 가능 거리까지 검사한다. `Resolve`로 교체된 인스턴스를 조회하고 `Clear`로 표시를 해제한다. UI 위 클릭은 제외한다. |
| `StageTurretControlView` | 프리팹의 Text/Button 참조를 보관하고 표시·버튼 상태·입력 이벤트만 담당한다. `Show`, `Hide`, `SetFeedback`을 제공한다. |
| `StageTurretPresenter` | 선택 변경 및 SnapshotChanged에 따른 갱신, 활성화·레벨·강화 API 호출과 결과 표시. 요청 시에도 거리와 조작 허용 조건을 검사한다. `Dispose`로 이벤트를 해제한다. |
| `StageTurretUpgradeView` | 호환되는 두 강화 분기의 단계와 버튼 상태를 표시하고 입력 이벤트를 전달한다. API 명령을 실행하지 않는다. |
| `StageTurretInteractionRules` | 표시와 클릭 직전 검증이 공유하는 활성화·강화 조작 조건. 강화 분기·최대 단계와 최신 API 허용 상태를 검사한다. |
| `StageTurretUIText` | 기존 터렛 API 결과를 영어 문구로 변환한다. |
| `StageLevelArrowGraphic` | 프리팹에 배치된 두 선의 회전으로 화살표 방향을 맞춘다. 오브젝트를 생성하지 않는다. |
| `StageUIReferenceValidation` | 필수 참조 조건으로부터 누락된 Inspector 필드 이름을 수집한다. 각 View의 `IsConfigured`와 `MissingReferences`가 같은 검사 기준을 사용한다. |

## 데이터와 동작

- 플레이어 체력 원본은 `PlayerInfo.curHp/maxHp`와 `onHpChange`다.
  같은 씬의 `PlayerActorIdentity.PlayerId`가 `local-player`인 활성 PlayerInfo를
  우선 연결한다. Identity 없는 기존 구성은 활성 PlayerInfo로 대체한다.
  대체 대상을 연결한 뒤에도 로컬 actor를 재탐색하며 생성되면 연결을 전환한다.
  UI 활성화 직후 연결 전에는 Waiting for player와 CU 체력 미연결 표시를 사용한다.
  null 대상을 다시 바인딩하는 경우에도 샘플 체력 값이 남지 않도록 갱신한다.
  다중 플레이어에서는 로컬 actor를 명시적으로 주입하는 연동이 추가로 필요하다.
- CU 체력은 `ControlUnitHealth.CurrentHealth/MaxHealth`와 `HealthChanged`를 사용한다.
  최초 연결은 Start 이후에 진행하여 다른 컴포넌트의 Awake 초기화를 기다린다.
- 전력은 `TurretController.Budget`의 CurrentPower·MaximumPower·PendingRecovery를
  읽는다. 전력 계산은 기존 API가 소유하며 UI는 예약이나 반환 수치를 직접 변경하지 않는다.
  PowerChanged 이벤트와 0.1초 표시 갱신으로 알림 없이 바뀌는 반환 대기 값도 반영한다.
- 터렛 표시 갱신은 선택·상태·조작 권한 변경 또는 0.1초 표시 주기에 수행한다.
  냉각 시간은 주기적으로 갱신하고 상태 변경은 SnapshotChanged로 알림을 받는다.
- 일시정지, 플레이어 사망, `PlayerControlState.CanAct`가 false인 상태와
  UI 비활성화 중에는 선택·활성화·레벨 변경 요청을 막는다.
- UI를 비활성화하면 선택·사거리를 해제하고 창과 Canvas를 숨기며 이벤트를 해제한다.
  다시 켜면 Presenter와 데이터 연결을 복구한다.
- 활성화는 컨트롤러의 ID 기반 RequestActivation으로 요청한다.
  이 API는 다른 전력 공급자를 사용하는 터렛에 대한 요청도 거부한다.
  레벨 변경은 기존 컴포넌트 요청 API를 사용한다. 현재 Initial의 씬당 단일 컨트롤러
  구성을 대상으로 하며 다중 컨트롤러의 전체 명령 계약은 별도 연동 대상이다.
- 레벨 변경 후 InstanceId로 새 터렛을 조회해 선택·사거리를 유지한다.
- API의 `일시 중단`을 무조건 과열로 해석하지 않는다.
  현재 상태와 마지막 요청 결과를 분리하며, 다른 터렛을 선택하면 이전 결과를 지운다.
- UI 문구·Stage 1 스프라이트·NEXON 폰트·중앙 Activate/Deactivate 버튼·Level 옆 화살표는
  기존 외형을 유지한다. 네트워크 테스트 패널과의 겹침 정리는 후속 작업이다.
- 플레이어는 잠금 상태를 조회만 한다. 구역 해금은 Core의 게임 규칙이 관리하며
  플레이어 UI에는 자물쇠 버튼이나 `SetLocked` 명령이 없다.
- ON은 잠금·파괴·냉각·자동 활성화 진행 중에 차단한다. 켜진 터렛의 OFF는
  과열 냉각 중에도 가능하나 자동 활성화 진행 중에는 차단한다.
  상태 표시에는 비활성 이유와 `CoolingDownRemaining`을 사용한다. 비활성 버튼을
  클릭해야만 이유가 표시되는 방식이 아니다.
  자동 활성화와 냉각이 동시에 진행되면 상태·강화 안내에 두 조건을 함께 표시한다.
  예: `Automatic activation - manual controls disabled | Cooling 3.2s`.
  냉각이 끝나면 냉각 문구만 사라지고, 자동 활성화가 진행 중이면 수동 조작 제한은 유지한다.
- 프리팹 레벨 변경과 세부 강화는 `CanModifyUpgrades`를 기준으로 허용한다.
  활성화·냉각·자동 활성화 중에는 차단하고, 제한 사유를 강화 안내에 표시한다.
- Low Power / High Firepower는 `ApplyUpgrade`와 `DowngradeUpgrade`로 조정한다.
  단계는 0~5다. 0일 때 둘 중 하나를 고르고, 이후에는 선택한 분기만 변경한다.
  0까지 되돌리면 다른 분기를 선택할 수 있다. 버튼 표시와 요청 직전에 모두 검사하고
  API 결과 enum을 처리한다. 수치 계산·전력 예약·강화 상태 변경은 API에 맡긴다.
- 터렛 클릭·선택에는 플레이어와의 거리 제한이 없다. 클릭한 위치에 겹치는 Collider2D의
  부모 터렛 중 같은 씬의 활성·등록 터렛을 선택한다. 빈 공간·Escape·Close로 선택을 해제한다.
- 플레이어와 터렛 중심 사이의 XY 거리가 `interactionDistance`(기본 10)보다 멀면
  선택·사거리·창을 유지하며 기존 Canvas의 피드백 줄에 `Move closer to control this turret.`를 표시한다.
  마지막 API 요청 결과가 있으면 접근 안내와 함께 유지하며, 현재 상태·냉각 안내도 별도로 유지한다.
  활성화·비활성화·레벨·강화 버튼을 비활성화하고, 요청 직전에도 같은 거리 조건을 재검사한다.
  범위 안으로 접근하면 다시 클릭하지 않아도 거리 안내가 사라지고 기존 API 조건에 따라
  버튼이 활성화된다. 멀어지면 창을 유지한 채 다시 조작을 차단한다. Z 좌표는 거리 계산에서 제외한다.

## 기존 시작 설정

앞선 UI 기능 커밋에서 Initial 씬은 최대 전력 100, 배치 터렛은 비활성 상태로
설정되었다. 초기 작동 터렛이 없으므로 전선·영역은 이후 터렛을 켠 배치에 따라 갱신된다.
이번 리뷰 대응에서 이 Scene 시작 설정은 변경하지 않는다. 터렛 API는
main에 병합된 PR #525의 냉각·조작 제한 변경을 사용한다.

## 확인 순서

1. InitialStagePlayerHUD 프리팹의 두 View 컴포넌트와 모든 Inspector 참조를 확인한다.
2. 플레이 시 체력·전력 표시, 터렛 클릭과 사거리 표시를 확인한다.
3. Activate/Deactivate 후 냉각 완료를 기다리고 LV1→LV2→LV3→LV2→LV1 변경과 동일 ID 선택 유지를 확인한다.
4. 빈 공간·Escape·Close, 잠금·파괴·전력 부족 처리를 확인한다.
5. 플레이어 사망·입력 차단·일시정지에서 버튼 요청이 막히는지 확인한다.
6. UI 루트 컴포넌트 비활성화·재활성화 후 표시·사거리·이벤트 중복 여부를 확인한다.
7. Prefab Mode에서 위치·폰트 등을 변경할 수 있고 실행 코드가 이를 덮어쓰지 않는지 확인한다.
8. Scene의 UI 인스턴스에 체력 칸 너비·위치 Override가 남아 프리팹의 배치를 덮어쓰지
   않는지 확인한다. 체력 바가 사라지는 원인이었던 예전 체력 칸 Override는 제거했다.
9. 자물쇠 조작이 없는지 확인하고, Core가 잠근 터렛의 Activate와 강화가 제한되는지 확인한다.
10. 켜진 터렛과 냉각 중 터렛의 레벨·강화 버튼이 비활성인지, 냉각 종료 시 별도 클릭 없이
    다시 활성화되는지 확인한다. 과열 중 OFF는 가능해야 한다.
11. 각 강화 분기를 0→5→0으로 조정한다. 다른 분기 선택 제한, 최대 단계,
    0에서 분기 초기화 및 교체 후 강화 상태 승계를 확인한다.
12. 기본 거리 10 밖의 터렛을 클릭하면 창·사거리·접근 안내가 표시되고 조작 버튼은 비활성인지 확인한다.
    선택을 유지한 채 범위 안으로 접근하면 안내가 사라지고 조작 가능 상태로 바뀌는지,
    다시 멀어지면 창은 유지되고 버튼만 차단되는지 확인한다. 잠금·냉각 등 기존 제한은 계속 적용돼야 한다.
13. 자동 활성화 진행 중 모든 수동 변경 버튼과 직접 요청이 차단되는지 확인한다.
    동시에 냉각 중이면 상태·강화 안내에 남은 시간이 함께 표시되고, 냉각 종료 후에도
    자동 활성화가 끝나기 전까지 조작 제한 안내가 유지되는지 확인한다.
14. 레벨 카탈로그 또는 터렛 버튼 참조를 임시로 제거하고 실행한다. 누락 필드 오류가
    표시되며 터렛 창만 중단되고 체력·전력 표시가 계속 갱신되는지 확인한다.
15. 상태 View 참조를 임시로 제거해도 터렛 조작이 독립적으로 초기화되는지 확인한다.
    참조 복구 후 컴포넌트 재활성화 시 정상 초기화되고 이벤트가 중복되지 않는지 확인한다.

## 이후 갱신 기준

플레이어 연동 스크립트를 추가·수정하면 같은 변경에서 책임·호출 조건·연동 API를
갱신한다. 자동 활성화 시작 버튼과 최종 UI 연출은 이번 범위에 포함하지 않는다.
HUD 요소를 각각 별도 프리팹으로 관리하는 구성은 리뷰의 후속 제안이며,
현재는 상태 View·터렛 View의 책임과 초기화를 분리한 단일 HUD 프리팹을 사용한다.
