# 플레이어 클릭 입력 분리

기준: 2026-10-27, Issue #564. 담당: 이영빈.

공격은 LMB를 유지해 사용한다. 포탑 선택·설치·UI에 쓴 클릭은 공격에서 제외하고, 버튼을 놓고 다시 누를 때까지 공격을 시작하지 않는다. 무기별 공격 간격·피해·산탄 수를 사용한다. 일반 필드 터렛 수리는 R 유지 입력으로 분리했다. Space 공격은 제거했다. 이후 #565에서 추가한 Space 대시는 [대시 API](player-dash-api.md)를 참고한다.

## 처리 순서와 책임

| 단계 | 소유 코드 | 동작 |
| --- | --- | --- |
| UI | PlayerInputHandler.Update (-200) | 현재 포인터의 GraphicRaycaster 결과를 검사해 클릭을 소비한다. 클릭으로 UI가 닫히더라도 소비 기록은 그 프레임에 남는다. |
| 스킬 시작·취소 | PlayerSkillController.Update (100), 각 설치 스킬 | 설치 문맥을 해당 프레임에 예약한다. Q/E와 LMB를 동시에 눌러도 포탑 선택·공격으로 넘기지 않는다. |
| 포탑 선택 | InitialStagePlayerHUD / TurretInteractionUI.Update (150) | 설치 예약과 UI를 제외한다. 실제 포탑을 클릭했으면 거리·기존 선택 여부와 관계없이 클릭을 소비한다. 빈 지면은 소비하지 않는다. |
| 무기 선택 | PlayerWeaponSelection.Update (200) | 숫자키·휠·Tab을 읽고 선택 상태와 장착 외형을 갱신한다. HUD 버튼도 같은 선택 API를 호출하며 공격 대기시간은 변경하지 않는다. |
| R 수리·복구 | PlayerTurretRepair.Update (250) | 스킬·무기 선택 후 수리/파괴 복구 조건과 이동 취소를 확정한다. 작업 중 실제 공격을 차단하며 키를 놓고 다시 눌러야 재개한다. 파괴 복구는 진행률 100%에서 Restore를 호출한다. |
| 설치 확정 | PlayerTurretSkill / PlayerPlacementSkillBase.LateUpdate (0) | UI에 사용하지 않은 새 클릭을 한 번 소비하고 위치를 재검사한다. 실패도 클릭을 소비하며 성공 때만 수량·쿨타임을 소모한다. 방벽은 두 번째 본체 연결 성공 때 소모한다. |
| 공격 | PlayerAttack.LateUpdate (300) | 소비·예약·UI·조작 상태를 검사한 뒤 유효한 공격 제스처일 때 TryFire를 호출한다. |

숫자는 DefaultExecutionOrder이며 같은 생명주기 안의 순서다. 모든 Update가 끝난 뒤 LateUpdate가 실행된다. 설치 스킬이나 공격 처리의 실행 순서를 변경할 때 이 우선순위를 함께 확인한다. 별도 스크립트의 무기 수치·스패너 타격 예약 처리는 기존 흐름을 따른다.

## 공개 입력 계약

| API | 의미 |
| --- | --- |
| GameInput.PrimaryPointerWasPressedThisFrame / PrimaryPointerIsPressed | 새 누름 / 누르는 중인 원시 입력 |
| GameInput.TryConsumePrimaryPointerPress() | 아직 사용하지 않은 새 클릭을 소비한다. 두 번째 호출은 false. |
| GameInput.PrimaryPointerWasConsumed | 이번 프레임에 다른 동작이 클릭을 소비했는지 조회 |
| GameInput.ReservePrimaryPointerForPlacement() | 설치 시작·진행·확정·취소 프레임을 선택·공격에서 제외 |
| GameInput.IsPointerOverUi() | 현재 위치를 직접 RaycastAll로 검사하며 GraphicRaycaster만 UI로 취급 |
| PlayerInputHandler.ResolvePrimaryAttack(bool canAttack) | 공격 단계에서 호출해 이번 프레임의 IsAttackPressed를 확정 |
| PlayerInputHandler.CancelAttackGesture() | 활성 연사 상태를 해제. 비활성화 시 호출 |
| PlayerSkillController.IsPlacingSkill | 터렛·폭탄·비콘 미리보기 또는 방벽의 첫 번째·두 번째 본체 배치 상태 |

