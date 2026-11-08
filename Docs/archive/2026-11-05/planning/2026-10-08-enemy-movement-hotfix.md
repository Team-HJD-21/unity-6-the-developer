> 보관 문서 · 2026-11-05 교체 전 스냅샷
> 기준 커밋: `1a03e9a9164e5db94cbc8775162467d08cf63a3b`. 당시 설명·검증 근거이며 현행 실행 지시가 아니다.

# Initial_Stage Enemy 이동 HOTFIX

## 확인한 원인과 범위

Enemy는 스폰만으로 임의 방향 이동하지 않는다. 서버의 `EnemySpawnExecutor.Update()`가 분대 목표를 선택하고 `EnemyNetworkController.Update()` → `EnemyAIBrain.UpdateAI()` → `EnemyController.ExecuteBehavior()`가 목표를 향해 이동한다. 분대에 등록되더라도 목표가 없으면 의도적으로 정지한다.

`EnemySquadTargetSelector`의 정책은 살아 있는 Player 또는 Turret 중 분대 중심에 가장 가까운 후보를 선택하는 것이다. 그런데 Initial_Stage의 `playerPrefab`이 참조하는 `Assets/Prefabs/Player/Player.prefab`에는 Enemy가 찾는 `TargetableComponent`가 없었다. 따라서 실제 Player가 화면에 있어도 후보가 아니었다. 모든 터렛이 비활성/파괴 상태라면 후보가 0개가 되어 모든 분대가 계속 대기한다. 이 누락은 10월 7일 PoC 통합 준비 문서와 10월 8일 2차 통합 문서에도 기록되어 있었다.

이미 활성 터렛이 있으면 이 누락만으로 전체 정지를 설명할 수는 없다. 수정 뒤 실제 Initial_Stage에서 서버·유효 후보·분대 등록·선택 목표·1초간 XY 위치 변화도 함께 확인한다. 이동 방식은 NavMesh가 아니라 Transform 직접 XY 이동이며 Rigidbody의 velocity만으로 이동 성공을 판정하지 않는다.

## 수정

- Enemy 소유 `PlayerTargetableAdapter`를 추가했다. Player의 `PlayerInfo.curHp/maxHp`를 읽어 타깃 종류·체력을 전달하고 생성/첫 프레임/체력 변경/비활성을 동기화한다.
- Player prefab에 `TargetableComponent`와 adapter를 부착했다. Core assembly가 Enemy/Assembly-CSharp를 역참조하는 경로는 추가하지 않았다.
- 사망하거나 adapter가 비활성화된 Player는 후보에서 제외된다. 체력 변경 구독은 비활성화 때 해제한다.
- 기존 최근접 목표 정책, 서버 권한, 스폰 수/분포, 터렛 활성 정책은 변경하지 않았다.

이 prefab을 사용하는 다른 Scene에서도 Player가 Enemy의 추적 후보가 된다. 이는 기존 Player/Turret 목표 정책의 누락을 복구하는 변화다. Player의 기존 사망 처리와 최종 승패 루프는 이 HOTFIX에서 재설계하지 않았다.

## 검증

`EnemyMovementHotfixTests`:

- EditMode: 실제 Player prefab에 타깃 및 실제 HP adapter가 있는지 확인한다.
- 명시적 Scene 통합: Initial_Stage의 자동 Host/Encounter로 적 생성, 유효 Player 후보, 실제 XY 이동, 체력 전달, 체력 소진 뒤 후보 제외를 확인한다. 최대 3개 적의 분대/목표/위치 변화/velocity를 로그로 남긴다. 같은 Play 세션에서 터렛과 Player 타깃을 잠시 끄면 정지하고, 터렛을 끈 채 Player 타깃만 복원하면 재이동하는 회귀도 확인한다. 이 fixture 변경은 Scene에 저장하지 않는다.

2026-10-08 Unity CLI 명시적 Scene 통합 **1/1 통과**. 결과: `%TEMP%/enemy-movement-hotfix.xml`. 자동 스폰 10개, 유효 Player 1개, 첫 관측에서 8개 Enemy가 XY 이동했다. 로그의 두 표본 위치 변화는 1.528/1.701 world units였다. 모든 타깃을 끈 상태의 이동은 0개, Player만 복원한 뒤 7개가 재이동했다. 실제 HP 전달/체력 소진 후 후보 제외도 통과했다. 이 실행은 Editor Scene의 실제 Host/Update를 사용한 자동 검증이며 사용자 수동 화면 확인과는 구분한다.
