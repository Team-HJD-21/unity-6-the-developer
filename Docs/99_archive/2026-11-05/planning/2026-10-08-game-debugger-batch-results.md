> 보관 문서 · 2026-11-05 교체 전 스냅샷
> 기준 커밋: `1a03e9a9164e5db94cbc8775162467d08cf63a3b`. 당시 설명·검증 근거이며 현행 실행 지시가 아니다.

# Game Debugger 확장 배치 결과

기준일: 2026-10-08 · 브랜치: `feature/core` · 기준 HEAD: `06fd1090`

[배치 계획](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/planning/2026-10-08-game-debugger-batch-plan.md) · [사용/API 가이드](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/tooling/game-debugger-guide.md)

## 구현 범위

| 배치 | 결과 |
| --- | --- |
| G0 | source/clock/Match/revision/request·실제 생성/제거/실패 계약, Scene 공통 snapshot 요청 |
| G1 | UI Toolkit 상위 창·USS·6개 기능. 독립 Battlefield 창 제거. 단일/좌우·상하 2분할/4분할, 영역별 상태, 크기 조절과 빠른 배치 추가. Scene Overlay 유지 |
| G2 | 배속/직접 입력/Pause/Resume/Step/Restore, 소유한 시간 복원. fixed step 자동 변경 없음 |
| G3 | authority/clock·수집 상태·터렛 HP/작동/전력·기하 지표, ID/OOB/소스/타깃 존재 검사 |
| G4 | 성공 Spawn/실제 Despawn 이벤트, 요청별 planned/actual, bounded buffer/recorder·archive, 네 source |
| G5 | 게임/실제 clock bin, 요청·생성·사망 선 그래프와 기타 metric, source/preset/type/point 필터·A/B. 요청별 공간 판단 Inspector |
| G6 | 타임라인·검색·북마크·터렛 선택·실패/파괴/OOB 정지·재무장 |
| G7 | JSON/CSV·schema 검증·readonly import/compare·metadata·Capture preset, reload session 기록 보존 |
| G8 | CLI 컴파일·집계/창 구성 테스트·Initial_Stage 실제 Host/스폰/topology 연결 검증. **시각 QA/물리 충돌/영상은 미실행** |

구현의 복구 경계는 ① Runtime 계약/요청·관측 ② 수집/시간 scope/집계 ③ 창/차트/패널·Overlay ④ 테스트·문서다. 같은 순서로 적용한다. 게임 정책·UnityEditor를 Core Domain에 추가하지 않았다. Scene YAML/프리팹·팀원 터렛 정책은 수정하지 않았다. `Validation/`의 기존 파일은 건드리지 않는다. 원격 push/Issue 변경/PR은 이 배치에서 하지 않는다.

| 복구 경계 | 로컬 커밋 |
| --- | --- |
| Runtime 요청·진단 계약 | `484dd038` |
| 수집·집계·Export·시간 제어 | `84fffb43` |
| 상위 창·차트·Timeline·Compare·Overlay | `ff01a51c` |
| 테스트·문서·인수 기록 | 이 문서를 포함한 후속 커밋 |

## 코드 읽는 순서

1. `Contracts/IGameDiagnosticsSource`, `GameDiagnosticEvent`, `EncounterExecutionContext` — Feature와 Editor 경계.
2. `EnemyNetworkTestLauncher.TryRequestCurrentEncounter/TryRequestEncounter/Emit` — 네 요청의 source와 Match 수명.
3. `EncounterRuntime.TrySpawn → EnemySpawnExecutor.Execute/Observe/UntrackEnemy` — 정책 유지, 실제 사실 기록.
4. `GameDebugger/Recording/GameDebuggerSession.Update/Accept/Sample/SaveAndDetach` — 이벤트/표본/종료 기록.
5. `GameDebugRecorder.Accept`, `GameDebugStatistics.Build`, `GameDebugChart.SetData/DrawGUI` — 중복/손실·집계·그림.
6. `GameSimulationControls`, `TimeScaleLease` — 게임 서비스와 분리된 Editor 시간 소유권.
7. `GameDebuggerWindow.CreateGUI/RebuildPanes/SetLayout`, `GameDebuggerPane.SelectPage/BuildPage/RefreshView`, `GameDebuggerPaneState` — 배치·영역별 UI 상태. Inspection panels, `GameDebugExport` — 사용·보고 경로.

## CLI 검증 증거

Unity `6000.3.23f1` / Unity CLI / 2026-10-08. 결과 XML은 로컬 `%TEMP%`이며 Git에 포함하지 않는다.

