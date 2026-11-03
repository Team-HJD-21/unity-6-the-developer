# Player skill responsibilities and review validation

수정일: 2026-10-22

2026-11-04 설치 흐름 갱신 · 담당: 이영빈

폭탄·비콘·방벽은 원형 범위 안에서 커서 XY 위치 그대로 미리보기와 LMB 확정을 사용한다. 폭탄·비콘 설치 반경은 플레이어 중심 12, 방벽 첫 본체는 12이다. 방벽의 첫 본체를 확정하면 첫 위치 중심 반경 13.5로 즉시 전환하고 플레이어가 이동해도 원을 고정한다. 범위 밖 후보는 빨간색·실패 안내를 표시하고 설치하지 않는다. 후보를 원의 가장자리로 보정하지 않는다. 터렛은 기존 주변 8칸(간격 4) 방식을 유지한다. `PlayerPlacementSkillBase`가 시작 프레임 클릭 재사용 방지, UI 클릭 제외, 확정 직전 범위·겹침 재검사, 우클릭/Esc/같은 키 취소를 담당한다. 스킬 전환·선택 초기화·비활성화 시 미리보기를 정리하며 공격·터렛 선택·회수 입력과 겹치지 않는다. 미리보기 중 대시는 계속 사용할 수 있다.

방벽은 첫 번째·두 번째 본체를 차례로 클릭한다. 두 번째 설치 실패 시 기존 빨간 안내에 이유를 표시하며 첫 위치를 유지한다. 첫 위치에는 자동 만료가 없으며 취소나 정상 연결까지 유지한다. 두 위치와 연결 구간의 지면·점유 공간을 다시 확인하고, 완성된 연결에만 수량 1개를 소모한다. 실패·취소·첫 지점 확정에는 쿨타임과 수량을 소모하지 않는다. 시한폭탄·비콘도 설치 확정 성공에만 소모한다.

`Assets/Prefabs/Player/PlayerBombPreview.prefab`, `PlayerBeaconPreview.prefab`, `PlayerBarrierPreview.prefab`은 콜라이더·공격·체력·수명 컴포넌트가 없는 표시 전용 프리팹이다. 방벽의 고정 본체·후보 본체·연결선과 각 스킬의 `Placement Range` 원도 미리 배치했다. 원은 프리팹의 64점 단위원 LineRenderer를 사용하며 런타임에는 위치와 크기만 갱신한다. 색·선 두께·본체 크기는 프리팹에서, 기본 설치 반경은 Player의 각 스킬 Placement Radius에서 편집한다. 범위는 본체 중심의 XY 거리 기준이다. 방벽의 두 번째 원과 판정은 첫 본체의 고정 위치를 기준으로 하며, 반경은 Max Distance(기본 13.5)를 공유해 표시와 실제 최대 연결 거리를 맞춘다. 슬롯과 안내 Canvas는 기존 씬 UI를 사용한다.

기존 PlayMode 회귀 테스트를 클릭 확정 계약에 맞추고, 방벽 두 번째 실패 후 재시도·첫 위치 유지·자동 만료 없음, 비콘 설치 실패 후 재시도, 스킬 전환과 선택 초기화 시 취소 검증을 추가했다. 런타임·Editor 컴파일 및 프리팹 참조 검사를 수행하며, Unity 실제 플레이 테스트 실행은 별도 확인이 필요하다.

PR #498 리뷰에 따라 스킬의 독립적인 책임을 분리했다. 기존 Player 소스와 몬스터 원본은 변경하지 않는다. 기존 MonoBehaviour의 GUID와 직렬화 필드는 유지하므로 현재 프리팹 연결을 그대로 사용한다. 공통 스킬 베이스와 역할별 보조 클래스로 분리했다. 플레이어 터렛 체력 컴포넌트는 프리팹에 연결했다. 피격 표시는 기존 색상 틴트만 유지한다.

## 역할

