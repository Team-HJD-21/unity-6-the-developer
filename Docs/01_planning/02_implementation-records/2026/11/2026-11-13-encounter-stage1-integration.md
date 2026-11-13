# Encounter 개편 / Stage1 통합 기록

2026-11-13 · 진행 중. 이 문서는 전체 Stage1 완료 보고가 아니다.

## B0 — 실제 전장 진입

### 바뀐 동작과 진입점

- `AppScreenPresenter.StageScene`을 `Assets/Scenes/Gameplay/Stage_1.unity`로 맞췄다. 출격/실패 화면 재시작이 같은 실제 전장을 참조한다.
- `SpaceShipGrayBox.unity`의 Exit 목적지 두 곳도 같은 전체 경로로 맞췄다. Reference 폴더의 동명 Scene과 혼동하지 않는다. Scene의 나머지 오브젝트·참조·배치는 변경하지 않았다.
- 실제 흐름: `StartupControlsView.Start` 버튼 → 기존 GameFlow의 우주선 진입 → 기존 `SpaceshipExit.ExitAsync` → `SpaceshipTravelService.ExitAsync` → `GameFlowController.LaunchMissionAsync` → Stage1 조립.

### 확인한 범위

- 현재 실행 Editor Unity `6000.3.23f1`에서 CLI로 실제 Play Mode를 실행했다.
- 실제 Start 버튼의 UnityEvent를 호출하고 우주선 도착을 확인했다. 우주선에 임시 Player와 활성 Hub Camera 각 1개가 준비됐다.
- 우주선 문이 사용하는 실제 출격 API를 호출했다. 비동기 Task가 예외 없이 완료되고 GameFlow `Mission`, Scene `Gameplay/Stage_1.unity`, Running Match를 확인했다.
- Stage1에서 AppRoot 1개·등록된 Player 1명·활성 Main Camera 1개를 확인했다. 검증 뒤 Play Mode를 종료했다.
- 이것으로 지속 생산·지역 확보·구조·저장/복원·보스·협동 플레이까지 통과했다고 판단하지 않는다. 해당 배치는 이후 별도 검증한다.

### 팀원 변경과 기존 작업

- World 제작 파일 수정은 `SpaceShipGrayBox.unity`의 `returnSceneName` 두 값이다. #716에서 실제 전장 이름이 변경되어 최초 출격도 새 전장에 연결하기 위한 수정이다.
- UI 제작기와 Unity 실행 시간을 조율했다. 기존 Tactical/HUD/입력/카메라·폰트 작업과 스테이징은 포함하거나 덮어쓰지 않았다.
- 이번 목표의 필요한 팀원 코드 변경은 사용자가 승인했다. 이후 실제 변경 파일과 이유를 배치별로 추가한다.

## B1 — Pipeline와 실제 요청 입구

### 먼저 볼 코드와 흐름

`Features/Enemy/Encounter/Pipeline/EncounterPipeline.cs`의 `Run`을 먼저 본다. **Collector → Processor → Planner → Executor**를 직접 순서대로 호출한다. 각 Step은 다음 Step을 숨겨서 호출하지 않는다.

- `EncounterCollector`: 같은 Scene의 실제 대상, 현재 Region, 등록 지점, 실제 적 수를 요청당 한 번 읽는다.
- `EncounterProcessor`: 스폰 불가 지점과 점유 구역을 제외하고 편성 판단용 타깃·주변 Player 정보를 계산한다. AI의 실제 공격 목표를 강제로 바꾸지는 않는다.
- `EncounterPlanner`: 기존 점유율 비용/Static 증가, 지역 예산 배분, 상황별 편성/분산 정책을 재사용해 최종 명령을 만든다. `SpawnCompositionPlanner`는 이 내부의 배분·편성 구성 helper다.
- `EncounterExecutorStep`: 실제 생성 직전의 Region으로 지점 제한을 갱신하고 실제 생성기 `EnemySpawnExecutor`를 호출한다. 실제 생성 결과만 분산 이력에 반영한다.
- `EncounterSceneBinding`: Core Match/난수·환경·Origin·저장·진단 연결을 담당한다. 기존 MonoBehaviour GUID를 그대로 옮겼으므로 Scene/Prefab의 연결을 유지한다. `Encounter/Scene/`의 세 partial을 확인한다.
- `Testing/EnemyNetworkTestLauncher`: 수동 standalone 네트워크 시험 도구로 분리했다. 운영 Match의 생산 정책을 소유하지 않는다. 기존 `EncounterRuntime`은 제거했으며 호환 래퍼를 병행 실행하지 않는다.

자동 요청은 `EncounterRequest.Automatic(...)`, 지정 편성은 기존 `new EncounterRequest(...)`를 사용한다. 두 요청 모두 `EncounterRequestService.Submit`의 Host·참가자·Match·중복 검증을 통과한다. Scene의 `RequestSpawn`이 해당 서비스를 우회하던 경로를 제거했다. 외부에서 Pipeline을 직접 생성할 때의 namespace는 `TeamHJD.Game.Enemies.Encounter`다.

### 점유 그룹 제외

