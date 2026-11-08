> 보관 문서 · 2026-11-05 교체 전 스냅샷
> 기준 커밋: `1a03e9a9164e5db94cbc8775162467d08cf63a3b`. 당시 설명·검증 근거이며 현행 실행 지시가 아니다.

# 2026-10-09 Assets 이관 결과

상태: 구현·최종 CLI 검증 완료, PR 제출 준비. Windows Build 미수행
관련: #441 / [폴더·Legacy 대응](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/architecture/asset-layout-and-migration.md)

## 범위

- main `fa8407a4` 기준. 이전 feature 이력은 `codex/backup-feature-core-before-assets-restructure-20261009`에 보존.
- 구형 C# 59개 제거. Scene/Prefab 45개에서 컴포넌트 170개·PersistentCall 40개 제거.
- 구형 Tower 에셋 25개 참조를 PoC 정본으로 재연결하고 Prefabs/Tower 제거. serialized 파일 34개 변경.
- 보존 코드의 Legacy 의존 분기를 표시·주석 처리. Player HP 초기화·테스트 수정, Player migration 자동 실행 제거.
- data/scripts/content/art 이동 94개 그룹과 마지막 터렛 애니메이션 3개 이관. namespace·asmdef·클래스명 유지. 경로 문자열·Build Settings 갱신.
- 개인 원시 자료와 Validation은 커밋에서 제외.

## 배치

| 배치 | 결과 |
| --- | --- |
| B0 동기화·원본/GUID 보존 | 완료 |
| B1 Legacy 의존 분리 | 완료 |
| B2 Scene/Prefab 정리·구형 코드/터렛 제거 | 완료 |
| B3 코드/데이터 이관 | 완료 |
| B4 Scene/Prefab·Build 경로 | 완료 |
| B5 Art/Audio/ThirdParty·빈 폴더 | 완료. 보존 파일 15,506개 GUID 일치 |
| B6 컴파일·EditMode/실제 Play Mode·문서 | 완료. 실패 보완 후 집중 8/8 성공 |

## 제한

#442 네트워크 Match, #495 터렛 생성/관리 계약, #496 공식 Encounter API는 별도다. 구형 승패·성장·Audio/HUD는 자동 대체되지 않는다. 신형 Enemy의 Cryo 감속도 이번 구현이 아니다. Reference Scene은 제작 참고이며 이전 게임 진행의 실행판이 아니다.

## 검증 기록

Unity 6000.3.23f1의 CLI batch Editor에서 실행했다. 단순 C# 정적 확인이 아니라 실제 Initial_Stage에 EnterPlayMode/ExitPlayMode하는 테스트를 포함한다.

| 검사 | 결과·증거 |
| --- | --- |
| 원본 파일/GUID 전체 | 보존 대상 15,506/15,506 일치. 원본 15,590개에서 Legacy C# 59개·구형 Tower 에셋 25개 제외 |
| 제거 GUID의 serialized 참조 | Scene/Prefab/Asset/meta/Animator/controller 검색 결과 0 |
| Unity meta CI 검사 | `.github/scripts/validate_meta.py Assets`: 0 오류·Git 누락 빈 폴더 0 |
| 전체 EditMode | 208개: 201 성공·3 실패·Explicit 4 제외. 아래 보완 후 실패 항목 모두 재검사 |
| 첫 이관 집중 재검사 | 7개: 6 성공·1 실패. 자동/수동 스폰·재진입 성공, App 파괴의 테스트 대기 방식 보완 |
| 최종 집중 검사 | 이관 7개 + 실제 Enemy 이동 1개 = **8/8 성공**, 제외/실패 0 |
| PR 제출 전 최종 전체 EditMode | **209개: 205 성공·실패 0·Explicit 4 제외** (`final-editmode.xml`). 일반 Player assembly 경계 검사 포함 |
| meta validator 자체 회귀 | 9/9 성공. 프로젝트 meta 검사 오류 0 |

원시 XML·Editor 로그는 Git ignore인 `Docs/Local/Validation/AssetMigration/`에 있다. `editmode.xml`, `migration-regression.xml`, `runtime-regression.xml`, `guid-verification.txt`를 보존한다. 전체 테스트를 반복 실행하지 않고 실패 및 실제 Scene 영향 부위만 재검사했다.

PR 제출 전 #441의 Player 빌드 경계 검사를 추가하여 전체 검사를 확정했다. `AssembliesType.Player`는 테스트용 Player assembly도 포함하므로 초기 검사에서 Turret 테스트 assembly를 배포 코드로 오인했다. 구현/asmdef를 바꾸지 않고 일반 배포 목록인 `PlayerWithoutTestAssemblies`로 수정해 최종 전체 검사가 통과했다. 프로젝트 Editor/Tests 소스와 UnityEditor DLL이 일반 Player graph에 없는 것을 검사했다. 실제 Windows Build 성공을 의미하지 않는다. [Unity 공식 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Compilation.AssembliesType.html)