| 진입점 | 분리한 책임 |
| --- | --- |
| `PlayerFieldDevice` | 체력·수명 및 장비 활성 상태 |
| `PlayerFieldDeviceVisual` | 펄스·방벽·비콘 외형과 피격 표시 |
| `PlayerFieldDeviceRegistry` | 활성 장비 등록과 방벽 우선 목표 선택 |
| `PlayerBarrierGeometry` | 방벽과 이동 경로의 교차 판정 |
| `PlayerFieldEnemyScanner` | 주변 서버 네트워크 적에 중재 컴포넌트 연결 |
| `PlayerFieldEnemyAdapter` | 신규 네트워크 적과 장비 행동 연결 |
| `PlayerFieldEnemyControlLease` | 서버 권한 확인 및 기존 AI 중지·복구 |
| `PlayerFieldEnemyBehaviour` | 장비 접근과 공격 간격 |
| `PlayerFieldEnemyAnimation` | 이동 표현 및 기존 네트워크 공격 이벤트 호출 |
| `PlayerBarrierSkill` | 두 펄스 설치 흐름과 설치된 방벽 소유 |
| `PlayerSkillCharges` | 보유 수량과 순차 충전 |
| `PlayerBarrierPlacementRules` | 설치 구간의 지형 검사 |
| `PlayerBarrierPlacementPreview` | 설치 미리보기 |
| `PlayerSkillHud` | UI 조립 및 갱신 순서 |
| `PlayerSkillSlotView` | 개별 슬롯과 방사형 쿨타임 |
| `PlayerSkillSelectionPanelView` | 선택 카드와 Q/E 미리보기 |
| `PlayerSkillTooltipView` | 툴팁과 hover 처리 |
| `PlayerTurretRecoveryPromptView` | 포탑 아래 F 안내 |
| `PlayerSkillDescriptions` | 스킬 표시 이름과 설명문 |
| `PlayerSkillHudSceneBinding` | 씬 HUD와 같은 씬의 로컬 플레이어 연결 |
| `PlayerSkillCardView` | 프리팹 스킬 카드의 선택·상태·포인터 안내 |
| `PlayerTurretTargeting`, `PlayerTurretWeapon` | 플레이어 터렛 적 검색과 탄환 생성 |
| `PlayerTurretPlacementRules` | 플레이어 터렛 바닥·충돌체의 지면 포함과 캐릭터·장애물·포탑 겹침 검사 |
| `PlayerTurretPlacementPreview` | 미리 제작한 반투명 프리팹의 위치·설치 가능 색상 갱신 |
| `PlayerTurretPlacementResult` | 설치 성공과 지면 부족·포탑 겹침·장애물 등 실패 사유 enum |
| `PlayerBombAreaDamage` | 범위 피해와 대상별 중복 피해 방지 |

## 공통 스킬과 Scene 연결

네 스킬은 PlayerSkillBase를 상속하며 공통 사용 조건과 상태 조회를 제공한다. PlayerSkillController는 공통 베이스로 실행·UI 상태를 조회한다. PlayerControlState는 IPlayerMatchBinding을 통해 Scene 소유 Match의 단계를 전달받는다. Player V2의 GeneralManager·InGameManager 참조를 제거했다. 기존 씬의 GameOver 연결은 InGameManager가 PlayerInfo.onDeath를 구독하여 유지한다.

플레이어 터렛은 체력 100이며 손상 비율에 비례하여 최대 30초 충전한다. 독립적인 PlayerTurretInventory가 설치 수량과 각 포탑 충전을 담당한다. 설치 간격 3초와 충전은 별도다. 입력과 수치 변경 안내는 [Player Input Guide](../team/player-input-guide.md)를 따른다.

## 신규 몬스터 연동

장비 행동 중재는 `EnemyNetworkController`, `EnemyHealth`, `EnemyNetworkAnimator`를 사용한다. 레거시 `Monster`의 이동·공격 수치와 행동을 참조하지 않는다. 스폰된 서버 적만 기존 AI 실행을 잠시 중지하며, 클라이언트와 미스폰 적은 제어하지 않는다. 기존 타깃 및 분대 정보는 변경하지 않는다. 장비 소멸, 적 사망·Despawn, 중재 컴포넌트 비활성화 시 기존 실행 상태를 복구한다.

신규 몬스터는 공격력·사거리·쿨타임을 공개하는 API가 없어 장비 공격에는 Adapter의 임시 설정을 사용한다. 담당자 API 제공 시 이 설정을 교체한다. 이동 속도는 실제 이동량으로 추정하며 측정 전에는 임시 기본값을 사용한다. 기존 네트워크 Animator가 이동 상태를 게시하기 전에 표현을 갱신한다.