Core `RegionSpawnPolicy`가 정적 구역의 모든 점유 셀을 확인해 같은 Region ID 그룹을 일반 스폰 할당 전에 제외한다. 초기 CU 한 셀·해금과 전체 구역 확보를 혼동하지 않는다. 인접 구역 지점도 실제 점유 셀에 스폰 반경+여유 거리가 닿으면 제외한다. 지점별 수동 OFF는 보존하며 Dynamic은 같은 셀 경계 검사로 처리한다. 정적 확보 규칙/해금 분리 자체는 B3에서 바꾼다.

### 실제 실행에서 발견한 문제와 수정

Stage1의 초기 고정 편성은 생성됐지만 자동 편성은 난수 미주입으로 거부됐다. 영속 Encounter가 DDOL Scene에 있어 Scene 전용 난수 수집에서 누락된 것이 원인이다. `InitialStageCompositionRoot`가 현재 Encounter를 명시적으로 `RandomConsumerScope`에 포함하도록 변경했다. 대신 사용하던 `EnemyTestRandomBinding`과 유일한 `EnemyTest` Scene 연결을 제거했다. 난수 원본/seed·저장·해제는 기존 Core 서비스를 유지한다.

### 확인 결과와 제한

- Unity 컴파일 오류 없음. 예산 10개·운영 바인딩 6개·공통 요청 8개(자동 요청 권한/재전송 포함), 점유/경계 11개를 각각 실제 Editor Test Runner로 확인했다.
- 실제 `Stage_1`에서 새 GUID 연결과 Running Match·초기 편성 **계획9/생성9**, 수정 후 자동 편성 **계획9/생성9**를 확인했다. 최대10은 상한이며 완전한 편성/비용 규칙 때문에 9가 될 수 있다. 총 생존18을 확인했다.
- 자동 요청의 실제 할당 지점 Region ID를 대조해 확보된 Region 그룹에 배정되지 않았음을 확인했다. 현재 한 요청의 할당은 Region3이며, 모든 지점의 균등 분산/시간에 따른 압박 검증 완료를 뜻하지 않는다.
- 별도 위치 대조에서 초기 적9 중5의 0.1월드 단위 초과 이동을 확인했다. 나머지 AI의 모든 행동·경로를 통과했다고 판단하지 않는다.
- 검증 뒤 Play를 종료했다. 현재 Editor Console 오류0·컴파일 실패false를 확인했다. 이전 실행을 보관한 진단 버퍼의 오래된 오류와 현재 Console 상태는 구분했다.
- 아직 정규 주기 생산·성과 기반 점유·후속 지역 해금·성장 복원·실패/구조·보스 진행·협동/전체 성능 검증은 완료되지 않았다.

## 현재 남은 작업

공통 소환/적 생명주기, 실제 전투 기반 정적 확보/해금과 저장·복제, 지속 생산/환경·시간, 성장 저장/월드 복원, 구조·실패, 33/66/100 진행 및 실제 콘텐츠 통합이 남아 있다. Game Debugger의 기존 진단 경계를 사용하고, 실행하지 않은 검증을 완료로 기재하지 않는다.

개인 실행 로그·명령 원본은 `Docs/Local/Execution/EncounterStage1-20261113/`에 공유 제외로 보존한다. 팀 공유 문서는 확인한 결과와 사용법을 이곳 및 관련 API 문서에 반영한다.

## B2 — 소환도 같은 Match의 적입니다

- 일반 Pipeline과 `EnemySummonGenerator.Spawn`은 `EnemySpawnExecutor.TrySpawnActor`에서 분대·NGO·안정 ID·시계·생명주기 등록을 공유합니다. Enemy가 정한 종류·수량·초기 타깃과 좌우 간격은 그대로이며 정규 생산 예산을 다시 뽑지 않습니다.
- 소환에는 `EncounterRequestSource.EnemySkill`과 독립 요청 번호가 붙습니다. 살아있는 적 수·Grid 위치·사망/제거·기존 체크포인트 원본에 포함되고 Match 종료 시 함께 정리됩니다.
- 일반 SpawnPoint도 실제 반경 안의 XY NavMesh 위치를 최대16회 확인합니다. 유효 위치가 없으면 실패 이유를 남기며 수중이나 임의 다른 지점으로 대체하지 않습니다.
- Enemy 담당자 변경 파일: `EnemySummonGenerator.cs`는 직접 Instantiate 대신 생성기 위임, `EnemySpawnExecutor`의 Creation/Summons partial은 공통 생성과 소환 문맥, `EncounterSceneBinding.Summons`는 독립 진단 요청입니다. AI·공격·스탯·소환 프리셋은 유지했습니다. 사망 소환은 부모 HP0 이후 호출되므로 제거 전 추적 소속으로 검증합니다.
- 실제 Stage1 검증: 일반 초기9개/정규 편성6개, 소환3/3, 실제 BomberSlime2개, 변태 완료 상태에서 실제 피해→사망 소환5/5. 시계·저장 원본·위치26개 일치, Match 정리26→0. 변태 완료 상태는 시험 준비로 설정했으며 자연 애니메이션 전체 재생과는 구분합니다. Editor 컴파일 통과, 바인딩6/6, 현재 Console error0, Play 종료.
- 전체 저장/복원과 지속 생산·지역 전투 확보는 아직 후속 배치입니다. B2 완료가 전체 Stage1 완료를 뜻하지 않습니다.
