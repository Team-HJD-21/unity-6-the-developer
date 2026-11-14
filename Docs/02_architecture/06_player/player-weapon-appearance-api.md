# 플레이어 몸체와 무기 스프라이트 분리

## 2026-11-12 Space Man 몸체와 자유 조준

현재 `Player.prefab`은 `PlayerSpriteMotion._useSpaceManBody`와 `PlayerWeaponVisual._continuousAim`을 켠다. 아래의 Canonical 8방향 연결 설명은 두 옵션을 모두 끈 기존 표시 방식에 해당한다.

- 몸체는 원본 `Indicators/space man.png`의 옆모습 5프레임에서 파생한 `SpaceManBody/Side_*`만 사용한다. 원본 시트와 GUID는 보존하며, 파생 프레임은 160×160, 32 PPU, Point다. 2026-11-13에는 옆모습 피벗을 (0.55625, 0.26), 즉 픽셀 (89, 41.6)으로 맞춰 발 중심 X가 좌우 반전에도 고정되도록 했다. 왼쪽 옆모습을 반전해 오른쪽을 표시한다. 위·아래를 조준해도 앞·뒤 모습으로 바뀌지 않으며, 정확한 수직 조준에서는 마지막 좌우 방향을 유지한다.
- `PlayerSpriteMotion.SpaceMan`은 Animator 평가 뒤 몸체 Sprite만 교체한다. 대기는 5 FPS, 이동은 10 FPS이며 기존 Animator의 공격·수리 진행률과 타격 멈칫을 유지한다. 물리 루트나 콜라이더를 애니메이션으로 움직이지 않는다.
- `PlayerWeaponVisual.ContinuousAim`은 기존 네 무기 아이콘을 별도 Sprite로 표시한다. 무기는 몸체보다 그룹 내부 정렬 순서가 1 높아 손 앞에 보이며, 기존 전경 손 레이어는 끈다. 왼쪽을 겨눌 때는 무기 자체를 세로 반전해 조준경이 아래로 뒤집히지 않게 한다.
- 조준은 플레이어 로컬 `_aimMount`의 월드 위치에서 마우스를 향한다. 발 기준 정렬 후 왼쪽 몸체의 기준점은 (-0.28125, 0.8)이며 오른쪽 몸체에서는 X를 반전한다. 회전 기준점은 그 방향으로 `_aimRadius` 0.35만큼 떨어진 곳에 고정한다. 무기별 `length`, `grip`, `muzzle`로 크기와 회전 기준점·총구를 편집한다. `grip`과 `muzzle`은 Sprite rect의 정규화 좌표다. 큰 무기 비율의 참고 이미지에 맞춰 기존 15% 확대 상태에서 네 무기를 약 1.6배 더 키웠다. 현재 `length`는 기본총 4.05, 냉각총 3.5, 스나이퍼 4.65, 스패너 3.87이며 `grip.x`를 0.5로 맞춰 무기 중간에서 회전한다. 두 점의 Y를 같게 유지하면 총열과 탄환이 같은 마우스 직선을 따른다.
- `PlayerAttack` → `PlayerWeaponRig` → `PlayerWeaponVisual`이 같은 조준 방향과 회전된 실제 총구를 사용한다. 첫 발과 무기 전환 시에도 표시 갱신을 먼저 수행하며 총구 섬광도 같은 각도로 표시한다. 탄창·피해·발사 간격·권한 규칙은 기존 경로를 유지한다.
- 세 총의 총구는 PNG의 실제 총열 끝에 맞춘다. `PlayerWeaponRig`은 자유 조준 중 같은 총구·섬광 오브젝트를 유지하며, `PlayerMuzzleAnchor`(LateUpdate 290)가 FirePoint 위치·회전을 맞추고 `PlayerMuzzleFlash`(295)가 표시 중인 섬광을 따라 붙인다. `Flash_0..3`의 GUID는 보존하고 피벗을 분사 시작점 (0.25, 0.5)로 통일했다.
- 2026-11-13에는 기본총·냉각총이 공유하는 `PlayerBullet`의 그림과 판정 중심을 총열 선에 맞췄다. 기존 Warm/Cryo 탄환 본체와 Polygon 중심의 로컬 Y 약 0.180592를 0으로 보정하며, 비행 8프레임의 피벗과 Collider Offset만 함께 조정한다. 발사 원점·머즐 섬광·속도·산탄 수·Polygon 외곽 크기와 GUID는 유지한다. 스나이퍼 탄환은 이 보정에 포함하지 않는다.
- 그림자는 기존 `PlayerGroundShadow`를 사용하며 좌우 공통 발밑 (로컬 X 0, Y 0.10625)에 둔다. `PlayerSpriteMotion.SpaceMan`이 Animator 평가 후 이전 자세의 그림자 위치·스케일 키를 덮어쓴다. 그림자 자식의 기존 오프셋은 보존하며, 타원은 1.4×0.5 배율로 몸체 뒤에 표시한다.
- `Player.prefab`의 충돌·피격 구성은 Left/Right 두 방향만 둔다. 두 발밑 캡슐은 중심 (로컬 X 0, Y 0.10625)과 크기 1.25×0.5가 같아 방향 전환으로 이동 충돌 범위가 옆으로 뛰지 않는다. 피격 Trigger와 기존 비 Trigger 본체는 옆모습 5프레임의 연결된 몸체 외곽을 합친 고정 Polygon을 사용하며, 피벗 이동량만큼 좌우 Offset을 보정한다. 떨어진 추진 불꽃과 별도 무기는 이 외곽에서 제외한다. `PlayerDirectionalColliders`가 발밑·피격·비 Trigger 본체를 함께 전환하며 Layer·Trigger·Rigidbody 설정과 암살자 감지 범위는 보존한다. 상하·대각선 Collider 자식은 현재 프리팹에서 제거한다. 다른 프리팹의 기존 8방향 구성은 계속 지원한다. `GroundPosition`을 사용하는 설치물 정렬도 같은 발밑 중심을 받는다.
- 자유 조준에서 사용하지 않는 일곱 방향 Gun·Muzzle·Flash 자식은 현재 Player 프리팹에서 제거한다. 기존 Right Gun의 총구·섬광 하나를 자유 조준용으로 유지한다. 모션 시간·스패너 행동에 사용하는 Animator와 MotionSet, 스크립트에서 참조하는 표시 컴포넌트는 유지한다.
- 스패너 공격과 R 유지 수리에는 기존 행동 진행률에 맞춘 무기 회전을 적용한다. 우주선에서도 `CanMove`가 허용되면 몸체는 마우스의 좌우를 따른다. `PlayerControlState.IsInSpaceship`이 시작 허브 바인딩 또는 `PlayerMatchPresence.Location == Spaceship`을 확인하며, 우주선에서는 별도 무기·전경 손을 숨기고 외형의 총구 좌표를 제공하지 않는다. 필드 복귀 시 선택된 무기를 다시 표시한다. 필드의 입력 잠금이나 스킬 선택 중에는 몸체의 마지막 좌우 방향을 유지하고 무기는 아래쪽 중립 자세를 사용한다.

