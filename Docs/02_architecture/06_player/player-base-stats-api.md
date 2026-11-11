# Player 기본 스탯·레벨 달성 보상

기준: 2026-11-10, 이영빈이 이번 세션에서 수치·투자·회수 규칙을 승인했다. 런타임/Editor 컴파일과 프리팹 정적 연결을 검증했으며 Unity 실제 플레이/자동 테스트 실행은 별도다.

## 규칙과 편집 위치

`Player.prefab`에 `PlayerBaseStats`를 배치하고 `Assets/Data/Player/PlayerBaseStatsSettings.asset`을 연결했다. 무기 고유 설정과 분리된 Player 공통 성장이다.

| 스탯 | 초기값 | 1포인트 투자 |
| --- | --- | --- |
| 공격력 | 무기 피해량의 100% | +5%p 배율. 예: 20 → 21 |
| 이동속도 | PlayerMovement의 기존 기본속도 | 기본값의 +2%. 기본 5 → 5.1 |
| 치명률 | 0% | +1%p, 최대 50% |

치명타 피해는 최종 일반 피해의 1.5배다. 스탯 투자는 합산 배율이고 포인트 회수는 무료이며, 갖고 있는 투자량을 넘어 회수할 수 없다. 최대 체력·스킬·무기 강화 등 다른 스탯은 추가하지 않았다. 수치는 Settings 에셋에서 조절하며 유효하지 않은 설정은 스탯 조작을 허용하지 않는다.

1레벨의 가용 포인트는 0개다. 2레벨부터 각 달성 레벨 행의 **보상받기**를 누르면 기본 1포인트를 지급한다. 여러 레벨 상승도 각 보상을 따로 수령할 수 있고 순서 제한은 없다. 미달성·1레벨·최대 레벨 초과·이미 수령한 행은 지급을 거부한다. 투자 전체 회수는 포인트만 반환하고 수령 기록은 유지한다. 다른 보상 후보(스킬 해금 포인트·연구 토큰·인게임 재화·무기 강화 포인트)는 지급하지 않는다.

## 실제 전투 연결

- `PlayerMovement.MoveSpeed`는 `BaseMoveSpeed × MovementMultiplier`다. 기존 일반 이동과 대시 시작이 이 값을 사용한다. 감속은 기존 위치에서 한 번만 적용한다.
- `PlayerAttack.FireProjectile`은 투사체마다 `ResolveWeaponDamage`를 호출한 값을 `PlayerBullet.Initialize`에 전달한다. 이미 발사된 탄환은 발사 때 정해진 피해를 유지하므로 투자/회수로 소급 변경되지 않는다. 기존 탄환 체크포인트도 이 확정 피해를 저장한다.
- `PlayerSpannerWeapon.ApplyMeleeDamage`는 한 타격의 배율/치명타를 결정하고 범위 내 명중 대상에 사용한다. R 수리 회복량은 스탯과 무관하다.
- 스킬·설치 터렛·공용 터렛의 피해량은 이 배율을 적용하지 않는다.
- 무기 UI의 피해량은 스탯 적용 후, 치명타 전의 값이다. 실제 EnemyHealth에는 기존과 같이 정수 반올림 피해를 전달한다.

치명타는 Core `RandomBinding`의 `combat/player-critical` 흐름을 사용한다. Player는 Scene의 초기 IRandomConsumer 수집 이후 생성되므로 기존 `IPlayerMatchBinding.BindMatch(session)`에서 주입된 session.Random을 연결하고, Unbind/Destroy에서 반환 lease를 해제한다. 별도 전역 Random/seed 서비스는 만들지 않는다. Core의 `core/random` writer가 이 흐름도 저장한다. **난수가 연결되지 않은 독립 Feature 시험이나 Client에서는 임의 난수로 대체하지 않으며 치명타를 추첨하지 않는다.** 실제 Initial_Stage의 Host Match 조립이 연결 지점이다.

## API와 권한

- `AvailablePoints`, `Invested(stat)`, `AttackMultiplier`, `MovementMultiplier`, `CriticalChance`, `Changed`로 상태를 조회한다.
- `CanClaimReward/TryClaimReward(level)`, `CanInvest/TryInvest(stat)`, `CanRefund/TryRefund(stat)`, `TryResetInvestments()`가 모든 UI/디버그 요청의 진입점이다.
- 기존 입력 잠금과 생존 상태를 존중하며 현재는 독립 로컬 또는 실행 중 Host Player만 변경할 수 있다. Client는 독립 지급/투자/치명타 추첨을 하지 않는다.
- 원격 Client 투자·수령 요청과 Host 결과 복제, 늦은 참가/actor 재생성 시 성장 snapshot, 계정/프로필 영속 저장은 **Core와 후속 연결이 필요**하다. 현재 이를 전체 멀티플레이 성장 완료로 취급하지 않는다.

## 저장

`PlayerBaseStats.Checkpoint.cs`는 actor의 기존 참여자 수집에 들어간다. 섹션 키는 actor prefix + `/base-stats`, schema 1 / **Progression** / RestoreOrder 240이다. 기본 Core 되감기는 XP처럼 Progression을 유지한다. 명시적으로 Progression을 복원할 때 투자량과 수령 레벨 목록을 함께 복원하고, 포인트는 목록×지급량−투자량으로 계산하여 따로 어긋나는 잔액을 저장하지 않는다.

Player ID·성장 설정·목록 중복·레벨 범위·치명률 상한·초과 투자 등을 준비 단계에서 검증하고 적용/롤백 단계를 분리한다. 새 참여자 섹션이 없는 기존 체크포인트와의 호환은 Core의 필수 섹션 정책을 따른다. 임의로 예전 저장에 포인트를 새로 지급하지 않는다. 파일 IO·저장 순서·프로필 저장은 Player가 소유하지 않는다.

## UI·디버그와 확인 순서

[상태창 배치/입력](player-ui-api.md#2026-11-10-플레이어-상태창). 모든 슬롯·버튼·텍스트·ScrollRect와 8개 보상 행은 HUD 프리팹에 작성했다. ScrollRect 위치에 맞춰 행을 재사용하므로 최대 레벨만큼 런타임 UI를 생성하지 않는다.

옆 디버그 패널의 XP 버튼은 현재 바인딩된 Player의 `PlayerExperience.AddExperience`를 호출한다. `다음 레벨`은 필요한 XP를 더하며 자동 수령하지 않는다. 최대 레벨·정지된 필드·권한 없는 Client는 XP 지급을 거부한다. `투자 전체 회수`는 경험치/수령 기록을 지우지 않는다. 에디터 또는 개발 빌드 전용이며 출시 빌드에서는 숨기고 실행도 거부한다.

Unity에서 I → XP +100 → 2레벨의 보상받기 → 스탯 +/−를 확인한다. 포인트가 1회만 지급되는지, 이동속도·실제 총알/스패너 피해·치명률 상한·무료 회수·스크롤 마지막 행을 확인한다. ESC가 상태창만 닫는지, Player 객체 교체 때 이전 값이 남지 않는지 확인한다. `PlayerBaseStatsTests`와 `PlayerStatusPanelTests`에 계산/중복 방지/복원/입력/디버그/전투 설정 회귀 검사를 추가했으며 실제 Unity 실행 결과는 아직 없다.
