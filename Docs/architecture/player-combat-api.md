# Player Combat API

최종 갱신: 2026-10-15
범위: 기본 무기 사격과 플레이어 탄환. #509 속도 가산 / #510 충돌 밀림 수정 기준.

2026-10-27 #564 입력 갱신: 공격 입력은 LMB 유지다. UI·설치·포탑 선택 뒤 LateUpdate에서 공격을 판정하고, 클릭을 누른 채 문맥이 바뀌어도 자동으로 공격을 재개하지 않는다. `TryFire()`는 일시정지·조작 차단·스킬 선택·설치 상태도 검사한다. 탄환 이동·피해 계약은 유지한다. [입력 분리 API](player-primary-input-api.md)

## 스크립트별 책임

위치: `Assets/Scripts/Features/Player/PlayerWeapon/`

| 스크립트 | 책임 |
| --- | --- |
| `WeaponType` | 산탄총 0 / 냉각총 1의 무기 종류. 기존 프리팹의 enum 숫자를 유지한다. |
| `PlayerAttack` | 무기 전환·발사 간격, 같은 플레이어의 총구 검증, 플레이어 기준 조준과 산탄 생성을 조율한다. 이동 Rigidbody에 의존하지 않는다. |
| `PlayerBullet` | 방향 정규화·설정 속도 적용·수명·태그 기반 충돌 피해를 처리한다. 발사자의 이동속도는 전달받지 않는다. |
| `PlayerWeaponRig` | 플레이어 방향에 맞는 총기와 총구를 선택하고 총기를 플레이어에 고정한다. |
| `PlayerMuzzleAnchor` | 활성 총구를 조회하고 기존 발사점 참조를 동기화한다. |

## 호출 계약

`PlayerAttack.TryFire()`는 조작 허용·발사 간격·프리팹·동일 플레이어 소유 총구·조준을
검사한다. 성공한 탄환이 있을 때만 발사 간격을 갱신하고 머즐 플래시를 재생한다.
내부는 `TryGetMuzzle`, `TryGetAimDirection`, `FirePellets`로 분리한다.

```csharp
bool initialized = bullet.Initialize(color, damage, worldDirection);
```

`PlayerBullet.Initialize`는 bool을 반환한다. Rigidbody가 없거나 방향이 0/비정상,
속도·피해가 음수/비정상, 수명이 0 이하/비정상이면 경고와 false를 반환한다.
호출자는 실패한 인스턴스를 정리한다. PlayerAttack은 실패한 탄환을 Destroy한다.
정상 초기화 시 방향을 정규화하고 `linearVelocity = direction * speed`를 적용한다.
속도는 월드 단위/초이며, 수명 종료 또는 유효 충돌 시 탄환이 제거된다.

탄환에는 같은 Rigidbody2D에 속한 Collider2D가 하나 이상 필요하다. Awake/Reset에서
해당 Collider들을 트리거로 설정하고 목록을 캐시한다. Init 시 Collider가 없으면 실패한다.
중첩된 별도 Rigidbody의 Collider는 변경하지 않는다.

기존 `InitBullet(WeaponType, Color, float, Vector2, Vector2)` API는 제거했다.
무기 종류는 PlayerAttack이 색상·피해·발사 간격 선택에 사용한다.
새 탄환 API에는 무기 종류나 플레이어 속도를 전달하지 않는다.
이름을 변경한 직렬화 필드는 `FormerlySerializedAs`로 기존 프리팹 값을 유지한다.

## #509 원인과 변경

이전에는 PlayerAttack이 플레이어 Rigidbody의 속도를 전달하고 PlayerBullet이
`bulletSpeed + playerSpeed.magnitude`를 사용했다. 프리팹 속도 30, 플레이어 속력 5면
정지 시 30 / 이동 시 35였으며 이동 방향과 관계없이 가속됐다.

이제 이동속도 매개변수와 발사 측 Rigidbody 참조를 제거했다. 같은 프리팹은
정지·전진·후진·횡이동에서 설정 속도 30을 사용한다. 총구에서 생성하고 플레이어 위치를
기준으로 조준하는 규칙, 기본 산탄 5발·각도 25도·발사 간격과 충돌 설정은 유지한다.

현재 사용하지 않는 구형 Monster 감속 주입 주석과 미사용 Slow 필드는 정리했다.
Cryo 감속은 기존에도 현행 Enemy에 적용되지 않았으며 이번 수정에서 새로 구현하지 않는다.
플레이어가 몬스터를 직접 미는 현상과 겹침은 별도 #511/#514의 검토 범위다.

## #510 원인과 변경

기존 PlayerBullet 프리팹은 질량 1인 Dynamic Rigidbody2D와 일반 PolygonCollider2D를
사용했다. 피해 콜백에서 Destroy를 호출해도 그 전에 물리 충돌 반응이 계산돼
몬스터에게 충격량이 전달될 수 있었다. EnemyHealth.TakeDamage에는 넉백 처리가 없다.

프리팹의 `Is Trigger`를 켜고, 실행 시에도 탄환 Rigidbody 소속 Collider들을 트리거로
설정한다. 피해 판정은 OnTriggerEnter2D를 사용하므로 물리 충격량을 전달하지 않는다.
자식 Collider는 attachedRigidbody의 GameObject를 대상으로 해 기존 Collision2D의
태그/소유자 기준을 유지한다. Player/Bullet은 통과하고, 적은 피해 후 제거하며,
그 외 장애물에도 탄환을 제거한다. 첫 유효 피격 시 즉시 처리 플래그를 세우고
탄환 Collider들을 꺼서 중복 콜백 피해를 막는다.