검증(2026-11-13): 최종 프리팹으로 `PlayerContinuousAimTests` EditMode 35개와 `DirectionalColliders_SelectOnePairAndPreserveManualPositions` 1개가 통과했다. Unity Preview Camera로 좌우 각 5프레임의 몸체·발밑 충돌·피격 외곽을 렌더링해 정렬을 확인했다. 정적 검사에서 중복 fileID, 누락된 내부 참조, 삭제한 자식을 가리키는 외부 참조는 없었다. 별도로 실행한 `PlayerEnemyDetectorTests` 12개는 현행 설정에 없는 이전 레이어명 `Player`·`EmenyBullet`을 사용해 실패했다. 감지 컴포넌트·범위·충돌 설정은 이번 정리에서 보존했으며 해당 테스트의 레이어명 갱신은 포함하지 않았다. 실제 PlayMode의 이동·사격·수리·벽 충돌 조작감 검수는 미실행이다.

## 기존 8방향 자세 방식

2026-11-01 현재 기준: 오른손은 화면 오른쪽이 아니라 캐릭터의 해부학적 오른손이다. 총기는 오른손이 방아쇠, 왼손이 앞쪽 지지 손이다. 몸체와 팔은 좌우별 Sprite를 사용하고 클립의 `flipX`는 0으로 둔다.

