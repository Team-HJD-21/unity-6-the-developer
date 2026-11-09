# 플레이어 무기 API

수정일: 2026-11-10. 담당: 이영빈. 스패너 구현은 PR #549로 main에 반영됐으며, 실행 검증 범위는 아래 검증 절에서 구분합니다.

## 상태창의 장착 무기 조회

`PlayerAttack.TryGetCurrentWeaponStats(out PlayerWeaponStats)`는 실제 공격 설정에서 피해량·공격 간격·투사체 개수·탄속·최대 사거리를 읽습니다. 피해량은 산탄 전체 합계가 아닌 탄환 한 발 또는 근접 타격 한 번 기준입니다. `PlayerWeaponAmmo.GetReloadDuration(weapon)`은 실제 재장전과 UI가 공통으로 사용하는 시간 조회입니다. [I 상태창과 무기 화살표 동작](player-ui-api.md#2026-11-10-플레이어-상태창)을 참고합니다. 새로운 선택 상태나 별도 UI 밸런스 데이터는 만들지 않습니다.

2026-10-25, Issue #554 작업에서는 전투·수리 계약을 유지하고 표시용 몸체·무기를 분리했습니다. Player 프리팹의 `PlayerWeaponVisual`과 무기별 외형 데이터 편집 방법은 [몸체·무기 분리 API](player-weapon-appearance-api.md)를 참고합니다. 기존 `PlayerSpanner`는 공격·수리 동작을 계속 담당합니다.

## 2026-10-24 수리 기획과 구현 경계

아래 API 설명은 현재 스패너 타격 수리 구현의 기준이다. 10월 24일 후반 전사는 별도 스패너 수리 무기 조작 대신 키 유지 수리 상태를 쓰되, 수리 중 스패너 모션은 유지할 수 있고 근접 무기는 별도로 다루는 방향을 확인했다. F키는 예시이며 모션 전체 삭제 지시가 아니다. 시간 비용·Interaction range, Repair/Restore 구분과 복구 취소 후 게이지 유지는 후속 설계이며 이 main 코드의 구현·검증 완료로 보지 않는다. 입력·중단·표시·네트워크 계약은 미정이다. [회의 검토 기록](../planning/2026-10-24-regular-meeting.md)

## 장착과 입력

`Player.prefab`에 미리 배치한 `PlayerWeaponSelection._spannerPrefab`에 `Assets/Prefabs/Player/PlayerSpanner.prefab`을 연결합니다.
플레이어가 생성되면 스패너 인스턴스를 자식으로 한 번 생성하고, 장착 상태에 따라 활성화합니다.
1번 스패너, 2번 기본총, 3번 냉각총, 4번 스나이퍼를 직접 선택합니다. 숫자패드와 하단 HUD 버튼도 같은 슬롯을 사용합니다. 최초 장착은 기본총입니다.
Tab과 휠 아래는 다음 슬롯, 휠 위는 이전 슬롯으로 순환하며 장비가 없는 슬롯은 건너뜁니다. 공격은 LMB를 유지해 사용합니다. Space 공격 입력은 제거했습니다.
UI 위의 휠과 Shift+휠은 무기 전환에서 제외합니다. 테스트 카메라 속도 조절에는 Shift+휠을 사용합니다.

2026-10-27 스나이퍼: 확산 없는 1발, 피해 45, 발사 간격 0.85초로 추가했습니다. 탄속 70과 수명 1.2초는 별도 `Assets/Prefabs/Player/SniperBullet.prefab`에 지정합니다.
기본총·냉각총의 기존 산탄 수치와 스패너 공격·수리는 유지합니다. 냉각총과 스나이퍼는 몸체와 분리된 별도 무기 Sprite를 사용하며 총열 끝도 외형 데이터에 연결합니다.

## 스크립트별 역할

2026-11-04: 무기 선택 API와 `CurrentWeapon`·`WeaponChanged`를 `PlayerAttack`에서
`PlayerWeaponSelection`으로 옮겼다. 호출부도 같은 플레이어의 선택 컴포넌트를 참조한다.
`_currentWeapon`·기본/스나이퍼 탄환·스패너 프리팹은 선택 컴포넌트에서 편집하고,
피해량·발사 간격·산탄 설정은 기존 `PlayerAttack`에서 편집한다.
선택의 Update(200)는 입력(-200)·스킬 상태(100) 이후에 실행하고 공격의 LateUpdate(300)
전에 장착 상태를 갱신한다. Unity PlayMode 실행 검증은 별도로 남는다.

| 스크립트 | 기능 / 호출 계약 |
| --- | --- |
| `PlayerWeaponSelection` | 현재 무기·장비 참조와 숫자키·휠·Tab 선택을 소유합니다. `CurrentWeapon` 조회, `TrySelectWeaponSlot(int)` 또는 `TrySelectWeapon(WeaponType)`으로 선택, `SelectAdjacentWeapon(int)`으로 앞뒤 순환하며 실제 전환에만 `WeaponChanged`를 알립니다. 스패너 인스턴스와 총기 표시를 장착 상태에 맞춥니다. |
| `PlayerAttack` | 선택된 무기의 공통 공격 간격·피해·탄환 생성을 관리합니다. `TryFire()`는 `PlayerWeaponSelection`의 현재 장비로 공격을 시도합니다. 스패너는 탄환과 총구 없이 공격합니다. |
| `PlayerWeaponAmmo` / `PlayerWeaponAmmoState` | 총기별 탄창과 수동 재장전 상태를 보관합니다. 수리 R 입력을 우선하고, 실제 발사 성공 한 번에 한 발을 소비합니다. |
| `PlayerWeaponAmmoView` | 기존 Hotbar 왼쪽에 미리 배치한 잔탄·Reload 문구와 회전 화살표를 로컬 Player에 연결합니다. |
| `PlayerWeaponSlotMapping` | 단축키와 HUD의 1~4 슬롯 매핑. 기존 `WeaponType` 직렬화 값은 유지합니다. |
| `PlayerWeaponHud` / `PlayerWeaponSlotView` | 기존 씬 HUD 소유자에게 배치된 무기 버튼을 연결하고 선택·장비 유무를 표시합니다. UI 오브젝트를 생성하지 않습니다. |
| `PlayerWeaponRig` | 기존 방향별 총기와 총구 관리. `SetRangedWeaponVisible(bool)`은 총기 표시만 제어하며 몸체 방향 계산은 계속합니다. |
| `PlayerSpannerWeapon` | `Bind(Transform, PlayerInputHandler)`로 소유자와 조준 입력 연결. `TryAttack(Vector2)`은 유효한 방향과 쿨타임을 검사한 뒤 근접 판정과 휘두르기를 시작합니다. 반환 true는 공격 시도 성공이며 적중을 보장하지 않습니다. |
| `PlayerSpannerView` | 프리팹의 SpriteRenderer 표시와 플레이어 기준 앞뒤 정렬만 제어합니다. 외형은 Sprite 에셋으로 편집하며 메쉬를 생성하지 않습니다. |
| `PlayerSpannerSlash` | 제작된 초승달 Sprite/Animator 프리팹을 타격 순간의 플레이어 월드 위치·조준 방향·공격 반경에 맞춰 표시합니다. Collider와 피해 판정은 없습니다. |
| `PlayerTurretRepair` | 가까운 일반 필드 터렛 하나를 선택해 R 유지 동안 최대 체력의 10%/초를 회복합니다. HP 0은 별도 진행률을 초당 10%씩 채워 완료 때 Restore합니다. 이동·거리 이탈·조작 차단 시 취소하며 R을 놓고 다시 눌러야 재개합니다. |
| `PlayerTurretRestoreProgress` | 실제 터렛마다 복구 진행률을 공유합니다. 중단해도 유지하고, 100%에서만 기존 Restore API를 호출합니다. 복구·새 파괴·등록 해제·Match 시계/epoch 변경 시 이전 진행률을 지웁니다. |
| `PlayerSpannerRepair` | 이전 타격 수리 경로입니다. `PlayerTurretRepair`가 있는 현재 Player 프리팹에서는 LMB 수리를 사용하지 않습니다. 이전 구성과 저장 자료 호환을 위해 API는 유지합니다. |
| `PlayerSpannerTargeting` | 공격·수리의 월드 거리, 전방 각도, 같은 씬, 장애물 가림 검사를 공유합니다. 대상 자신의 콜라이더는 가림에서 제외합니다. 선택 인자 `interactionOrigin`으로 R 수리는 발 위치를 전달하며 기본 근접 공격은 기존 원점을 사용합니다. |

무기 선택 API는 `CanSelectWeapon`을 공통으로 검사합니다. 비활성·일시정지·조작 차단·스킬 선택 중 또는 장비가 없으면 false입니다. 같은 무기를 다시 선택하면 true지만 `WeaponChanged`는 발생하지 않습니다. 실제 전환에만 `WeaponChanged(WeaponType)`를 통지하며 기존 공격 대기시간은 유지합니다. `SelectNextWeapon()`은 다음 슬롯 선택을 위한 호환 메서드입니다. 전환 표시를 구독한 UI는 비활성화할 때 구독을 해제합니다.

원거리 무기와 스패너를 오가는 실제 전환은 `PlayerSpriteMotion`도 `WeaponChanged`를 구독해 새 무기 계열의 몸체를 같은 프레임에 평가한다. 이때 현재 이동 입력에 맞는 대기·이동 자세를 고르고 기존 공격·대시 표시를 정리한다. 기본총·냉각총·스나이퍼 사이의 전환은 현재 모션과 재생 진행률을 유지한다. 차단되어 실패한 전환은 표시를 강제하지 않으며, Motion 비활성 시 구독을 해제한다. 이전 무기 그림을 임시로 표시하는 대체 경로는 사용하지 않는다.

#564에서는 무기 선택을 Update에 두고 물리적 클릭에 따른 공격 시도를 LateUpdate로 옮겼습니다. UI·포탑 선택과 설치 처리가 먼저 클릭을 사용하며, 사용된 클릭은 발사로 이어지지 않습니다. `TryFire()`도 일시정지·조작 차단·스킬 선택과 설치 미리보기 상태를 검사합니다. 공격 수치와 기존 타격 예약·탄환 이동·피해 처리는 유지합니다. [클릭 입력 분리 API](player-primary-input-api.md)

## 2026-10-31 원거리 탄환과 장애물 충돌

기본총·냉각총의 `PlayerBullet.prefab`과 스나이퍼의 `SniperBullet.prefab`은 적뿐 아니라 벽·나무·포탑·CU·플레이어 설치물의 실제 본체 Collider에 닿으면 탄환을 제거하고, 해당 탄환에 이미 연결된 `PlayerProjectileView._impactPrefab`을 한 번 재생한다. 장애물에 피해를 주거나 물리 힘을 가하지 않는다. 스나이퍼는 기존 전용 명중 프리팹을 유지한다.

전역 Bullet 충돌표는 바꾸지 않는다. 두 탄환 PolygonCollider2D의 `Include Layers`에 `Default`, `CU`, `Tower`, `Forest`를 추가한다. `Enemy`와 `PlayerFieldDevice`는 기존 충돌표를 사용한다. `Ground_1`은 포함하지 않고, Collider 또는 Rigidbody 소유자에 `Ground` 태그가 있는 경우에도 통과한다. `Player`·`Bullet` 태그의 자식 Collider와 적이 아닌 감지용 Trigger(포탑 사거리·상호작용 등)도 통과한다. 적 피격 Trigger는 기존 적중 대상으로 유지한다. Collider가 없는 장식은 탄환을 막지 않는다.

`Initial_Stage`의 실제 지면 `Initial_Overworld_Base`는 `Default/Untagged`이므로 태그와 레이어만으로 제외할 수 없다. 기존 `PlayerTurretPlacementRules.FindGround(scene)`으로 찾은 지면 Tilemap의 Collider도 제외한다. 씬별 조회 결과를 공유하며 지면이 제거·비활성화되면 다시 찾고, 씬 언로드와 플레이 초기화 때 캐시를 비운다. 별도 `Environment_Obj`·`Environment_Base` 장애물 Tilemap은 제외하지 않는다.

`PlayerBullet.FixedUpdate`는 프리팹 Collider의 레이어 설정으로 이번 물리 틱의 이동 경로를 Cast하고 가장 가까운 유효 본체에서 정지시킨다. 이 경로와 기존 `OnTriggerEnter2D`는 같은 적중 함수를 사용해 얇은 장애물 관통과 중복 이펙트를 방지한다. 적 피해는 기존 `EnemyHealth.TakeDamage`의 서버 권한을 따르며 새로운 공격 RPC는 추가하지 않는다.

플레이어 설치물의 본체는 Player/Enemy만 접촉시키도록 `Exclude Layers`에 Bullet을 포함해 접촉을 차단하고 있다. 이 설정은 유지하며 탄환의 명시적 `ContactFilter2D` Cast로 본체를 조회한다. 접촉용 제외 설정과 조회 필터의 역할은 [Unity Rigidbody2D.Cast](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D.Cast.html)와 [Collider2D.excludeLayers](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider2D-excludeLayers.html)를 따른다.

`PlayerBulletRuntimeTests`에는 기본·냉각·스나이퍼와 5개 본체 레이어의 얇은 장애물, 중복 Collider의 이펙트 1회, 지면·플레이어·탄환·감지 Trigger 통과, 적 Trigger 적중 회귀 검증을 추가했다. 실제 PlayerTurret/비콘/방벽/폭탄 프리팹의 명시적 Bullet 접촉 제외 설정과 체력 유지, Default/Untagged 지면과 별도 장애물 Tilemap의 구분도 검사한다. 테스트 작성과 컴파일은 실제 PlayMode 실행 통과를 의미하지 않으며 에디터에서 실행 확인이 필요하다.

## 스패너 수치와 판정

수치는 `PlayerSpanner.prefab`의 `PlayerSpannerWeapon`에서 변경합니다.

| 필드 | 초기값 | 의미 |
| --- | --- | --- |
| `_range` | 2 | 플레이어 중심에서 월드 반경 |
| `_arc` | 100 | 조준 방향 기준 전체 타격 각도 |
| `_damage` | 20 | 적에게 주는 피해 |
| `_attackInterval` | 0.6초 | 공격 간격 |
| `_swingSeconds` | 0.22초 | 휘두르기 표시 시간 |
| `_downHand` | (-0.6, -0.45) | 아래를 볼 때 오른손 위치 |
| `_upHand` | (0.6, 0.15) | 위를 볼 때 오른손 위치 |
| `_leftHand` | (-0.65, -0.35) | 왼쪽을 볼 때 오른손 위치 |
| `_rightHand` | (0.65, -0.35) | 오른쪽을 볼 때 오른손 위치 |

손 위치는 플레이어 로컬 좌표이며 `PlayerWeaponRig.Facing`의 몸체 방향을 따릅니다.
아래를 볼 때 캐릭터의 오른손은 화면 왼쪽에 있습니다. 장착 위치는 휘두르는 중에도 고정하고 스패너 회전만 변경합니다.
장착 외형은 `Assets/Art/Sprites/Player/Motions/Spanner.png`의 Spanner 서브 스프라이트입니다.
이 PNG는 Attack_Right_2와 동일한 원본 바이트를 사용하고 Sprite Editor에서 스패너 부분(110, 63, 30, 24)을 지정합니다.
공격 그림과 같은 색·윤곽선이며 원본 공격 PNG는 수정하지 않습니다. 프리팹의 SpriteRenderer에서 교체할 수 있습니다.

공격 수락 시에는 판정을 예약합니다. Attack/Repair 클립의 50% 지점(기본 공격 0.2초, 수리 0.3초)에
범위 안 Collider의 가장 가까운 점을 기준으로 전방을 다시 검사합니다.
애니메이션이 없는 구성에서는 `_swingSeconds`의 절반을 사용합니다.
무기 전환·비활성화·조작 차단은 남은 판정을 취소합니다. 준비 중 들어오거나 나간 대상은 타격 시점의 위치로 판단합니다.
같은 씬의 `EnemyHealth`를 대상당 한 번 처리합니다. 벽 검사는 플레이어와 충돌하는 일반 Collider를 기준으로 하며 트리거와 적은 가림 장애물에서 제외합니다.
`EnemyHealth.TakeDamage`의 기존 서버 권한 검사를 따르므로 실제 피해는 스폰된 서버 적에서 검증해야 합니다. 클라이언트 공격 RPC는 추가하지 않았습니다.
물리 힘은 가하지 않습니다. 타격 시점에 한 번 조회하며 휘두르는 동안 연속 판정하지 않습니다.
기존 원거리 탄환의 속도와 충돌 처리는 현재 main 동작을 유지합니다.

2026-10-31: 일반 스패너 공격은 적중 여부와 관계없이 실제 타격 시점에 초승달 검기를 한 번 재생한다. `PlayerSpannerWeapon._slashPrefab`에 제작된 SpriteRenderer·Animator·`PlayerSpriteEffect` 프리팹을 연결한다. 검기는 타격 순간의 플레이어 중심과 예약된 조준 방향을 사용하고 공격 반경 `_range`에 맞춰 균일 확대한다. 플레이어가 이후 이동해도 손이나 몸을 따라가지 않는다. 수리 의도의 타격은 검기를 재생하지 않으며 기존 수리 파티클을 유지한다. 타격 전 무기 전환·비활성화·조작 차단으로 예약이 취소되면 검기도 생기지 않는다.

`PlayerSpannerSlash._authoredRadius`는 Sprite 피벗에서 검기의 바깥쪽 끝까지의 로컬 거리다. `SpannerSlash.prefab`은 256×256 그림 4장, PPU 128, 공통 피벗 `(74,128)` 픽셀(정규화 `(0.2890625,0.5)`)을 사용한다. 네 프레임의 알파 32 이상인 픽셀로 측정한 바깥 반경 178픽셀을 기준으로 `_authoredRadius=1.390625`를 지정한다. 원본 그림·피벗 또는 Pixels Per Unit을 변경하면 함께 맞춘다. 검기는 기본 공격의 100도 부채꼴 안에 놓는 장식용 표시이며 실제 충돌 판정은 기존 코드를 따른다. `_arc`를 바꿀 때에는 그림도 함께 조정해야 한다. 수명은 프리팹 `PlayerSpriteEffect._duration` 0.18초로, 몸체와의 순서는 `_sortingOrderOffset`으로 편집한다. Collider·메쉬·런타임 그림을 추가하지 않으며 피해량·반경·재사용 시간·수리 API는 변경하지 않는다.

`PlayerWeaponMigrationTests`에는 검기 프리팹 구성, 빗나간 공격의 타격 시점 1회 재생·조준 방향·월드 반경·정렬, 수리 의도와 취소된 타격의 검기 생략 검증을 추가했다. 실제 PlayMode 실행 확인은 별도로 남는다.

## 포탑 수리

2026-11-09: 현재 Player 프리팹의 수리·파괴 복구는 `PlayerTurretRepair`가 담당합니다. 발 위치 기준 XY 반경 3 안의 가장 가까운 손상 또는 파괴 터렛 아래에 R 안내와 진행 막대를 표시합니다. 원점은 `PlayerDirectionalColliders.GroundPosition`이며 거리·가림 검사·작업 방향에 공통으로 사용합니다. 방향별 콜라이더가 없는 구성만 루트 위치를 사용합니다. 기존 캐릭터 중심 원점은 몸 콜라이더의 아래쪽 오프셋 때문에 포탑 위에서 접근하면 범위를 벗어날 수 있어 수정했습니다. 살아 있는 터렛은 R 유지 동안 최대 체력의 10%/초를 연속 회복합니다. 최대 체력 100은 초당 10, 200은 초당 20입니다. 무기 선택과 마우스 방향은 수리 조건이 아닙니다.
HP 0인 터렛은 실제 HP·파괴 상태를 유지하면서 별도 복구 진행률을 초당 10%씩 채웁니다. 약 10게임초 후 100%에 도달한 시점에 기존 `Restore()`를 한 번 호출해 최대 체력·OFF 상태로 복구합니다. 완료 전에는 가동할 수 없고, 완료 후에는 기존 켜기 요청이 필요합니다. 복구 막대는 주황색, 살아 있는 터렛의 체력 막대는 초록색입니다. 안내와 막대는 HUD 프리팹에 배치합니다.
수리·복구 중에는 선택된 무기를 유지한 채 기존 스패너 일반 공격 자세와 외형을 반복 표시하고 실제 공격을 막습니다. 모션만 반복하며 적 타격·검기 판정은 호출하지 않습니다. 현재 Player 프리팹의 LMB 스패너 공격은 체력을 회복하지 않습니다.
작업 중 `TryGetFacingDirection(out direction)`은 발 위치에서 현재 작업 터렛을 향한 표시 방향을 제공합니다. `PlayerWeaponRig`와 `PlayerSpriteMotion`이 이를 같은 8방향 기준으로 사용합니다. 시작·종료 프레임에는 Rig와 Animator를 즉시 평가하며, 종료 후에는 마우스 방향으로 돌아갑니다. `PlayerInputHandler.AimDirection` 원본은 변경하지 않습니다.
키 해제는 즉시 중단합니다. 이동 입력·실제 XY 위치 변화·Rigidbody2D 이동, 거리 이탈, 메뉴·사망·필드 정지·설치 미리보기, 대상 비활성화·파괴 등으로 취소되면 R을 놓고 다시 눌러야 합니다. 멈추거나 범위로 돌아와도 누른 키로 자동 재개하지 않습니다. 수리 대상을 키 유지 중 고정하며, 완전히 수리한 뒤 다른 터렛으로 자동 전환하지 않습니다.
벽 뒤, 다른 씬, 잠금·자동 활성화 진행 중인 터렛은 제외합니다. 활성 상태 또는 냉각 중인 살아 있는 터렛은 수리할 수 있습니다. 일반 수리 도중 파괴되면 해당 입력을 취소하며 R을 다시 눌러야 복구를 시작합니다.
일반 FieldTurretBase 계열이 대상이며 임시포탑·CU는 이번 수리 대상에 포함하지 않습니다. 별도 재화 소비는 없습니다. 복구 완료의 전력 정산은 기존 Restore API에 맡깁니다.

`FieldTurretBase.Repair(MaxHealth × 0.1f, deltaTime)`의 시간 기반 API를 사용합니다. 소수 회복량은 터렛 API가 누적하고 최대 체력에서 제한합니다. `deltaTime`은 공통 `FieldSimulationTime`에서 받으며 정지 중에는 회복하지 않습니다. 같은 씬·같은 Match 시계·상태 권한이 있는 터렛만 대상으로 삼습니다.
상태 변경 후 기존 HealthChanged/SnapshotChanged를 통지하므로 터렛 선택 UI는 기존 구독 경로로 갱신됩니다.
RuntimeState의 체력을 외부에서 직접 바꾸지 않습니다. 살아 있는 터렛 수리는 Repair, 파괴 복구 완료는 Restore로 구분합니다. 기존 터렛 API와 권한을 사용하며 별도 Client 요청/RPC는 구현하지 않았습니다.

파괴 복구 진행률은 `PlayerTurretRestoreProgress`가 터렛 객체별로 공유하므로, 이동·키 해제·다른 대상으로 전환 후 돌아와도 같은 진행률에서 이어갑니다. 복구 완료·새 파괴·등록 해제·Match 시계 또는 epoch 변경 시 지웁니다. 이는 현재 세션의 메모리 상태이며 체크포인트 저장/복원과 네트워크 복제는 추가하지 않았습니다. 동시 작업자는 동일 진행률에 각 작업 시간을 더합니다.

편집 위치: `Player.prefab`의 `PlayerTurretRepair._repairRange`, `_maximumHealthFractionPerSecond`. 안내는 `PlayerSkillHUD.prefab`에 미리 배치된 `Field Turret R Repair Prompt`를 `PlayerTurretRepairPromptView`가 터렛 하단에 투영합니다. 키보드 안내이므로 UI Raycast로 LMB를 소비하지 않습니다. R은 가까운 수리·복구 대상이 있으면 수리에 먼저 사용합니다. `ConsumesRepairInput`은 해당 R 누름을 놓을 때까지 유지하므로 수리 완료·이동 취소 후 같은 입력으로 재장전하지 않습니다.

실행 순서: 입력 수집 Update(-200), 스킬 Update(100), 무기 선택 Update(200) 다음 수리 Update(250)가 조건과 이동 취소를 확정합니다. 재장전 Update(275)는 수리가 소비한 R을 제외하고 탄창 상태를 확정합니다. 모든 Update 이후의 공격 LateUpdate(300)는 확정된 수리·재장전·잔탄 상태로 공격을 차단합니다. 화면 위치는 카메라의 최종 위치를 반영하도록 Canvas 렌더 직전에도 갱신합니다.

## 2026-11-09 탄창과 재장전

스패너는 `∞`, 기본총·냉각총은 `15/15`, 스나이퍼는 `5/5`로 시작합니다. 분모는 예비 탄약 수가 아닌 탄창 최대치입니다. 예비 탄약은 무제한이며, 마지막 발을 쏘면 자동 재장전을 시작합니다. 빈 총으로 전환하거나 수리·설치가 끝났을 때도 현재 총이 비었으면 자동 재장전합니다. 산탄도 실제 발사 한 번에 한 발만 소비하고 탄환 생성 실패는 소비하지 않습니다.

수리·복구할 터렛이 가까이 없으면 R을 눌러 재장전합니다. 만탄창·스패너에서는 시작하지 않습니다. 우선 모든 총기의 재장전 시간은 1.5초이며 `Player.prefab`의 `PlayerWeaponAmmo`에서 용량과 시간을 총기별로 조절합니다. 재장전 중에는 탄수 표시를 숨기고 그 공간에 `Reload` 문구와 회전하는 두 화살표를 표시합니다. 실제 발사는 막지만 LMB 유지 입력은 보존해 재장전 완료 후 사격을 이어갑니다. 이동·대시는 허용하며 무기 변경·설치 진입·메뉴/사망 입력 잠금·수리 시작·컴포넌트 비활성화는 재장전을 취소합니다. 취소 시 잔탄을 보존하고 다음 재장전은 처음부터 시작합니다. 무기를 바꿔도 각 총의 잔탄은 유지합니다.

`PlayerWeaponAmmo`는 `FieldSimulationTime.Seconds`의 경과를 사용합니다. Core 필드 정지 중에는 남은 재장전 시간이 흐르지 않습니다. Host 바인딩이 있으면 `PlayerMatchPresence.CanReport`를 따릅니다. 신규 공격/재장전 RPC나 원격 Player 복제는 추가하지 않았습니다.

UI는 `PlayerSkillHUD.prefab / Weapon Hotbar / Weapon Ammo`에 배치했습니다. 무기 1번 슬롯 왼쪽에 있고 터렛 조작 패널을 열어 Hotbar를 내릴 때 같이 내려갑니다. 무기 행은 기존처럼 하단에서 18px 띄우고 520×102 사각 배경으로 슬롯들을 묶습니다. 탄약 패널과 사각 배경 사이의 간격은 오른쪽 SPACE 슬롯과 같은 16px입니다. 전체 배경은 반투명 흰색(Alpha 0.35), 개별 패널은 Alpha 0.65, 문구는 어두운 색을 사용합니다. 선택 슬롯은 청록색으로 구분합니다. 숫자·문구·화살표의 모든 Graphic은 Raycast를 받지 않습니다. 재장전 화살표는 `Assets/Art/UI/PlayerReloadIcon.png`의 연결된 두 원형 화살표를 Image 하나로 표시·회전하며 런타임 UI 생성은 하지 않습니다.

기존 `PlayerAttack.Checkpoint`의 `/weapon` 스키마는 v2로 올려 세 탄창의 잔탄과 진행 중 재장전의 무기·남은 시간·전체 시간을 함께 복원합니다. 기존 v1 또는 탄창 컴포넌트 없이 저장한 데이터는 탄창을 가득 채우고 재장전하지 않는 상태로 마이그레이션합니다. 복원 실패 롤백도 잔탄과 재장전 상태를 되돌립니다.

검증 범위: 실제 탄창 상태 코드를 사용하는 순수 C# 테스트 9개(소진·독립 유지·재장전 차단/완료/취소·정지 시간·저장 왕복·잘못된 저장 거부)가 통과했고, Runtime/Editor C# 컴파일 오류는 0개입니다. 프리팹의 신규 fileID 중복·참조·부모/자식 연결·Raycast 제외도 정적으로 검사했습니다. 프리팹 참조·수리 R 예약의 Unity 회귀 테스트는 추가·컴파일했으며 실행 통과로 간주하지 않습니다. 실제 Unity 화면 배치와 R 입력/필드 정지 연동은 에디터 플레이 확인이 필요합니다.

## 검증

2026-10-19: 새 4방향 대기·이동·스패너 공격·수리 프레임을 연결했다.
`PlayerSpannerWeapon`은 시작 시 수리 가능한 대상 유무로 모션을 고르며,
공구 포함 행동 프레임 재생 중에는 `PlayerSpannerView`의 별도 SpriteRenderer를 숨깁니다.
실제 체력 감소 또는 수리 성공 때만 `Assets/Prefabs/Player/SpannerImpact.prefab`을 재생합니다.
이펙트는 편집 가능한 Particle System이며 공격은 노란색, 수리는 연두색으로 재생합니다.
프리팹에서 입자 수·크기·속도·수명을 조정할 수 있습니다.
`_impactPauseSeconds` 기본 0.045초 동안 플레이어의 Animator만 타격 자세에서 멈춥니다.
전체 Time.timeScale, 이동, 적 AI는 변경하지 않습니다. 다른 소유자의 코드는 이번 연출 변경에서 수정하지 않았습니다.
세부 구성과 검증 한계는 [Player Motion API](player-motion-api.md)를 따른다.

`PlayerWeaponMigrationTests.WeaponSwitch_CyclesSpannerAndRestoresDirectionalGun`은 실제 플레이어 프리팹을 생성해 네 무기의 순환, 총기 숨김과 복원, 무효 방향 및 연속 근접 공격 제한을 확인합니다.
Unity Test Runner에서 실행할 테스트이며 작성만으로 통과를 의미하지 않습니다.
실제 Host 플레이에서는 전방/후방 적, 벽 뒤 적, 여러 콜라이더를 가진 적, 방향별 장착 위치를 추가로 확인합니다.

수리는 `FieldTurretInitializationTests.PartialRepairCapsHealthAndPreservesActivationAndReservation`에서 회복 상한·이벤트·활성화·전력 예약 유지를,
`RepairDoesNotReviveDestroyedOrUnlockLockedTurret`에서 파괴·체력 0·잠금 거부를 확인합니다.
`PlayerWeaponMigrationTests.SpannerTargetingRejectsRearDistantAndBlockedTargets`는 벽·후방·거리 제한을 검사합니다.
2026-10-17 코드 및 터렛 테스트 어셈블리 컴파일은 통과했습니다. Unity Test Runner 실행 및 실제 플레이에서의 수리 확인은 별도로 남아 있습니다.

2026-10-19 외형/타격 연출 후속 검증: 런타임·Editor 컴파일 통과. 열린 Unity 에디터에서 MotionSet, Spanner 프리팹 Sprite/Particle 연결, Targeting 검증 메서드 3건을 직접 호출해 통과했습니다. Test Runner 실행 기록과는 구분하며 새 지연 판정/멈칫의 PlayMode 검증은 남아 있습니다. 생성용 임시 Editor 코드는 제거하고 완성된 Sprite/Prefab만 유지했습니다.


## 2026-10-20 무기별 자세와 부유 공격

총은 Idle/Move/Fire, 스패너는 ToolIdle/ToolMove/Attack/Repair 자세를 사용한다. 무기가 몸체 그림에 포함되므로 별도 총·스패너 SpriteRenderer를 숨기고 기존 총구 Transform은 발사 기준점으로 유지한다. 복장은 기존 흰색·회색이며 무기는 무채색이다.

`PlayerBullet.Initialize(Color, float, Vector2)`는 유지한다. `bool cryogenic` 오버로드로 비행 그림의 Warm/Cryo 상태를 선택한다. `PlayerProjectileView`는 비행 Animator와 ProjectileImpact 프리팹을 연결하고, `PlayerSpriteEffect`는 명중 그림의 색·정렬·수명을 담당한다. 총구는 `PlayerMuzzleFlash.Show(Color)`를 사용한다. 탄환 피해·이동·충돌 판정은 이 외형 연결과 분리돼 있다.

이동 중에는 HoverFire/HoverAttack/HoverRepair 클립을 사용해 떠 있는 다리 자세와 추진 효과를 유지한다. 정지·부유 버전은 재생 길이가 같고, 스패너 타격 시점도 각각 클립의 50%로 동일하다. 공격 도중 이동 입력이 바뀌면 진행률을 유지하며 표시 상태만 전환한다. 공격 판정이나 예약을 새로 시작하지 않는다.

총을 든 좌우 이동과 사격은 LeanLeft/LeanRight 자세를 사용한다. 이동 입력 X로 기울임을 선택하고 마우스 조준 Facing은 유지한다. 스패너는 기존 부유 이동/공격 자세를 유지한다. 자세만 바뀌므로 탄환 발사 방향·피해·콜라이더에는 영향을 주지 않는다.

좌우 이동은 각 자세·조준 방향마다 Sprite 한 장을 고정한다. 이동 중 사격도 해당 이동 그림을 공유해 공격 입력에 따라 몸이 흔들리거나 다른 컷으로 바뀌지 않는다. 발사 섬광과 공격 재사용 시간은 기존 흐름을 유지한다.

2026-10-21에는 오른쪽 얼굴 비율을 보정하고 전체 V2 몸체 224장의 표시 크기를 같은 헬멧 기준으로 정리했다. Sprite 참조와 클립 길이·고정 이동 선택은 유지한다. 비율 보정의 원본과 검증은 [Player Motion API](player-motion-api.md)에 기록한다.

후속 사격 수정에서는 팔을 앞으로 뻗는 두 손 자세와 방향성이 있는 총구 섬광을 적용했다. PlayerWeaponRig의 `_usePoseMuzzles`가 켜져 있으면 `_firingMuzzles`/`_leftLeanMuzzles`/`_rightLeanMuzzles`의 방향별 로컬 좌표로 총구를 맞춘다. 발사 전에 `TryGetMuzzle`이 최신 자세 위치를 반영하므로 기존 PlayerAttack은 같은 호출 흐름을 사용한다. 좌우 이동 사격은 여전히 몸체 그림 한 장을 고정하고 섬광으로 발사를 표현한다. 피해량·산탄 수·탄환 속도·발사 간격과 콜라이더는 그대로다.

에셋과 표시 API는 [Player Motion API](player-motion-api.md)를 참고한다. 현재 몸체 224장·56클립, 이펙트 16장·4클립이다. 런타임·Editor 빌드와 부유 공격·기울임 에셋의 정적 검사는 통과했다. 이동 전환·타격 시점의 실제 Unity Test Runner 및 플레이 확인은 남아 있다.
