# 플레이어 무기별 스프라이트·이펙트 API

2026-11-12 자유 조준 표시: 현재 Player는 기존 `space man` 시트에서 파생한 옆모습 5프레임을 좌우 반전한 몸체와 마우스를 따라 연속 회전하는 별도 무기를 사용한다. `PlayerSpriteMotion.SpaceMan`은 기존 Animator의 행동 타이밍을 유지하고 표시 Sprite만 교체한다. 원본·설정·총구 연결과 검증 범위는 [몸체·무기 분리 API](player-weapon-appearance-api.md#2026-11-12-space-man-몸체와-자유-조준)를 따른다. 아래 V2 8방향 자세 설명은 기존 표시 방식의 기록이다.

2026-11-12 일반 이동 표시: `Player.prefab`의 Rigidbody2D Interpolate를 활성화했다. `PlayerMovement.FixedUpdate`는 기존 `linearVelocity` 적용을 유지한다. 물리 위치 갱신 사이의 렌더 위치를 보간해 카메라가 추적하는 Player 표시의 계단 현상을 줄인다. 이동 속도·대시·충돌·전역 Fixed Timestep·카메라 API는 변경하지 않는다. 프레임 지연이나 다른 렌더 원인까지 해결됐다는 뜻은 아니며 실제 Game View의 끊김 체감은 별도로 확인한다.

2026-11-12: `PlayerSpriteMotion`의 이동 자세와 `PlayerWeaponRig`의 방향 표시는 `PlayerControlState.CanMove`를 따른다. 시작 허브와 진행 중 우주선 방문에서 전투가 막혀도 이동·방향 전환 애니메이션은 동작한다. 메뉴·사망·필드 정지로 이동이 막히면 이동 자세도 멈춘다. Rig 없는 `PlayerVisual`도 같은 기준을 사용하며, 공격·수리·스킬의 기존 사용 조건과 최신 main의 피벗·Y 정렬은 유지한다.

우주선 마우스 방향: `PlayerInputHandler`는 `CameraManager.OutputChanged`를 구독해 현재 로컬 출력 카메라로 커서의 월드 좌표를 계산한다. 최초 연결·재활성화 시 현재 출력을 찾고, 비활성/파괴/태그 변경된 카메라는 다시 연결한다. 출력 교체 시 이전 조준 유효 상태를 비우고 비활성화 시 구독을 해제한다. Manager 없는 구성은 `Camera.main`을 사용한다. 카메라를 직접 움직이거나 새 카메라를 만들지 않는다. `SpaceshipMouseOrientationUsesTheCurrentCameraAfterOutputReplacement`는 실제 마우스 입력으로 허브와 우주선 방문의 8방향 Idle·전투 차단을 확인한다.

2026-10-30 후속: 좌우 방향은 손과 무기의 위치를 유지하도록 별도 원본을 사용하며, `flipX`로 공유하지 않는다. 대각선 대기는 각 방향별 4프레임 호흡 루프를 사용한다. 다리·불꽃 검토는
[독립 좌우 자세와 제트팩 분사구](https://github.com/Team-HJD-21/unity-6-the-developer/blob/6ed4f15650965830dda1139b0a1b4b3baa0e1f78/Docs/art/player-independent-facing-review.md)를 따른다.
아래 기록 중 대각선 대기가 고정 한 장이라는 설명은 확장 전 상태다.

수정일: 2026-10-23. 스패너 브랜치의 작업 중 변경이며 main 반영 전이다.

2026-10-25, Issue #554 작업: 기존 8방향 모션을 유지하고 몸체와 무기 Sprite를 분리했다. 최신 표시 연결은 [몸체·무기 분리 API](player-weapon-appearance-api.md)를 따른다. 아래 날짜별 설명 중 몸체에 무기가 포함된다는 내용은 분리 전 기록이다.

스패너 대각선 확장 후 최신 동작·연결·검사 기록은 [스패너 8방향 자세](https://github.com/Team-HJD-21/unity-6-the-developer/blob/6ed4f15650965830dda1139b0a1b4b3baa0e1f78/Docs/art/player-spanner-eight-way.md)를 참고한다. 아래 날짜별 기록 중 스패너를 4방향으로 설명하는 부분은 확장 전 상태다.

## 에셋 구성

기존 흰색·회색 우주복과 검은 바이저를 유지한다. 헬멧에 추가됐던 귀 패드·뒤 돌기를 제거하고 무기는 검정·회색·흰색·은색으로 맞췄다. 무기 종류에 맞춰 팔 자세를 따로 그렸고, 몸체 그림에 해당 무기가 포함된다.

- 새 원본 및 프레임: `Assets/Art/Sprites/Player/Motions/V2/`
- 원본 시트: 같은 폴더의 `Source/`. 기존 버전의 원본·프레임은 보존한다.
- 실제 재생 설정: `Assets/Data/Player/PlayerMotions.asset`
- 새 Animation Clip과 Controller: `Assets/Art/Animations/Player/WeaponPosesV2/`
- 탄환 명중 프리팹: `Assets/Prefabs/Player/VisualsV2/ProjectileImpact.prefab`
- 총구 섬광: Player 프리팹의 각 방향 총구 아래 `Authored Muzzle Flash`

| 모션 | 길이 | 반복 | 용도 |
| --- | --- | --- | --- |
| Idle | 0.8초 | O | 총을 양손으로 받치는 대기 |
| Move | 0.8초 | O | 양손 조준을 유지하는 부유 이동 |
| Fire | 0.2초 | X | 사격 반동·복귀 |
| ToolIdle | 0.8초 | O | 스패너를 한 손에 쥔 대기 |
| ToolMove | 0.8초 | O | 스패너를 오른손에 든 부유 이동 |
| Attack | 0.4초 | X | 스패너 휘두르기 |
| Repair | 0.6초 | X | 스패너 작업·수리 |
| HoverFire | 0.2초 | X | 이동 중 부유 자세를 유지하는 사격 |
| HoverAttack | 0.4초 | X | 이동 중 부유 자세를 유지하는 스패너 공격 |
| HoverRepair | 0.6초 | X | 이동 중 부유 자세를 유지하는 수리 |
| MoveLeanLeft / MoveLeanRight | 0.8초 | O | 좌우 이동에 따라 몸과 총이 쏠리는 자세 |
| HoverFireLeanLeft / HoverFireLeanRight | 0.2초 | X | 좌우로 쏠리는 자세를 유지하는 사격 |
| MoveLeanBack | 0.8초 | O | 대각선 조준 반대쪽으로 이동하는 고정 자세 |
| HoverFireLeanBack | 0.2초 | X | 같은 대각선 후진 자세를 유지하는 사격 |

총을 든 Idle/Move/Fire/HoverFire와 스패너의 대기·이동·공격·수리는 Down/Up/Left/Right와 대각선 네 방향을 사용한다. 기존 4방향 기본 모션은 방향별 4프레임이고, 좌우 기울임과 대각선 이동·이동 사격은 고정 Sprite 한 장을 사용한다. 대각선 정지 사격은 준비·반동·복귀의 세 그림을 사용한다. 기존 몸체 56클립에 총기 대각선 16클립, 대각선 후진 8클립, 스패너 대각선 24클립을 추가해 총 104클립을 사용한다. 몸체는 160×160, 32 PPU, 중앙 피벗, Point 필터와 공통 팔레트를 사용한다. 기존 탄환·섬광·명중 이펙트는 96×96이며 탄환 128 PPU, 섬광·명중 96 PPU이다. 추진 불꽃은 등쪽 표현을 사용하며 발밑에 별도 불꽃을 추가하지 않는다.

2026-10-21 비율 보정: 오른쪽을 볼 때 길어 보이던 바이저·턱 실루엣을 짧고 둥글게 편집한 56프레임을 반영했다. 원본은 `Source/RightProportionsGrounded.png`와 `RightProportionsHover.png`다. 전체 몸체 224장은 밝은 헬멧 영역 폭 48픽셀을 목표로 같은 모션·방향의 4프레임에 동일한 균일 배율을 적용했다. 가로·세로를 별도로 늘이지 않으며 기존 헬멧 위치와 프레임별 이동을 유지한다. Sprite GUID·PPU·피벗·클립 참조·고정 이동 자세 선택은 유지했다. 원본과 보정 전 파일은 로컬 제작 자료에 보관했다.

같은 날 사격 자세 후속 수정: Fire/HoverFire 32장과 고정 좌우 이동 자세 8장을 팔을 더 앞으로 뻗고 어깨를 여는 두 손 사격 자세로 교체했다. 정지·상하 이동 사격은 4프레임 반동을 사용하고 좌우 이동 사격은 기존처럼 고정된 이동 그림을 공유한다. 원본은 `Source/ConfidentFire.png`, `ConfidentHeld.png`다. 원래 복장·무기 색상·둥근 헬멧과 기존 Sprite 참조를 유지하며, 발사 피해량·속도·간격을 바꾸지 않는다.

대기 자세 후속 수정에서는 Idle/ToolIdle의 좌우 16장만 조금 작게 조정하고 신발의 밝은 면을
흰색·중립 회색으로 맞췄다. 내장 `image_gen`으로 기존 프레임의 신발 색을 편집한 원본은
`Source/IdleSideBoots.png`다. 4프레임마다 같은 균일 배율을 사용해 밝은 헬멧 폭을 약 44.5픽셀로
맞추고 기존 헬멧 중심·160×160 캔버스·32 PPU·Sprite GUID를 유지했다.
상하 대기와 이동·공격·수리 등 나머지 몸체 208장은 수정하지 않았다.

총구 효과는 별 모양 대신 오른쪽으로 퍼지는 짧은 섬광 4장(`Source/DirectionalMuzzleFlash.png`)으로 교체했다. 96×96·96 PPU이며 왼쪽 발사 원점을 고정하려고 Custom 피벗 (0.25, 0.5)를 사용한다. Player 프리팹의 기존 방향별 섬광 회전으로 상하좌우를 표시한다. 발사 효과가 활성화되는 시점과 0.1초 클립은 유지한다.

이동은 발을 번갈아 내딛는 걷기 대신, 발을 내려두고 작은 추진 효과와 함께 미끄러지듯 떠 있는 형태다. 원본은 `Source/HoverLocomotion.png`이며 두 무기의 이동 32장만 교체했다. 프레임의 헬멧 기준 높이를 0/-2/0/+2 픽셀로 등록해 미세한 부유감을 표현한다. 이 오르내림은 Sprite 그림 안에만 있으며 실제 Transform·Rigidbody·Collider를 움직이지 않는다. 몸체의 손은 프레임 사이에 반전하지 않는다.

이동 중 공격하면 부유 공격 48장·12클립을 사용한다. 원본은 `Source/HoverFire.png`, `HoverAttack.png`, `HoverRepair.png`다. `PlayerMotion`의 기존 행동 값은 유지하고 `GetClip(motion, hovering)`으로 표시 자세만 선택한다. 공격 중 이동을 시작하거나 멈춰도 기존 진행률을 유지하며 행동·타격 예약을 새로 시작하지 않는다. 정지/부유 버전의 길이와 타격 지점은 같다.

총을 든 좌우 이동은 별도 LeanLeft/LeanRight 그림을 사용한다. 원본은 `Source/MoveLateralLean.png`, `HoverFireLateralLean.png`다. `PlayerLateralLean`은 Neutral/Left/Right/Back이며 `GetClip(motion, hovering, lean)`으로 조회한다. 상하좌우 조준 중 좌우 이동 입력이 ±0.01을 넘으면 해당 기울임을 선택하고, 위아래만 이동하거나 멈추면 Neutral로 돌아간다. 대각선은 아래의 후진 규칙을 사용한다. 조작 차단과 스패너 장착도 Neutral이다. 조준 방향은 기존 Facing으로 유지하고, 움직이는 방향으로 몸체 전체를 반전시키지 않는다. 총기 사격 중에도 같은 기울임을 사용하며 방향을 바꿔도 행동 진행률을 유지한다. 기울임은 그림에 포함되므로 물리 루트·콜라이더 회전은 변경하지 않는다.

좌우 이동 자세는 여러 프레임을 순환하지 않는다. 각 방향의 `MoveLeanLeft/Right_*_1.png` 한 장을 고정하며, 이동 중 사격도 같은 Sprite를 참조한다. 좌우 기울임 전체에서 실제 사용하는 그림은 2자세×4조준 방향의 8장이다. Animator 클립의 모든 Sprite 키가 같은 그림을 가리키므로 재생 시간이 지나거나 사격해도 몸·추진 효과 그림이 바뀌지 않는다. 사격 재생 길이와 발사 섬광은 유지한다.

## 표시와 판정의 책임

- `PlayerMotionSet`: 방향별 AnimationClip과 Controller 참조를 보관한다. `GetClip(PlayerMotion)`은 정지 버전, `GetClip(PlayerMotion, bool hovering)`은 이동 중 행동의 부유 버전, 세 번째 `PlayerLateralLean` 인자는 총기 이동/사격의 좌우 쏠림을 조회한다. Sprite 키·반복·재생 길이는 AnimationClip이 소유하며 별도 프레임 배열이나 `duration`을 저장하지 않는다.
- `PlayerSpriteMotion`: 현재 무기와 이동 입력에 따라 Idle/Move 또는 ToolIdle/ToolMove를 선택한다. `PlayRangedShot()`은 Fire를, `PlayToolAction(repairing)`은 Attack/Repair를 시작한다. 이동 중에는 해당 행동의 Hover 상태를 사용한다. 방향이나 이동 여부만 바뀌면 진행 비율을 이어간다. 상태를 고를 뿐 피해량·콜라이더를 변경하지 않는다.
- `PlayerVisual`: 조준 방향에 따른 기존 방향별 콜라이더 선택을 유지한다. `PlayerSpriteMotion.OwnsDisplay`가 true면 Sprite와 반전 값은 AnimationClip에 맡긴다. 현재 방향별 원본 클립은 `flipX=0`을 사용한다.
- `PlayerWeaponRig`: `_bodyIncludesGunSprite`가 켜진 구성에서는 별도 총 SpriteRenderer를 숨긴다. 방향별 총구·발사 이펙트 오브젝트는 유지한다. 기존 그림과 새 몸체 속 총이 중복되지 않는다. `_usePoseMuzzles`가 켜진 Player 프리팹은 Weapon Rig 로컬 좌표의 `_firingMuzzles`, `_leftLeanMuzzles`, `_rightLeanMuzzles`에서 방향별 총구 위치를 편집한다. `RefreshPose`/`TryGetMuzzle`에서 현재 이동 입력에 맞춰 해당 총구 자식 Transform을 갱신한다. 이 옵션이 꺼진 구성은 기존 Transform 위치를 사용한다.
- `PlayerSpannerWeapon`: 몸체가 무기 그림을 포함해 재생하는 동안 별도 장착 스패너 SpriteRenderer를 숨긴다. 수리 가능 여부로 준비 모션을 고르고, 타격 때 대상 조건을 다시 검사한다.
- `PlayerProjectileView`: Warm/Cryo 비행 클립을 선택하고 명중 시 프리팹을 재생한다. 탄환 이동·피해는 `PlayerBullet`이 계속 담당한다.
- `PlayerSpriteEffect`: 짧은 명중 그림의 색·정렬을 맞추고 지정한 수명 뒤 자기 인스턴스만 제거한다.
- `PlayerMuzzleFlash`: 발사 성공 시 프리팹에 배치된 섬광 오브젝트를 잠깐 활성화한다. 색은 흰 탄심을 유지하는 약한 틴트로 적용한다.

몸체에 그려진 무기까지 회전시키지는 않는다. 실제 탄환은 기존 연속 마우스 조준 벡터로 발사하고, 총기 외형과 총구 자식은 가장 가까운 8방향을 사용한다. 사격 중 발 위치 그림은 짧은 Fire 포즈로 전환되지만 플레이어 이동은 중단하지 않는다.

## 바닥 그림자 프리팹

`Assets/Prefabs/Player/PlayerGroundShadow.prefab`은 Transform과 SpriteRenderer로 구성한
반투명 검정 원형 그림자다. 기존 `TurretRangeCircleSmooth` 스프라이트와 조명의 영향을 받지 않는
`Sprites-Default` 재질을 사용한다. 별도의 런타임 생성·추적 스크립트와 충돌 컴포넌트는 없다.

`Player.prefab`의 직계 자식인 중첩 프리팹으로 배치했다. 루트 `PlayerGroundShadow`는 발 기준점이며
자식 `Shadow Visual`이 실제 원형 그림과 크기를 가진다. 기본 Visual Scale은 `(1.7, 0.65, 1)`이다.
색상은 검정·Alpha `0.28`, Sorting Layer는 `Default`, Order in Layer는 `4`로 몸체의 `5`보다 뒤에 둔다.

56개 몸체 Animation Clip에 자식 경로 `PlayerGroundShadow`의 Position/Scale 키를 추가했다.
현재 참조하는 168장 Sprite의 발 아래 위치를 제작 단계에서 계산하고 Sprite 교체 시점과 같은
시간에 위치 키를 기록했다. 런타임에 텍스처를 읽거나 발을 탐색하지 않는다.
크기는 하체 폭과 방향별 프로필을 사용하며 기본 상하 폭 1.7, 좌우 폭 1.95, 기울임 폭 2.1을
최소 기준으로 한다. 무기의 길이는 크기 기준에 포함하지 않는다. 한 클립 안에서는 가로폭을
일정하게 유지하며 세로폭·투명도도 유지한다. 동일 그림을 공유하는 기울임 이동·사격은
동일한 발 위치와 폭을 사용한다.

Prefab Mode에서 `Shadow Visual`의 Transform으로 전체 위치 보정·기본 크기를 조절하고
SpriteRenderer의 Color Alpha로 농도를 조절한다. 특정 행동만 보정하려면 Animation 창에서
해당 클립의 `PlayerGroundShadow` Position/Scale 키를 수정한다. 애니메이션은 그림자 루트만
움직이며 플레이어 루트·Rigidbody·Collider·총구에는 키를 추가하지 않는다.
Sprite 참조·기존 클립 길이·반복 설정도 유지한다. 원형 원본과 다른 터렛 에셋은 수정하지 않는다.
프리팹·키·참조 보존은 정적으로 검사하며 실제 Unity 재생과 발 정렬·가려짐 확인은 별도다.

## 타격 시점

2026-10-21 구조 개선: `PlayerMotionSet.Clip`의 Sprite 배열 56개와 중복 재생 시간 14개를
제거했다. 클립 연결 이름과 GUID는 유지한다. 프레임을 수정할 때 Animation 창에서
해당 AnimationClip만 편집하면 표시와 행동 종료 시간에 같은 데이터가 사용된다.
`Frames`와 `Sample` 조회는 제거했으며, Editor 검증은 클립의 실제 Sprite 키를 읽는다.
`Clip.IsConfigured`는 네 방향 모두 유효한 길이의 클립인지 검사하고,
`HasAnimatorConfiguration`은 전체 모션과 Controller 참조를 함께 검사한다.
기존 56개 몸체 클립의 그림자 키·길이·Sprite와 Controller는 수정하지 않았다.

스패너 판정은 Attack/Repair 클립의 50% 지점에 한 번 적용한다. 기본 공격 0.2초, 수리 0.3초이다. `ToolImpactDelay(repairing)`가 연결된 클립 길이에서 지연 시간을 계산한다. 무기 전환·비활성화·조작 차단은 예약 판정을 취소한다.

성공한 스패너 타격은 `PauseAtImpact(seconds)`로 해당 플레이어 Animator만 잠깐 멈춘다. 기본 0.045초이고 전체 게임 시간·물리·적 AI는 바꾸지 않는다. 취소나 비활성화 시 Animator 속도를 복구한다.

기본 총과 냉기 무기는 한 번의 발사마다 조준 방향을 중심으로 5발·25도 산탄을 생성한다. 기본 총은 `_defaultGunPelletCount`·`_defaultGunSpreadAngle`, 냉기는 `_cryoPelletCount`·`_cryoSpreadAngle`로 별도 조정한다. 기존 `PlayerBullet.Initialize(Color, float, Vector2)`를 유지하고, 새 오버로드의 `cryogenic` 인자가 비행 그림만 구분한다. 별도 서버 RPC는 추가하지 않았다.

## 제작과 검증

`generate2dsprite`의 에셋 제작·등록 지침을 사용하고 실제 그림은 내장 `image_gen`으로 생성·수정했다(`art_source: host_image`). 영상 API나 Grok은 사용하지 않았다. 사용자의 흰색·회색 복장 유지, 둥근 헬멧, 무채색 무기, 자연스러운 상하 자세 요구를 반영했다.

균일 분할에서 이웃 캐릭터의 총열·공구가 들어오는 문제가 확인돼 연결된 파트를 캐릭터별로 배정한 후 헬멧 기준점으로 등록했다. 프레임별 바운딩 박스로 크기를 재조정하지 않는다. 부유 이동 프레임은 스패너를 오른손에 쥔 그림을 사용하므로 기존 아래보기 이동의 프레임별 flipX 보정은 제거했다.

검증 자료: `Docs/Local/PlayerVisualV2/`의 manifest, 동작별 시트·GIF 및 검증 결과. PNG 128장의 크기·알파·안전 여백은 스크립트로 검사했다. 수치 검사는 자세·동작의 미적 승인이나 실제 플레이 검증을 대신하지 않는다. Unity 에셋 연결 및 실제 플레이 결과는 별도로 기록한다.

부유 공격 추가분 48장·12클립은 `hover-actions-verification.json`으로 크기·알파·여백·프레임 구분·Sprite GUID와 Controller 참조·재생 길이 일치를 검사했다. `HoverActions_KeepOriginalTimingAndHaveMatchingAnimatorStates`는 연결과 길이를, `MovingDuringAction_ChangesHoverPoseWithoutRestartingActionOrMovingRoot`는 이동 시작/정지 시 Animator 상태·진행률·루트 위치 유지를 검사한다. 두 테스트를 추가하고 컴파일했으며 Unity Test Runner에서는 아직 실행하지 않았다.

좌우 쏠림 16클립과 현재 참조하는 고정 그림 8장은 `lateral-lean-verification.json`으로 참조·알파·여백·반복 및 길이를 검사했다. 모든 Sprite 키의 참조가 동일하고 이동·사격이 같은 그림을 쓰는지도 확인했다. `LateralLean_KeepsAimDirectionAndOriginalClipDuration`도 고정 Sprite와 동일 사격 자세를 검사한다. 기존 PlayMode 전환 테스트에는 좌/우/상/정지 입력을 넣어 조준 유지와 사격 진행률 보존을 검사하도록 확장했다. 런타임·Editor 컴파일은 통과했으나 Unity Test Runner/플레이 검증은 아직 수행하지 않았다. 수치 검사가 미적 승인이나 실제 조준 감각 검증을 대신하지 않는다.

비율 보정의 검사 결과는 `proportion-verification.json`, 비교 이미지는 `proportions-before.png`와 `proportions-after.png`다. 224장 크기·이진 알파·여백·PPU·피벗·Sprite 참조를 확인했고, 클립별 헬멧 밝은 영역 폭 중앙값은 46~49.5픽셀이다. 이 값은 밝은 영역의 측정치이며 전체 헬멧 외곽 치수를 뜻하지 않는다. 부유 이동·공격·고정 자세의 기존 에셋 검사도 재실행해 통과했다. 이번 변경은 PNG와 제작 자료만 바꿨으며 실제 Unity 플레이에서의 외형 확인은 남아 있다.

사격 후속 검사는 `shooting-verification.json`에 기록한다. 몸체 40장·섬광 4장, 섬광의 원점 피벗, 자세별 총구 좌표 12개, 프리팹의 물리·다른 컴포넌트 보존을 확인했다. `MovingDuringAction_ChangesHoverPoseWithoutRestartingActionOrMovingRoot`는 이동 입력 변경 시 실제 총구의 Rig 로컬 좌표도 확인하도록 확장했다. 런타임·Editor 빌드는 오류 0건·기존 경고 4건이며 Unity Test Runner/실제 플레이는 아직 실행하지 않았다. `shooting-before-after.gif`는 32 PPU 몸체·96 PPU 섬광을 조합한 제작용 미리보기로 게임 실행 캡처와 구분한다.

현재 검증: `shadow-and-idle-validation.json`으로 몸체 56클립의 Sprite 키·길이·반복·GUID 보존,
그림자 Position/Scale 키의 자식 경로, 플레이어 루트에 Transform 키가 없는지 확인했다.
좌우 대기 16장의 160×160·이진 알파·Sprite GUID와 나머지 몸체 208장의 원본 바이트 보존을 확인했다.
그림자 폭은 1.7~2.1이며 각 클립 안에서는 일정하다. 부유 이동·부유 행동·고정 기울임 에셋 검사도
그림자 자식 키를 허용하도록 갱신해 통과했다. 기존 C# 빌드는 오류 0건·기존 경고 4건이며
이번 후속 수정은 에셋만 변경했다. Unity의 실제 클립 임포트·Test Runner·플레이 확인은 남아 있다.
로컬 `validation.txt`의 이전 Controller 실패는 이번 변경의 검증 결과가 아니다.

## 좌우 반대 이동 자세 (2026-10-22)

- `Backpedal/Backpedal_Left.png`: 왼쪽 조준·오른쪽 이동. 상체는 화면 오른쪽으로 눕고, 하체와 발은 화면 왼쪽 앞으로 나오는 고정 자세.
- `Backpedal/Backpedal_Right.png`: 오른쪽 조준·왼쪽 이동. 상체는 화면 왼쪽으로 눕고, 하체와 발은 화면 오른쪽 앞으로 나오는 대칭 자세.
- 생성 경로는 내장 `image_gen`이다. 사용자가 선택한 **현재 AI 대기·이동 스프라이트**를
  기준으로 다시 생성했다. `Idle_Left_0.png`·`Idle_Right_0.png`가 외형·비율 기준이고,
  이전 Backpedal은 자세 참고로만 사용했다. AI 적용 전 원본이나 직전 시안 대비 축소율을 기준으로 삼지 않는다.
- 최신 원본은 V2/Source의 `Backpedal_*_IdleMatched_Source.png`에 보존한다.
  원본과 기존 대기·이동 그림은 그대로 두고, Backpedal 두 장만 교체한다.
  헬멧의 흰 부분만 보지 않고 검은 바이저·윤곽까지 포함한 전체 머리 폭을 비교한다.
  좌우 모두 대기 기준 54px이며, 가로·세로에 동일한 배율을 사용해 얼굴을 늘이지 않는다.
  실제 파일은 160×160, 32 PPU, 중앙 피벗, 기존 공통 팔레트와 이진 알파로 패키징했다.
- 기존 방향/이동 판정은 이미 조준 방향과 이동 X를 따로 선택한다. 새 런타임 상태를 추가하지 않고
  `MoveLeanRight_Left`·`HoverFireLeanRight_Left`,
  `MoveLeanLeft_Right`·`HoverFireLeanLeft_Right`의 그림만 새 자세로 연결했다.
  이동과 사격에서 동일한 고정 그림을 유지하며 나머지 자세와 애니메이션 길이/반복은 보존한다.
- 새 그림의 발 위치에 맞춰 해당 네 클립의 그림자 Position 키를 맞추고, Player 프리팹의
  `_rightLeanMuzzles.left`, `_leftLeanMuzzles.right`를 실제 총구 끝에 맞췄다.
  플레이어 루트/Rigidbody/Collider를 기울이지 않는다.
- 회귀 테스트는 두 반대 방향 조합의 실제 Sprite/Animator 상태, 총구 방향과 루트 위치를 검사한다.
  컴파일·참조·프레임/그림자 키의 정적 검증을 수행했으며 Unity Test Runner/실제 플레이는 별도 확인이 필요하다.

## 기본 총 단일 탄환 시도 (2026-10-22, 이후 아래 5발 복원으로 대체)

- `PlayerAttack`에서 기본 총의 단일 발사와 냉기 산탄 발사를 분리했다. 기본 총은 공격 입력 유지 중
  0.2초마다 한 발, 탄당 피해 13이다. 기존 5발이 전부 맞을 때의 최대 피해 65를 한 발에 합산하지 않는다.
  공유 탄환 프리팹의 속도는 45, 수명은 약 1.333초이며 기본 총과 냉기 총에 함께 적용한다.
  이전 속도 30·수명 2초보다 1.5배 빠르고, 장애물이 없을 때 최대 비행거리는 약 60으로 유지한다.
  기존 탄환 콜라이더는 유지한다.
- `WeaponType.DefaultGun = 0`으로 명칭을 맞췄으며 직렬화된 무기 값 0/1/2는 보존한다.
  기본 총 설정은 `_defaultGunColor`·`_defaultGunDamage`·`_defaultGunInterval`이다.
  기존 `_shotgun*`, `_pelletCount`, `_spreadAngle` 필드는 `FormerlySerializedAs`로 이관한다.
- `FX/Warm/Warm_0.png`부터 `Warm_3.png`까지 흰색 중심과 주황색 꼬리가 있는 비행 그림으로 교체한다.
  `Warm.anim`과 프리팹의 GUID·연결은 유지하므로 Inspector와 Animation 창에서 계속 수정할 수 있다.
  꼬리는 시각 효과이며 전체 꼬리에 피격 판정을 늘리지 않는다. 냉기 그림은 그대로 둔다.
- 내장 `image_gen`으로 2×2 FX 시트를 생성하고 공통 배율·16색 팔레트·이진 알파·96×96으로 패키징했다.
  원본은 `V2/Source/DefaultGunProjectile_Source.png`, 프롬프트·생성 경로·파일 검증은
  `Docs/94_art/default-gun-projectile.json`에 보존한다.
- `DefaultGun_FiresOneStraightBulletAndRespectsFireInterval`은 단일 생성·조준·연사 간격을,
  `CryoBlaster_PreservesMuzzlePositionPelletCountAndSpread`는 냉기 산탄 유지를 검사한다.
  테스트 소스와 빌드를 확인했으며 Unity Test Runner/플레이 검증은 별도 진행한다.

## 8방향 대기 외형 시안 (2026-10-23)

현재 총을 든 대기 외형을 기준으로 대각선 네 방향의 고정 자세를 제작한다.
기존 상하좌우는 `Idle/Idle_{Down,Up,Left,Right}_0.png`를 그대로 사용하고,
추가 그림은 `IdleEightWay/Idle_{DownLeft,DownRight,UpLeft,UpRight}.png`에 보관한다.
총 여덟 방향의 원본·프롬프트·참조 파일·비율 측정값은 `Docs/94_art/player-idle-eight-way.json`을 참고한다.
추가 그림은 160×160, 32 PPU, 중앙 피벗, Point 필터, 기존 중립색 팔레트와 이진 알파를 사용한다.
머리·몸·무기를 가로와 세로로 따로 늘이지 않고 전체에 동일한 배율을 적용하며 발 기준선을 맞춘다.

최초에는 대기 외형 확인용으로 제작했으며, 후속 요청으로 아래의 총기 8방향 연결에 사용한다.
스프라이트는 방향별 한 장의 고정 그림이다.

## 총기 8방향 연결과 추진 효과 (2026-10-23)

- `PlayerAimFacing`은 기존 Down/Right/Up/Left 값 0~3을 유지하고 대각선을 4~7에 추가한다.
  `GetFacing`은 기존 4방향 계약이고 `GetEightWayFacing`은 45도 간격으로 구분한다.
  유효하지 않은 벡터는 Down이다. 탄환의 실제 비행 방향은 이 각도로 강제하지 않는다.
- `PlayerMotionSet.Clip`에 대각선 네 클립을 추가한다. `HasEightWayRanged`는 Idle/Move/Fire/HoverFire의
  여덟 방향이 모두 연결됐을 때만 true다. `PlayerWeaponRig`은 해당 구성이 있고 몸체에 총이 포함된
  원거리 무기에서 8방향을 사용한다. 스패너는 기존 4방향이며 콜라이더는 기존 4방향 선택을 유지한다.
- 대각선 이동은 고정 부유 자세를 사용한다. 이동 중 사격도 해당 Move 그림을 공유하므로 발사할 때
  지면 자세로 내려오지 않는다. 기존 상하좌우 이동의 좌우 기울임은 유지한다. 대각선에는 별도
  좌우 기울임 변형 대신 아래의 대각선 후진 전용 자세를 사용한다. 추진 불꽃은 등쪽에만 표시한다.
- 새 뒤쪽 대각선 Move/Fire는 내장 `image_gen`으로 제작했다. 앞쪽 대각선 총 각도 편집은 생성 도구에서
  거절돼 승인된 앞쪽 대각선 대기 그림을 사용한다. 앞쪽 Move는 전체 그림을 3픽셀 위에 등록하며
  새로운 앞쪽 사격 반동 그림을 제작했다고 간주하지 않는다. 제작 정보·프롬프트·원본·총구 픽셀은
  `Docs/94_art/player-ranged-eight-way.json`, 비교 이미지는 `player-ranged-eight-way-preview.png`에 보존한다.
- Player 프리팹의 Weapon Rig 아래 총기·총구·섬광 자식 네 벌을 추가해 총 여덟 방향을 배치했다.
  `_idleMuzzles`·`_moveMuzzles`·`_firingMuzzles`에 자세별 위치를 저장한다. 총구 픽셀을 Player 로컬
  좌표로 바꾼 뒤 실제 Rig 로컬 배율 1.6666667로 나누므로, Rig 배율이 총구 오프셋에 중복 적용되지 않는다.
  기존 세 MuzzlePose의 상하좌우 좌표도 같은 단위로 보정한다. Rig 배율을 바꾸면 이 좌표를 다시 맞춰야 한다.
- `TryGetMuzzle(out muzzle, forShot: true)`는 첫 발 시점에도 Fire 총구를 사용한다. 화면 표시 위치를
  동기화하는 `PlayerMuzzleAnchor`의 LateUpdate는 `forShot: false`로 현재 대기/사격 자세를 따른다.
  각도 전환과 이동 시작/정지는 행동의 진행 비율을 유지한다.
- 사용자의 후속 요청으로 새로 추가했던 발밑 추진 불꽃의 프리팹·스크립트·Animator·클립을 제거했다.
  기존 등쪽 불꽃과 몸체 그림은 유지하며, 8방향 자세·총구·그림자 연결도 유지한다.
- 각도 경계, 여덟 방향 클립/상태와 첫 발 총구 테스트를 추가했다.
  C# 빌드와 에셋 정적 검증을 수행하며 Unity Test Runner/실제 플레이 검증은 별도로 기록한다.

## 대각선 후진 자세와 등 불꽃 보완 (2026-10-23)

- 대각선 조준 중 이동 방향과 조준 방향의 정규화 내적이 -0.5보다 작으면 `Back` 자세를 선택한다.
  반대 방향 중심의 120도 범위이며 수직으로 비껴가는 이동과 정방향 이동은 일반 자세를 유지한다.
  8방향 총기 설정과 Move/이동 Fire에만 적용한다. 새 후진 클립 네 방향이 모두 연결됐을 때만 사용한다.
- `BackpedalEightWay/Backpedal_{DownLeft,DownRight,UpLeft,UpRight}.png` 네 장을
  `MoveLeanBack_*`와 `HoverFireLeanBack_*`가 공유한다. 머리와 상체는 뒤에, 발은 앞에 오는 자세다.
  현재 대기 그림의 헬멧 폭 약 54픽셀에 동일 배율로 맞췄다. 원본과 프롬프트는
  `Docs/94_art/player-diagonal-backpedal.json`, 비교 이미지는 `player-diagonal-backpedal-preview.png`다.
- `_backpedalMuzzles`는 새 후진 그림의 총구 좌표이며 다른 자세와 같은 Weapon Rig 로컬 단위다.
  방향이나 전진/후진 전환은 사격 진행률을 유지한다. 이동과 이동 사격의 그림자 키도 같은 발 위치를 사용한다.
- 등 불꽃은 기존 제트팩 아래의 짧은 하얀 표현을 기준으로 한다. 후속 수정에서는 한쪽만 보이던 자세와
  불꽃 방향이 어긋났던 14자세·20프레임을 양쪽 불꽃으로 교체했다. 양쪽이 이미 맞는 나머지 28프레임은 유지한다.
  `Source/BackpackExhaust_PairedMaster.png`는 생성 원본, `FX/Backpack/BackpackExhaust.png`는 공통 흰 불꽃이다.
  제작 단계에서 자세별 제트팩 노즐 두 곳에 배치하고, 머리에서 골반으로 향하는 몸의 축을 따라 같은 각도로 회전한다.
  가려지는 반대편 불꽃도 끝이 보이도록 등록한다. 교체 대상의 기존 불꽃은 제거하므로 중복으로 표시하지 않는다.
  몸체의 불투명 픽셀은 기존 불꽃 제거 영역 밖에서 보존하며 기존 흰색·중립 팔레트와 이진 알파를 사용한다.
  등 불꽃은 Sprite 안의 그림이고 새 런타임 컴포넌트나 발밑 이펙트는 없다.
  원본·프롬프트·색상 처리·검증 기록은 `Docs/94_art/player-backpack-exhaust.json`에 보관한다.
- `DiagonalBackpedal_PreservesShotPhaseAndUsesTheSameMovingSpriteAndMuzzle`는 자세 전환 시
  사격 진행률·Sprite·총구·물리 루트 보존을 검사한다. 컴파일 및 에셋 연결을 정적으로 확인하며,
  Unity Test Runner와 실제 플레이 결과는 별도다.

## 대각선 정지 반동과 부유 자세 후속 수정 (2026-10-23)

- `Fire_{DownLeft,DownRight,UpLeft,UpRight}.anim`에 실제 Sprite 교체 키를 연결한다.
  0초 준비(기존 Idle), 0.05초 반동, 0.1초 복귀, 0.15초 준비 자세이며 전체 길이는 기존 0.2초다.
  `RangedEightWay/Fire_*.png`는 반동, `FireRecover_*.png`는 복귀 그림이다. 두 키 자세는 생성 도구로 그렸고
  원래 Idle의 발 기준점에 균일 배율로 등록한다. 첫 발 총구도 Idle 위치를 사용한다.
- 아래쪽 대각선 `Move_DownLeft/DownRight.png`의 발을 넓게 벌린 정지 자세를 바꾸고,
  무릎을 살짝 접은 부유 자세와 원근에 따른 앞뒤 발 위치로 수정한다. 이동 사격은 같은 이동 그림을 공유한다.
- 대각선 이동/후진 여덟 자세의 불꽃은 캐릭터·제트팩과 함께 다시 그린 이미지다.
  공통 불꽃을 별도로 붙이는 방식은 이 여덟 자세에서 사용하지 않는다. 기존 상하좌우 그림은 유지한다.
  후진 자세의 가스는 등 가방의 노즐에서 다리가 뻗은 방향으로 흐른다. 왼쪽 대각선은 아래 왼쪽,
  오른쪽 대각선은 아래 오른쪽이며 발·허리에 발생기를 붙이지 않는다.
- 다시 그린 그림에 맞춰 그림자 자식의 위치 키와 이동/후진 총구 좌표를 갱신한다.
  Animator 상태·클립 GUID·사격 간격·공격 판정·플레이어 루트와 콜라이더는 유지한다.
- 실제 원본·프롬프트·등록 배율·클립 키와 검증 결과는 `Docs/94_art/player-diagonal-polish.json`에 보관한다.
  `player-diagonal-recoil-preview.gif`와 `player-integrated-hover-preview.png`는 에셋 미리보기이며 Unity 실행 캡처는 아니다.
  `StationaryDiagonalShot_HasRecoilAndRecoveryThenReturnsToIdle`는 정지 반동과 준비 자세 복귀를 검사한다.
