# Player 클래스 이름과 역할

담당: 이영빈 · 갱신: 2026-10-29 · PR #575 리뷰 반영.

상태: 현재 브랜치 Player 구현의 명명 기준.

클래스와 파일 이름은 대상과 역할이 함께 드러나게 짓는다. 화면 표시, 입력 상태,
시간 계산, 충돌 조회를 구분하며 `Manager`, `Gesture`, `Depth`처럼 범위가 모호한
이름으로 묶지 않는다. public 클래스 이름과 `.cs` 파일 이름을 같게 유지한다.

## 변경한 이름

| 이전 이름 | 현재 이름 | 담당하는 동작 |
| --- | --- | --- |
| PlayerDashAfterimage | PlayerDashAfterimageEffect | 잔상 한 개의 몸체·무기 복사, 위치 고정, 투명도 감소 |
| PlayerDashTrail | PlayerDashAfterimageController | 대시 중 표시 간격·이동 거리를 확인하고 배치된 잔상을 순환 사용 |
| PlayerDashState | PlayerDashTimer | 대시 지속시간과 쿨타임 계산 |
| PlayerAttackGesture | PlayerAttackInputState | 새 클릭으로 공격을 허용하고 차단 후 입력 재사용 방지 |
| PlayerFieldBodyCollision | PlayerDeviceBodyCollision | 설치물 본체 충돌과 최초 겹친 플레이어의 탈출 예외 |
| PlayerFieldTargetCollision | PlayerDeviceContactQuery | 적 몸통과 설치물 사이의 경로 교차·접촉 거리 조회 |
| PlayerSkillDepth | PlayerDeviceSortingOrder | 설치자 발밑과 설치물 접지점을 비교한 표시 순서 갱신 |
| PlayerCombatHudLayout | PlayerCombatHudSlideController | 포탑 조작창에 맞춰 무기·대시 HUD를 아래로 접고 복귀 |
| PlayerWeaponSlots | PlayerWeaponSlotMapping | 숫자 슬롯과 무기 종류의 대응 조회 |

`PlayerDash`, `PlayerWeaponAppearance`, `PlayerWeaponHud`, `PlayerWeaponSlotView`처럼
역할이 이미 드러나는 이름은 유지한다. 이름 변경을 위해 책임을 추가하거나 동작을 바꾸지 않는다.

## Unity 연결

2026-11-04 리뷰 대응: `PlayerWeaponSelection`이 현재 무기·선택 입력·장착 참조와
`WeaponChanged`를 소유한다. `PlayerAttack`은 선택된 장비로 공격하고 공격 대기시간을
관리한다. HUD·몸체 모션·무기 외형은 선택 컴포넌트를 직접 참조하며, 선택 API를 공격
컴포넌트에 중복 제공하지 않는다. Player 프리팹에 선택 컴포넌트를 미리 배치하고 기존
시작 무기·탄환·스패너 참조를 이동했다. 이동 대상은 이 브랜치에서 사용하는
`Assets/Prefabs/Player/Player.prefab`이며, 다른 PlayerAttack 에셋을 추가할 때도
같은 선택 컴포넌트의 장비 참조를 작성해야 한다.

스크립트와 `.meta`를 함께 이동해 기존 MonoScript GUID를 유지한다. 프리팹의
컴포넌트 타입 표기와 코드·테스트·문서 참조를 함께 변경한다. 이름이 바뀐
MonoBehaviour에는 이전 타입 이름을 `MovedFrom`으로 기록한다.
직렬화 필드 이름과 값은 유지하므로 Inspector에서 작성한 배열과 오브젝트 연결은 그대로 사용한다.

잔상 프리팹도 `PlayerDashAfterimageEffect.prefab`으로 이름을 맞추되 GUID를 유지한다.
이번 명명 정리는 Enemy·일반 포탑의 공개 API 변경을 포함하지 않는다.