기본총·냉각총·스나이퍼는 `SeparatedWeapons/Canonical/{무기}/{방향}.png`의 독립 8방향 완성 그림을 사용한다. 각 무기의 108개 몸체 연결은 이 8개 그림을 공유한다. 자세별 그림 조각을 다시 추출하거나 크기를 변경하지 않고, 실제 오른손 위치에 손잡이를 맞춘다. 대기 4프레임은 같은 무기·위치·각도를 유지한다. 총열 끝과 첫 발 총구도 새 그림에 맞춰 지정했다.

`_muzzleLocalPosition`은 현재 프레임의 총열 끝이다. `_shotMuzzleLocalPosition`은 실제 사격 클립의 0초 몸체에서 계산한 총구다. 발사 시점에는 Animator가 아직 대기·이동 몸체를 표시할 수 있으므로 둘을 구분한다. 대각 사격 첫 몸체는 파일 이름으로 추정하지 않는다.

에셋의 `_frames` YAML 배열은 빈 줄 없이 연속해서 저장한다. Native importer가 읽은 배열 크기와 작성된 항목 수의 일치 여부를 Editor 테스트로 검사한다. 현재 개수는 총기 각각 108개, 스패너 168개다. 제작·검토 범위는 [오른손 무기 연결 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/6ed4f15650965830dda1139b0a1b4b3baa0e1f78/Docs/art/player-right-handed-review.md)에 둔다.


기준: 이영빈 6주차, Issue #554. 기존 8방향 모션과 전투 동작을 유지하며 표시용 에셋을 분리한다.

## 편집 위치

- `Player.prefab` 루트의 `PlayerWeaponVisual`: 몸체, 별도 무기 렌더러, 무기별 외형 데이터 연결.
- 같은 루트의 `SortingGroup`: 몸체·무기를 하나의 캐릭터로 정렬한다. 외부 정렬값은 기존 몸체 설정을 따르고 무기의 정렬 차이는 그룹 안에서만 적용한다.
- `Assets/Prefabs/Player/PlayerWeaponVisual.prefab`: 플레이어 자식으로 배치하는 표시 전용 프리팹. 물리·피해·수리 컴포넌트가 없다.
- `Assets/Data/Player/Weapons/DefaultGunAppearance.asset`, `CryoBlasterAppearance.asset`, `SpannerAppearance.asset`, `SniperAppearance.asset`: 몸체 프레임별 무기 그림·위치·각도·정렬 차이와 선택적 총구 좌표.
- `Assets/Art/Sprites/Player/Motions/V2/SeparatedWeapons/Canonical/`: 현재 사용하는 무기 원본. 기존 자세별 추출 그림은 과거 자료로 남아 있지만 새 총기 연결에서 참조하지 않는다.
- 기존 V2 몸체 PNG: 같은 GUID·160×160·32 PPU·중앙 피벗을 유지하고 무기를 제거한 그림을 사용한다.

`PlayerMotionSet`, Animator Controller와 AnimationClip의 상태·Sprite 키·길이·그림자 키는 유지한다.
무기 없는 몸체와 별도 무기를 실제 손잡이 위치로 합성한다. 손의 해부학적 소속과 가림은 몸체 그림에서도 확인한다. 무기 중심과 손 위치를 같은 좌표로 취급하지 않는다.

