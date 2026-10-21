# 플레이어 무기별 스프라이트·이펙트 API

수정일: 2026-10-21. 스패너 브랜치의 작업 중 변경이며 main 반영 전이다.

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

모션은 Down/Up/Left/Right의 4방향이다. 기본 모션은 방향별 4프레임이고, 좌우 기울임 이동과 사격은 방향별 고정 Sprite 한 장을 사용한다. 몸체 PNG는 이전 시안을 포함해 224장 보존하며 현재 클립이 참조하는 고유 그림은 168장이다. 몸체 56클립과 이펙트 16장·4클립이다. 몸체는 160×160, 32 PPU, 중앙 피벗, Point 필터와 중립색·옅은 청록을 포함하는 공통 32색 팔레트를 사용한다. 이펙트는 96×96이며 탄환 128 PPU, 섬광·명중 96 PPU이다.

2026-10-21 비율 보정: 오른쪽을 볼 때 길어 보이던 바이저·턱 실루엣을 짧고 둥글게 편집한 56프레임을 반영했다. 원본은 `Source/RightProportionsGrounded.png`와 `RightProportionsHover.png`다. 전체 몸체 224장은 밝은 헬멧 영역 폭 48픽셀을 목표로 같은 모션·방향의 4프레임에 동일한 균일 배율을 적용했다. 가로·세로를 별도로 늘이지 않으며 기존 헬멧 위치와 프레임별 이동을 유지한다. Sprite GUID·PPU·피벗·클립 참조·고정 이동 자세 선택은 유지했다. 원본과 보정 전 파일은 로컬 제작 자료에 보관했다.

같은 날 사격 자세 후속 수정: Fire/HoverFire 32장과 고정 좌우 이동 자세 8장을 팔을 더 앞으로 뻗고 어깨를 여는 두 손 사격 자세로 교체했다. 정지·상하 이동 사격은 4프레임 반동을 사용하고 좌우 이동 사격은 기존처럼 고정된 이동 그림을 공유한다. 원본은 `Source/ConfidentFire.png`, `ConfidentHeld.png`다. 원래 복장·무기 색상·둥근 헬멧과 기존 Sprite 참조를 유지하며, 발사 피해량·속도·간격을 바꾸지 않는다.

총구 효과는 별 모양 대신 오른쪽으로 퍼지는 짧은 섬광 4장(`Source/DirectionalMuzzleFlash.png`)으로 교체했다. 96×96·96 PPU이며 왼쪽 발사 원점을 고정하려고 Custom 피벗 (0.25, 0.5)를 사용한다. Player 프리팹의 기존 방향별 섬광 회전으로 상하좌우를 표시한다. 발사 효과가 활성화되는 시점과 0.1초 클립은 유지한다.

이동은 발을 번갈아 내딛는 걷기 대신, 발을 내려두고 작은 추진 효과와 함께 미끄러지듯 떠 있는 형태다. 원본은 `Source/HoverLocomotion.png`이며 두 무기의 이동 32장만 교체했다. 프레임의 헬멧 기준 높이를 0/-2/0/+2 픽셀로 등록해 미세한 부유감을 표현한다. 이 오르내림은 Sprite 그림 안에만 있으며 실제 Transform·Rigidbody·Collider를 움직이지 않는다. 몸체의 손은 프레임 사이에 반전하지 않는다.

이동 중 공격하면 부유 공격 48장·12클립을 사용한다. 원본은 `Source/HoverFire.png`, `HoverAttack.png`, `HoverRepair.png`다. `PlayerMotion`의 기존 행동 값은 유지하고 `GetClip(motion, hovering)`으로 표시 자세만 선택한다. 공격 중 이동을 시작하거나 멈춰도 기존 진행률을 유지하며 행동·타격 예약을 새로 시작하지 않는다. 정지/부유 버전의 길이와 타격 지점은 같다.

총을 든 좌우 이동은 별도 LeanLeft/LeanRight 그림을 사용한다. 원본은 `Source/MoveLateralLean.png`, `HoverFireLateralLean.png`다. `PlayerLateralLean`은 Neutral/Left/Right이며 `GetClip(motion, hovering, lean)`으로 조회한다. 좌우 이동 입력이 ±0.01을 넘으면 해당 기울임을 선택하고, 위아래만 이동하거나 멈추면 Neutral로 돌아간다. 조작 차단과 스패너 장착도 Neutral이다. 조준 방향은 기존 Facing으로 유지하고, 움직이는 방향으로 몸체 전체를 반전시키지 않는다. 총기 사격 중에도 같은 기울임을 사용하며 방향을 바꿔도 행동 진행률을 유지한다. 기울임은 그림에 포함되므로 물리 루트·콜라이더 회전은 변경하지 않는다.

