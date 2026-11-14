# Enemy 테스트 씬 사용법

`Assets/Scenes/Tests/EnemyTest.unity`에서 몬스터 편성 요청부터 실제 생성까지 확인하고, 개별 몬스터·타깃·전투 상태를 관찰하는 방법이다. 이 씬은 테스트용이며 운영 씬의 연결부가 아니다.

## 준비

1. Unity에서 `Assets/Scenes/Tests/EnemyTest.unity`를 연다.
2. `Tools > TeamHJD > The Developer > Enemy Test`를 열고 `Encounter 연결`에 씬의 `EncounterSceneBinding`이 지정됐는지 확인한다. 실행기는 이 Binding에 연결된 `Encounter Debug Spawner`를 사용한다.
3. Play Mode에 들어가면 씬의 Core `InitialStageNetworkBinding`이 `SinglePlayerHost` 진입으로 네트워크 연결·Match 준비·시작을 진행한다. 테스트 창에서 Host/Client를 따로 시작하지 않는다. Match와 실행기 상태가 표시되면 연결·카탈로그 새로고침을 누른다. 시험 조건을 직접 구성할 수 있도록 이 씬의 Core 반복 편성 생성은 꺼 두었다.

씬에는 `EnemyTestAreas` 아래 `01_Turret`과 `02_Player` 구역이 있다. 두 구역 모두 SpawnPoint와 체력 1000인 시험 표적을 포함한다. Scene 뷰에서 보기 어렵다면 해당 구역을 선택하고 `선택 구역을 Scene 뷰에서 보기`를 누른다.

## 편성 요청

- **지정 편성 요청**: `지정 편성`에서 Squad Order를 고르고 요청당 상한을 설정한 뒤 누른다. 해당 편성으로 기존 Core 요청부터 Runtime·Planner·Executor까지 이어지는 경로를 호출한다.
- **현재 상황으로 편성 선택·요청**: 같은 상한을 사용하되 최신 전장 상황에 따라 편성을 고르는 경로를 호출한다.
- 창 결과의 `계획 수 / 실제 생성 수`와 거부 사유를 확인한다. 요청 상한은 정확한 생성 수가 아니다. 지역 예산, 완전한 편성, Grid 셀 수용량에 따라 상한보다 적게 생성될 수 있다.
- 상황별 선택의 타깃·주변 병력·가중치 기록은 Game Debugger와 Console에서 확인한다.

## 개별 몬스터 시험

`개별 몬스터 기능 시험`에서 카탈로그 등록 몬스터와 구역을 선택하고 `지정 지점에 1마리 생성·관찰`을 누른다.

- `01_Turret`은 `TargetType.Turret` 시험 표적을 사용한다.
- `02_Player`는 `TargetType.Player` 시험 표적을 사용한다. PlayerOnly 몬스터가 이 표적을 타깃으로 삼는지 확인할 때 선택한다.
- `출현 지점`과 `초기 타깃`은 직접 지정할 수 있다. 초기 타깃을 비우면 분대의 타깃 탐색에 맡긴다.
- `타깃 규칙`은 프리셋을 거치지 않는 개별 시험의 명시 설정이다. 암살자 시험은 `PlayerOnly`, 시설 전용 시험은 `TurretOnly`, 일반 시험은 `PlayerOrTurret`를 선택한다. `PlayerOrTurret`의 자동 검색은 시설만 대상으로 하며 감지한 플레이어로 전환할 수 있다. 지정한 초기 타깃이 `PlayerOnly`·`TurretOnly`의 허용 유형과 다르면 생성 전에 거절한다. 정규 편성 요청은 이 선택란 대신 해당 프리셋의 규칙을 사용한다.
- 이 경로는 편성 선택과 예산 계산을 건너뛰고 몬스터 한 마리를 요청한다. 실제 Encounter 편성 흐름은 위의 두 편성 요청 버튼으로 따로 확인한다.

시험 구역은 벽으로 분리되어 있지 않다. 실제 터렛·플레이어·다른 몬스터도 같은 씬에 있으므로 주변 타깃 정책과 전투에 영향을 줄 수 있다.

## 관찰과 피해 확인