이 변경은 적 행동의 서버 권한을 보장한다. 스킬 장비 자체의 네트워크 Spawn·동기화 및 클라이언트의 설치 요청 처리는 별도의 후속 작업이다.

## 테스트

- 테스트는 `Test/Editor` 아래에 배치한다. 기존 Assembly-CSharp 런타임을 참조하므로 Editor 전용 컴파일을 유지한다. `AssetDatabase.MoveAsset`을 사용하는 `PlayerSkillFolderMigration`이 다음 에디터 컴파일 후 스킬을 Controllers·UI·Common·Devices·Enemy·Skills 폴더로 옮기고 테스트 폴더도 이동한다. 현재 에디터 연결이 응답하지 않아 실제 이동 완료는 아직 확인하지 못했다. 메뉴 `Tools > Player > Organize Skill Scripts`로도 실행할 수 있다. GUID와 meta는 Editor가 보존한다.
- Editor 폴더의 `AssetDatabase`는 기존 프리팹을 로드하는 검증용이다. 런타임 장비 생성은 직렬화된 프리팹 참조와 `Instantiate`를 사용한다.
- 테스트를 외형, 충돌, 목표 선택, 충전, 입력·선택 흐름, 폭발 및 무기 연결로 나눈다.
- 좌표·스케일·피해량·시간은 거리 또는 허용 오차로 비교한다.
- 테스트에서 `Awake`, `OnEnable`, `Start`, `LateUpdate`를 `SendMessage`로 호출하지 않는다. 생명주기 검증은 `EnterPlayMode` 후 실제 활성화와 프레임 진행을 사용한다.
- `PlayerFieldNetworkIntegrationTests`는 명시적 테스트다. `Initial_Stage` Host에서 스폰된 적이 비콘을 공격하고 기존 AI로 복구하는지 확인한다. 완료 후 이전 Scene 구성을 복구한다.

Unity Test Runner에서 `PlayerFieldSkillTests`, `PlayerFieldTargetSelectionTests`, `PlayerSkillChargeTests`, `PlayerFieldEnemyTests`, `PlayerWeaponMigrationTests`, `PlayerSkillSelectionTests`, `PlayerSkillPrefabTests`와 Runtime 테스트를 실행한다. 네트워크 통합 테스트는 명시적으로 선택하여 실행한다. 코드 컴파일 성공은 Host·Client 실행 검증을 대신하지 않는다.

## 이번 작업의 확인 결과

- 신규 스크립트의 `.meta`, 기존 직렬화 필드와 스크립트 GUID 유지, 원본 Player·Enemy 코드 및 몬스터 프리팹 미변경을 확인했다.
- 게임 코드와 전체 프로젝트 의존성을 포함한 컴파일에서 오류 0개였다. 기존 런타임 경고 12개는 유지된다.
- 충전 규칙은 초기 수량, 빈 수량 거부, 순차 충전, 충전 진행 유지, 최대 수량 및 잘못된 경과 시간 거부를 포함한 독립 실행 검증 8개가 통과했다.
- `git diff --check`가 통과했다.
- 전체 빌드를 막던 Core Editor 두 파일의 Application 이름 충돌을 UnityEngine.Application으로 명시하여 수정했다.
- Unity 테스트는 동일 프로젝트를 열고 있는 다른 Unity 인스턴스로 인해 실행되지 않았다. 열려 있는 에디터를 종료하지 않았다. NUnit·PlayMode·Host/Client 실행 통과를 주장하지 않는다.

- 공개 팀 문서 사이트는 Cloudflare Access 로그인 화면으로 연결되어 본문을 확인할 수 없었다. 로컬 Docs와 실제 Core/Enemy 계약을 기준으로 반영했다.

- 플레이어 터렛 재충전 규칙은 독립 실행 검증 17개가 통과했다. 초기 3개, 초과 설치 거부, 정상 회수 즉시 반환, 반피 15초, 파괴 30초, 병렬 충전, 중복 반환 거부, 잘못된 시간·체력 입력을 확인했다. 실행 환경의 구형 C# 컴파일러에 맞추어 검증용 복사본의 프로퍼티·초기화 문법만 변환했다.