좌우 이동 자세는 여러 프레임을 순환하지 않는다. 각 방향의 `MoveLeanLeft/Right_*_1.png` 한 장을 고정하며, 이동 중 사격도 같은 Sprite를 참조한다. 좌우 기울임 전체에서 실제 사용하는 그림은 2자세×4조준 방향의 8장이다. Animator 클립의 모든 Sprite 키가 같은 그림을 가리키므로 재생 시간이 지나거나 사격해도 몸·추진 효과 그림이 바뀌지 않는다. 사격 재생 길이와 발사 섬광은 유지한다.

## 표시와 판정의 책임

- `PlayerMotionSet`: 방향별 원본 Sprite와 AnimationClip, Controller 참조를 보관한다. `GetClip(PlayerMotion)`은 정지 버전, `GetClip(PlayerMotion, bool hovering)`은 이동 중 행동의 부유 버전, 세 번째 `PlayerLateralLean` 인자는 총기 이동/사격의 좌우 쏠림을 조회한다. `duration`/`Sample`은 프레임 제작 기준이고 실제 행동 종료 시간은 AnimationClip 길이다.
- `PlayerSpriteMotion`: 현재 무기와 이동 입력에 따라 Idle/Move 또는 ToolIdle/ToolMove를 선택한다. `PlayRangedShot()`은 Fire를, `PlayToolAction(repairing)`은 Attack/Repair를 시작한다. 이동 중에는 해당 행동의 Hover 상태를 사용한다. 방향이나 이동 여부만 바뀌면 진행 비율을 이어간다. 상태를 고를 뿐 피해량·콜라이더를 변경하지 않는다.
- `PlayerVisual`: 조준 방향에 따른 기존 방향별 콜라이더 선택을 유지한다. `PlayerSpriteMotion.OwnsDisplay`가 true면 Sprite 반전은 AnimationClip에 맡긴다.
- `PlayerWeaponRig`: `_bodyIncludesGunSprite`가 켜진 구성에서는 별도 총 SpriteRenderer를 숨긴다. 방향별 총구·발사 이펙트 오브젝트는 유지한다. 기존 그림과 새 몸체 속 총이 중복되지 않는다. `_usePoseMuzzles`가 켜진 Player 프리팹은 Weapon Rig 로컬 좌표의 `_firingMuzzles`, `_leftLeanMuzzles`, `_rightLeanMuzzles`에서 방향별 총구 위치를 편집한다. `RefreshPose`/`TryGetMuzzle`에서 현재 이동 입력에 맞춰 해당 총구 자식 Transform을 갱신한다. 이 옵션이 꺼진 구성은 기존 Transform 위치를 사용한다.
- `PlayerSpannerWeapon`: 몸체가 무기 그림을 포함해 재생하는 동안 별도 장착 스패너 SpriteRenderer를 숨긴다. 수리 가능 여부로 준비 모션을 고르고, 타격 때 대상 조건을 다시 검사한다.
- `PlayerProjectileView`: Warm/Cryo 비행 클립을 선택하고 명중 시 프리팹을 재생한다. 탄환 이동·피해는 `PlayerBullet`이 계속 담당한다.
- `PlayerSpriteEffect`: 짧은 명중 그림의 색·정렬을 맞추고 지정한 수명 뒤 자기 인스턴스만 제거한다.
- `PlayerMuzzleFlash`: 발사 성공 시 프리팹에 배치된 섬광 오브젝트를 잠깐 활성화한다. 색은 흰 탄심을 유지하는 약한 틴트로 적용한다.

몸체에 그려진 무기까지 회전시키지는 않는다. 실제 발사 방향은 기존 마우스 조준 벡터를 유지하므로 몸체의 네 방향 그림과 대각선 발사각은 근사 관계다. 사격 중 발 위치 그림은 짧은 Fire 포즈로 전환되지만 플레이어 이동은 중단하지 않는다.

## 타격 시점

스패너 판정은 Attack/Repair 클립의 50% 지점에 한 번 적용한다. 기본 공격 0.2초, 수리 0.3초이다. `ToolImpactDelay(repairing)`가 연결된 클립 길이에서 지연 시간을 계산한다. 무기 전환·비활성화·조작 차단은 예약 판정을 취소한다.

성공한 스패너 타격은 `PauseAtImpact(seconds)`로 해당 플레이어 Animator만 잠깐 멈춘다. 기본 0.045초이고 전체 게임 시간·물리·적 AI는 바꾸지 않는다. 취소나 비활성화 시 Animator 속도를 복구한다.

총기 사격의 피해·속도·사거리·산탄 수와 탄환 콜라이더는 변경하지 않는다. 기존 `PlayerBullet.Initialize(Color, float, Vector2)`를 유지하고, 새 오버로드의 `cryogenic` 인자가 비행 그림만 구분한다. 별도 서버 RPC는 추가하지 않았다.

## 제작과 검증

