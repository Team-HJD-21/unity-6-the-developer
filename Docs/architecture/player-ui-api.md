# Initial Stage Player UI API

최종 갱신: 2026-10-13
상태: 프리팹 기반 UI 구현. 최종 PlayMode 외형·조작 확인은 별도.

[문서 목차](../README.md) · [터렛 API](turrets/api-reference.md)

## 적용 위치와 편집 방법

`Assets/Scenes/Gameplay/Initial_Stage.unity`에서
`Assets/Prefabs/UI/InitialStagePlayerUI.prefab`을 사용한다.
원본 Stage 1과 `TurretInteractionUI`는 변경하지 않는다.

상태 UI, 터렛 창, 텍스트, 버튼과 화살표는 모두 프리팹에 미리 배치되어 있다.
런타임에 UI 오브젝트를 만들거나 이름으로 UI 참조를 찾지 않는다.
위치·크기·색상·폰트·스프라이트·앵커는 Prefab Mode에서 편집한다.
체력 칸의 위치·크기도 프리팹에 저장하며 코드에서 Layout을 끄거나 재배치하지 않는다.

```text
Initial Stage Player UI (Canvas)
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
   ├─ LockToggleButton
   │  ├─ LockedIcon
   │  └─ UnlockedIcon
   └─ CloseButton
```

Canvas는 편집 중 숨기도록 프리팹에서 비활성화한다. 디자인을 미리 볼 때 Prefab Mode에서
Canvas를 잠시 켜고, 터렛 창은 해당 GameObject를 잠시 켠다. 미리 보기 후 초기 비활성 상태로
돌려둔다. 플레이 시 `InitialStagePlayerUi.OnEnable`이 Canvas를 켜며 터렛 창은 선택 시에만
표시한다. CanvasScaler는 프리팹에서 1920×1080, 화면 비율 혼합 0.5로 설정한다.
현재 Scene의 EventSystem과 InputSystem UI 모듈을 사용한다.

## Inspector 연결

- Canvas의 `InitialStagePlayerUi`: `vitalsView`, `turretView`, `levelCatalog`.
- Canvas의 `StagePlayerVitalsView`: 체력 칸 Image 배열, 플레이어 체력 Text,
  CU 체력 Fill/Text, 전력 Fill/Percent/Details Text 및 체력 칸 색상.
- TurretControlPanel의 `StageTurretControlView`: 제목·레벨·수치·상태·결과·활성화 Text,
  Activate·Up·Down·Close·Lock Button, 잠금/해금 아이콘과 활성화 상태 색상.
- Arrow의 `StageLevelArrowGraphic`: 왼쪽·오른쪽 Stroke RectTransform과 위/아래 방향.
  두 Stroke는 프리팹에 배치된 기본 Image다.

Button의 Inspector OnClick에 요청 메서드를 중복 연결하지 않는다. View가 OnEnable에서
구독하고 OnDisable에서 해제한다. 필수 참조가 누락되면 조립 진입점이 오류를 남기고
실행을 중단한다.

## 스크립트별 책임

파일 위치: `Assets/Scripts/UI/Turrets/`

| 스크립트 | 책임 / 호출 조건 |
| --- | --- |
| `InitialStagePlayerUi` | Canvas·CanvasScaler·GraphicRaycaster 필수. Inspector로 View를 연결하고 씬 데이터와 Presenter를 조립한다. 생성이 늦은 데이터는 0.5초마다 재탐색한다. |
| `StagePlayerVitalsView` | 상태 표시 MonoBehaviour. `BindPlayer`, `BindControlUnit`, `BindPower`로 이벤트를 연결하고 `Unbind`로 해제한다. 데이터 값이 바뀐 경우에만 체력 칸·전력 텍스트를 갱신한다. |
| `StageTurretSelection` | 클릭·빈 공간·Escape 입력, 씬 소속 터렛 ID 유지와 사거리 표시. `Resolve`로 교체된 인스턴스를 조회하고 `Clear`로 표시를 해제한다. UI 위 클릭은 제외한다. |
| `StageTurretControlView` | 프리팹의 Text/Button 참조를 보관하고 표시·버튼 상태·입력 이벤트만 담당한다. `Show`, `Hide`, `SetFeedback`을 제공한다. |
| `StageTurretPresenter` | 선택 변경 및 SnapshotChanged에 따른 갱신, 활성화·레벨 API 호출과 결과 표시. 요청 시에도 조작 허용 조건을 검사한다. `Dispose`로 이벤트를 해제한다. |
| `StageTurretUiText` | 기존 터렛 API 결과를 영어 문구로 변환한다. |
| `StageLevelArrowGraphic` | 프리팹에 배치된 두 선의 회전으로 화살표 방향을 맞춘다. 오브젝트를 생성하지 않는다. |

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
- 선택 패널 왼쪽 아래 LockToggleButton은 `SetLocked(!IsLocked)`를 요청한다.
  한 번 누르면 잠기고 다시 누르면 해금되며 닫힌/열린 자물쇠 아이콘으로 상태를 표시한다.
  아이콘은 프리팹에 배치한 기본 Image로 구성해 Inspector에서 편집할 수 있다.
  잠금 시 기존 API가 활성화를 해제하고 전력 반환을 처리한다. 해금 후 활성화는 별도다.
  아직 열리지 않은 구역의 터렛은 API가 해금을 거부하며 `Unlock zone N first.`를 표시한다.
  자동 활성화 진행 중에는 Lock 버튼도 비활성화한다. 구역 자체를 해금하는 버튼은 아니다.

## 기존 시작 설정

앞선 UI 기능 커밋에서 Initial 씬은 최대 전력 100, 배치 터렛은 비활성 상태로
설정되었다. 초기 작동 터렛이 없으므로 전선·영역은 이후 터렛을 켠 배치에 따라 갱신된다.
이번 프리팹 리팩터링은 이 Scene 설정이나 터렛 API를 변경하지 않는다.

## 확인 순서

1. InitialStagePlayerUI 프리팹의 두 View 컴포넌트와 모든 Inspector 참조를 확인한다.
2. 플레이 시 체력·전력 표시, 터렛 클릭과 사거리 표시를 확인한다.
3. Activate/Deactivate, LV1→LV2→LV3→LV2→LV1 변경과 동일 ID 선택 유지를 확인한다.
4. 빈 공간·Escape·Close, 잠금·파괴·전력 부족 처리를 확인한다.
5. 플레이어 사망·입력 차단·일시정지에서 버튼 요청이 막히는지 확인한다.
6. UI 루트 컴포넌트 비활성화·재활성화 후 표시·사거리·이벤트 중복 여부를 확인한다.
7. Prefab Mode에서 위치·폰트 등을 변경할 수 있고 실행 코드가 이를 덮어쓰지 않는지 확인한다.
8. Scene의 UI 인스턴스에 체력 칸 너비·위치 Override가 남아 프리팹의 배치를 덮어쓰지
   않는지 확인한다. 체력 바가 사라지는 원인이었던 예전 체력 칸 Override는 제거했다.
9. 같은 자물쇠 버튼을 연속으로 눌러 잠금/해금, 아이콘 전환, 활성화·레벨 버튼 상태와
   전력 반환을 확인한다. 미해금 구역의 터렛은 해금이 거부되는지 확인한다.

## 이후 갱신 기준

플레이어 연동 스크립트를 추가·수정하면 같은 변경에서 책임·호출 조건·연동 API를
갱신한다. 자동 활성화 버튼, 세부 강화 선택과 최종 UI 연출은 이번 범위에 포함하지 않는다.