| 검사 | 결과 | 파일 |
| --- | --- | --- |
| 전체 EditMode suite | 37개: **36 성공, 실패 0, Explicit 1 제외** | `game-debugger-final-editmode.xml` |
| 마지막 보강 후 GameDebuggerTests | **10/10 성공** | `game-debugger-final-unit.xml` |
| 명시적 Initial_Stage 통합 | **1/1 성공** | `game-debugger-final-integration.xml` |

실제 Scene 통합에서는 자동 첫 Host/Spawn 및 `SceneCollision` adapter, `RuntimeUI`, `DebugWindow`의 같은 경계에서 Normal/10 요청이 각각 성공했다. 당시 snapshot은 **삼각형 8개·외곽 6개**였다. 빈 snapshot/다른 Match 요청 거부, 실제 NetworkObject Despawn의 제거 사실, 창 없이 수집, 무장한 실패의 로컬 Pause, 정상 종료 마커·종료/reload 후 archive 보존을 확인했다. 이것은 전체 전투/승패를 인증하지 않는다.

초기 추가 테스트에 잘못된 `MonsterBase` 타입 참조가 있어 컴파일 오류를 발견하고 실제 `EnemyController`로 수정했다. 최종 검사에는 해당 오류가 없다. 전체 suite를 배치마다 반복하지 않고 순수 테스트와 명시적 Scene 검사를 좁게 수행했다.

## 마지막 사람이 확인할 화면

### 독립 Battlefield 창 제거 후속 정리

기존 메뉴 호환·UI 재사용 외에 독립 창의 고유 기능은 없으므로 제거했다. `BattlefieldDebugWindow.cs`의 기능을 `GameDebugger/Panels/BattlefieldDebugPanel.cs`의 일반 C# 패널로 이관했다. 기존 EditorWindow GUID를 재사용하면 저장된 native 창이 일반 클래스로 재로드되어 `ExtensionOfNativeClass` 경고가 발생하므로 내부 패널에는 새 GUID를 부여했다. Grid/Scene 설치·Encounter 명령은 유지한다. 메뉴, Legacy window 버튼, Overlay의 구 창 링크와 숨은 EditorWindow 생성은 삭제했다. 기존 창 종료 테스트는 실제 GameDebuggerWindow 종료 검증으로 변경했다.

### 여러 기능 동시 보기 후속 확장

`GameDebuggerLayout`과 `GameDebuggerPaneState`/`GameDebuggerPane`으로 창 배치·영역 상태·개별 뷰를 분리했다. 단일/가로·세로 2분할/4분할, 영역별 기능 선택과 기록·지표·필터·bin/clock·입력·기능별 스크롤, 활성 영역 탐색, 경계선 크기 조절을 제공한다. Battlefield+Stats, Encounter+Stats, Monitor×4 빠른 배치를 추가했다. 같은 기능을 중복 표시할 수 있으며 공용 Match/시간/수집기는 복제하지 않는다. 숨긴 영역의 설정은 유지한다. 영역 Attach는 공용 표시 설정을 다시 켜지 않는다.

처음에는 Editor PID 48360이 프로젝트를 열고 있어 CLI headless 실행이 거부됐다. 사용자가 저장 후 종료한 뒤 Unity CLI로 `BattlefieldEditorDiagnosticsTests;GameDebuggerTests`를 실행했고 **15/15 성공, 실패·제외 0**을 확인했다. 결과는 `%TEMP%/game-debugger-multipane-editmode.xml`이다. 최종 Unity 로그에는 컴파일 오류와 `ExtensionOfNativeClass` 경고가 없다. 위 기본 배치 결과와 별개의 후속 검증 증거다.

후속 테스트는 전체 기능×4개 배치의 구성, split의 두 자식, 영역 상태 독립성·숨김/재구성 보존·직렬화·잘못된 입력·창 종료 시 시간/소스 유지를 검사한다. 실제 드래그·스크롤 복원·dock 화면 검사는 별도 인수다. Runtime/Scene 코드는 변경하지 않았으며 PlayMode 통합 테스트를 중복 실행하지 않았다. 현재 후속 변경은 로컬 변경이며 push/PR은 하지 않았다.

- 새 창의 Single/상하·좌우/4분할, 드래그 크기 조절·활성 영역 탐색·독립 필터·스크롤 복원, domain reload의 상태 보존, 좁은 dock/넓은 창과 USS, 선 그래프 축/클릭 값 조회/A-B 축, 종료된 기록의 배지.
- 실제 Player의 Scene 충돌과 Game UI 클릭이 올바른 source로 기록되는지.
- 시간 배속·한 프레임 Step, turret destroyed/OOB 조건 정지, Capture 화면.
- 동일 Build의 터렛 실제 전투·Enemy 타깃/피해·지역 해금·승패는 각 Owner와 통합 Gate에서 확인.