`generate2dsprite`의 에셋 제작·등록 지침을 사용하고 실제 그림은 내장 `image_gen`으로 생성·수정했다(`art_source: host_image`). 영상 API나 Grok은 사용하지 않았다. 사용자의 흰색·회색 복장 유지, 둥근 헬멧, 무채색 무기, 자연스러운 상하 자세 요구를 반영했다.

균일 분할에서 이웃 캐릭터의 총열·공구가 들어오는 문제가 확인돼 연결된 파트를 캐릭터별로 배정한 후 헬멧 기준점으로 등록했다. 프레임별 바운딩 박스로 크기를 재조정하지 않는다. 부유 이동 프레임은 스패너를 오른손에 쥔 그림을 사용하므로 기존 아래보기 이동의 프레임별 flipX 보정은 제거했다.

검증 자료: `Docs/Local/PlayerVisualV2/`의 manifest, 동작별 시트·GIF 및 검증 결과. PNG 128장의 크기·알파·안전 여백은 스크립트로 검사했다. 수치 검사는 자세·동작의 미적 승인이나 실제 플레이 검증을 대신하지 않는다. Unity 에셋 연결 및 실제 플레이 결과는 별도로 기록한다.

부유 공격 추가분 48장·12클립은 `hover-actions-verification.json`으로 크기·알파·여백·프레임 구분·Sprite GUID와 Controller 참조·재생 길이 일치를 검사했다. `HoverActions_KeepOriginalTimingAndHaveMatchingAnimatorStates`는 연결과 길이를, `MovingDuringAction_ChangesHoverPoseWithoutRestartingActionOrMovingRoot`는 이동 시작/정지 시 Animator 상태·진행률·루트 위치 유지를 검사한다. 두 테스트를 추가하고 컴파일했으며 Unity Test Runner에서는 아직 실행하지 않았다.

좌우 쏠림 16클립과 현재 참조하는 고정 그림 8장은 `lateral-lean-verification.json`으로 참조·알파·여백·반복 및 길이를 검사했다. 모든 Sprite 키의 참조가 동일하고 이동·사격이 같은 그림을 쓰는지도 확인했다. `LateralLean_KeepsAimDirectionAndOriginalClipDuration`도 고정 Sprite와 동일 사격 자세를 검사한다. 기존 PlayMode 전환 테스트에는 좌/우/상/정지 입력을 넣어 조준 유지와 사격 진행률 보존을 검사하도록 확장했다. 런타임·Editor 컴파일은 통과했으나 Unity Test Runner/플레이 검증은 아직 수행하지 않았다. 수치 검사가 미적 승인이나 실제 조준 감각 검증을 대신하지 않는다.

비율 보정의 검사 결과는 `proportion-verification.json`, 비교 이미지는 `proportions-before.png`와 `proportions-after.png`다. 224장 크기·이진 알파·여백·PPU·피벗·Sprite 참조를 확인했고, 클립별 헬멧 밝은 영역 폭 중앙값은 46~49.5픽셀이다. 이 값은 밝은 영역의 측정치이며 전체 헬멧 외곽 치수를 뜻하지 않는다. 부유 이동·공격·고정 자세의 기존 에셋 검사도 재실행해 통과했다. 이번 변경은 PNG와 제작 자료만 바꿨으며 실제 Unity 플레이에서의 외형 확인은 남아 있다.

사격 후속 검사는 `shooting-verification.json`에 기록한다. 몸체 40장·섬광 4장, 섬광의 원점 피벗, 자세별 총구 좌표 12개, 프리팹의 물리·다른 컴포넌트 보존을 확인했다. `MovingDuringAction_ChangesHoverPoseWithoutRestartingActionOrMovingRoot`는 이동 입력 변경 시 실제 총구의 Rig 로컬 좌표도 확인하도록 확장했다. 런타임·Editor 빌드는 오류 0건·기존 경고 4건이며 Unity Test Runner/실제 플레이는 아직 실행하지 않았다. `shooting-before-after.gif`는 32 PPU 몸체·96 PPU 섬광을 조합한 제작용 미리보기로 게임 실행 캡처와 구분한다.

현재 V2 검증: 런타임 및 Editor C# 빌드 오류 0건, 기존 경고 4건. 128장 PNG와 32개 AnimationClip의 정적 검사를 수행했다. 부유 이동으로 교체한 32장·8클립은 알파·여백·프레임 구분·GUID 참조·반전 키·반복 길이를 다시 검사했다. Unity 에셋 검증 메서드의 첫 실행에서 Animator Controller 레이어가 저장되지 않은 문제를 발견해 네 컨트롤러를 복구했다. 복구 후 Unity 검증 메서드의 재실행과 실제 플레이 검증은 아직 수행하지 않았다. 임시 자동 검증 코드는 Assets에서 제거했으며 기존 Editor 테스트로 다시 확인할 수 있다.