- **시험 표적**: `관찰·초기화할 표적`을 지정하거나 Hierarchy에서 표적을 선택한다. 체력, 피격 횟수, 최근 피해, 누적 피해를 본다. 선택 표적 또는 실행기 씬의 모든 시험 표적을 초기화할 수 있다. 실제 터렛·CU·플레이어 체력은 변경하지 않는다.
- **몬스터 관찰**: Hierarchy에서 몬스터 또는 자식 `HitBox`를 선택한다. 체력, 네트워크 생성 여부, 타깃, 타깃 가능 여부, 배정 인원, 분대 상태, 경로와 속력, 능력 실행 상태, 탐지 상태를 읽어 표시한다. 창은 0.5초마다 상태를 읽으며 능력 조건 검사나 게임 행동을 호출하지 않는다.
- **선택 몬스터에 피해 적용**: 피해량과 공격자 종류를 고르고 피해 적용을 누른다. `현재 체력만큼 피해 · 사망 시험`은 실제 피해 API로 현재 체력만큼 피해를 요청한다. 탐지·피격 규칙이 거부할 수 있고, 허용되면 실제 사망과 소환 동작을 일으킨다.
- **시험 씬 몬스터 전체 정리 · 소환물 포함**: 실행기와 같은 씬·같은 서버의 생성 몬스터를 즉시 Despawn한다. 죽음 효과는 실행하지 않는다. 버튼을 누르기 전에 다른 반복 스폰 도구도 중지한다. 씬에 직접 배치된 적이나 실제 플레이어·터렛·CU는 제거하지 않는다.

## 시험 표적의 의미와 제한

Player 구역의 박스는 `EnemyDamageTestTarget`으로 만든 정지 더미다. `Target Type`은 박스의 타깃 종류를 정하고, `PlayerFeetBox` 레이어는 Physics 2D의 레이어 충돌·쿼리 구분에 사용된다. `TargetableComponent`의 유형만 바꾸거나 GameObject 레이어만 바꾸지 말고 시험 표적의 `Target Type`을 사용한다. Play Mode에서 값을 바꿨다면 표적 초기화를 눌러 적용한다.

이 더미는 플레이어의 조작, 실제 플레이어 네트워크 상태, 암살자 감지 반경 진입·이탈 처리를 대체하지 않는다. 따라서 Player 더미를 공격하는 시험만으로 실제 플레이어가 암살자에게 감지되는지는 확인할 수 없다. 플레이어 측 감지 연결은 별도 확인이 필요해 현재 시험 씬의 완료 범위에서 제외되어 있다.

## 씬 구역 제작 메뉴

`Tools > TeamHJD > The Developer > Configure Enemy Test Areas`는 EnemyTest의 Edit Mode에서만 실행한다. 현재 씬에 구역 Root가 이미 있으면 기존 구역을 선택할 뿐이며 값을 다시 만들거나 덮어쓰지 않는다. 따라서 현재 저장된 `01_Turret`·`02_Player` 구역은 별도 제작 메뉴 실행 없이 바로 사용할 수 있다. 신규 구역을 만들 때는 기존 NavMesh에서 출현 지점과 표적 위치, 두 지점 사이의 완전한 경로를 먼저 검사한다. 제작 후 씬 저장은 사용자가 수행한다.

## 현재 완료 범위

테스트 창과 두 시험 구역, 편성 요청, 개별 생성, 상태 관찰, 피해 적용, 씬 몬스터 정리 기능은 준비되어 있다. 실제 플레이어의 감지 콜라이더·감지 이벤트와 연결하는 암살자 감지 확인은 플레이어 측 구현 확인이 끝날 때까지 보류한다. Host/Client 및 전투 시나리오의 최종 플레이 검증은 사용자가 Play Mode에서 수행한다.

## 관련 코드

- `Assets/Scripts/Features/Enemy/Editor/EnemyTestWindow.cs`
- `Assets/Scripts/Features/Enemy/Editor/EnemyTestWindow.Spawning.cs`
- `Assets/Scripts/Features/Enemy/Editor/EnemyTestWindow.Inspection.cs`
- `Assets/Scripts/Features/Enemy/Testing/EnemyTestArea.cs`
- `Assets/Scripts/Features/Enemy/Editor/EnemyTestSceneTools.cs`
- `Assets/Scripts/Features/Enemy/Testing/EnemySpawnExecutor.Testing.cs`

## 테스트 도구와 운영 연결의 경계

`EnemyTestSceneTools`는 Editor 전용 스크립트이며 `EnemyTest` 경로의 실행기만 조작한다. 개별 생성·피해·전체 정리 기능을 운영용 `EncounterSceneBinding`의 partial에 추가하지 않는다. 상황별 편성 요청은 Core가 확정한 Host 참가자로 기존 `Submit()` API를 호출한다. 개별 생성은 Core 조립부의 현재 전장·환경 Snapshot과 Match 종료 토큰을 사용한다. 전체 정리는 현재 생성된 몬스터를 제거하며 다른 도구의 예약·반복 요청을 취소하지 않는다.

운영용 `EncounterSceneBinding`과 기존 `EnemyNetworkTestLauncher` 및 다른 씬은 `main` 구성을 유지한다. EnemyTest의 `EncounterSceneConfiguration`이 기존 `ConfigureScene()` API로 시험 실행기를 연결한다. Core 스크립트는 수정하지 않고 EnemyTest의 참조와 진입 설정만 구성한다.
