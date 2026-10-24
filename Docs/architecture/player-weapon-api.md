# 플레이어 무기 API

수정일: 2026-10-19. 스패너 작업 브랜치 기준이며 main 반영 전입니다.

## 장착과 입력

`Player.prefab`의 `PlayerAttack._spannerPrefab`에 `Assets/Prefabs/Player/PlayerSpanner.prefab`을 연결합니다.
플레이어가 생성되면 스패너 인스턴스를 자식으로 한 번 생성하고, 장착 상태에 따라 활성화합니다.
Tab은 산탄총 → 냉각총 → 스패너 → 산탄총 순서입니다. 공격은 기존 Space 입력을 사용합니다.
스패너 프리팹이 없으면 기존 두 무기만 순환합니다.

## 스크립트별 역할

| 스크립트 | 기능 / 호출 계약 |
| --- | --- |
| `PlayerAttack` | 입력, 무기 선택, 공통 공격 간격. `CurrentWeapon` 조회, `SelectNextWeapon()`으로 순환. `TryFire()`는 장착한 무기로 공격을 시도합니다. 스패너는 탄환과 총구 없이 공격합니다. |
| `PlayerWeaponRig` | 기존 방향별 총기와 총구 관리. `SetRangedWeaponVisible(bool)`은 총기 표시만 제어하며 몸체 방향 계산은 계속합니다. |
| `PlayerSpannerWeapon` | `Bind(Transform, PlayerInputHandler)`로 소유자와 조준 입력 연결. `TryAttack(Vector2)`은 유효한 방향과 쿨타임을 검사한 뒤 근접 판정과 휘두르기를 시작합니다. 반환 true는 공격 시도 성공이며 적중을 보장하지 않습니다. |
| `PlayerSpannerView` | 프리팹의 SpriteRenderer 표시와 플레이어 기준 앞뒤 정렬만 제어합니다. 외형은 Sprite 에셋으로 편집하며 메쉬를 생성하지 않습니다. |
| `PlayerSpannerRepair` | `CanRepair(owner, direction)`은 준비 동작을 고르기 위한 조회입니다. 타격 시 `TryRepair(owner, direction, out position)`으로 대상을 다시 검사하고 `TurretBase.Repair`를 호출합니다. 기존 bool 오버로드도 유지합니다. |
| `PlayerSpannerTargeting` | 공격·수리의 월드 거리, 전방 각도, 같은 씬, 장애물 가림 검사를 공유합니다. 대상 자신의 콜라이더는 가림에서 제외합니다. |

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

## 포탑 수리

스패너를 장착한 상태에서 일반 터렛을 향해 기존 공격 입력을 누릅니다. 공격 버튼을 유지하면 0.6초 간격으로 시도합니다.
앞쪽 100도·반경 3 안에서 가장 가까운 손상 터렛 하나를 10씩 회복합니다. 이 값은 PlayerSpanner 프리팹의 PlayerSpannerRepair에서 조정합니다.
수리 준비 동작으로 시작한 타격은 적 공격을 함께 수행하지 않습니다. 타격 때 수리 대상이 사라지거나 조건을 벗어나면 수리하지 않습니다. 사거리와 방향은 플레이어 중심의 XY 기준입니다.
벽 뒤, 다른 씬, 잠금·파괴·자동 활성화 진행 중인 터렛은 제외합니다. 활성 상태 또는 냉각 중인 살아 있는 터렛은 수리할 수 있습니다.
일반 TurretBase 계열이 대상이며 임시포탑·CU는 이번 수리 대상에 포함하지 않습니다. 전력·재화 소비와 파괴 터렛 부활은 추가하지 않았습니다.

TurretBase.Repair(int amount)는 실제 회복이 있을 때 true를 반환하고 최대 체력을 넘지 않습니다.
상태 변경 후 기존 HealthChanged/SnapshotChanged를 통지하므로 터렛 선택 UI는 기존 구독 경로로 갱신됩니다.
RuntimeState의 체력을 외부에서 직접 바꾸거나 Restore로 대체하지 않습니다. 기존 로컬 터렛 API 경로이며 별도 서버 요청/RPC는 구현하지 않았습니다.

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

`PlayerWeaponMigrationTests.WeaponSwitch_CyclesSpannerAndRestoresDirectionalGun`은 실제 플레이어 프리팹을 생성해 세 무기의 순환, 총기 숨김과 복원, 무효 방향 및 연속 근접 공격 제한을 확인합니다.
Unity Test Runner에서 실행할 테스트이며 작성만으로 통과를 의미하지 않습니다.
실제 Host 플레이에서는 전방/후방 적, 벽 뒤 적, 여러 콜라이더를 가진 적, 방향별 장착 위치를 추가로 확인합니다.

수리는 `TurretInitializationTests.PartialRepairCapsHealthAndPreservesActivationAndReservation`에서 회복 상한·이벤트·활성화·전력 예약 유지를,
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
