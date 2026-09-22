# 기존 몬스터 동작 흐름 및 의존성 분석

| 항목 | 내용 |
|---|---|
| 관련 작업 | Issue #384 — 기존 몬스터 구조 분석 및 Netcode 테스트 환경 구성 |
| 담당 영역 | E — Enemy / Encounter / AI Platform |
| 문서 유형 | 레거시 코드 분석 |
| 최종 수정일 | 2026-09-22 |

## 1. 개요

이 문서는 기존 몬스터 코드의 생성, 이동, 대상 추적, 공격, 피격 및 사망 흐름을 정리하고 현재 코드가 의존하는 외부 시스템을 기록한다.

분석 내용은 현재 구현을 기준으로 하며 신규 Enemy AI의 최종 설계나 리팩터링 방향을 정의하지 않는다.

## 2. 클래스 구성

```text
Monster
├─ WalkAndAttackMonster : 일반 근접 몬스터
├─ AdcMonster           : 원거리 몬스터
├─ BossMonster          : Stage 1 보스
├─ BossMonster2         : Stage 2 보스
├─ Treant               : 별도 근접 구현
├─ DefaultMonster       : 능력치 설정용
├─ TankerMonster        : 능력치 설정용
└─ AssassinMonster      : 능력치 설정용

AdcBullet               : 원거리 공격 투사체
Billboard               : 체력바 카메라 정렬
```

현재 Prefab에서 주로 사용되는 행동 구현은 다음과 같다.

- `WalkAndAttackMonster`
- `AdcMonster`
- `BossMonster`
- `BossMonster2`

`DefaultMonster`, `TankerMonster`, `AssassinMonster`는 주로 능력치 구성을 담당한다.

## 3. 공통 생성 및 초기화 흐름

```text
Monster Prefab 생성
→ 자식 클래스 Start()
→ Monster.Start()
→ 현재 체력 초기화
→ 체력바 생성 및 연결
→ Billboard 추가
→ Player 참조 확인 또는 태그 검색
→ Control Unit 참조 확인 또는 태그 검색
→ 행동 Update 시작
```

`Monster.Start()`의 주요 동작은 다음과 같다.

- `currentHealth`를 `maxHealth`로 초기화
- 체력바 Prefab을 생성하고 `Current` 이름의 `Image`를 조회
- Player 참조가 없으면 `Player` 태그로 검색
- Control Unit 참조가 없으면 `CU` 태그로 검색
- 사망 상태와 대상 상태 초기화

## 4. 근접 몬스터 행동 흐름

대상 클래스:

- `WalkAndAttackMonster`
- `Treant`

```text
Update()
→ Player와 거리 계산
→ 감지 범위 안이면 Player 선택
→ 감지 범위 밖이면 가장 가까운 Control Unit accessPoint 선택
→ 대상 방향과 거리 계산
→ 공격 범위 밖이면 대상 방향으로 이동
→ 공격 범위 안이면 이동 정지
→ 공격 재사용 대기시간 확인
→ PlayerInfo.TakeDamage() 또는 ControlUnitStatus.GetDamage() 호출
→ 일정 시간 후 공격 상태 해제
```

이동 방향은 `Animator`의 `direction` 정수 파라미터로 전달한다.

| 값 | 방향 |
|---:|---|
| `0` | 아래 |
| `1` | 위 |
| `2` | 왼쪽 |
| `3` | 오른쪽 |

사용하는 Animator 파라미터:

- `direction` (`int`)
- `isMoving` (`bool`)
- `isAttacking` (`bool`)

## 5. 원거리 몬스터 행동 흐름

대상 클래스:

- `AdcMonster`
- `AdcBullet`

대상 선택과 이동 방식은 일반 근접 몬스터와 유사하다. 공격 범위에 들어오면 투사체를 생성한다.

```text
공격 범위 진입
→ bulletPrefabGreen 생성
→ 현재 대상 방향 계산
→ AdcBullet.SetDirection()
→ FixedUpdate에서 Rigidbody2D.linearVelocity 설정
→ Player 또는 Control Unit과 충돌
→ 대상에 피해 적용
→ 투사체 제거
```

투사체가 대상과 충돌하지 않으면 생성 후 5초가 지나 제거된다.

### 확인 필요 사항

`AdcMonster.Update()`에서 다음 조건이 확인되었다.