## #512 임시포탑 설치 판정 (2026-10-16)

기존 PlayerTurretSkill은 위치 검사 없이 수량을 먼저 차감하고 포탑을 생성했다.
작은 충돌 반경만으로는 큰 바닥 외형의 맵 밖 돌출이나 포탑끼리 겹침을 막을 수 없었다.

`PlayerTurretPlacementRules`는 설치 판정을 전담하는 정적 클래스이며 씬에 붙이지 않는다.

- `FindGround(Scene)`: 해당 씬에서 활성 TilemapCollider2D가 있는 가장 큰 Tilemap을 찾는다.
  Initial_Stage에서는 Initial_Overworld_Base가 선택된다. 더 큰 Water Tilemap은 Collider가 없어 제외된다.
- `TryGetFootprint(GameObject, Vector3, out Bounds)`: 프리팹 루트 SpriteRenderer의 localBounds와
  루트 스케일로 고정 바닥의 월드 XY 영역을 구한다. 실제 BoxCollider2D가 더 넓으면
  해당 영역까지 포함한다. 회전하는 포신은 제외한다.
- `CanPlace(GameObject, Vector3, GameObject, Tilemap)`: 바닥 영역에 0.05 단위 여유를 더하고
  그 영역이 걸치는 모든 지면 타일, 기존 임시포탑 바닥, 일반 장애물 Collider를 검사한다.
  렌더링 정렬용 Z 차이는 포탑 겹침 판정에 사용하지 않는다.

PlayerTurretSkill의 Inspector `Placement Ground`를 지정하면 그 Tilemap을 사용한다.
미지정이면 같은 씬에서 자동으로 찾고 캐시한다. 선택할 지면이 없으면 설치를 거부한다.
자동 선택은 현행 씬 구조를 위한 기본값이다. 다른 맵에서 Collider가 있는 더 큰 배경 레이어를
추가한다면 명시적으로 지면을 지정해야 한다. Core Grid나 터렛 해금 구역을 지형 판정으로 사용하지 않는다.

현재 축에 정렬된 XY Tilemap과 고정된 임시포탑 바닥을 기준으로 한다. 외형을 교체할 때는
루트 SpriteRenderer에 바닥 Sprite를 유지해야 한다. 지면 타일이 있어도 벽 Collider와
겹치면 거부한다. 2026-10-22부터 설치자 자신·다른 플레이어·몬스터의 실제 몸 Collider도
설치를 막는다. 설치자 하위 몸, PlayerInfo를 가진 부모, Player 레이어는 플레이어로,
EnemyHealth를 가진 부모 또는 Enemy 레이어는 몬스터로 분류한다. 자식 Collider의 태그에
의존하지 않으며 캐릭터 점유 판정은 충돌 행렬·excludeLayers와 관계없이 적용한다.
트리거(탄환 피격·공격·탐지·사거리 표시 포함)와 탄환 레이어는 계속 제외한다.
일반 지형은 플레이어 레이어 충돌 설정과 Collider의 excludeLayers를 따른다.
기존 영구 포탑의 일반 본체 Collider는 별도로 차단한다.

2026-10-22부터 발밑 즉시 설치 대신 플레이어 주변 8칸 중 마우스 방향의 칸을 선택하고 왼쪽 클릭으로 확정한다.
플레이어의 정렬 Z를 유지하며 자동으로 몸 밖으로 밀어내던 `spawnOffset`과
`MoveOutsidePlayer`는 제거했다. 플레이어를 가운데 둔 3×3 격자에서 가운데를 제외한
8칸을 사용한다. 마우스 각도를 45도 구간으로 나누며, 커서 거리와 무관하게 인접 한 칸에
고정한다. Inspector의 `Placement Cell Size` 기본값은 4로 포탑 바닥 약 3.77보다 크다.
플레이어가 이동하면 격자 중심도 같이 이동한다. 월드 Tilemap 셀에 고정하는 방식은 아니다.
후보 위치는 플레이어를 중심으로 매 프레임 추적하되 지면·충돌 재검사는 0.05초 간격으로 제한한다.
확정 시에는 `Physics2D.SyncTransforms()`와 전체 판정을 반드시 다시 수행하므로
미리보기 이후 들어온 캐릭터와 겹치는 설치도 차단한다.
다른 포탑은 주변 Collider 쿼리에서 찾으며 매번 전체 포탑 목록을 탐색하지 않는다.