### 발견한 문제와 보완

- Edit Mode SendMessage(Awake)의 엔진 assertion: 메서드 초기화 검사를 reflection 호출로 변경. 실제 Player Awake는 Play Mode에서 별도 확인.
- 빈 SceneSetup 복원 오류: 유효한 복원 구성이 없으면 빈 Scene으로 복원.
- 재진입 시 복수 Encounter 소비자: Scene의 소비자 명시 연결 및 임시 NGO Host의 Executor Scene 수명 귀속.
- Destroy 요청 직후 Dispose assertion: Unity의 실제 파괴 완료까지 제한 시간 안에서 기다리고 `_isDisposed`를 확인. OnDestroy를 직접 호출해 결과를 만들지 않음.

### 실제 검증 범위

- 현재 Scene 3개에 Missing Script 없음. Player authoring HP와 기본 HP 400 유지.
- 터렛 Upgrade Catalog의 prefab가 현행 정본 GUID를 사용하고 Resources Squad Catalog 로드 성공.
- AppRoot 단일 생성, Legacy Manager 부재, Player 1개, 터렛·삼각형·외곽 변·Grid 점유 확인.
- Initial_Stage 자동 Enemy 10개 생성 및 DebugWindow 요청 성공. 재진입 후 새 Match/Host·Player 단일 생성과 자동 재스폰 성공.
- Spaceship Additive 로드/언로드 중 Match 유지. Initial_Stage 언로드 후 Match 해제, AppRoot 실제 파괴 후 Dispose 확인.
- Enemy 10개 모두 이동. 타깃 제거 후 이동 0개, Player만 복원하면 10개 이동. 실제 HP가 타깃 상태에 반영되고 사망 Player 제외.

실행한 시나리오에 실패로 처리되는 Runtime Exception/Error는 없다. **모든 상황·모든 기획 기능이 검증됐다는 뜻은 아니다.** 공식 승패·보상·성장, 실제 다중 Client·Network 전환, Windows Build, 마우스 조작/영상·도킹·모든 Art의 시각 인수는 이번 검증 밖이다. Explicit 반복 분산 Spawn·별도 자동 Spawn 테스트·Player Beacon 네트워크 테스트는 이번에 재실행하지 않았다(자동 Spawn 자체는 신규 회귀 검사에서 확인).

### 남은 경고

- ContentCatalog 실체가 없는 경우 기존 Fake catalog fallback 경고가 유지된다. 제작 Catalog/운영 서비스가 완성됐다는 의미가 아니다.
- Scene 재진입 중 터렛 Instance ID 충돌 시 Registry가 새 런타임 ID를 배정하는 경고를 관측했다. 새 Scene의 topology와 스폰은 성공했다.
- NGO 테스트 Host의 Scene 파괴 순서에서 SDK UnityTransport 종료 경고를 관측했다. 우리 코드가 UnityTransport.Shutdown을 직접 호출하는 것은 아니다. 재진입 후 NetworkManager 1개·재스폰은 검증했으나, 메시지를 drain하는 정식 네트워크 종료/전환 정책은 #442에서 다룬다. 경고를 숨기거나 SDK를 변경하지 않았다.

## 커밋 구분

1. Legacy 분리·제거, GUID 보존 Assets 이관, Build/경로 갱신을 함께 기록한다. serialized 참조가 이동 에셋과 엇갈리지 않도록 구조 변경은 원자적으로 묶는다.
2. 검사에서 드러난 임시 Host 재진입 수명 보완과 신규 이관 회귀 테스트를 기록한다.
3. 공유 구조·Legacy 기능 제한·검증 기록 및 기존 가이드를 갱신한다.

기록한 커밋: `feb1a539` 구조/Legacy 이관, `6702000d` 임시 Host 수명/회귀 검사. 문서 갱신은 그 다음 독립 커밋으로 기록한다. 원격 push/PR은 하지 않았다. 검증 결과는 이 커밋들을 모두 적용한 최종 작업 트리 기준이며 중간 커밋 각각의 독립 실행 보증이 아니다.

PR 제출 시 추가: 최종 Player assembly 검사를 별도 커밋으로 기록한다. 원격 feature/core의 이전 tip `17fa8334`와 main의 #491 squash 커밋 `b4dd215e`는 파일 tree가 동일함을 확인했다. 이 이미 반영된 이력은 파일 변경 없는 history-only merge로 연결하여 원격 브랜치를 강제로 덮어쓰지 않고 feature/core에서 PR을 제출한다.