```csharp
if (player || controlUnitStatus) return;
```

Player 또는 Control Unit 참조가 존재할 때 `Update()`가 종료되는 조건으로 보인다. 정상적인 AI 실행을 차단하는지 실제 Prefab과 Scene에서 추가 확인이 필요하다.

## 6. 보스 몬스터 행동 흐름

대상 클래스:

- `BossMonster`
- `BossMonster2`

기본 대상 선택과 이동은 일반 몬스터와 유사하다. 공격 범위에 진입하면 다음 행동 중 하나를 선택한다.

```text
일반 공격 → attackDamage 적용
밟기 공격 → treadDamage 적용 및 카메라 흔들림
소환       → MonsterSpawner.BossSkillSpawn() 호출
```

추가 동작:

- `GeneralManager.Instance.inGameManager.ListenBossSpawn()`을 통해 보스 생성을 알림
- 보스 전용 Spawner 위치를 보스 위치에 맞춤
- `BossMonster`는 Coroutine으로 소환 행동을 반복
- `BossMonster2`는 시작 시 한 차례 소환

## 7. 피격 및 사망 흐름

```text
Monster.TakeDamage(damage)
→ currentHealth 감소
→ 0~maxHealth 범위로 제한
→ 체력바 fillAmount 갱신
→ 체력이 0 이하면 isDead 설정
→ Die()
→ 일반 몬스터 사망 사실을 InGameManager에 전달
→ 체력바와 PolygonCollider2D 제거
→ 이동 속도 0으로 변경
→ 자식 Animator 비활성화
→ Renderer 알파를 1초 동안 감소
→ Monster GameObject 제거
```

`isDerivedBoss`가 `true`인 보스 소환 몬스터는 일반 몬스터 사망 알림에서 제외된다.

## 8. 외부 의존성

| 의존 대상 | 사용 위치 | 사용 목적 | 연결 방식 |
|---|---|---|---|
| `PlayerInfo` | 근접 몬스터, 보스, `AdcBullet` | Player 체력 및 피해 처리 | `GetComponent<PlayerInfo>()`, `TakeDamage()` |
| `ControlUnitStatus` | `Monster` 및 행동 클래스 | Control Unit 위치, 접근점 및 피해 처리 | Inspector 또는 `CU` 태그 검색 |
| `ControlUnitStatus.accessPoints` | 근접·원거리·보스 | Control Unit 공격 위치 선택 | 접근점 전체 순회 |
| `MonsterSpawner` | 보스 | 보스 스킬 몬스터 소환 | `BossSkillSpawn()` |
| `GeneralManager` / `InGameManager` | `Monster`, 보스 | 생성 및 사망 상태 전달 | Singleton 직접 접근 |
| `AudioManager` | 보스 | 공격 및 소환 효과음 | Singleton 직접 접근 |
| `CameraController` | 보스 | 밟기 공격 시 카메라 흔들림 | `Camera.main.GetComponent()` |
| `Animator` | 행동 클래스 | 방향, 이동 및 공격 애니메이션 | 같은 GameObject에서 조회 |
| `SpriteRenderer` | 일부 행동 클래스 | Sprite 표현 | 같은 GameObject에서 조회 |
| `Rigidbody2D` | `AdcBullet` | 투사체 이동 | Inspector 직렬화 참조 |
| `PolygonCollider2D` | `Monster` | 사망 시 충돌 제거 | 같은 GameObject에서 조회 |
| `healthBarPrefab` / `Image` | `Monster` | 체력 표시 | Inspector 참조 및 `Current` 이름 검색 |
| `Billboard` | 체력바 | 체력바의 카메라 방향 유지 | 런타임에 컴포넌트 추가 |

## 9. 요약

- `Monster`가 체력, 체력바, 참조 탐색 및 사망 처리를 공통으로 담당한다.
- 근접, 원거리 및 보스 클래스가 각각 이동과 공격 행동을 구현한다.
- 일반적인 대상 선택은 Player를 우선 확인하고, 조건에 따라 Control Unit의 접근점을 선택하는 방식이다.
- 피해 처리는 `PlayerInfo`와 `ControlUnitStatus`의 구체 메서드를 직접 호출한다.
- 보스는 Spawner, Manager, Audio 및 Camera 시스템에 추가로 의존한다.
- 원거리 몬스터의 조기 반환 조건은 실제 동작 확인이 필요하다.
