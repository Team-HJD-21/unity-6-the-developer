# Player Combat API

최종 갱신: 2026-10-15
범위: 기본 무기 사격과 플레이어 탄환. #509 이동속도 가산 버그 수정 기준.

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
탄환·플레이어·몬스터의 물리 충돌 및 밀림은 별도 #510/#511/#514의 검토 범위다.

## 검증

`PlayerBulletRuntimeTests`는 기존 Editor 테스트와 동일하게 EnterPlayMode/ExitPlayMode를
사용한다. Test Runner의 EditMode에서 해당 클래스를 실행한다.

- 실제 PlayerAttack 호출로 정지·이동·역방향·대각선·고속 이동 시 탄환 속도와 물리 이동 확인.
- 총구 생성 위치, 산탄 수 5, -12.5~12.5도 분산 유지 확인.
- 0/NaN/Infinity 방향 거부 확인.

Initial 씬에서 정지·이동 사격 외형과 기존 무기 전환도 별도로 확인한다.
테스트 코드 추가 자체가 실행 통과 기록은 아니다.
