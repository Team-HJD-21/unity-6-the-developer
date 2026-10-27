# 플레이어 Space 대시

기준: 2026-10-27, Issue #565. 담당: 이영빈.

대시는 Q/E 선택 스킬과 분리된 이동 능력이다. Space를 새로 누르면 짧게 빠른 속도로 이동한다. 별도 Animation Clip·대시 자세·신규 스프라이트·무적·공격 피해는 추가하지 않았다. 기존 몸체·무기 표시를 사용하고 잔상만 짧게 남긴다.

## 동작과 수치

| 설정 | 기본값 | 편집 위치 |
| --- | --- | --- |
| 속도 배율 | 2.5 | Player.prefab의 PlayerDash |
| 지속시간 | 0.16초 | PlayerDash |
| 쿨타임 | 1.2초 | PlayerDash |
| 잔상 수 | 4개 | Player.prefab에 배치된 Dash Afterimage 1~4 및 PlayerDashTrail 배열 |
| 잔상 표시 간격 | 0.045초 | PlayerDashTrail |
| 잔상 최소 이동 거리 | 0.15 | PlayerDashTrail |
| 잔상 수명 | 0.14초 | PlayerDashAfterimage.prefab |
| 잔상 색·투명도 | 옅은 청록, Alpha 0.22 | PlayerDashAfterimage.prefab |

현재 Player 프리팹의 기본 이동속도는 17이므로 장애물이 없을 때 대시 속도는 42.5, 총 이동 거리는 약 6.8 월드 단위다. 이동속도 원본 값을 덮어쓰지 않으며 대시 종료 후 현재 기본 이동속도로 복귀한다.

시작 시 이동 입력을 정규화해 방향을 확정한다. 정지 중에는 마지막 유효 이동 입력을 사용하고, 이동 입력이 한 번도 없으면 아래 방향을 사용한다. 대시 중 방향키를 바꿔도 진행 방향은 유지한다. Space를 계속 눌러도 다시 실행되지 않는다.

대시 중 기존 조준·공격·무기 전환을 유지한다. 일시정지·조작 차단·CanMove=false·비활성화·스킬 선택·스킬 설치 문맥에서는 시작하지 않거나 진행 중인 대시를 중단한다. 중단해도 쿨타임을 환불하지 않는다. 쿨타임은 게임 시간으로 진행하며 일시정지 중에는 멈춘다.

## 코드 책임과 API

위치: `Assets/Scripts/Features/Player/Movement/`.

| 코드 | 책임 / 계약 |
| --- | --- |
| PlayerDashState | 지속시간·쿨타임 계산. ConsumeStep은 물리 스텝에서 남은 시간만 소비해 마지막 스텝의 거리 초과를 막는다. |
| PlayerDash | Space 요청, 방향, 속도와 상태 관리. TryDash()는 수락 여부, IsDashing·IsReady·CooldownRemaining·CooldownProgress는 조회다. TryGetVelocity는 PlayerMovement가 물리 스텝당 한 번 호출한다. |
| PlayerMovement | 일반 이동과 대시의 Rigidbody2D 속도를 FixedUpdate에서 한 번 적용한다. CanAcceptInput은 공통 조작 가능 여부다. StopImmediately는 대시도 취소하고 속도를 0으로 만든다. |
| PlayerDashTrail | 미리 배치한 4개 잔상을 순환 사용한다. 캐릭터가 실제로 일정 거리 이상 이동했을 때만 장면을 기록한다. |
| PlayerDashAfterimage | 몸체·분리된 무기의 Sprite, 플립, 위치·회전·스케일·정렬을 복사한다. LateUpdate에서 월드 위치를 고정하고 서서히 투명하게 만든다. |
| PlayerDashHud | PlayerSkills/UI에 위치. 기존 PlayerSkillHud.Owner를 따라 로컬 플레이어의 대시 상태를 표시한다. 추가 플레이어 검색을 하지 않는다. |

PlayerInputHandler.WasDashPressed는 Space의 새 누름만 전달한다. 대시 속도는 AddForce나 Transform 이동으로 적용하지 않는다. PlayerMovement가 Rigidbody 속도의 유일한 작성자이며 두 이동 컴포넌트가 같은 속도를 덮어쓰지 않는다.

Player 프리팹의 충돌 감지만 Continuous로 바꿨다. 질량 50, Collider 모양·크기·트리거·레이어 설정은 유지한다. 진행 방향을 막는 실제 Collision2D 접촉이 발생하면 대시를 끝낸다. 충돌 대상의 기존 물리 반응은 유지하며 별도의 넉백을 추가하지 않는다. 테스트 카메라 줌은 Q/Space 대신 PageUp/PageDown으로 옮겨 게임 입력과 충돌하지 않게 했다.

## 프리팹과 UI 편집

`Assets/Prefabs/Player/PlayerDashAfterimage.prefab`에는 Body Snapshot과 Weapon Snapshot SpriteRenderer, SortingGroup, 표시 컴포넌트가 있다. Player 프리팹에 이 프리팹 인스턴스를 4개 미리 배치했다. 실행 중 Instantiate·AddComponent·Sprite 생성 없이 재사용하며, Collider·Rigidbody·Animator는 없다.

잔상은 Player 자식으로 수명을 함께 관리하되, 표시하는 동안에는 복사한 월드 위치를 유지한다. Player의 스케일 0.8도 보정하며 캐릭터와 함께 끌려가지 않는다. SortingGroup의 Sort At Root를 켜서 원래 캐릭터 그룹과 분리해 뒤에 표시한다. 플레이어가 비활성화되거나 조작이 차단되면 표시 중인 잔상도 숨긴다.

화면 표시는 `Assets/Prefabs/UI/PlayerSkillHUD.prefab > Dash Slot`이다. 기존 Canvas 하단 무기 선택창 오른쪽에 Space 키·원형 대기 표시·남은 시간을 배치했다. 크기·위치·색·폰트는 프리팹에서 수정한다. 표시 전용으로 클릭을 받지 않으며 Q/E Loadout이나 선택 카드는 변경하지 않는다.

## 검증과 남은 확인

- 동일 PlayerDashStateTests 소스로 .NET에서 5개 케이스 통과: 물리 스텝 0.02/1⁄60/1⁄30초의 총 지속시간, 중단 후 쿨타임 유지, 잘못된 시간 입력 거부.
- 런타임·Editor 코드 컴파일 통과.
- 프리팹 정적 검사: 중복 ID·끊긴 로컬 참조 없음, 잔상 프리팹 4개 연결, 몸체·무기 연결, 기존 Collider 유지, 질량 유지, Canvas 1개 재사용, 기존 HUD 오브젝트 설정 유지.
- PlayerDashRuntimeTests에 속도 증가·정지 중 방향·일시정지 중단·얇은 벽 충돌·잔상의 월드 위치 및 분리 무기 복사·프리팹 참조 검증을 작성했다. Unity Test Runner는 아직 실행하지 않았다.

사용자가 에디터에서 직접 플레이 검증하기로 했다. Initial_Stage에서 Space 대시와 키 유지, 벽·몬스터·포탑 접촉, LMB 공격 중 대시, 무기 전환 중 잔상, 스킬 선택·일시정지·비활성화 시 정리를 확인한다. 실제 이동감과 충돌·프리팹 임포트 결과는 이 플레이 검증으로 확인해야 한다.

현재 플레이어의 기존 로컬 입력·이동 경로를 확장했다. 신규 네트워크 요청·권한·동기화와 몬스터·터렛 Core 내부 변경은 포함하지 않는다.