PlanId/피해/전술 우세 지표는 없는 계약을 추정해 채우지 않았다. 아래 후속 확장에서 실제 후보 판단과 EnemyHealth 확정 사망은 관측 계약에 추가됐다. 본 결과가 기존 2차 문서의 “실제 생성·authority 관측 미연결” 부분을 대체하지만, B4/B5의 전체 Feature 설계·타깃·idempotency 및 전투 Gate까지 닫은 것은 아니다.

## 실사용 모니터 후속 확장

- 요청·실제 생성·확정 사망을 함께 그리는 선 그래프, X/Y축, 최신 60초 추적, 가시 구간 최대값 + 여유 기반 Auto Y, 수동 양축 탐색/확대·축소, Statistics 독립 확대 창.
- Overview 수치 카드, 터렛/요청/Timeline 스크롤 표 및 선택 상세. 긴 설명·설정은 foldout으로 분리하고 Editor 기본 스타일을 따른다.
- Statistics 공간 판단 화면: 요청 당시 snapshot, 후보/제외 사유·밀도·외곽 거리, 지점/몬스터별 계획과 실제 수량, 실제 생성 XY/Network ID. 무거운 자료는 최근 32개 요청만 보존하며 생략을 표시한다.
- 반복 Encounter 요청: 간격·유한/무제한 횟수·게임/실시간 clock, Pause·실패·Match 교체·시작 pane 해제에 따른 대기/중지. 별도 Spawn 우회 경로가 아니다.
- 현재 전선의 기하 Inspector 및 Scene 경계 선택·강조·안/밖 화살표. Editor 파생 진단만 추가하고 Core topology 정책은 바꾸지 않는다.
- Player 타깃 등록 HOTFIX `dcb583d1` 별도 커밋. 생성 10마리 중 8마리 이동, 모든 타깃 제거 시 0마리 이동, Player만 복원하면 7마리 재이동을 명시적 Scene 검사에서 확인했다.

첫 후속 단위 검사는 33/33 성공(`game-debugger-monitor-editmode.xml`), 이동 HOTFIX 명시적 통합은 1/1 성공(`enemy-movement-hotfix.xml`)이다. 반복·전선 도구의 마지막 보완 및 실제 판단/사망 통합 검증은 아래 최종 검사 결과로 구분한다. 화면 드래그/도킹과 실제 전투 밸런스·전선의 재미는 자동 검사 결과로 대신하지 않는다.

### 최종 집중 검사 (후속 변경 반영 후)

| 검사 | 결과 | 로컬 XML |
| --- | --- | --- |
| 창 구성·차트/viewport·Spawn Inspector·공간 정책·반복 scheduler·전선 보고/선택 | **54/54 성공, 실패·제외 0** | `%TEMP%/game-debugger-monitor-final.xml` |
| 실제 Initial_Stage Host·Spawn·snapshot·사망·반복 명령 통합 | **1/1 성공, 실패·제외 0** | `%TEMP%/game-debugger-monitor-integration.xml` |

54개에는 반복 scheduler 14개, 전선 보고/선택 7개, 차트 7개(독립 Statistics 창의 GUI 레이아웃/렌더링 포함), Spawn Inspector 7개가 포함된다. Collector/Match 중복 생성 없이 조회 창을 구성한다. 그래프의 사람이 보는 색·클리핑·드래그/스크롤 조작까지 자동으로 인증한 것은 아니다.

최종 Scene은 터렛 점 8개·삼각형 8개·외곽 6개였다. 자동/Scene adapter/UI adapter/Debug 경로를 확인하고, 같은 반복 상태 머신으로 Pause 중 0회, 재개 후 2회/추가 20마리 실제 생성 및 횟수 상한 종료를 확인했다. 실제 완료 진단에 후보·계획·생성 위치가 포함되고 HP 소진은 `EnemyDied` 정확히 1회, 일반 Despawn은 별도 `EnemyRemoved`로 기록됐다. 정상 종료 archive 보존도 통과했다. 이 검사는 UI 버튼을 물리 클릭하거나 반복 Editor update의 실제 시간 간격을 화면에서 관측한 검사는 아니다.

