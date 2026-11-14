# Player Skill HUD 씬 연결

갱신: 2026-11-12. HUD v6 적용과 Editor의 실제 Scene·단계별 입력·애니메이션 관찰을 완료했다. 범위와 제한은 [검증 기록](../../94_art/ui-validation-2026-11-12.md)을 따른다.

## HUD v6 표시와 장착

공통 외형·상태 규칙은 [공통 UI 규칙](../../94_art/common-ui-rules.md)을 따른다. 기존 씬 바인더와 로컬 소유자 판정을 유지하며 `PlayerSkillHUD.prefab`의 선택 표시를 `CombatSkillPicker`에 연결했다. 아래의 기존 카드·패널은 호환용 참조로 남지만 v6에서는 반원 선택창을 사용한다. 이전 배치·청록색 강조·남은 초 표시에 대한 설명보다 이 절의 규칙이 우선한다.

- 하단 Q/E/SPACE에 접근하면 화면 아래에 붙은 네 구획 반원이 나온다. 드래그 전 호버는 위에 설명 하나, 드래그 중에는 큰 디스크 아래에 설명 하나를 표시한다.
- `PlayerSkillController.TryEquip(int slot, PlayerSkillId skill)`은 0=Q, 1=E에 장착한다. 새 스킬은 대상 슬롯만 교체하며, 반대 슬롯의 스킬을 가져오면 두 슬롯을 교환한다. 같은 슬롯·잘못된 슬롯/ID·준비 불가 상태는 `false`이며 변경 이벤트와 성공 표시가 없다. Space는 대상이 아니다.
- `PlayerSkillLoadout.TryEquip`은 위 교체 규칙과 중복 방지를 담당한다. `Restore`는 E만 있는 정상 상태도 복원하며 같은 스킬의 Q/E 중복은 거부한다. 체크포인트 바이너리 형식은 바꾸지 않았다.
- `PlayerSkillController.TryUseSlot(int slot)`은 실제 Q/E 입력 진입점이다. 잘못된 슬롯·입력 잠금·선택창 열림은 시도 이벤트 없이 거부한다. 허용된 입력만 `TryUse` 후 `SlotUseAttempted(slot, skill, accepted)`를 한 번 송출한다. 기존 `TryUse(PlayerSkillId)` 직접 호출의 동작은 유지한다.
- 슬롯 View는 위 이벤트로 짧은 눌림을 표시한다. `PlayerSkillHud`는 미배정 슬롯일 때만 [공통 Alert](../03_scene-flow/common-alert-api.md)에 `skill/unassigned/0`, `skill/unassigned/1`을 보낸다. 소유자 교체·해제 때 구독을 정리한다. 알림은 App의 표시 서비스에 연결되며, App 초기화 전이나 종료 후에는 임시 Canvas를 만들지 않는다.
- 설치형 스킬과 PlayerTurret은 실제 `TryConfirmPlacement` 거부 때 `PlacementRejected`를 송출한다. 미리보기의 실패 상태 갱신은 알림을 보내지 않는다. 이전 `_placementFailure` 라벨은 참조 호환용으로 남기고 숨긴다. 확정 실패는 `skill/placement-rejected` 하나로 중복 억제한다.
- v6의 긴 설치 조작 안내도 숨긴다. 월드 미리보기와 범위는 유지한다. `CombatHudAlertLayout`이 기본 슬롯/반원의 실제 화면 높이를 `AlertStripView.SetScreenBottomClearance`에 전달하며, 텍스트 알림과 설명/디스크가 실제로 겹칠 때만 추가 여유를 확보한다. HUD 해제/우주선 방문 때 이 임시 경계를 해제한다.
- Q/E/SPACE의 쿨타임은 카드 전체 크기의 Filled 이미지와 카드 윤곽 Mask를 사용한다. 입력 피드백은 실제 사용 시도에만 반응하며, 슬롯을 클릭해 선택창을 여는 행동을 스킬 발동으로 표시하지 않는다. SPACE는 실제 Dash 입력 결과를 별도 이벤트로 읽는다.
- 드롭 영역은 화면 중앙을 기준으로 가로 거리에 따라 양끝으로 옅어진다. 대상 호버 시 장착 예고, 영역 밖에서는 취소 배지를 표시한다. 실제 장착 API가 성공한 뒤에만 디스크 확인 효과와 닫힘을 실행한다. 취소는 기존 장착을 보존한다.
- `CombatSkillPicker.IsOpen`에는 닫힘 애니메이션이 끝나기 전 구간도 포함된다. 이 동안 기존 전투 입력 제한을 유지한다. ESC는 선택창이 먼저 소비하며 포커스/일시정지 전환은 드래그를 취소한다.
- `CombatWeaponStack`은 기존 네 버튼 전체의 CanvasGroup 알파와 위치·회전을 갱신한다. 선택 슬롯을 마지막 sibling으로 옮기므로 런타임 자식 순서는 슬롯 번호가 아니다. `PlayerWeaponHud._slots`의 직렬화 순서와 각 `SlotNumber`는 1~4를 유지한다.
- 대시는 고정 `SPACE`와 Filled 쿨타임으로 표시한다. 남은 초는 표시하지 않는다. `CombatPlayerIndicators`는 기존 owner의 실제 탄약·조준·재장전 상태를 읽으며, XP는 `PlayerExperience.ExperienceGranted` 이벤트에만 반응한다. 저장 복원은 획득 팝업을 발생시키지 않는다.

