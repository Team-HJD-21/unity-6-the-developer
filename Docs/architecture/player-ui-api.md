# Initial Stage Player UI API

최종 갱신: 2026-10-13
상태: Initial 씬용 상태·터렛 선택 UI 구현. 플레이 모드 시각 검증은 별도.

[문서 목차](../README.md) · [터렛 API](turrets/api-reference.md)

## 적용 위치

`Assets/Scenes/Gameplay/Initial_Stage.unity`에
`Assets/Prefabs/UI/InitialStagePlayerUI.prefab`을 배치한다. Stage 1에서 사용하는
플레이어 체력 바, CU 체력과 전력 원형 게이지의 스프라이트·배치만 새 프리팹으로
분리했다. 원본 Stage 1과 `TurretInteractionUI`는 변경하지 않는다.

새 UI는 Canvas/uGUI와 TextMeshPro를 사용한다. 에디터 전용 자동 생성이나 테스트
패널이 아니며 빌드에서도 씬에 배치된 프리팹을 사용한다. `InitialStagePlayerUi`의
Inspector에서 세 상태 그룹, 폰트, 메뉴·버튼 스프라이트와 레벨 카탈로그를 설정한다.
Canvas는 프리팹에서 비활성화해 편집 중에는 숨긴다. 플레이 시 Awake에서 Canvas를
켜고 Start에서 상태 원본을 연결한다. 터렛 선택창은 플레이 중에도 터렛을 클릭한
경우에만 표시한다. 폰트는 한글을 지원하는 NEXON Lv1 Gothic OTF SDF를 사용한다.

## 스크립트별 책임

파일 위치: `Assets/Scripts/UI/Turrets/`

| 스크립트 | 책임 / 호출 조건 |
| --- | --- |
| `InitialStagePlayerUi` | MonoBehaviour 조립 진입점. Canvas·CanvasScaler·GraphicRaycaster 필수. 같은 씬의 PlayerInfo, ControlUnitHealth와 TurretController를 연결한다. 늦게 생성되는 대상은 0.5초 간격으로 다시 찾는다. |
| `StagePlayerVitalsView` | 실제 PlayerInfo 체력 이벤트를 칸형 바에 반영하고 CU 체력·전력 이벤트를 게이지에 반영한다. `BindPlayer`, `BindControlUnit`, `BindPower`로 연결하고 `Dispose`에서 구독을 해제한다. |
| `StageTurretSelection` | 클릭 대상 판정, 빈 공간·Escape로 해제, 같은 씬 터렛 ID 유지. `Resolve`에서 최신 인스턴스를 찾고 선택한 터렛에만 `ShowRange`를 켠다. `Clear`에서 표시를 해제한다. UI 위 클릭은 제외한다. |
| `StageTurretControlView` | 선택창 표시와 버튼 상태만 담당한다. Stage 1 스타일의 Activate/Deactivate 버튼을 패널 정중앙에, Level과 위·아래 화살표를 왼쪽에 배치한다. 클릭 이벤트를 Presenter에 전달한다. |
| `StageTurretPresenter` | `RequestActivation`, `RequestLevelUpgrade`, `RequestLevelDowngrade` 호출과 결과 표시. 레벨 교체 후 같은 InstanceId로 다시 조회하여 선택·사거리 표시를 이어간다. |
| `StageTurretUiText` | 활성화와 레벨 변경 결과의 영어 표시 문구. 터렛 규칙은 계산하지 않는다. |
| `StageLevelArrowGraphic` | 기본 UI Image 두 개로 위·아래 화살표를 구성한다. 폰트 글리프와 커스텀 메시 렌더링에 의존하지 않는다. |

## 데이터와 동작

- UI 문구는 영어다. 활성화 버튼은 Stage 1처럼 Activate(초록)/Deactivate(빨강)로
  표시한다. 레벨 조절 화살표는 최소·최대 레벨과 잠금·파괴 조건에 따라 비활성화된다.
- Initial 씬의 최대 전력은 100이며, 배치 터렛의 시작 활성화 설정을 끄므로 가용
  전력 100으로 시작한다. 터렛을 직접 켜면 API가 전력을 예약한다. 초기 작동 터렛이
  없으므로 전선·영역은 이후 터렛을 켠 배치에 따라 갱신된다.

- 플레이어 체력 원본은 `PlayerInfo.curHp/maxHp`와 `onHpChange`다. 현재 Initial 씬의
  `PlayerActorIdentity`가 `local-player`인 활성 PlayerInfo를 우선 연결하고, Identity가
  없는 기존 구성에서는 같은 씬의 활성 PlayerInfo로 대체한다. 체력 칸의 크기와 간격은
  상태 View에서 명시적으로 배치하여 원본의 자동 레이아웃 갱신 순서에 의존하지 않는다.
  다중 플레이어에서는 로컬 actor를 명시적으로 주입하는 연동이 추가로 필요하다.
- CU 체력은 `ControlUnitHealth.CurrentHealth/MaxHealth`와 `HealthChanged`에서 받는다.
- 전력은 `TurretController.GetSnapshot()`의 가용·최대·반환 대기 전력이다.
  `PowerChanged`와 주기적 조회로 반환 대기까지 갱신한다. 임의의 체력·전력 값을 쓰지 않는다.
- 사거리의 실제 크기와 최소 사거리 표시는 TurretBase가 소유한다. UI는 `ShowRange`만
  변경하며 반지름이나 Sprite 스케일을 재계산하지 않는다.
- 승급/강등 가능 여부는 연결한 `TurretLevelUpgradeCatalog`로 확인한다. 현재는 Stage 1
  카탈로그다. 잠금·파괴·초기화 전 터렛은 조작 버튼을 막고 활성화 냉각과 자동 활성화
  진행 중에는 해당 ON/OFF 조작을 막는다. 전력 부족 등 최종 판정은 요청 API가 한다.
- `일시 중단`을 무조건 `과열`로 표시하지 않는다. 현재 상태와 마지막 요청 결과는
  별도 줄에 표시한다. 다른 터렛을 선택하면 이전 요청 결과를 지운다.

## 확인 순서

1. 플레이를 종료한 상태에서 Initial 씬을 다시 열고 `Initial Stage Player UI`를 확인한다.
2. 플레이어 피해 후 체력 바, CU 피해 후 체력 게이지가 변하는지 확인한다.
3. 터렛 ON/OFF 후 전력·반환 대기와 실제 활성 상태를 확인한다.
4. 터렛 선택, 다른 터렛 선택, 빈 공간 클릭, Escape, 닫기에서 사거리 표시를 확인한다.
5. LV1 → LV2 → LV3 → LV2 → LV1 변경 후 동일 ID의 선택창·사거리·수치가 유지되는지 확인한다.
6. 최대/최소 레벨, 잠금·파괴·전력 부족 상황과 UI 위 클릭을 확인한다.

## 이후 갱신 기준

플레이어 연동 스크립트를 추가·수정하면 같은 변경에서 이 문서의 책임·호출 조건·
연동 API를 갱신한다. 자동 활성화 버튼, 세부 강화 선택과 최종 UI 연출은 이번 범위에 포함하지 않는다.