PlayerTurretSkill은 판정 성공 후에만 TryDeploy와 생성을 수행하며, 실패 시 수량과
설치 쿨타임을 사용하지 않는다. 생성한 포탑은 설치 플레이어와 같은 씬에 둔다.
보유 수량·회수·수리 충전 규칙은 기존 PlayerTurretInventory를 그대로 사용한다.

`PlayerTurretPlacementTests`는 Test Runner의 EditMode에서 실행한다. Play Mode 진입 후
별도 테스트 씬에 지면을 만들어 다음을 검사한다.

- 중심은 지면 안이어도 바닥이 맵 밖으로 나가거나 빈 타일에 걸치면 설치 거부.
- 기존 임시포탑과 Z가 달라도 바닥 겹침 거부, 충분히 떨어진 위치 허용.
- 벽·설치자·다른 플레이어·몬스터 몸은 거부하고 트리거·탄환 레이어는 허용.
- 충돌 행렬에서 무시한 캐릭터 몸과 태그 없는 자식 Collider도 점유 판정에 포함.
- 미리보기 진입·취소 시 수량/쿨타임 미사용, 몸과 겹치는 후보는 거부하고 선택 위치에 설치.
- 먼 커서도 인접 칸에만 설치, 8방향/각도 경계/플레이어 이동·정렬 Z 유지, 이중 확정 거부.
- 선택창 진입·비활성화 시 미리보기 취소.
- 플레이어/몬스터와 전역 충돌이 꺼져 있어도 본체 충돌 설정으로 통과 차단, 파괴 시 차단 해제.
- 캐릭터 때문에 설치 실패하면 수량·쿨타임 유지, 캐릭터가 이동한 다음 요청은 성공 가능.
- 설치 실패 후 수량 3/쿨타임 0 유지, 성공 1회 차감, 같은 위치 재설치 거부.

컴파일 성공은 Unity 테스트 실행 통과가 아니다. 실제 Initial_Stage에서 외곽·오목한 경계·
영구 포탑 옆·다른 플레이어가 설치한 임시포탑 옆 설치와 회수 후 재설치를 확인해야 한다.

### 설치 실패 안내

`PlayerTurretPlacementRules.Evaluate`는 `PlayerTurretPlacementResult`를 반환한다.
Allowed / GroundUnavailable / InvalidFootprint / OutsideGround / OverlapsTurret /
BlockedByObstacle / OverlapsPlayer / OverlapsEnemy로 구분한다. 기존 `CanPlace`는
Allowed 여부만 반환하는 편의 API로 유지한다. 새 결과는 enum 끝에 추가해 기존 값은 유지한다.

PlayerTurretSkill은 미리보기가 켜진 동안 최신 결과를 `PlacementFeedback`으로 노출한다.
설치 불가 위치를 클릭해도 미리보기는 유지하고 수량과 쿨타임은 그대로 둔다.
설치 성공 또는 취소·비활성화 시 결과를 즉시 지운다.

PlayerSkillController의 `TurretPlacementFeedback`이 HUD에 결과를 전달하고,
PlayerSkillDescriptions의 `TurretPlacementFailure`가 표시 문구를 결정한다.
PlayerSkillHud는 Canvas 프리팹의 화면 하단 안내 영역에 빨간색 볼드체로 다음을 표시한다.

- 지면 부족: "지면이 부족합니다. 안쪽으로 이동해 주세요."
- 포탑 겹침: "다른 포탑과 겹쳐 설치할 수 없습니다."
- 장애물: "장애물과 겹쳐 설치할 수 없습니다."
- 플레이어 겹침: "플레이어와 겹쳐 설치할 수 없습니다."
- 몬스터 겹침: "몬스터와 겹쳐 설치할 수 없습니다."
- 지면 미설정·외형 오류: "이곳에는 포탑을 설치할 수 없습니다."

표시 중에는 일반 설치 안내보다 실패 문구를 우선한다. 정상 위치에서는
"왼쪽 클릭으로 설치 · 오른쪽 클릭 / Esc로 취소"를 표시한다.
스킬 선택창이 열려 있을 때는 안내를 숨긴다. 수량 부족·충전 상태는 기존 슬롯 표시를 사용한다.