v6 UI 계층은 Editor에서 제작한다. XP 팝업은 별도 World Space 프리팹 네 인스턴스를 풀링하며 Player HUD 내부 Canvas를 늘리지 않는다. 원본 무기/스킬 이미지와 기존 프리팹 GUID를 보존하고 본문 SDF만 HUD 전용으로 분리했다.

우주선 내부에서는 `IsFieldVisible`이 `false`이며 Canvas/GraphicRaycaster를 숨긴다. actor는 해제하지 않는다. 드래그·선택 설명·월드 XP 팝업도 정리하고, Field 복귀 후 같은 owner의 최신 상태를 다시 표시한다. 판정은 `PlayerMatchPresence.Location`을 읽는 `CombatHudContext`이며 이동 권한이나 위치 원본을 변경하지 않는다.

## 배치와 편집

`Assets/Prefabs/UI/PlayerSkillHUD.prefab`을 `Initial_Stage`와 `PlayerTest`의 루트에 배치한다.
이번 변경에 두 씬의 인스턴스를 포함했다. 다른 테스트·참조 씬에서 스킬 UI를 사용할 때도
이 프리팹을 씬 루트에 하나 배치해야 한다. Player 프리팹에는 HUD 참조나 HUD 자식이 없다.

프리팹에는 Q/E 슬롯, 스킬 선택 카드 4개와 개발 중 카드 4개, Q/E 선택 미리보기,
툴팁, F 키 회수 안내, 모든 설치형 스킬의 설치 안내와 설치 실패 안내가 들어 있다.
2026-10-27에는 기존 Canvas 자식 `Weapon Hotbar`에 무기 슬롯 4개를 미리 배치했다.
실행 중 GameObject·UI 컴포넌트·쿨타임 Sprite를 만들지 않는다.
`SkillCooldownMask.png`는 Radial360 쿨타임 표시를 위한 흰색 Sprite다.

Unity에서 씬의 `Player Skill HUD`를 선택하거나 Prefab Mode로 열어
위치·크기·폰트·색·아이콘·앵커를 편집한다. 선택창과 툴팁은 초기 비활성이므로
편집할 때 잠시 켜고 완료 후 다시 끈다. Canvas는 편집 중 켜져 있다.
실행 시에는 소유자를 연결할 때까지 Canvas와 GraphicRaycaster를 숨긴다.

