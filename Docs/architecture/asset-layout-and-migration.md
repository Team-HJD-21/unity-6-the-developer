# Assets 구조와 Legacy 교체 기준

수정일: 2026-10-09

관련 Issue: #441

기준: main `fa8407a4` (#491 Core, #497 Turret, #498 Player 포함). [실행 결과](../planning/2026-10-09-asset-migration-results.md)

## 현재 구조

Unity가 강제하는 유일한 표준이 아니라 **종류별 최상위 + 기능별 하위 폴더**라는 팀 표준이다. 물리 경로와 assembly 의존성은 별개다. 이번에는 namespace, 클래스명, asmdef 이름과 참조를 유지한다.

```text
Assets/
├─ Scripts/
│  ├─ Core/                     # App/Match, 공간 계산·계약, Editor, Tests
│  ├─ Features/
│  │  ├─ Player/                # 이동·무기·스킬·Tests
│  │  ├─ Enemy/                 # Enemy, Encounter, Spawn, Editor
│  │  ├─ Turrets/               # 터렛·CU·Editor
│  │  └─ Spaceship/             # 허브·SceneFlow
│  ├─ Shared/Input/
│  └─ UI/Turrets/
├─ Data/{Core,Enemies,Turrets,Spaceship}/
├─ Prefabs/{Player,Enemies,Turrets,World,UI}/
├─ Scenes/{Gameplay,Spaceship,Tests,Reference}/
├─ Art/{Sprites,Animations,Materials,Atlases,Fonts,UI,VFX,Reference}/
├─ Audio/{Music,Sfx,Mixers}/
├─ Settings/Rendering/RenderTextures/
├─ Resources/Spawning/
├─ ThirdParty/                 # 패키지·라이선스 단위
├─ TextMesh Pro/               # 지정 구조 유지
├─ WebGLTemplates/             # Unity 지정 위치
├─ DefaultNetworkPrefabs.asset # NGO 설정 참조 보존 예외
└─ UserChoices.choices         # 도구 저장 위치 예외
```

`Content/Authoring`에는 ScriptableObject **타입 코드**, `Data`에는 제작한 **실제 .asset**을 둔다. `Settings`는 엔진 설정, `Data/Core`는 Grid처럼 게임 실행 정의값이다. USS는 해당 Editor 코드 옆에 둔다. 외부 패키지 내부 폴더는 임의 재분류하지 않는다.

## 이전 위치 대응

| 이전 | 현재 | 처리 |
| --- | --- | --- |
| `PoC/Core` | `Scripts/Core` | Core 전체·asmdef·Editor·Tests·README |
| `Po김진태·조수빈nemy/Scripts`, `Editor` | `Scripts/Features/Enemy` | 현행 Enemy/Encounter/Spawn |
| `Scripts/Player_V2` | `Scripts/Features/Player` | Player·무기·스킬. Test → Tests |
| `Scripts/Tower/Turret` 및 Tower의 나머지 코드 | `Scripts/Features/Turrets` | CU·전력·상태·업그레이드·연동 |
| `PoC/Spaceship/Scripts` | `Scripts/Features/Spaceship` | MutiScene → SceneFlow |
| `PoC/Spaceship/Scenes/Initial_Stage.unity` | `Scenes/Gameplay/Initial_Stage.unity` | 현행 통합 Scene |
| `PoC/Spaceship/Scenes/SpaceShipGrayBox.unity` | `Scenes/Spaceship/SpaceShipGrayBox.unity` | 허브 |
| `Po김진태·조수빈nemy/Scenes/EnemySandbox.unity` | `Scenes/Tests/EnemySandbox.unity` | Enemy 테스트 |
| `PoC/Tower` | `Prefabs/Turrets` | **PL이 지정한 현행 터렛 정본** |
| `Prefabs/Tower` | 제거 | 정본 GUID/fileID 검사·재연결 후 제거 |
| `Po김진태·조수빈nemy/Prefabs` | `Prefabs/Enemies/{Runtime,Spawning}` | 신형 Enemy·Spawn |
| `Scripts/Tower/TurretDefinitions` | `Data/Turrets` | 정의값·Catalog |
| `Po김진태·조수빈nemy/Data` | `Data/Enemies` | EnemyCatalog·정의값 |
| Grid 설정 asset | `Data/Core/BattlefieldGridSettings_Default.asset` | 사용자 설정값·GUID 보존 |
| `Po김진태·조수빈nemy/Resources/Spawning` | `Resources/Spawning` | Resources 키 유지 |
| 구형 Main·Stage·UI Demo | `Scenes/Reference` | 배치·화면 참고. 이전 실행 기능 보장 안 함 |
| 구형 Monster·Spawner prefab | `Prefabs/Enemies/{Reference,ReferenceSpawning}` | 시각 참고. 제거된 Legacy 동작 없음 |

터렛 내부의 기존 종류·레벨 구분(`Turrets/CanonTurret` 등)과 타입명은 유지한다. 새로운 터렛 생성/관리 계약은 황재동과 연계하는 #495의 별도 범위다.

## 제거 기능과 대체 상태

59개 구형 C#을 제거한다. 원본은 이관 전 Git 커밋과 보존 브랜치에서 조회한다. **제거 완료가 기존 기능의 완전 이식을 뜻하지 않는다.**

| 이전 책임 | 이번 처리·현재 경계 | 아직 대체 완료가 아닌 것 |
| --- | --- | --- |
| GlobalBootstrap·Global/Game/EventBus | 자동 Legacy 진입 제거, AppBootstrap/AppRoot/AppServices 유지 | 네트워크 Match (#442) |
| GeneralManager/GameManager/SceneController | App·Match와 Scene composition | 구형 Stage UI 흐름 전체 |
| InGameManager/StageController | 구형 Wave·대화·승패·보상 호출 제거 | 정식 승패·결과/보상 루프 |
| DataManager/ShopManager | Player authoring HP(기본 400), 현행 터렛 Catalog | 계정 성장·구매·재화 영속성/백엔드 |
| Monster/MonsterSpawner | EnemyHealth·AI·Encounter/Spawn 보존 | 구형 보스·공격의 일대일 이식 |
| Audio/Setting/Cursor Manager | 컴포넌트·호출 제거/주석 | 신규 Audio·옵션·커서 |
| 구형 HP/미니맵/CU HUD | 컴포넌트·UnityEvent 제거 | 새 HUD 전체 |

보존 코드에 `Legacy 제거로 인해 로직 무효화: 이유`를 표시한다. EnemyHealth 피해·Player 스킬·터렛 활성화·충돌은 함께 끄지 않는다. `MonsterSlowDebuff.cs`는 구형 전용 참고 소스로 보존하되 `#if false`로 컴파일하지 않는다. 신형 Enemy의 Cryo 감속은 이번에 새로 구현하지 않는다.

Scene/Prefab에서 구형 MonoBehaviour와 해당 PersistentCall을 먼저 정리한 뒤 C#을 삭제한다. 터렛은 GUID와 참조 fileID 검사 후 재연결한다. 동일 이름만 보고 덮어쓰지 않는다.

## 실행·팀원 영향

1. `Assets/Scenes/Gameplay/Initial_Stage.unity`를 연다.
2. Play Mode에서 AppRoot/Player, 터렛 topology/Grid, 자동 Encounter와 Debugger 요청을 확인한다.
3. 도구 메뉴는 그대로 `Tools > TeamHJD > Game Debugger`다. [사용법](../tooling/game-debugger-guide.md)

활성 Build Scene은 Initial_Stage → SpaceShipGrayBox다. EnemySandbox는 등록하되 비활성이다. 과거 Main부터 시작하던 Build와 참고 Scene은 현행 진입점이 아니다. 완성된 제품 진입 화면이나 Windows Build 검증을 의미하지 않는다.

재진입 검사에서 발견한 이전 NGO 루트 잔존을 보완했다. InitialStageCompositionRoot의 Encounter 소비자를 Scene에서 명시적으로 연결하고, `EnemyNetworkTestLauncher.BindTestHostToExecutorScene()`이 **임시 테스트 Host만** Executor Scene에 귀속시킨다. Scene 종료 시 SDK가 이 Host를 정리하고 새 Scene은 새 Host로 스폰한다. AppRoot는 계속 App 수명을 유지한다. 정식 App 네트워크 연결/Match 전환 정책은 #442에서 별도로 설계하며 이번 임시 정책을 그대로 멀티플레이 요구로 확정하지 않는다.

팀원은 로컬 도구·테스트의 문자열 경로를 새 위치에 맞춘다. GUID 기반 serialized 참조는 유지하므로 Prefab/Scene을 임의 재연결하지 않는다. Player 폴더 migration은 자동 실행을 제거하고 수동 메뉴만 유지한다. 오래된 브랜치가 구형 폴더를 다시 만들지 않도록 이관 변경을 먼저 받아야 한다.

## 이후 이동 규칙

- Unity Project 창/AssetDatabase.MoveAsset 사용, `.meta` 보존.
- Resources 키·Editor 경로·USS·테스트·Build Settings·현행 문서를 함께 확인.
- GUID와 sub-asset fileID는 별개. Prefab 교체 시 둘 다 검사.
- 개인 Docs/Local·YGDocs·Validation을 팀 커밋에 포함하지 않음.
- 일회성 삭제/이동 도구를 런타임 프로젝트에 남기지 않음.