### 반투명 미리보기와 설치된 본체 (2026-10-22)

- `Assets/Prefabs/Player/PlayerTurretPreview.prefab`: 바닥·포신 SpriteRenderer와
  `PlayerTurretPlacementPreview`만 가진 시각 전용 프리팹. 충돌체·발사·체력·적 타깃 등록이 없다.
  위치는 플레이어 주변 8칸 중 마우스 방향을 따라가며 설치 가능하면 초록, 불가능하면 빨강,
  두 경우 모두 반투명이다.
  색·투명도와 Sprite는 Prefab Mode에서 편집한다. 게임플레이 포탑을 복제해 숨기는 방식이 아니다.
- Player 프리팹의 `PlayerTurretSkill`에 미리보기 참조를 미리 연결했다.
  스킬 키로 진입하고, 왼쪽 클릭으로 확정, 우클릭/Esc/같은 스킬 키로 취소한다.
  선택창 열기·다른 스킬 사용·행동 불가·장착 해제·플레이어 비활성화 때도 취소한다.
  UI 위 클릭은 설치에 사용하지 않는다. 설치 확정은 LateUpdate에서 처리하며
  `InitialStagePlayerHUD`는 설치 중 기존 포탑 선택을 막아 같은 클릭의 중복 처리를 피한다.
- `PlayerTurret.prefab`: 고정 바닥을 덮는 BoxCollider2D를 미리 배치했다.
  크기·Offset은 프리팹에서 편집한다. `PlayerTurretHealth`는 Player/Enemy의
  includeLayers와 나머지 레이어 excludeLayers, 우선순위 10을 적용하여 전역 제외 설정보다 우선한다.
  플레이어의 탄환 피격 트리거와 탄환은 본체에 막히지 않는다.
- 기존 체력 100, 손상 충전, 파괴 시 충돌 해제는 유지한다.
  F 회수는 중심이 아닌 Collider 표면까지의 XY 거리 2.5를 사용한다. 큰 본체를 통과하지
  않아도 회수할 수 있으며, 정렬용 Z가 달라져도 회수 거리에 영향을 주지 않는다.
  기존 임시 몬스터 Adapter의 접근점은 Collider.ClosestPoint, 경로 교차는 본체 XY 경계로 맞췄다.
  정렬 Z는 무시한다. 일반 `FieldTurretBase` API 통합과 정식 몬스터 공격 계약 연결은 후속 작업이다.
  이번 변경은 해당 담당자의 원본 Turret/Enemy 코드를 수정하지 않는다.

확인 범위: Runtime/Editor C# 컴파일과 프리팹 참조·구성 정적 검사.
회귀 테스트 코드는 추가했으나 열린 Unity 에디터의 Test Runner/실제 플레이는 실행하지 않았다.
Initial_Stage에서 Q/E → 위치 선택 → 왼쪽 클릭, UI 위 클릭 무시, 취소, 플레이어/슬라임 충돌,
포탑 파괴·회수를 직접 확인해야 한다.

### Canvas 프리팹에서 안내 UI 편집

`Assets/Prefabs/UI/PlayerSkillHUD.prefab`에 Canvas, CanvasScaler, GraphicRaycaster,
PlayerSkillHud와 두 TMP 텍스트를 미리 배치했다. 2026-10-21에는 Q/E 슬롯, 선택창,
툴팁, 포탑 회수 안내도 같은 프리팹에 미리 배치하고 런타임 UI 생성 팩토리를 제거했다.
`Initial_Stage`, `PlayerTest` 씬에 HUD 프리팹 인스턴스를 두고,
`PlayerSkillHudSceneBinding`이 생성된 로컬 플레이어를 연결한다.
필수 슬롯·선택창 참조가 누락되면 오류를 남기고 해당 HUD를 연결하지 않는다.
툴팁·회수/설치 안내·스킬 아이콘은 누락된 기능만 건너뛰고 선택 기능을 유지한다.

- `Placement Failure`: 빨간색(1, 0, 0), Bold, 글자 크기 28. 하단 중앙에서 위로 160,
  영역 1200×60. 프리팹 편집 시 예시 문구가 보이고, 실행 시 Bind가 먼저 숨긴다.