설치 실패 안내의 빨간색·Bold 설정은 유지한다. 슬롯 위치와 선택창은 위 v6 배치가 우선한다.
상태에 따라 달라지는 슬롯 색상과 회수 안내 위치 오프셋도 각 View의 Inspector에서 편집한다.
코드는 내용·활성 여부·쿨타임·포인터/대상 위치를 갱신하며 레이아웃과 폰트를 재구성하지 않는다.

2026-10-27 회수 안내는 폭 100·높이 30, 글자 크기 14로 축소했다. `PlayerTurretRecoveryPromptView`는 가까운 회수 대상의 표시 여부를 HUD 갱신에서 결정하고, `Canvas.preWillRenderCanvases`에서 포탑 바닥의 최종 화면 좌표를 다시 계산한다. 추적 카메라가 LateUpdate에서 움직여도 포탑과 안내가 서로 다른 카메라 좌표를 쓰지 않도록 한다. 활성화할 때 이벤트를 구독하고 비활성화할 때 해제한다. `RecoveryPrompt_UsesFinalCameraPositionBeforeDrawing`은 HUD 갱신 후 카메라를 이동시키는 PlayMode 회귀 테스트다. 컴파일은 확인했으며 해당 PlayMode 테스트 실행은 남아 있다.

### 무기 선택창 편집

2026-10-27 #565 대시 표시는 같은 Canvas의 `Dash Slot`에 배치했다. 하단 무기 선택창 오른쪽이며 Q/E 선택 카드에 포함하지 않는다. `PlayerDashHud`는 기존 로컬 HUD 소유자의 PlayerDash를 참조해 준비 상태·원형 쿨타임·남은 시간을 갱신한다. 위치·크기·키 글씨·배경·색은 프리팹에서 편집한다. 입력을 받는 버튼은 없고 Raycast Target은 꺼져 있다. [대시 API](player-dash-api.md)

`Player Skill HUD > Weapon Hotbar`를 선택해 하단 위치·크기를 조절한다. 자식 `1 Spanner`, `2 DefaultGun`, `3 CryoBlaster`, `4 Sniper`에는 Button, 배경, 아이콘, 무기 이름, 숫자 배지와 선택 표시가 이미 들어 있다. 각 RectTransform과 Image/TMP 속성을 직접 편집한다. 아이콘 원본은 `Assets/Art/UI/PlayerWeapons/`다. 별도 Canvas는 추가하지 않았다.

`PlayerWeaponHud`의 `_slots`는 1~4 순서로 연결한다. `PlayerWeaponSlotView`의 슬롯 번호와 같은 순서여야 한다. 선택된 슬롯의 테두리·밑줄은 청록색이며 색은 각 Slot View의 Inspector에서 바꾼다. 표시만 바꾸려면 키 번호나 PlayerAttack 설정을 수정할 필요가 없다.

무기 HUD는 기존 `PlayerSkillHudSceneBinding`이 선택한 `PlayerSkillHud.Owner`를 따라가며 추가 플레이어 탐색을 하지 않는다. 소유자의 `PlayerWeaponSelection.WeaponChanged`를 구독하고 소유자 교체·비활성화 때 해제한다. 주인 없는 HUD는 숨기고, 조작 차단 시 버튼을 비활성화한다. 장비 참조가 없는 슬롯도 비활성화한다. 버튼·숫자키·휠은 모두 PlayerWeaponSelection의 같은 선택 API를 사용한다.

`PlayerWeaponHudTests`는 프리팹의 슬롯·아이콘 연결과 Canvas 재사용, 버튼과 로컬 HUD 소유자 연결, 입력 차단, 앞뒤 순환, 장비 누락 및 공격 대기시간 유지를 검사한다. 런타임·Editor 컴파일과 에셋 정적 검증은 확인했으며 Unity Test Runner 및 실제 화면의 클릭·휠 동작 확인은 남아 있다.

## 스크립트별 책임