GameInput의 기록은 로컬 마우스 한 개에 대한 프레임 상태이며 네트워크 전송 상태가 아니다. 씬의 기존 UI 이벤트는 원시 마우스 입력을 그대로 받는다. 소비 기록은 게임플레이에서 같은 클릭을 다시 사용하지 않기 위한 것이다. 정적 기록은 SubsystemRegistration에서 초기화한다.

PlayerAttackGesture는 새 누름으로만 연사를 시작한다. UI 진입·일시정지·조작 차단·스킬 선택·설치 예약·클릭 소비는 연사를 중단한다. 문맥이 해제되어도 누르는 중인 입력으로 재개하지 않는다. PlayerInputHandler나 PlayerAttack이 비활성화되는 경우도 상태를 해제한다.

## 동작과 기존 기능

- 기본총·냉각총·스나이퍼·스패너에 같은 클릭 우선순위를 적용한다. 포탑을 직접 클릭하면 스패너도 선택만 한다. 현재 Player 프리팹은 LMB 타격 수리 대신 R 유지로 일반 필드 터렛을 수리한다. [수리 조건](player-weapon-api.md#포탑-수리)
- 먼 포탑의 접근 안내와 가까운 포탑의 조작 조건은 기존 선택·Presenter 경로를 유지한다.
- 설치형 스킬은 Q/E로 미리보기를 시작하고 LMB로 확정한다. 터렛은 기존 플레이어 중심 주변 8칸 중 마우스 방향을 선택한다. 폭탄·비콘은 플레이어 중심 반경 12, 방벽 첫 본체는 반경 12의 원 안에서 커서 위치에 설치한다. 방벽 첫 본체를 확정하면 원과 판정을 첫 위치 중심 반경 13.5로 고정하며 플레이어 이동에 따라 옮기지 않는다. 범위 밖 클릭은 소비하되 설치하지 않는다. 두 번째 설치 실패 시 사유를 표시하면서 첫 위치를 유지하며, 자동 만료 없이 재시도한다. 우클릭·Esc·같은 스킬 키로 취소하고 다른 스킬로 전환해도 기존 미리보기는 취소한다.
- 무기 숫자키·휠·HUD 버튼과 씬·프리팹 배치는 유지한다. 새 UI 오브젝트를 런타임에 만들지 않는다.
- 몬스터 내부 코드와 터렛 Core/API는 변경하지 않는다.

## 검증

PlayerAttackGestureTests의 새 클릭·버튼 해제·차단된 클릭의 드래그/메뉴 닫기·공격 중 차단·비활성화 후 입력 유지 시나리오 3건을 동일 소스의 .NET 실행으로 통과했다. 런타임·Editor 코드는 컴파일했다.

PlayerPrimaryInputTests는 실제 Input System 마우스와 UI Raycast를 사용해 UI가 닫힌 클릭, 중복 소비, 설치 종료 후 입력 유지, 새 월드 클릭, 잠금 해제 후 입력 유지를 검사한다. Unity가 프로젝트를 열고 있어 이 PlayMode 테스트 및 실제 플레이는 아직 실행하지 않았다.

플레이 확인: 네 무기의 LMB 유지 공격, 가까운/먼/이미 선택된 포탑 클릭, 설치 성공·실패·취소, UI를 누른 채 밖으로 이동, 조작 잠금·선택창·일시정지 해제, 숫자키·휠 전환을 확인한다. Space는 플레이어 공격을 시작하지 않아야 한다. #565에서 테스트 카메라 줌은 PageUp/PageDown으로 옮겼다.
