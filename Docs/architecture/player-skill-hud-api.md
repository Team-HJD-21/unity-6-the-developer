# Player Skill HUD 씬 연결

갱신: 2026-10-22. 프리팹·씬 연결과 런타임 소유자 연결을 분리했다.

## 배치와 편집

`Assets/Prefabs/UI/PlayerSkillHUD.prefab`을 `Initial_Stage`와 `PlayerTest`의 루트에 배치한다.
이번 변경에 두 씬의 인스턴스를 포함했다. 다른 테스트·참조 씬에서 스킬 UI를 사용할 때도
이 프리팹을 씬 루트에 하나 배치해야 한다. Player 프리팹에는 HUD 참조나 HUD 자식이 없다.

프리팹에는 Q/E 슬롯, 스킬 선택 카드 4개와 개발 중 카드 4개, Q/E 선택 미리보기,
툴팁, F 키 회수 안내, 포탑/방벽 설치 안내와 설치 실패 안내가 들어 있다.
2026-10-27에는 기존 Canvas 자식 `Weapon Hotbar`에 무기 슬롯 4개를 미리 배치했다.
실행 중 GameObject·UI 컴포넌트·쿨타임 Sprite를 만들지 않는다.
`SkillCooldownMask.png`는 Radial360 쿨타임 표시를 위한 흰색 Sprite다.

Unity에서 씬의 `Player Skill HUD`를 선택하거나 Prefab Mode로 열어
위치·크기·폰트·색·아이콘·앵커를 편집한다. 선택창과 툴팁은 초기 비활성이므로
편집할 때 잠시 켜고 완료 후 다시 끈다. Canvas는 편집 중 켜져 있다.
실행 시에는 소유자를 연결할 때까지 Canvas와 GraphicRaycaster를 숨긴다.

설치 실패 안내의 빨간색·Bold 설정, 기존 슬롯 위치와 선택창 배치는 유지한다.
상태에 따라 달라지는 슬롯 색상과 회수 안내 위치 오프셋도 각 View의 Inspector에서 편집한다.
코드는 내용·활성 여부·쿨타임·포인터/대상 위치를 갱신하며 레이아웃과 폰트를 재구성하지 않는다.

2026-10-27 회수 안내는 폭 100·높이 30, 글자 크기 14로 축소했다. `PlayerTurretRecoveryPromptView`는 가까운 회수 대상의 표시 여부를 HUD 갱신에서 결정하고, `Canvas.preWillRenderCanvases`에서 포탑 바닥의 최종 화면 좌표를 다시 계산한다. 추적 카메라가 LateUpdate에서 움직여도 포탑과 안내가 서로 다른 카메라 좌표를 쓰지 않도록 한다. 활성화할 때 이벤트를 구독하고 비활성화할 때 해제한다. `RecoveryPrompt_UsesFinalCameraPositionBeforeDrawing`은 HUD 갱신 후 카메라를 이동시키는 PlayMode 회귀 테스트다. 컴파일은 확인했으며 해당 PlayMode 테스트 실행은 남아 있다.

### 무기 선택창 편집

2026-10-27 #565 대시 표시는 같은 Canvas의 `Dash Slot`에 배치했다. 하단 무기 선택창 오른쪽이며 Q/E 선택 카드에 포함하지 않는다. `PlayerDashHud`는 기존 로컬 HUD 소유자의 PlayerDash를 참조해 준비 상태·원형 쿨타임·남은 시간을 갱신한다. 위치·크기·키 글씨·배경·색은 프리팹에서 편집한다. 입력을 받는 버튼은 없고 Raycast Target은 꺼져 있다. [대시 API](player-dash-api.md)

`Player Skill HUD > Weapon Hotbar`를 선택해 하단 위치·크기를 조절한다. 자식 `1 Spanner`, `2 DefaultGun`, `3 CryoBlaster`, `4 Sniper`에는 Button, 배경, 아이콘, 무기 이름, 숫자 배지와 선택 표시가 이미 들어 있다. 각 RectTransform과 Image/TMP 속성을 직접 편집한다. 아이콘 원본은 `Assets/Art/UI/PlayerWeapons/`다. 별도 Canvas는 추가하지 않았다.

`PlayerWeaponHud`의 `_slots`는 1~4 순서로 연결한다. `PlayerWeaponSlotView`의 슬롯 번호와 같은 순서여야 한다. 선택된 슬롯의 테두리·밑줄은 청록색이며 색은 각 Slot View의 Inspector에서 바꾼다. 표시만 바꾸려면 키 번호나 PlayerAttack 설정을 수정할 필요가 없다.

무기 HUD는 기존 `PlayerSkillHudSceneBinding`이 선택한 `PlayerSkillHud.Owner`를 따라가며 추가 플레이어 탐색을 하지 않는다. 소유자의 `PlayerAttack.WeaponChanged`를 구독하고 소유자 교체·비활성화 때 해제한다. 주인 없는 HUD는 숨기고, 조작 차단 시 버튼을 비활성화한다. 장비 참조가 없는 슬롯도 비활성화한다. 버튼·숫자키·휠은 모두 PlayerAttack의 같은 선택 API를 사용한다.

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
포탑이 설치 불가 위치에 있으면 `TurretPlacementFeedback`의 실패 사유를 빨간 볼드체로 먼저 표시한다.
설치 가능 위치에서는 왼쪽 클릭/우클릭/Esc 안내를 표시한다. 미리보기 중 회수 안내는 숨긴다.
선택창 진입 시 미리보기를 취소하며, 설치 전 취소는 수량과 쿨타임을 사용하지 않는다.
포인터가 UI 위에 있으면 설치 확정하지 않는다.
월드 미리보기는 Canvas가 아닌 별도 `PlayerTurretPreview.prefab`으로 편집한다.