| 스크립트 | 역할 |
| --- | --- |
| `PlayerSkillHudSceneBinding` | 같은 씬의 로컬 플레이어 탐색과 연결·해제 |
| `PlayerSkillController` | 선택 상태·스킬 입력·사용 조건, `BindHud`와 `DetachHud` |
| `PlayerSkillHud` | 프리팹 참조 검증, Loadout 구독, 전체 표시 갱신 |
| `PlayerSkillSlotView` | Q/E 아이콘·개수·쿨타임, 선택창 열기·툴팁 요청 |
| `PlayerSkillSelectionPanelView` | 선택창 열기·닫기·초기화와 미리보기 |
| `PlayerSkillCardView` | 개별 스킬 선택 가능 여부와 툴팁 요청 |
| `PlayerSkillTooltipView` | 툴팁 내용과 화면 밖으로 벗어나지 않는 위치 |
| `PlayerTurretRecoveryPromptView` | 회수 대상 포탑 아래에 F 키 안내 배치 |

## 필수 참조와 보조 UI

`PlayerSkillHud.IsConfigured`는 Q/E 슬롯과 선택창의 필수 참조를 검사한다.
필수 참조가 없으면 오류를 남기고 연결을 거부한다. 슬롯의 쿨타임 마스크도 필수다.
`IsFullyConfigured`는 여기에 툴팁·회수 안내·설치 안내·스킬 아이콘까지 포함한
전체 프리팹 검증이며, Editor 프리팹 검사는 이 조건을 요구한다.

보조 참조가 빠지면 시작 시 해당 필드 이름을 경고하고 스킬 선택과 슬롯은 계속 사용한다.
툴팁의 제목/설명 연결이 불완전하면 툴팁만 숨긴다. 아이콘이 없으면 해당 Image를 숨기고
카드의 스킬 이름, 슬롯의 Q/E 키와 수량, 선택 기능을 유지한다.
설치 실패 안내가 없으면 방벽 안내를 함께 숨기지 않는다.
런타임에 대체 UI를 생성하거나 Inspector의 폰트·레이아웃을 덮어쓰지 않는다.

## 표시 갱신 시점

- 선택 카드·등록 안내·선택 이름은 창을 열거나 `Loadout.Changed`가 발생할 때 갱신한다.
- 닫힌 선택창의 카드·미리보기는 갱신하지 않는다. 다시 열 때 최신 선택을 반영한다.
- 메인 슬롯의 쿨타임·조작 가능 상태, 열린 창의 미리보기 쿨타임은 매 프레임 갱신한다.
- 슬롯 수량 문자열은 선택 스킬 또는 수량이 바뀔 때만 갱신한다.
- 툴팁의 포인터 위치와 회수 대상 위치, 설치 피드백은 현재 상태를 계속 추적한다.

## 플레이어 연결 규칙

HUD는 씬이 소유하고 플레이어는 실행 중 생성될 수 있다. 씬 바인더는 미연결 상태에서
기본 0.25초 간격(일시정지와 무관한 시간)으로 활성 플레이어를 찾는다.
로컬 소유자가 연결된 후에는 매 프레임 전체 플레이어를 탐색하지 않는다.

- 다른 씬·비활성 플레이어는 제외한다.
- `NetworkObject`가 있으면 Spawn 완료와 `IsOwner`를 모두 요구한다.
- `PlayerActorIdentity`가 있으면 `Local Player Id`와 일치해야 한다. 현재 로컬 기본값은 `local-player`다.
- 둘 다 없는 테스트 플레이어는 같은 씬에 후보가 하나일 때만 자동 연결한다.
  이 임시 연결 중에도 재탐색하여 나중에 생성된 명시적인 로컬 소유자를 우선한다.
- 플레이어나 HUD가 비활성화되면 이벤트와 양방향 참조를 해제하고 선택창·안내를 숨긴다.
  씬의 HUD GameObject와 선택 데이터는 삭제하지 않는다.

현재 프로젝트의 로컬 스포너 경로는 `PlayerActorIdentity`를 사용한다.
향후 실제 멀티플레이 스폰 경로에서 직접 연결하려면
`PlayerSkillHudSceneBinding.BindLocalPlayer(player)`를 호출한다. 이 진입점도 소유자와 씬을 검사한다.
이는 UI 소유자 선택이며 스킬 실행에 서버 권한이나 RPC를 추가하는 기능은 아니다.

