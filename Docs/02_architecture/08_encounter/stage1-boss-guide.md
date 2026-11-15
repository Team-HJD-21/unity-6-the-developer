# Stage 1 보스 제작 안내

상태: 프리팹과 전용 애니메이션 준비 완료. 보스 패턴·스킬 효과·페이즈 실행은 미구현이다.

## 프리팹

`Assets/Prefabs/Enemies/Base/MovingEnemy.prefab` → `Assets/Prefabs/Enemies/Base/BossEnemy.prefab` → `Assets/Prefabs/Enemies/Runtime/Boss/Stage1Boss.prefab` 순서로 상속한다.

Stage1Boss에는 기존 MushroomTreant의 스프라이트·크기·체력·이동·그림자·콜라이더 설정을 복사했다. 기존 근접 능력과 이동 컨트롤러를 유지하며, 일반 몬스터 원본은 변경하지 않았다. 보스 컨트롤러를 구현할 때 기존 컨트롤러를 교체하고 EnemyNetworkController의 참조도 함께 연결한다.

## 전용 Animator와 모션

프리팹의 Animator는 `Assets/Art/Animations/Enemies/Runtime/Boss/Stage1Boss/Stage1Boss.controller`를 사용한다. 일반 트렌트의 AnimatorOverrideController와 분리되어 있다.

| 상태 | 원본 모션 | 재생 요청 | 방향 |
| --- | --- | --- | --- |
| Idle | MushroomTreant Idle | 기본 상태 | Down / Up / Left / Right |
| Move | MushroomTreant Walk | IsMoving = true | Down / Up / Left / Right |
| Attack | Mushroom_Treant_Attack_2 | Attack Trigger | Down / Up / Left / Right |
| Stomp | Mushroom_Treant_Attack_1 | Stomp Trigger | Down / Up / Left / Right |
| Roar | Mushroom_Treant_Ability | Roar Trigger | Down / Up / Left / Right |

- 각 상태는 MoveX·MoveY로 방향을 선택하는 BlendTree를 사용한다.
- Move는 기존 MoveAnimSpeed 파라미터로 재생 속도를 조절한다.
- 클립 20개는 보스 전용 폴더에 저장했다. 스프라이트 텍스처는 기존 에셋을 참조한다.
- 공격·땅구르기·포효 클립은 반복하지 않으며, 재생이 끝나면 Idle로 복귀한다. IsMoving이 true이면 이후 Move로 전환한다.
- 효과 발생 프레임의 Animation Event는 아직 추가하지 않았다. 보스 능력 코드의 수신 메서드와 함께 연결해야 한다.

## 현재 확인 방법과 한계

Animation 창에서 Stage1Boss의 각 방향 클립을 선택해 미리보기로 외형과 모션을 확인한다. Animator에는 Attack·Stomp·Roar Trigger가 준비되어 있다.

현재 게임 코드가 자동으로 요청하는 모션은 기존 일반 Attack뿐이다. Stomp·Roar의 선택, 실행 중 이동 잠금, 네트워크 스킬 구분 전달, 소환·분출 피해 판정은 후속 구현이다. Animator 상태가 있다는 이유로 보스 스킬이 동작한다고 판단하지 않는다.

## 합의한 패턴 방향

- 1페이즈: 일반 공격.
- 2페이즈: 포효 소환과 1회 뿌리 분출 추가.
- 3페이즈: 기존 스킬 연계와 2회 뿌리 분출. 연계 후에는 대응할 빈틈을 둔다.
- 생존 소환수 제한과 스킬별 쿨타임·예고 시간·체력 구간은 이후 코드에서 설정값으로 제공한다.

이 절은 구현할 기획이며 현재 동작의 검증 결과가 아니다.
