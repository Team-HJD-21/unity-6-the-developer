# Initial Stage Player HUD API

2026-11-14 결과·안전장치 선택·Pause 메인 복귀·전장 진입 순서는 [Mission 복귀 API](../02_match/match-recovery-api.md#2026-11-14-선택형-안전장치와-결과-ui)와 [구현/검수 기록](../../01_planning/02_implementation-records/2026/11/2026-11-14-mission-outcome-ui.md)을 따른다. `CombatHudIntro`의 MessageFadeSeconds/MessageHoldSeconds/MessageEraseSeconds/Duration과 `AnnouncementPrefab`이 편집 접점이다. 문구 프리팹은 Skill HUD 하나만 참조한다.

최종 갱신: 2026-11-14. 전술 화면의 구현과 URP 재검수 범위는 다음 절을 따른다. 기존 HUD v6 인수와 새 전술 UI 인수는 구분한다.

## 2026-11-13 전술 화면

`InitialStagePlayerHUD.prefab`의 기존 `StageTurretControlView`를 제거하고 `TacticalCommandController`로 연결했다. 아래 과거 기록의 일반 클릭 선택·상세 패널보다 이 절을 우선한다. 실제 검증 결과와 제한은 [전술 화면 구현 기록](../../01_planning/02_implementation-records/2026/11/2026-11-13-tactical-command-view.md)을 확인한다.

우클릭 이동·F·지도 설정·상태/점유 표시의 11-14 변경은 [후속 가독성 기록](../../01_planning/02_implementation-records/2026/11/2026-11-14-tactical-readability.md)에서 이번 실행 결과와 남은 인수를 구분한다.

- 필드에서 **M / 전술 버튼**으로 열고 M·Esc·닫기 버튼으로 복귀한다. 플레이어 중심에서 실제 점유 셀을 최대한 담는 배율로 시작한다. 맵 가장자리에서는 플레이어가 보이도록 중심을 보정하고 실제 카메라 사각형을 맵 안에 제한한다. 화면비보다 맵이 좁아지면 무광 여백과 임시 viewport로 분리해 맵 밖의 월드를 그리지 않는다.
- 카메라 경계는 Scene instance에 연결한 `CameraLeftTop`/`CameraRightBottom`을 우선한다. PR #722의 Stage_1은 LeftTop(-138,120), RightBottom(138,-120)인276×240이다. 참조가 없거나 좌표가 비정상/역전이면 기존 지역 geometry를 사용한다. 지역 정의·점유·권한의 원본은 바꾸지 않는다.
- 플레이어 이동·발사·스킬 준비를 막고, WASD/방향키·우클릭 드래그·마우스 가운데 드래그는 카메라 이동에 사용한다. 우클릭은 drag threshold를 넘으면 이동만 수행하며 짧은 클릭만 핑을 지정한다. 실제 viewport 픽셀 높이로 이동량을 환산한다. 휠은 확대·축소, Home은 플레이어 복귀다. 게임 시간은 정지하지 않는다.
- 터렛을 선택하면 좌측 하단 도크에 HP·전력·사거리 내 적 수와 켜기/끄기·강화를 표시한다. Enter는 선택 터렛 토글, U는 강화창이다. 성공을 미리 표시하지 않고 기존 Host 명령 결과·확정 스냅샷을 따른다.
- 근처 터렛은 기존 거리 규칙으로 조작한다. 멀리 있는 개별 터렛은 Host의 **최신 점유 셀**에 있을 때만 허용한다. 사망·메뉴·전환 잠금·전력·냉각·잠금·강화 제약은 유지한다. CU 자동 활성화는 원격 허용에서 제외한다.
- CU 근처에서 자동 활성화와 다중 선택 버튼·Shift+클릭/Shift+드래그 후 선택 켜기/끄기를 표시한다. 다중 선택 도크에는 합산 체력·사용 전력·사거리 합집합 안의 적 수량을 집계한다. 일괄 변경은 개별 명령들의 모음이며 원자적 트랜잭션이 아니다. 각 결과를 기존 권한 처리기가 검증한다.
- 전체도는 전술 화면 안에만 있다. 클릭/드래그로 카메라를 옮기며 현재 시야 사각형·점유 셀·터렛·CU·플레이어·적을 표시한다. 우클릭 목표 지정은 필드 복귀 후 방향·거리를 표시하고 가까워지면 해제한다.
- 적 정보는 현재 같은 전장 Scene의 활성·생존 `EnemyHealth` 위치를 집계한다. 숨은 적을 제외하는 별도 탐지 규칙은 적용하지 않는다. 현재 전장에 생성되지 않은 적이나 스폰 계획을 추정하지 않는다.
- 일반 필드 클릭은 터렛 패널을 열지 않는다. 플레이어의 성공한 발사/근접 공격 뒤 2초가 지나야 Hover 정보가 보이고 다음 공격에 숨는다. 근접 **F**는 가까운 필드 터렛을 토글한다. 실제 구조 대상과 회수 가능한 플레이어 설치 포탑이 있으면 각각 기존 구조·회수를 우선하고 같은 F로 필드 토글을 실행하지 않는다. G 시험 배치는 폐기했다.

11-14 수정에서 지도 버튼은 우하단의 전용 지도 심볼과 `전술 지도`로 표시한다. M은 계속 입력으로 지원하지만 버튼에 중복 표기하지 않는다. 전체도 패널 안의 `지도 설정`에서 사거리를 가동 중 전체/선택한 터렛만으로 전환하고 지형지물 표시를 바꾼다. 두 선택은 별도 PlayerPrefs 키로 저장한다. 사거리는 기본 가동 중 전체이며 선택한 비가동 터렛의 사거리도 미리 볼 수 있다. `IsActivated`와 `IsOperational`을 구분하고 냉각 중인 활성 터렛을 OFF로 표시하지 않는다.

`TacticalPresentationRules`는 가동·대기·잠금·냉각·전력 부족·파괴·조작 대기·미준비를 분리한다. 금색 선택 테두리와 별개로 전원/잠금/시계/전력/X/일시정지 형태를 그려 색만으로 구분하지 않는다. 선택 도크·필드 tooltip에도 같은 의미를 사용한다. 터렛 본체 SpriteRenderer의 색이나 알파는 변경하지 않는다.

지역은 확정 점유 셀, 해금된 점유 가능 셀, 실제 확보 진행이 남아 있는 셀, 미점유/미해금 셀을 구분한다. 점유 중은 사선 패턴과 실제 확보 시간 비율의 진행 링을 사용하며, UI가 별도 시간이나 점유 완료를 추정하지 않는다. 터렛 연결 전선/삼각형 외곽은 지도와 전체도에서 표시하지 않는다. Core의 전선 계산이나 점유 계산은 유지한다. **현재 정적 확보 진행의 읽기 원본은 Host에만 있다. Client에는 그 복제 계약이 없어 미수신 상태에서 임의 타이머를 표시하지 않는다.** Client 확정 점유·해금·터렛 상태는 기존 replica를 계속 읽는다.

지형 숨김 대상은 Scene의 `SceneryRenderers` 명시 참조다. Stage_1은 `Environment_Obj` TilemapRenderer 한 개만 연결한다. 전술 화면 동안 `forceRenderingOff`만 바꾸고 원래 값을 닫기/disable/파괴 때 복원한다. 물·지면·충돌·오브젝트 활성 상태·터렛/Player/Enemy renderer는 변경하지 않는다. Editor의 `BindActiveSceneScenery`는 실제 장식 대상이 있는 Scene에서만 실행하고 저장 diff를 확인한다.

플레이어/CU/이전 시야/목표 해제는 전체도 헤더의 아이콘이다. 닫기·설정은 화면 오른쪽의 별도48×40 열이며 상시 문장 범례를 두지 않는다. 시설 도크/필드 hover/위협 카드는 체력 분할 게이지와 실제 감소 잔상을 사용하고 전력·적 수량은 의미 심볼 옆에 표시한다. 실제 HP 감소만0.42초 아이콘·상태 배지를 흔들고 카메라·사거리 좌표는 움직이지 않는다. 첫 연결·최대 HP 변경·회복은 피격을 새로 재생하지 않는다.

`TacticalModeEffect`는 배경이 투명한 전면 육각선0.12초/선 유지0.06초/중앙 파동 진입0.42초·종료0.30초의 순서다. `IsOpen`은 입력 lease의 전체 수명, `IsTransitioning`은 조작 대기, `IsMapPresented`는 완료한 UI 표시 상태다. 육각선이 전면에 펼쳐진 시점에 카메라만 교체하고 완료 시 지도/필드 UI를 공개한다. ModeEffect.Update(-300)는 Controller.Update(-250)와 일반 HUD 갱신보다 앞서 handoff하여 한 프레임의 중복 UI도 피한다. 전환 Canvas800의 전용 GraphicRaycaster가 표식을 가로채며 완료 시-900/raycast false로 돌아간다. 중앙 alpha0/가장자리 잔상은 유지한다. 연속 취소/재요청은 현재 막에서 이어지며 메뉴/사망/씬 해제는 CloseImmediate로 카메라·입력·renderer 상태를 정리한다.

`TerritoryFill`/`OverviewFill`은 확정 셀 상태의8배 샘플 mask와 `TacticalTerritory.mat`를 사용한다. 내부 셀 선은 없고 실제 점유 외곽선은 동일 월드 선분이다. 포인트/조작 권한은 여전히 원래 CapturedCells로 판정하며 feather를 점유로 읽지 않는다. 도크 위 `가까이에서 조작` 표시를 없앴다. 점유 완료 지역이면 플레이어 거리와 무관하게 지도에서 개별 조작하고, 미점유 지도 대상은 근접해도 잠김으로 표시하고 조작을 차단한다. Field F는 기존 해금·근접 규칙을 유지한다. Host가 허용하는 범위 안에서 지도 UI를 더 좁히며 Host의 권한을 확대하지 않는다.

필드 타이머 아래 `FieldStatusGroup`은 실제 Region/StaticRegionCaptureSnapshot과 플레이어 현재 셀로 `구역 확보 중`/`확보 일시 중단`/`확보 구역 이탈`/`미점유 지역`을 표시한다. 정적 미해금 구역은 `잠긴 구역`, 실제 적 압박은 누적0초여도 `적 우세 · 확보 중단`으로 구분한다. 진행 구역에서 실제로 나왔을 때 이탈 문구2.4초, 그 후 현재 영역 상태로 복귀한다. 진행 원본이 없으면 진행을 추정하지 않는다. FieldStatusContent만 상태 변경 때0.5초 확대/복귀하고 HUD 배율 root는 보존한다.

편집 원본은 `Assets/Prefabs/UI/Tactical/TacticalHud.prefab`, 설정은 `Assets/Resources/Core/TacticalHudStyle.asset`이다. 같은 `CombatHudStyle`의 패널·프레임·폰트를 사용한다. `TacticalHudSetup.Configure`는 명시적으로만 프리팹을 다시 작성하므로 수동 편집 후 무심코 실행하지 않는다. 런타임에는 작성된 UI를 복제하고 실제 점유 셀 텍스처/월드 표식만 갱신한다. 카메라와 AudioListener를 추가하지 않는다.

Stage Scene의 두 marker는 `TacticalHudSetup.BindActiveSceneCameraBounds`로 해당 HUD instance에 명시 연결하고 Scene diff를 확인한 뒤 저장한다. 참조를 prefab asset에 Scene 참조로 굽지 않는다. 이후 지형만 갱신하려면 `TacticalArtSetup.BakeOverviewTerrain`을 사용하며 전체 HUD/atlas를 다시 만들지 않는다.

Focus·이전 위치·전체도 이동과 zoom은 Smoothstep을 사용한다. `TacticalHudStyle.CameraMoveSeconds` 기본0.18초, `CameraZoomSeconds` 기본0.16초이며0이면 즉시 적용한다. 연속 이동/drag는 gesture 시작 때만 easing을 시작하고, 모서리 zoom-out의 재중앙화도 실제 현재 위치부터 보간한다. 실제 viewport는 보간 중에도 매 프레임 제한한다. 처음 열 때 framing과 닫을 때 플레이어 시점 복원은 즉시다.

멀어지면 Cannon/Missile 심볼과 적 무리·수량으로 읽는다. 진입/복귀 배율2.5/2.2의 히스테리시스와 짧은 알파 전환을 사용하고, 터렛은 작은 Game View에서도32 화면 픽셀을 유지한다. Player/CU는 기존 심볼을 재사용한다. 상세 이미지는 실제 게임 프리팹의 베이스/헤드 정지 상태를 렌더한 Sprite이며, 전체도 지형도 authored Stage_1을 bake한다. Scene/geometry가 다르면 지형 이미지를 숨긴다. `TacticalArtSetup`은 Edit Mode의 명시 제작 도구다. [생성·입고·검수 기록](../../94_art/tactical-ui-assets-2026-11-14.md)을 따른다.

전술 조작 파츠의 `CombatHudScaleBinding.MinimumScreenScale`는0.8이다. 800px 이상 창에서는16pt 정보가 최소12.8 화면 픽셀로 유지되며, 더 좁은 창에서는 폭에 비례해 줄어든다. 기존 전투 HUD 배율 설정은 유지한다. 적 수량 라벨은 도크·전체도·CU 명령·사건·표시 중인 Alert와 터렛 도상/상태/HP·Player/CU를 피한다. 가까운 빈 위치에도 놓을 수 없으면 수량 카드를 생략하고 실제 적 점은 유지한다.

`PlayerControlState.EnterTacticalView()`의 lease는 전술 이동/전투 제한만 소유한다. `CanIssueTurretCommands`는 이 제한만 제외하고 다른 입력·생존·Match·Presence 잠금을 검사한다. lease를 닫은 프레임의 클릭은 발사로 전달하지 않는다. 카메라는 Canvas 밖의 Scene marker와 `CameraManager.PushView` lease를 사용하며 `TrySetViewSize`/`TrySetViewViewport`는 현재 최상위 target 소유자만 갱신한다. 닫을 때 원래 플레이어 시점·배율·viewport로 즉시 복귀한다.

2026-11-12 기록: HUD v6와 공통 프레임 적용, Editor의 실제 Scene·단계별 입력·시각 확인 완료. [당시 검증 범위·한계](../../94_art/ui-validation-2026-11-12.md)에서 자동 검사와 직접 입력 관측을 구분한다.

2026-11-12: 상태창의 공격력·이동속도·치명률 행은 왼쪽 `+` 투자, 오른쪽 `-` 회수 순서로 배치한다. 두 버튼의 위치만 교환하고 기존 이벤트와 수치 계산은 유지한다.

2026-11-13: 필드 터렛 수리 안내는 포탑 SpriteRenderer의 중앙에 `GravityBold8 SDF` 픽셀 폰트로 `R` 한 글자만 표시한다. 배경·테두리는 숨기고 글자에는 검은 그림자를 적용한다. 수리·복구 진행률은 기존 게이지로 표시한다.

API 기준: main `8d08001a`의 PR #525 냉각·수동 조작 제한 계약이다.
PR #522 리뷰에 맞춰 HUD를 연결했다. 스크립트와 프리팹은
`InitialStagePlayerHUD`, UI 약어는 `UI`로 표기를 통일한다.

[문서 목차](../../README.md) · [터렛 API](../07_turrets/api-reference.md)

## 2026-11-12 HUD v6

현재 전투 표시 기준은 [공통 UI 규칙](../../94_art/common-ui-rules.md)이다. 아래 날짜별 기록의 기존 HP/XP·관리 버튼 배치보다 이 절이 우선한다. `Player Status Panel`과 터렛 조작창·스킬 트리는 기존 기능을 유지한다.

`StagePlayerVitalsView`가 같은 로컬 actor와 CU의 실제 값을 새 `CombatVitalsGroup`으로 전달한다. Player HP는 좌측 헬멧, CU HP/전력은 우측 릴레이 심볼로 구분한다. 각 그룹은 독립적으로 자동 숨김·가장자리 접근·실제 값 변경·클릭 고정을 처리한다. 호버한 바 위에서만 정수 현재/최대 값을 표시한다. 소유자 대기·해제 또는 CU 데이터 부재 시 표시를 지운다. 상시 레벨·XP는 감추며 실제 XP 획득만 월드 팝업으로 표시한다.

`CombatHudHeader`는 같은 씬의 실제 낮밤 시계와 현재 감속 효과를 표시한다. 점유율의 유효 분모가 없으면 점유 제목·수치·분리선을 숨기고 타이머와 배경을 중앙에 맞춘다. `SetConfirmedOccupancy(captured, eligible)`는 유효한 확정 분모/분자를 전달하는 접점이다. Header는 같은 actor·Match의 전술 지역 투영에서 `CapturedCellCount`와 `Definition.CapturableCellCount`를 읽는다. 전체 Grid 셀 수를 분모로 쓰지 않는다. 작은 양수는 소수 또는 `<0.1%`로 표시하며 Stage_1의 시작1/201은0.5%다. 보스 표시는 `CombatBossView.Bind(EnemyHealth, name)`의 실제 보스 소스가 있을 때만 사용한다. 일반 적을 보스로 지정하지 않으며 현재는 숨긴다.

`CombatHudDebugPanel`은 로컬 actor가 있고 Core 개인 Menu가 최상위일 때만 우측에 나타난다. `WeatherDebugHud.CanChangePhase`를 따르며 기존 `ResetTimerAndWeather`/`AdvanceDayNight`로 요청한다. 기존 관리 버튼의 표시만 숨긴다. 자동 활성화는 해당 Menu의 정확한 ID만 닫고 한 프레임 뒤 `InitialStagePlayerHUD.TryRequestAutoActivation()`을 호출한다. 생존·다른 입력 잠금·CU 거리·Host 명령 Port 검사는 원본 경로를 유지한다. `true`는 요청 제출을 뜻하며 Host 확정은 `AutoActivationFeedback` 이벤트로 별도 표시한다. 공유 월드를 정지하거나 다른 입력 lease를 해제하지 않는다.

씬/프리팹 GUID와 로컬 actor 주입 수명을 유지하며 자동 테스트와 직접 조작 검증을 구분한다. 제작 도구는 `HudV6AssetImport`/`HudV6PrefabSetup`, 실행 조정값은 `CombatHudStyle.asset`이다.

## 2026-11-10 플레이어 상태창

`InitialStagePlayerHUD.prefab`의 `Player Status Panel`은 처음에는 비활성화되어 있다. 루트의 `PlayerStatusPanel`이 I로 열기/닫기, ESC와 X 버튼으로 닫기를 처리한다. 배경·텍스트·게이지·버튼·보상 스크롤 행은 프리팹에 미리 배치하며 런타임에 생성하지 않는다. 화면 중앙의 네 구역 구성으로 기존 화면 가장자리 체력/경험치 HUD 위치는 유지한다.

- 왼쪽 위: `PlayerStatusCharacterPreview`가 현재 몸체·무기·앞손 Sprite를 제작된 세 Image에 복사한다. 몸체 pivot과 무기 상대 위치·레이어를 따라가며 카메라·RenderTexture·별도 Player 객체는 생성하지 않는다. 뒤에는 주변 패널과 구분되는 240×240 청회색 사각형 Image를 배치했다. 대시 상태도 표시한다.
- 왼쪽 아래: 레벨/XP/HP와 플레이어 공통 공격력 배율·이동속도·치명률, 남은 스탯 포인트 및 `+` 투자/`-` 회수 버튼이다. 이동속도는 성장과 현재 감속을 반영한다. [기본 스탯/보상 규칙](player-base-stats-api.md)을 사용한다.
- 오른쪽 위: 좌우 화살표는 기존 `PlayerWeaponSelection.SelectAdjacentWeapon()`을 호출하여 **실제 장착 무기**를 순환한다. 기존 하단 슬롯의 Sprite를 무기 이미지에 재사용하고 탄창도 표시한다. 숫자키/휠 교체도 같은 표시를 갱신한다. 기존 선택 자격을 유지하므로 전투 입력이 제한되는 허브 등에서는 화살표를 비활성화한다. 교체로 탄약을 초기화하지 않는다.
- 오른쪽 아래: `PlayerLevelRewardsView`의 ScrollRect·Scrollbar·RectMask2D와 제작된 8개 `PlayerLevelRewardRow`를 재사용하여 최대 레벨까지 나열한다. 2레벨부터 각 행에 `미달성` / `보상받기` / `수령 완료`를 표시한다. 레벨을 달성해도 자동 지급하지 않으며 버튼이 PlayerBaseStats 원본에 수령을 요청한다. 보상 내용은 현재 스탯 포인트 +1이며 다른 재화 종류 지급은 후속이다.
- 무기 수치는 `PlayerAttack.TryGetCurrentWeaponStats(out PlayerWeaponStats)`로 실제 발사/스패너 설정을 조회한다. 피해량은 기본 스탯 배율을 적용한 탄환 한 발 또는 근접 타격 한 번 기준이며 치명타는 제외한다. 산탄 개수는 따로 표시한다. 공격 간격, 탄속, 최대 사거리, 재장전 시간을 함께 보여준다. 원거리 최대 사거리는 탄속×수명으로, 장애물 충돌 전의 이론값이다. 스패너는 근접 범위를 사용하고 탄속/재장전은 해당 없음으로 표시한다.
- 재장전 시간은 실제 재장전도 사용하는 `PlayerWeaponAmmo.GetReloadDuration()`에서, 탄속/수명은 `PlayerBullet.FlightSpeed/FlightLifetime`, 근접 수치는 `PlayerSpannerWeapon.AttackDamage/AttackRange/AttackInterval`에서 읽는다. UI에 별도 밸런스 수치를 저장하지 않는다.
- 스킬 해금 포인트·연구 토큰·인게임 재화는 값 `-`인 표시 자리다. 프로필 재화 조회·저장·보상 처리는 추가하지 않았다. 해당 `Currency Value` TMP를 Core와 추후 연결한다. 무기 강화는 추후 추가 문구만 표시한다.

옆의 `PlayerProgressionDebugPanel`은 에디터/개발 빌드에서만 활성화된다. `XP +10`, `XP +100`, `다음 레벨`, `투자 전체 회수` 버튼을 배치했다. 기존 XP/스탯 공개 API의 권한·필드 정지·중복 수령 검사를 우회하지 않는다. 투자 전체 회수는 경험치와 보상 수령 기록을 지우지 않는다. 일반 출시 빌드에서는 패널을 숨기고 작업 함수도 실행을 거부한다.

플레이어는 `StagePlayerVitalsView.Player`만 사용한다. 다른 Player를 검색해서 대체하지 않으며 로컬 actor 교체/해제·비활성화 시 패널을 닫고 참조를 갱신한다. Core 메뉴·생존 상태 등 기존 입력 잠금이 있으면 상태창을 열지 않고, 열린 패널도 닫는다. 텍스트 입력 중에는 I를 상태창 단축키로 처리하지 않는다.

`PlayerCombatHudSlideController`는 포탑 조작창 또는 상태창이 열리면 기존 하단 무기·탄약·대시 UI를 같은 애니메이션으로 아래에 접어 10px 가장자리만 남긴다. 둘 중 하나라도 열려 있으면 접힌 상태를 유지하고, 모두 닫혀야 제작 위치로 돌아온다. 접힌 무기 슬롯의 클릭 차단도 기존 처리를 사용한다.

전투 탄약은 `CombatPlayerIndicators`가 현재 `PlayerWeaponHud.Owner`의 `PlayerWeaponAmmo`를 읽어 표시한다. 무기 패널 오른쪽의 제작된 `AmmoRoot` 안에 있으며 Player 월드 좌표나 조준 방향으로 움직이지 않는다. 무기 부모의 배율과 접힘을 상속하므로 `CombatHudScaleBinding`에서 탄약을 별도로 다시 축소하지 않는다. 유한 탄창은 현재/최대 숫자만 표시하며 실탄/빈 탄환 이미지는 사용하지 않는다. 재장전 중에는 수량 아래에 작은 회전 화살표를 표시한다. 무기 패널 전체 배경이 UI raycast를 차단하며 필드 커서도 같은 패널 경계를 따른다.

감소 피드백은 `PlayerAttack.ShotFired`의 실제 성공 이벤트에만 연결한다. 수량이 짧게 밝아지며 작게 확대·복귀하고 탄약 영역에 작은 반동을 준다. `ShotFeedbackSeconds`는 Inspector에서 조정한다. 무기 변경으로 수량이 작아지거나 actor를 새로 연결해도 발사 효과를 만들지 않는다. actor 교체/비활성화 때 발사·무기 변경·XP 이벤트를 해제하며 현재 무기와 탄약의 원본 상태는 변경하지 않는다. XP는 계속 Player 주변 World Space 팝업이다.

### 스킬 트리 UI 틀

네 섹터 Image의 색을 `(0.065, 0.125, 0.16)`으로 올렸다. 기존보다 밝고 캐릭터 미리보기 사각형 `(0.1, 0.18, 0.23)`보다 어두워 각 영역을 구분한다. 무기 하단 안내와 레벨 보상 제목 사이 우측 `(530, 50)`에 `스킬 트리` 버튼을 프리팹으로 배치했다.

`PlayerSkillTreePanel`은 버튼 클릭 시 오른쪽 무기/보상 영역을 감추고 같은 자리의 스킬 회로 화면을 연다. 140×140 정사각형 8개와 직각 연결선·접점은 모두 Image/TMP 오브젝트다. 앞 네 칸에는 현재 Q/E 카탈로그 순서인 **시한폭탄 → 플레이어 터렛 → 방벽 투사기 → 유도 비콘**의 이름·기존 HUD 아이콘·설명을 표시한다. 이름과 설명은 `PlayerSkillDescriptions`, 아이콘과 표시 대상 `PlayerSkillId`는 제작된 카드 참조를 사용한다. 나머지 네 칸은 `추가 예정`이다. 이 순서는 임시이며 Dash는 별도 이동 능력이므로 이 목록에 포함하지 않는다.

높이 1840인 Content를 세로 ScrollRect/Scrollbar/RectMask2D로 위에서 아래까지 스크롤한다. 런타임에 노드·선·버튼을 만들지 않는다. 스킬 트리와 레벨 보상 목록의 Scrollbar Handle은 `sizeDelta = (0, 0)`으로 두어 Unity Scrollbar가 계산한 anchor 크기만 사용한다. 여기에 고정 높이를 더하면 맨 아래에서 손잡이가 트랙 밖으로 길게 튀어나온다.

`< 스테이터스` 버튼 또는 ESC는 스킬 트리만 닫고 원래 무기/보상 영역으로 복귀한다. 스킬 화면이 열린 상태의 I 또는 전체 상태창 X는 상태창 전체를 닫는다. 다시 스킬 화면을 열면 맨 위에서 시작한다. 트리가 열려 있는 동안에도 상위 상태창은 열려 있으므로 하단 무기/탄약/대시 UI의 접힘을 유지한다.

이번 스킬 트리 범위는 UI 표시/이동만이다. 표시용 스킬 ID는 연결했지만 선택·사용 가능 여부를 변경하지 않는다. 잠금 상태/Unlock 라벨/포인트 소비/레벨별 스킬 해금 보상/저장/RPC는 추가하지 않았다. 기존 Q/E 스킬 선택창과 동작도 변경하지 않았다. 포인트 수량과 잠금 해제 정책이 확정된 뒤 별도 연결한다. 포털 설치 스킬은 이번 UI에 포함하지 않으며 후속 작업이다.

### ESC 실행 순서와 Core 접점

`PlayerStatusPanel.Update`는 실행 순서 -300으로 `AppRoot.Update`(기본 0)보다 먼저 실행한다. 열린 상태창의 ESC만 `TeamHJD.Game.Presentation.UIEscapeInput.TryConsume()`으로 그 프레임에 소비한다. `AppScreenPresenter.Render`와 `GameInput.WasPressedThisFrame(Escape)`는 같은 소비 여부를 확인하므로 상태창을 닫는 ESC가 메뉴 열기/설치 취소/포탑 선택 해제까지 이어지지 않는다. 패널이 닫혀 있으면 소비하지 않으며 다음 ESC는 기존 Core 메뉴로 전달된다. Core가 Update에서 ESC를 처리하므로 이 검사를 LateUpdate로 옮기면 이미 메뉴가 열린 뒤가 된다.

이 변경의 Core 범위는 ESC 조회 연결과 소비 헬퍼뿐이다. 메뉴의 화면 수명·정지/입력 잠금 정책은 기존 경로를 사용한다. X 및 무기 화살표 클릭도 기존 `GameInput.TryConsumePrimaryPointerPress()`로 소비한다.

검증은 런타임/Editor 코드 컴파일과 프리팹 직렬화 참조·기존 HUD 보존 검사로 구분한다. `PlayerStatusPanelTests`에 I/ESC/X, 무기 교체와 탄창 유지, actor 교체, 기존 입력 잠금 보존 회귀 테스트를 추가했다. Unity 자동 테스트 실행 및 실제 Game View 조작·해상도별 외형 확인은 별도다.

## 2026-11-09 Player 성장 표시 추가

`InitialStagePlayerHUD.prefab`에 `PlayerExperienceView`와 레벨·XP 텍스트, 경험치 채움 오브젝트를 미리 배치했다. 기존 HP 아래 사다리꼴의 빈 영역을 XP 바로 사용한다. 기존 `StagePlayerVitalsView`에 바인딩된 로컬 Player의 `PlayerExperience`를 구독하며, actor 교체·UI 비활성화 시 구독을 해제한다. HP 프레임 스프라이트와 체력 계산은 유지한다. [성장 규칙·보상/저장 연결·검증 범위](player-experience-api.md).

## 10월 15일 CU·지역 표시 추가

`StageControlUnitView`를 기존 HUD 프리팹 우측 하단에 추가했다. 정적/동적 정책과 점령 셀 수를 `FieldTurretController.Regions`에서 읽는다. Dynamic은 지역 잠금이 없고 활성화로 점령한다는 안내를 표시한다. 터렛 선택창에도 동적 점령 안내를 표시한다.

살아 있는 Player가 CU와 interactionDistance(현재 10) 이내에 있고 조작 가능한 상태이면 자동 활성화 버튼을 사용할 수 있다. 버튼은 황재동의 `TryStartAutoActivation()`을 호출하며 진행 중/재실행 대기/전력·냉각 규칙을 새로 구현하지 않는다. 프리팹·실제 Scene Button.onClick 검사는 통과했다. 사람의 마우스 조작·해상도별 외형은 별도다.

현재 CU/터렛 입력은 로컬 경로이고 원격 Client 전송은 남아 있다. 로컬 actor는 이제 Core가 `ILocalPlayerActorConsumer`로 명시적으로 주입한다. [Core 연결 API](../01_core/core-sprint-region-and-request-api.md)를 함께 확인한다.

### 로컬 actor 주입과 종료

`PrepareLocalActor(matchId, playerId)`로 대기 상태를 만들고 생성/소유자가 확인한 뒤 `BindLocalActor(matchId, playerId, actor)`를 호출한다. actor에는 같은 PlayerId의 PlayerActorIdentity와 PlayerInfo가 있어야 한다. `UnbindLocalActor(matchId)`는 같은 Match만 해제한다. 대기/해제 중 다른 Player를 대신 선택하지 않으며 HUD 비활성/재활성은 주입 정보를 보존한다. UI가 actor를 파괴하지 않는다.

InitialStageCompositionRoot가 이 수명을 연결한다. standalone Feature Scene의 기존 검색은 호환 경로일 뿐이며 네트워크 조립부는 SDK 연결에서 확인한 PlayerId와 실제 로컬 actor를 주입한다. Client의 PlayerControlState는 IPlayerNetworkMatchBinding으로 공유 Match 단계를 받아 종료 후 입력을 차단한다. Client에 권한 MatchSession을 만들지 않는다. 현재 각 인스턴스의 자기 로컬 actor만 생성하며 별도 Windows Client Scene의 슬롯·종료/재시작을 확인했다. 원격 actor 생성·이동/공격 복제나 Client HUD 명령/시각 인수를 이 결과로 완료했다고 보지 않는다.

## 적용 위치와 편집 방법

`Assets/Scenes/Gameplay/Initial_Stage.unity`에서
`Assets/Prefabs/UI/InitialStagePlayerHUD.prefab`을 사용한다.
원본 Stage 1 씬과 프리팹 배치는 유지한다. #564에서는 `TurretInteractionUI`의 클릭 처리에도 동일한 UI 검사·클릭 소비 계약을 적용했다.

2026-10-27 #564: 포탑 선택 클릭은 LMB 공격에서 제외한다. 먼 포탑 또는 이미 선택한 포탑을 클릭해도 발사하지 않는다. UI·스킬 설치 중에는 포탑 선택을 처리하지 않으며, 기존 거리 안내와 조작 조건은 유지한다. [클릭 입력 분리 API](player-primary-input-api.md)

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
- 전력은 `FieldTurretController.Budget`의 CurrentPower·MaximumPower·PendingRecovery를
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

## 2026-11-13 플레이어 피격 화면

`PlayerInfo.ApplyDamage`가 실제로 줄인 체력만 `Damaged(int appliedDamage)`로 알립니다. 회복, 체력 표시 갱신, 초기 바인딩과 체크포인트 복원은 피격 이벤트를 발생시키지 않습니다. 치명타도 사망 통지 전에 한 번 전달하며, 피해 권한·일시정지 등 기존 피해 거부 조건을 유지합니다.

`InitialStagePlayerHUD.prefab`의 `PlayerHitFeedback`은 `StagePlayerVitalsView.Player`에 연결된 로컬 actor만 구독합니다. actor 교체·HUD 비활성화 때 구독과 연출을 정리합니다. `PlayerDamageVignette`는 프리팹에 배치된 전체 화면 Graphic으로, 중앙을 비우고 가장자리만 붉게 표시합니다. 전용 Canvas의 sorting order는 -1로 HUD 아래에 표시합니다. 상위 HUD의 시작 연출에 가려지지 않게 CanvasGroup의 `ignoreParentGroups`를 켜고 Graphic의 `maskable`을 끕니다. Raycast와 CanvasGroup 입력 차단은 꺼져 있습니다. 별도 텍스처나 실행 중 UI 오브젝트 생성은 사용하지 않습니다.

Inspector에서 기본 붉은 효과 시간 0.45초, 최대 알파 0.65, 흔들림 시간 0.2초, 거리 0.14를 조절합니다. 피해 비율에 따라 강도를 조절하고 연속 피격은 합산 대신 시간을 다시 시작합니다. 감쇠하는 고정 진동을 사용해 랜덤 스트림을 소비하지 않습니다. `CameraManager.OutputChanged`를 따라 출력 카메라만 흔들고 렌더 콜백 종료에 기존 위치를 복구합니다. 추적·컷씬이 쓰는 카메라 위치에 변위를 누적하지 않습니다. 연출 시간은 unscaled time으로 진행합니다.

`PlayerHitFeedbackTests`는 실제 피해/회복/거부 구분, 사망 직전 이벤트, actor 해제, 카메라 위치 복구와 UI 입력 통과를 검사합니다. `PlayerAssassinAttackRuntimeTests`의 HUD 검사는 Play Mode에서 실제 카메라 렌더 결과의 붉은 가장자리·투명 중앙·효과 소멸을 확인합니다. 원격 Player 체력 복제 경로는 이번 변경에 추가하지 않았으며 Host/Client 피격 전달은 별도 검증이 필요합니다.

## 이후 갱신 기준

### Core 명령 연결 시 유의점 (2026-10-15)

`PlayerControlState`는 `IPlayerInteractionActor`로 현재 actor의 XY·생존·CanAct를 제공한다. Host의 명령 검증은 이 Port를 실제 서버 actor에 연결해 사용한다. 원격 Client의 좌표/생존 payload를 그 대신 넣지 않는다. Player/UI의 서버 actor 생성/복제 경로에서 `InitialStageNetworkBinding.RegisterAuthorityPlayerActor(matchId, playerId, actor)`를 호출하고 반환 lease를 교체/Despawn에 해제한다. 현재 각 인스턴스에는 자기 로컬 actor만 생성되므로 이 API가 존재한다고 상대 이동 복제가 완료된 것은 아니다.

공용 터렛 ON/OFF·CU 자동 활성화·승급·강화/강등은 모든 참가자가 요청하고 Host가 검증하도록 확정했다. Core `TurretCommands` requester와 실제 FieldTurret 명령 서비스는 Scene의 Match 시작/종료에 자동 조립한다. 기존 HUD 버튼은 `ITurretCommandConsumer`로 requester와 표시 source를 주입받아 사용한다. Client의 초기 로컬 FieldTurret 값을 서버 확정 상태로 표시하지 않는다.

### 현재 HUD 입력/표시 흐름

1. Scene이 로컬 actor를 주입한 뒤 `BindTurretCommands(matchId, playerId, requester, presentation)`를 호출한다. 같은 Port 재주입은 구독·대기 요청을 유지하고, 다른 Port로 교체하려면 먼저 명시적으로 해제한다.
2. `StageTurretPresenter`는 `TurretPresentationFrame`의 논리 ID/정의/활성/강화/냉각 상태로 버튼과 의도를 구성한다. Client에 원래 배치된 터렛의 초기 상태로 ON/OFF를 판단하지 않는다.
3. `StageTurretCommandBinding`이 GUID를 만들어 요청하고 해당 대상의 추가 입력을 잠근다. 서버 결과와 그 순번을 반영한 표시 Frame을 모두 받은 뒤 입력을 다시 연다. ExecutionFailed도 일부 부작용이 있을 수 있어 최신 상태를 기다린다. 거부는 즉시 표시한다.
4. HUD 잠시 숨김/재활성화는 대기 추적을 보존하며, Presenter/View 이벤트를 한 번씩 다시 연결한다. Match 해제·실제 파괴는 요청/표시 구독을 정리한다. 다른 Match의 결과나 해제는 현재 UI에 적용하지 않는다.

`TrySelectTurret(entityId)`는 튜토리얼 등 명시적 선택에도 기존 Scene/거리 조건을 적용한다. `RetryPendingTurretCommand(entityId)`는 같은 GUID를 재전송하며 CU는 빈 ID로 지정한다. View/프리팹을 코드로 재생성하지 않는다.

Host는 FieldTurret 원본을 최대 0.1초 주기로 조회해 표시 복사본을 만든다. 전력·CU·자동 작업·레벨/강화·냉각을 포함하며, 내용이 같으면 새 revision을 만들지 않는다. **Host 표시·Client replica·실제 Frame 전송을 구현했다.** App이 접속 전송을 소유하고 Match별 replication이 source/replica를 연결한다. Client는 수신 전 대기 표시한다. 원격 FieldTurret 실제 생성/레벨 외형/공격 복제는 이 표시 복사본이 대신하지 않으며, Client의 오래된 로컬 사거리 원도 표시하지 않는다. 실제 UTP/Scene과 Windows 두 프로세스의 전체 Frame 해시 검사는 통과했다. 후자는 Host interaction actor만 개발 fixture이며 Player/UI의 원격 Player 복제를 검증하지 않는다. 실제 원격 actor 연계와 PL 시각 인수는 [인계 안내](../../99_archive/planning-links/2026-10-15-core-sprint-review-handoff.md)를 따른다.

CU의 점령 수치는 모드뿐 아니라 표시 Frame의 RegionGridId/RegionRevision이 현재 지역 Snapshot과 일치할 때만 결합한다. 독립 채널이 서로 다른 시점의 상태를 전달하면 대기 안내를 표시한다.

실제 HUD 버튼의 ON/OFF·승급/강등·강화·CU·입력 잠금·재활성화, 응답/표시 도착 순서·부분 실패·멱등 주입·구독 해제 검사를 통과했다. 검사 파일/횟수는 [배치 기록](../../99_archive/2026-11-05/planning/2026-10-15-core-sprint-implementation.md#hud-정식-명령표시-투영-연결)을 따른다. 자동 Button 호출 검사이며 사람의 마우스/해상도별 시각 인수를 대신하지 않는다.

플레이어 연동 스크립트를 추가·수정하면 같은 변경에서 책임·호출 조건·연동 API를
갱신한다. CU 자동 활성화 시작 버튼은 현재 정식 명령 경계에 연결돼 있으며 최종 UI 연출/제품 밸런스 확정은 이번 기반 작업과 구분한다.
HUD 요소를 각각 별도 프리팹으로 관리하는 구성은 리뷰의 후속 제안이며,
현재는 상태 View·터렛 View의 책임과 초기화를 분리한 단일 HUD 프리팹을 사용한다.