몬스터 AI·Rigidbody·피해 API와 전역 Layer Collision Matrix는 변경하지 않는다.
고속 탄환이 작은 적을 통과하지 않는지는 Initial 씬의 단발·연사에서 함께 확인한다.

## 검증

`PlayerBulletRuntimeTests`는 기존 Editor 테스트와 동일하게 EnterPlayMode/ExitPlayMode를
사용한다. Test Runner의 EditMode에서 해당 클래스를 실행한다.

- 실제 PlayerAttack 호출로 정지·이동·역방향·대각선·고속 이동 시 탄환 속도와 물리 이동 확인.
- 총구 생성 위치, 산탄 수 5, -12.5~12.5도 분산 유지 확인.
- 0/NaN/Infinity 방향 거부 확인.
- 자식 Collider를 가진 동적 타깃에 연속 발사해 탄환 피격과 대상 속도·위치 유지 확인.
- 자식 Player Collider 통과와 일반 장애물에서 탄환 제거 확인.
- `ServerEnemy_TakesDamageOnceFromTriggerProjectile`는 실제 Initial_Stage Host가 필요한
  Explicit 테스트다. 서버 적에 피해가 1회 적용되는지 확인한다.

Initial 씬에서 정지·이동 사격 외형과 기존 무기 전환도 별도로 확인한다.
테스트 코드 추가 자체가 실행 통과 기록은 아니다.

## 수동 편집용 8방향 콜라이더 (2026-10-27)

Player 프리팹에서 몸 충돌과 탄환 피격을 각각 8방향으로 분리한다.

```
Player
├─ Body Colliders              (Player 레이어)
│  └─ Down / Right / Up / Left / DownRight / UpRight / UpLeft / DownLeft
│                             CapsuleCollider2D
└─ PlayerBulletCollider        (PlayerBulletCollider 레이어)
   └─ Down / Right / Up / Left / DownRight / UpRight / UpLeft / DownLeft
                              PolygonCollider2D, Trigger
```

`PlayerDirectionalColliders._directions`의 각 항목은 Facing·Body·Bullet을 연결한다.
`SetFacing(PlayerAimFacing)`은 해당 방향의 몸 충돌 하나와 탄환 피격 하나만 켠다.
누락·중복 방향/참조 또는 잘못된 Trigger 역할은 초기화 경고와 함께 선택 컴포넌트를 비활성화한다.
`PlayerVisual`은 `PlayerWeaponRig.Facing`의 대각선을 그대로 전달한다. 구형 Animator bool만
4방향으로 변환하며, 콜라이더는 이 변환 이전에 선택한다.

런타임 선택은 Collider.enabled만 변경한다. Transform, Collider.offset, 크기, Polygon 점을
생성하거나 덮어쓰지 않는다. Sprite에서 외곽을 자동 추출하지 않으므로 발자취를 포함하지
않도록 사용자가 각 Polygon을 직접 편집할 수 있다. 캡슐은 발밑, Polygon은 현재 8방향
대기 스프라이트의 몸체 윤곽을 기준으로 작성했다. 무기·불꽃은 포함하지 않는다. 방향별 기준
모양을 사용하므로 공격·이동 프레임마다 피격 외곽을 바꾸는 시스템은 아니다.

기존 UpDown의 Collider fileID는 Down에 유지했다. 콜라이더들은 자식이지만 기존 Player
Rigidbody를 공유한다. 탄환용 셋은 Trigger이며 Enemy 레이어를 제외하고, 몸 충돌 셋은 일반
Collider다. Sprite Pivot·총구·질량·Rigidbody 설정은 이번 변경에서 수정하지 않는다.

조정은 Play Mode 밖에서 Player.prefab을 열어 해당 자식을 선택하고 Transform 위치,
Collider Offset/Size 또는 Edit Collider를 사용한다. Down 외의 Collider는 기본 비활성이지만
Inspector에서 수치를 편집할 수 있다. 미리보기를 위해 켰다면 Down만 활성인 초기 상태로
되돌리거나 플레이 시작 시 자동 선택되는 상태를 확인한다. Play Mode에서 변경한 값은 종료 시
사라지므로 영구 조정은 프리팹 편집에서 진행한다.

PlayerWeaponMigrationTests의 DirectionalColliders_SelectOnePairAndPreserveManualPositions는
실제 프리팹의 방향별 선택, 활성 쌍 개수, 역할별 Trigger, Rigidbody 공유 및 수동 위치/offset
유지를 검사한다. 컴파일 성공과 Unity 테스트 실행은 구분한다. 위치 조정 후 Scene에서 여덟 방향
전환, 벽/몬스터 접촉과 탄환 피격을 확인해야 하며 이것만으로 #514 해결을 확정하지 않는다.

콜라이더 자식을 복제한 뒤 기존 객체를 삭제/교체하면 PlayerDirectionalColliders의 참조가
None이 될 수 있다. Player 루트 Directions의 해당 항목에 새 Collider 컴포넌트를 다시 연결해야 한다.
하나라도 비면 초기화 경고 후 선택 컴포넌트가 비활성화된다. 실행 중 생성된 Clone에서 이미
비활성화된 경우 프리팹 연결을 저장하고 Play Mode를 재시작해 초기화를 다시 진행한다.