`PlayerSkillController.BindHud(hud)`는 검증된 소유자를 직접 연결하는 내부 통합·테스트용 진입점이다.
로컬 판정은 바인더가 수행하며, 컨트롤러는 활성 상태·같은 씬·프리팹 참조를 검사한다.
재연결 전에 이전 구독과 버튼 리스너를 해제한다. Q/E 선택 상태는 컨트롤러의 Loadout에 남는다.

## EventSystem과 확인 항목

HUD 프리팹은 EventSystem을 생성하지 않는다. `Initial_Stage`의 기존 EventSystem을 사용하고,
없던 `PlayerTest`에는 InputSystemUIInputModule과 함께 미리 배치했다.
새 씬에도 활성 EventSystem과 입력 모듈을 하나 준비해야 한다.

기존 PlayMode 테스트를 씬 HUD 연결 방식으로 바꾸고, 프리팹의 View·폰트·아이콘 참조,
재연결 시 객체 수 유지, 플레이어 비활성화 시 해제, 원격 플레이어 연결 거부 회귀 검사를 추가했다.
구조 개선 후에는 보조 UI와 아이콘 누락 시 선택 유지, 불완전한 툴팁만 숨김,
필수 슬롯 누락 시 연결 거부, 닫힌 동안 변경한 선택의 재표시 검사도 추가했다.
Unity Test Runner 실행 및 실제 화면·마우스 조작 확인은 별도로 필요하다.

1. 플레이 전 Hierarchy에서 `Player Skill HUD`와 슬롯을 선택해 편집할 수 있는지 확인한다.
2. 플레이어가 늦게 생성돼도 Q/E 슬롯이 표시되고 Player 아래에 새 Canvas가 생기지 않는지 확인한다.
3. 카드 선택·초기화·닫기와 전투 중 슬롯 툴팁, 쿨타임 표시를 확인한다.
4. 설치 실패 안내와 F 키 회수 안내가 기존 조건대로 표시되는지 확인한다.
5. 씬 재진입과 플레이어 비활성화 시 이전 플레이어 안내가 남지 않는지 확인한다.

## 포탑 설치 위치 선택

`PlayerSkillController.IsPlacingTurret`는 현재 설치 미리보기 여부이며,
`PlacementHint`는 포탑 또는 방벽 안내를 반환한다. `BarrierPlacementHint`는 기존 호출을 위해 유지한다.
포탑의 설치 불가 위치는 월드 미리보기 색으로 표시하고, 실제 확정이 거부되면 `TurretPlacementFeedback`의 실패 사유를 공통 Alert로 표시한다. 미리보기 이동만으로 반복 송출하지 않는다.
설치 가능 위치에서는 왼쪽 클릭/우클릭/Esc 안내를 표시한다. 미리보기 중 회수 안내는 숨긴다.
선택창 진입 시 미리보기를 취소하며, 설치 전 취소는 수량과 쿨타임을 사용하지 않는다.
포인터가 UI 위에 있으면 설치 확정하지 않는다.
월드 미리보기는 Canvas가 아닌 별도 `PlayerTurretPreview.prefab`으로 편집한다.

2026-11-14: 포탑·방벽·비콘·폭탄의 월드 미리보기는 `Entities`보다 높은 `PlayerSkillPreview` Sorting Layer를 사용한다. 후보 본체, 고정한 첫 본체, 연결선과 설치 범위선에 함께 적용하며 기존 내부 Order는 유지한다. 나무·몬스터·설치물 앞에 표시하고 Screen Space Overlay HUD 아래에 그린다. 확정된 설치물과 일반 월드 효과의 표시 순서는 바꾸지 않는다. 설치물의 바닥 영역이 육지를 벗어나면 기존 공통 지면 판정으로 설치를 거부한다.