## 런타임 책임

| 타입 | 책임 |
| --- | --- |
| `PlayerWeaponAppearance` | 몸체 Sprite를 키로 무기 Sprite와 표시 좌표를 조회한다. 조회 사전은 한 번 구성하며 Inspector 갱신 시 다시 구성한다. |
| `PlayerWeaponVisual` | Animator 평가 이후 LateUpdate에서 현재 몸체 프레임에 맞는 무기를 표시한다. 색상·뒤집기·Sorting Layer와 정렬 차이를 반영하고, `TryGetMuzzlePosition`으로 외형에 지정된 총구 좌표를 제공한다. |
| `PlayerSpriteMotion` | 기존 행동·방향·진행률과 타격 멈칫을 선택한다. 무기 표시를 위해 행동을 다시 시작하지 않는다. |
| `PlayerWeaponRig` | 기존 총구와 섬광을 선택한다. 외형에 총구가 지정되면 해당 좌표를 우선 사용하고, 없으면 기존 자세별 총구를 사용한다. 구형 방향별 총 Sprite를 숨기고 8방향 선택을 유지한다. |
| `PlayerWeaponSelection` | 현재 무기와 장착 상태를 소유한다. 외형과 몸체 모션이 이 상태를 조회하고 변경 알림을 구독한다. |
| `PlayerAttack`, `PlayerSpannerWeapon` | 기존 공격·수리 입력, 피해, 간격과 판정을 담당한다. |

`PlayerWeaponAppearance.TryGetFrame(body, out frame)`은 알려진 몸체 프레임과 유효한 무기 Sprite가 있을 때 true다.
null·중복 키·비정상 좌표가 포함된 외형 데이터는 미완성으로 처리한다. 누락된 프레임을 임의의 다른 방향 무기로 대체하지 않는다.
표시 연결이 없거나 모션이 외형을 소유하지 않으면 별도 무기를 숨긴다. 기존 다른 Player 프리팹을 전환할 때는 몸체 제거와 별도 표시 연결을 함께 적용한다.

## 무기 외형 교체

1. 기존 외형 데이터를 복제하고 해당 무기의 별도 그림을 준비한다.
2. 몸체 `_body` 연결을 유지하고 각 프레임의 `_weapon`을 새 Sprite로 교체한다.
3. `_localPosition`, `_localAngle`, `_sortingOffset`으로 손 위치와 정렬을 맞춘다. 위치 단위는 플레이어 로컬 월드 단위다.
4. Player 프리팹의 해당 무기 Appearance 필드에 새 데이터를 연결한다.
5. 총열 길이가 다르면 해당 프레임의 `_overrideMuzzle`을 켜고 `_muzzleLocalPosition`을 플레이어 루트 기준 총열 끝으로 지정한다. `_shotMuzzleLocalPosition`에는 해당 사격 클립의 첫 프레임 총구를 지정해, 대기·이동 상태에서 첫 발이 낮은 위치에서 나오지 않도록 한다. 몸체 뒤집기도 같은 기준으로 반영한다. 지정하지 않으면 기존 Weapon Rig의 자세별 총구를 사용한다.
6. 8방향, 부유 공격·수리, 좌우 기울임과 대각선 후진에서 확인한다.

무기는 중앙 피벗·32 PPU를 사용한다. 무기 그림의 손잡이 좌표와 몸체 그림의 오른손 좌표를 각각 로컬 단위로 환산하여 두 점의 차이를 `_localPosition`에 저장한다. 같은 방향의 무기는 여러 행동에서 공유한다. 다른 크기·모양의 무기는 손 가림과 총구 끝도 새 그림에 맞춰 제작한다.
캐릭터 전체를 새 무기와 함께 다시 그리지 않고 무기 에셋과 외형 데이터만 수정하는 것이 이번 분리의 목적이다.

## 이전 제작 기록 — 현재 연결 기준과 구분