- `Placement Hint`: 기존 방벽 안내용 텍스트. 같은 위치에 배치하고 실패 안내와 번갈아 표시한다.
- Canvas: Screen Space Overlay, 정렬 순서 20. 기준 해상도 1920×1080, 폭/높이 비율 0.5.

PlayerSkillHud는 텍스트 내용을 갱신하고 GameObject의 표시 여부만 바꾼다.
텍스트 위치·색·크기·볼드 설정은 코드로 덮어쓰지 않는다. 두 텍스트의 Raycast Target은
꺼져 있으므로 설치/선택 입력을 가리지 않는다. 슬롯과 선택창도 Bind 과정에서 생성하지 않는다.

PlayerSkillPrefabTests는 Canvas 활성 상태, 직렬화된 텍스트 연결, 빨간색 Bold 설정과
입력 차단 여부를 검사한다. PlayerSkillSelectionTests는 HUD 프리팹의 아이콘 연결도 확인한다.
프리팹 편집 후 실제 실행에서 해상도별 위치·문구 표시와 스킬 입력을 추가 확인해야 한다.

씬 연결 조건과 편집 방법은 [스킬 HUD 연결 API](player-skill-hud-api.md)를 참고한다.
## #513 폭탄 피해 반경과 폭발 외형 일치 (2026-10-16)

PlayerBombExplosion의 Radius와 bomb.prefab의 `_radius`를 9로 맞췄다.
현재 비콘 반경 12보다 작으며 폭탄 지름은 18, 비콘 최대 지름은 24다.
피해량 250, 대기 2.1초, 이펙트 재생 0.8초와 폭탄 본체 스케일 2.5는 유지한다.

기존 PlayerTimeBombVisual.PlayExplosion은 전달받은 radius를 이펙트 크기에 사용하지 않았다.
기존 클립의 최대 외곽은 스케일 1에서 약 반경 5.76이고, 부모 스케일 2.5가 곱해져 약
반경 14.4까지 보였지만 실제 피해 반경은 6이었다.

PlayerTimeBombVisual의 MatchExplosionRadius가 폭발 자식 Transform의 XY 스케일만 계산한다.

`이펙트 로컬 스케일 = (피해 반경 × 2) / (기준 외형 지름 × 부모 월드 스케일)`

- PlayerBombExplosion: 피해 반경을 단일 기준으로 시각 효과와 PlayerBombAreaDamage에 전달.
- PlayerTimeBombVisual: 기존 애니메이션 재생·타이머 표시와 최대 폭발 외형 크기 동기화.
- bomb.prefab: 실제 반경과 외형 기준값을 직렬화. 기존 Legacy Explosion 자식과 클립을 유지.

`Explosion Diameter At Scale One`은 11.521이다. 현재 effect.anim의 11개 Sprite(20 PPU)에 대해
투명 여백을 제외하고, 알파 10/255 초과 픽셀의 중심에서 Sprite 피벗까지 최대 거리와
픽셀 반대각선을 포함해 구한 최대 외곽 지름이다. 이미지 전체 사각형 크기로 계산하면
투명 여백 때문에 시각적 크기가 달라지므로 이 측정값을 사용한다. 클립 이미지·PPU·피벗을
교체할 때는 이 기준값도 다시 측정한다. Radius만 조정할 때는 별도 크기 조정이 필요 없다.

기존 불규칙한 폭발 그림과 확장·소멸 모션은 그대로다. 애니메이션 내내 원형 범위를
채우는 것이 아니라 가장 크게 퍼진 프레임의 외곽이 피해 반경에 맞는다.
사전 반경 표시나 새 범위 링은 추가하지 않는다. 실제 피해는 기존대로 폭발 시점 1회이며,
반경 안에 적 Collider가 걸치면 해당 적에 피해를 적용한다.

PlayerBombRuntimeTests에 본체 스케일 0.5/2.5/5 및 비균일 XY 스케일, 반경 6/9/10에서도
최대 이펙트 지름이 피해 지름과 같고 본체 스케일은 유지되는지 확인하는 회귀 테스트를 추가했다.
컴파일과 테스트 실행은 구분하며, Initial_Stage에서 비콘 최대 범위와 실제 폭발 최대 외곽도 비교한다.