검사 중 Unity 6의 동명 `UnityEngine.EntityId`와 Domain ID 충돌은 명시적 alias로 수정했다. 첫 통합 실행은 테스트 coroutine의 closure가 EnterPlayMode domain reload 후 복원되지 않아 실패했고, static helper로 분리한 뒤 동일 검사 재실행이 통과했다. 최종 결과에 컴파일/테스트 실패는 없다. 테스트용 Scene 변경은 저장하지 않았으며 전체 승패·전술 밸런스는 이 도구 검사의 범위가 아니다.

후속 복구 커밋: 이동 HOTFIX `dcb583d1` → 실제 판단/사망 관측 `a0cd5848` → 실사용 UI·분할 창·반복 요청·전선 도구/단위 검사 `268fba68` → 이 문서와 실제 Scene 회귀 검사를 담은 후속 커밋. 원격 push/PR은 실행하지 않았다. 기존 untracked `Validation/`은 포함하지 않았다.

## 디자인·구조·일반 개발 도구 활용성 재검수

[Design.md](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/tooling/game-debugger/Design.md)를 추가했다. native Editor 글꼴·계층(본문 12px/보조 11px/계기 17px), Dark/Light 의미 색, 표/박스/스크롤, LIVE와 과거 기록 구분, 명령 안전성, 확장 계약과 사람이 확인할 항목을 명시한다.

| 확인 축 | 발견한 문제와 보완 |
| --- | --- |
| 디자인/읽기 | 가로 scroll 시 헤더/본문 분리 → 같은 scroll 안에 배치. 미배정 지점 전체를 실패처럼 표시 → 회색으로 구분. 공통 수치 스타일·그래프/지도 색을 공유하고 skin별 대비를 검사 |
| 상태/구조 | Snapshot 선택·필터·zoom/pan 재구성 초기화 → 영역별 serialized view state. 고정 요청이 보존 범위에서 사라지면 다른 요청으로 치환하지 않고 unavailable 표시. Match ID 경계 유지 |
| 실질 기능 | 지도 drag 후 잘못된 선택 → 4px 클릭 임계값/버튼 분리. bin 보정값과 표시 일치. Client의 서버 미관측 0을 N/A로 표시하되 실제 양수 사실 유지 |
| Scene 탐색/공유 | Gizmos와 도구 layer가 모두 켜진 경우만 그리기/선택. Frame Grid/Cell, Copy context/전체 cell·OOB ID, 요청 JSON evidence 추가. 목록은 박스/scroll 및 표시 상한 안내 |
| 비용/책임 | 요청/Timeline 정렬·검색 cache로 반복 IMGUI repaint의 재계산 방지. Scene 이동·스타일·cache·view state를 Editor helper로 분리. Runtime 정책을 Editor에서 재구현하지 않음 |

일반 개발 작업에는 **관측 → 사건 선택 → Scene 위치 확인 → 재현 자료 공유** 흐름으로 활용할 수 있다. 현재는 이 프로젝트의 Core/Match/Spawn 진단에 특화된 도구다. 다른 게임에 그대로 적용 가능한 범용 패키지, Profiler/Console 대체, AI 의사결정·DPS·네트워크 trace·결정론적 Replay·전체 승패 검증까지 제공한다고 해석하지 않는다. 추가 Feature는 실제 관측 계약을 제공한 뒤 panel/집계로 확장한다.

### 이번 검수의 최종 자동 검사

| 검사 | 결과 | 로컬 XML |
| --- | --- | --- |
| 사용성·Spawn Inspector·Chart·Battlefield Editor·창 구성 | **40/40 성공, 실패·제외 0** | `%TEMP%/game-debugger-usability-review.xml` |
| 서버 미관측 보완 후 사용성·Chart·창 구성/집계 회귀 | **31/31 성공, 실패·제외 0** | `%TEMP%/game-debugger-authority-coverage-review.xml` |

두 실행은 상당한 중복을 포함하므로 서로 다른 테스트 71개라고 계산하지 않는다. 새 `GameDebuggerUsabilityTests`의 최종 12개 케이스에는 영역 상태/직렬화·고정 기록 소실·Match 전환·입력/drag 임계값·cache 교체·중앙 XY Frame bounds/전체 ID·서버 미관측·Dark/Light 선 대비·실제 native GUI 생성/렌더링 검사가 포함된다. 초기 검사에서 발견한 새 meta GUID 길이 오류와 소실 요청 placeholder 선택 오류는 수정했고 최종 실행에서 통과했다.