아래 내용은 2026-10-25~27의 분리 작업 당시 기록이다. 투명한 무기·프레임마다 다른 추출 그림을 사용하는 설명은 현재 Canonical 총기 연결에 적용하지 않는다.

2026-10-27에는 스나이퍼를 `WeaponType.Sniper = 3`으로 추가했다. 기본총·냉각총·스패너의 기존 번호는 유지한다.
냉각총과 스나이퍼는 각 92개 몸체 프레임에 별도 무기 Sprite와 총구 좌표를 연결한다. 이 과정에서 몸체 244개와 기존 모션은 수정하지 않았다.
위로 이동할 때 무기가 몸 뒤에 완전히 가려지는 기존 4개 프레임은 투명 무기 Sprite와 기존 총구를 사용한다.
스나이퍼 외형은 Player 프리팹의 `_sniper`에 연결한다. 전투 수치는 `PlayerAttack`, 탄속은 `SniperBullet.prefab`에서 편집한다.
제작 원화·프롬프트·비교 화면과 확인 범위는 [무기 외형 확장 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/6ed4f15650965830dda1139b0a1b4b3baa0e1f78/Docs/art/player-weapon-variants/README.md)에 둔다.

## 제작 근거와 검증 경계

제작 경로는 내장 `image_gen`이다. 모델로 무기 제거·가려진 몸체 복원·무기 선택 마스크와 불꽃 수정 자료를 만들고,
원본 좌표에 등록한 뒤 해당 수정 영역만 합성한다. 무기 레이어는 새로 그린 무기가 아니라 원본 픽셀의 추출 결과다.
원본 프레임 보관본, 프롬프트·모델 결과와 영역별 검사 기록은 `Docs/94_art/01_player-weapon-separation/`에 둔다.

현재 몸체 클립 104개가 참조하는 244개 몸체 프레임을 처리했다. 총기 92개·스패너 152개 무기 Sprite를 따로 등록한다.
뒤를 보는 공격·수리 6개 프레임은 무기가 완전히 가려져 투명한 Sprite를 사용한다. 무기가 없는 방향을 임의로 그리지 않는다.

- `review.html`: 원본·몸체·무기·합성 비교, 검색과 밝은/어두운 배경 및 무기 숨김.
- `original-frames.zip`: 기존 그림 보관본.
- `model-sources.zip`, `provenance.json`: 내장 이미지 편집 자료·프롬프트·해시. 마젠타는 추출용 표식이며 런타임에 사용하지 않는다.
- `layers.json`, `qa.json`: 프레임 참조·좌표·변경 전후 해시와 정적 검사 범위.

정적 검사에서 기존 클립 전체 108개(몸체 104개·이펙트 4개), 물리 컴포넌트 7개, 기존 Transform 37개가 유지됐으며 분리 무기의 자르기·위치 환산이 원본 캔버스를 정확히 복원하는지 확인했다.
몸체와 무기의 합성은 무기 분리·복원 영역 을 제외하면 원본 픽셀을 보존한다. 손을 포함한 무기 가림은 프레임별 Sprite의 투명 영역이므로 다른 외형의 무기로 교체할 때 새 손 가림을 맞춰야 한다.

`PlayerWeaponAppearanceTests`는 실제 클립의 프레임별 외형 누락, 잘못된 참조와 무기 전환·방향별 표시를 검사한다.
Test Runner 실행과 실제 화면 검증 결과는 컴파일·에셋 정적 검사와 구분하여 기록한다.
검증 전에는 이 문서의 연결 설명만으로 실행 통과를 주장하지 않는다.

2026-10-25 정적 검증: 런타임·Editor 테스트 코드 컴파일 오류 0개, 기존 다른 기능의 경고 5개. `.meta` 검사와 `git diff --check` 통과.
Unity Test Runner·게임 화면에서 무기 교체, 8방향 이동/사격/수리, 후진·타격 멈칫과 다른 캐릭터 사이 정렬은 아직 실행 확인하지 않았다.