이번 변경은 Editor 코드·EditMode 테스트·문서뿐이다. Runtime/Scene/프리팹/Package 설정과 기존 `Validation/`은 변경하지 않았다. 이전 실제 Spawn/사망/이동 통합 증거는 위에 보존하되 이번에는 해당 PlayMode 검사를 중복 실행하지 않았다. 실제 dock/드래그·한글 glyph·DPI·전체 Dark/Light 화면·Gizmos/Alt 조작·영상은 사람이 확인할 항목이며 자동 GUI 검사를 시각 QA 완료로 표기하지 않는다. 원격 push/Issue 수정/PR은 하지 않았다.

복구 경계: Editor 구현·검사 `dff8cd33` → 디자인 기준·사용 가이드·본 검수 기록을 담은 문서 커밋.

## SpawnPoint 자동 등록 및 전체 사용 매뉴얼 후속

- 이전 `Awake` 한 번/Executor 자식만 수집하던 방식에서, 계획용 `SpawnPointList` 조회 직전에 **같은 Scene 전체**를 갱신하도록 변경했다. 다른 Executor 계층 아래 지점도 포함하며 다른 Scene은 제외한다. 비활성/점령 상태는 등록 후 기존 후보 정책에서 판단한다. 매 Update마다 Scene을 검색하지 않는다.
- 빈/복제 ID는 Editor authoring 보정으로 구분한다. 기존 고유 ID는 보존하고 Undo·Scene 저장 대상이다. Runtime 복제는 serialized ID를 덮어쓰지 않고 객체 수명 동안 ID를 유지한다. 좌우 두 지점 하드코딩을 추가하지 않았고 전체 요청 예산·Grid/분포 배정 정책은 유지했다.
- 사용자 Editor의 첫 실시간 조회에서 **31개**(두 Executor의 기존 계층에 25개/6개)를 확인했고 당시 Scene은 저장되지 않은 dirty 상태였다. 이후 사용자 Editor가 종료되어 마지막 전체 Scene 재등록 수의 live 조회/추가 배치의 영속 보존은 확인하지 못했다. Scene을 강제로 저장하거나 복제 위치를 임의 복원하지 않았다.
- 디스크에는 Left/Right의 직접 serialized 지점과 기존 `EnemySpawnRoot`/`SpawnPoint` Prefab 인스턴스가 남아 있다. 따라서 직접 `spawnPointId:` 항목 두 개만 세어 전체 지점이 두 개라고 판단하면 안 된다. 다만 메모리 조회에서 확인한 `Encounter Spawn Right (1)`부터 `(23)`까지의 추가 복제는 저장 Scene에서 찾지 못했고, 종료 후 임시 Scene 백업도 찾지 못했다. 코드의 자동 등록과 별개로 해당 추가 배치는 저장/복구가 필요한 사용자 자료다.
- Unity CLI 재컴파일에서 컴파일 오류 0을 확인했다. 연결 Editor 테스트는 저장 확인/응답 지연으로 완료 증거가 없어 성공으로 기록하지 않는다. 종료 후 headless 검사의 첫 실행은 fixture의 Untitled Scene Additive 생성 제한으로 실패하여, batch에서만 Single을 사용하고 foreign Scene은 preview Scene으로 분리했다.
- 최종 `SpawnPointRegistrationTests;SpawnPointSpatialPolicyTests`는 **8/8 성공, 실패·제외 0**, `%TEMP%/spawn-point-registration-review.xml`이다. 실제 등록/후속 복제·삭제·비활성 전환·동일 Scene의 다른 부모/Executor 계층 포함·다른 Scene 제외·ID 보존/authoring 수정과 기존 공간 배정 회귀를 확인했다. 실제 전투/31개 지점 전부의 실제 스폰을 인증하는 검사는 아니다.
- [Game Debugger 전체 사용법](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/tooling/game-debugger-guide.md)을 빠른 시작, 목적별 작업 흐름, 큰 통계 창·그래프 조작, Scene Overlay/Gizmo, 반복 요청, Timeline 조건부 정지, Export/Compare, 문제 해결까지 최신화했다. API/수집 한계는 별도 하단에 유지하고 중복된 이전 실행 순서는 제거했다.

사용자의 Scene 배치는 이번 코드/문서 커밋에 포함하지 않는다. 저장하지 않은 Edit Mode 배치나 Play Mode 복제는 다음 Editor 실행에 유지된다고 가정하지 않는다. 원격 push/PR/Issue 변경은 하지 않는다.

복구 경계: 자동 등록·ID·검사 `2a4b90eb` → 전체 사용 매뉴얼·README·본 후속 결과를 담은 문서 커밋.
