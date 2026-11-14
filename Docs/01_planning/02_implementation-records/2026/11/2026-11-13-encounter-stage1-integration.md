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

## 별도 보완 — 낮밤 연결의 재로드 수명

- 반복 Play 검사 중 `MatchDayNightCycle.OnEnable → MayAdvance → _mayWrite()`에서 NullReferenceException을 확인했습니다. `_bound/_authority`만 남고 시계·판정 delegate가 없는 부분 바인딩을 다시 사용하려던 경로입니다. 스크립트 재로드가 가능한 Editor에서 런타임 소유권 플래그가 직렬화되지 않도록 표시하고, OnEnable에서 불완전한 연결을 해제합니다.
- 현재 연결이 완성되기 전에는 `MayAdvance`와 남은 시간 조회가 null 참조를 사용하지 않습니다. 기존 정상 Host/Client 시계·정지·배속·Weather 전환 규칙은 변경하지 않았습니다. 김진태 담당 Weather 선택/효과 본체는 수정하지 않았습니다.
- `DayNightReplicaTests`8/8을 확인했습니다. 새 회귀 검사는 불완전한 소유권 상태→OnEnable→새 Match 재바인딩을 검사합니다. EditMode에서는 MonoBehaviour의 Player 콜백을 명시 호출합니다. 앞선 실패/이전 DLL 결과와 실제 최신 통과 기록은 로컬 실행 기록에 구분해 보존했습니다. 수정 후 실제 Stage1 재진입과 낮밤 진행도 확인했습니다.

## 개발 검증용 피해 보호

- `DebugDamageProtection`은 선택한 Player/CU에 런타임에 붙이는 표식입니다. `FieldSimulationTime.CanReceiveDamage`를 거치는 두 체력 원본만 보호하며 이동·공격·시간은 계속 진행됩니다. 일반 Release에서는 보호가 동작하지 않습니다. Scene/Prefab이나 체크포인트에 추가하지 않았습니다.
- PlayerInfo와 ControlUnitHealth의 피해 직전 검사만 공용 함수로 연결했습니다. 피해량·회복·사망 규칙은 유지합니다. 기본 상태에서는 기존대로 피해를 받고, 검증 중 명시적으로 표식을 붙였을 때만 무적입니다.
- 범위/자식/비보호 대상/시뮬레이션 진행 검사1/1, 실제 Stage1에서 PlayerHP400→400·Player/CU ApplyDamage=false를 확인했습니다. 보호 없는 첫 장시간 검사에서는 실제 Player 사망도 확인했으며, 보호한 결과를 정상 난이도·승패 검증으로 보고하지 않습니다. Game Debugger의 명시 토글은 지역 통합 변경에 함께 추가합니다.

## B3 — 정적 확보·해금·지형·스폰을 함께 연결

[실제 API/규칙](../../../../02_architecture/02_match/static-region-capture.md)을 기준으로 읽습니다. 이 배치는 Host Stage1 연결이며 전체 승패/보스·성장 저장·원격 Player 인수 완료는 아닙니다.

### 먼저 볼 파일과 흐름

- `InitialStageCompositionRoot.RefreshDynamicInput`: 실제 생존 Player/Enemy 위치를 표본화하고, 같은 셀에 머물러도 지역 타이머를 갱신합니다. Spawn에는 실제 IMatchTime을 주입해 정지를 확인할 수 있게 했습니다.
- `EnemyHealth.DamageApplied` → `EnemySpawnExecutor.Combat`의 `IEnemyCombatSource` → `RegionSceneBinding.Static`: 실제 피해만 팀 성과로 전달합니다. 오버킬·출처 불명·단순 제거는 성과가 아닙니다.
- `StaticRegionCapture`: 60초 확보, 이탈 유예180초, 연속 적 우세30초를 절대 게임 시각으로 판단합니다. 새 조건이 처음 확인된 순간에 이전 미확인 시간을 소급 누적하지 않습니다.
- `RegionGridDefinition/State/Snapshot`: 지형 마스크·정적 해금·확정 점유를 구분합니다. 외부 진행 조회는 불변 `StaticRegionCaptureSnapshot`이며 실제 판정기를 노출하지 않습니다.
- `RegionSceneBinding`: 첫 구역 해금/CU1셀, 확보 후 다음 구역 자동 해금, 상실 후 해금 유지와 점유 회수, 진행·공간 저장 연결을 조립합니다.
- `RegionSpawnPolicy`/Registry와 Executor: 같은 Region 그룹을 할당 전에 제외하고 반경+여유 거리도 검사합니다. 개체마다 최신 점유를 확인하되 같은 불변 표본은 재계산하지 않습니다. 부분 생성은 재실행하지 않습니다.
- `world/spatial`/`world/region-start` v2, Match wire v3/Region wire v2: 새 의미를 저장·복제에 함께 반영했습니다. 이전 Windows 두 실행 파일 검증은 새 wire 인수가 아닙니다.

### 실제 확인

| 항목 | 결과 | 범위 |
| --- | --- | --- |
| 자동 검사 | 최신 고유61개 통과 | Edit53 + Play8. 이전 실패·0개 실행·중복 실행은 합산하지 않음 |
| Stage1 시작 | CU1 / 유효201 / 전체256, 해금[1] | 실제 기본16×16, 실제 NavMesh |
| 팀 성과 | Unknown 피해 뒤 성과0, Player/Turret 피해 집계 | 실제 Health 호출. 물리 입력 전체 전투를 대신하지 않음 |
| 확보/해금 | 첫 구역51셀, 51/201, 해금[1,2], Controller 표시2 | 보호/위치 이동/확보2초라는 명시 시험값 |
| 스폰 제한 | Region1 할당0, 실제12/12 모두 점유 밖 | 실제 Planner→NGO 실행 |
| 상실 | 적3 대 참가자1로 점유51→CU1, 해금[1,2] 유지 | 연속 적 우세 시험값2초. 그룹 재사용, 수동 OFF 유지 |
| 생성 도중 변화 | 계획3/실제1로 중단 | 첫 개체 뒤 권위 점유를 주입한 경계 검사 |
| 전체 점유 | 실제201/201에서 일반 요청 거부, 생존4→4 | 권위 상태 주입. Stage 승리 인수 아님 |
| 저장/복원 | 공간·진행 v2 메모리 왕복51/[1,2]/확보2초, rollback1/[1] | 디스크·성장·모든 actor 전체 복원 아님 |
| 이전 저장 | v1 해금166셀→해금[1,2], 점유CU1, 유효201 | 합성 v1 payload를 실제 writer로 이관 |
| 최종 표본 | 불변 DTO, 실제 첫 구역51셀·해금[1,2] 재확인 | 최종 시각 누적/표본 구현 사용 |

첫 비보호 장시간 시험에서는 Player가 실제 사망했습니다. 이후 표식으로 Player/CU만 보호해 데이터 연결 검사를 수행했습니다. 정상 난이도·패배·재미 평가 통과로 표시하지 않습니다. 기본60/180/30초 경계는 자동 검사, Scene 연결은2초 시험값으로 구분합니다.

검사 중 Core 낮밤 OnEnable의 불완전 바인딩을 발견해 별도 커밋 `94e95d49c`로 보완했습니다. 개발용 피해 보호는 `6dcf8499a`로 분리했습니다. 그 밖의 실패 기록과 재검사 원본은 로컬 실행 기록에 보존했습니다.

### 팀원 코드에 반영한 접합

- Enemy: 생성기의 실제 피해 통지 구독/해제·복원 재구독, EnemyHealth의 실제 피해 사건. 공격 가능 규칙·AI·스탯·편성은 수정하지 않았습니다.
- Player: PlayerBullet/Spanner/Bomb의 출처는 Player, 설치 터렛 탄환의 출처는 Turret로 명시했습니다. 공격/피해량을 다시 계산하지 않습니다. 기존 출처별 EnemyDamageRule도 실제 공격자를 읽게 되므로 암살자 등의 기존 규칙과 접점이 생깁니다. 해당 규칙 본체나 드론 판정을 이번에 변경하지 않았습니다.
- Defense: 탄환/레이저에 Turret 출처, Controller의 정적 해금 표시를 Core 원본에 연결했습니다. 전력·강화·자동 활성화 규칙은 그대로입니다.
- World/Weather: Weather 선택·효과 본체를 수정하지 않았고 Core 시계 연결/수명만 보완했습니다.
- HUD/Audio: 기존 UI·입력·Camera·Audio 변경을 보존했습니다. 진행률의 유효 분모 API를 UI 제작기 세션에 전달했으며 해당 세션에서 Header/CU 표시에 연결합니다.

### 남은 범위 / 주의

Host 세부 확보 타이머 표는 실제 원격 Player 입력·Client 표 복제 인수와 구분합니다. 현재 지형 투영은 활성 XY NavMesh 기준이며 여러 전장의 NavMesh를 동시에 활성화하는 경우의 Scene 지형 공급은 별도 검토가 필요합니다. 전체 저장·복원, 지속 자동 생산, 사건/보스/승패는 다음 배치입니다.

최종 실행에서 Game Debugger의 현재 메뉴를 열고 OnGUI 예외는 관측하지 않았습니다. 이전 메뉴 경로를 잘못 호출한 QA 명령 오류1건은 숨기거나 지우지 않았습니다. 앞선 중단 Play 종료 중 NGO `NetworkObject.OnNetworkBehaviourDestroyed → NetworkManagerOwner` null 예외도1회 관측했으며, 이후 최종 종료에서는 재발하지 않았습니다. 이를 해결 완료로 위장하지 않고 최종 네트워크 정리 검사에서 추적합니다.


## B4a — Match 정규 생산과 Field 시간 (11/14)

진입점은 `InitialStageCompositionRoot → MatchCoordinator.Start → MatchEncounterProduction → IMatchScheduler → IEncounterRequestService.Submit → EncounterPipeline`이다. Scene의 첫 자동 요청은 정식 Match에서 억제하여 생산 소유자를 하나로 만들었다. 기본 첫0.5초/간격15초/요청상한500이며, 실제 비용·공간·편성에 따라 생성한다. 기존 비용 증가/분산 정책은 유지한다. 자세한 사용법은 [정규 생산 API](../../../../02_architecture/08_encounter/encounter-production-api.md)에서 확인한다.

먼저 볼 파일:

1. `Core/Application/Encounter/MatchEncounterProduction.cs`: 한 예약·현재 실행 시각부터 다음 예약·권위 요청·수명·복원.
2. `Features/Enemy/Encounter/Pipeline/EncounterPlanner.cs`, `Planning/SpawnCompositionPlanner.cs`: 자동 편성도 실제 환경 규칙을 사용. 기존 마릿수 배율을 비용 배율로 바꾸지 않았다.
3. `Core/Application/Time/FieldSimulationClock.cs`, `MatchTimeService.cs`: 공용 시간과 기존 Field의 보류 시각 분리. Player는 공용 시간, Field Enemy/터렛/투사체/정규 생산/확보는 투영 시각.
4. `Core/Application/Persistence/{FieldTimeCheckpointParticipant,EncounterProductionCheckpointParticipant,CheckpointService}.cs`: 남은 대기/Field 시각의 저장과 시간 선행 rollback. `world/encounter`v2는 예산 증가 단계와 자동 대기 구분도 저장한다.

| 구분 | 확인 내용 | 결과/제한 |
| --- | --- | --- |
| 준비/실제 컴파일 | main #719 URP2D 통합·새 소스 | 실제 URP 활성/오류0, Sprite-Lit 기본 셰이더 유효 |
| 자동 검사 | 생산6·Field시각5·복원실패1·예산11·자동편성62 | 고유85개 통과. 먼저 단독 실행한 환경1/구버전시각4와 재실행은 중복 합산하지 않음 |
| 실제 Stage_1 정규 생산 | Debugger 버튼 없이 첫3회 | 매회 계획12/실제12, 첫 임시 Coroutine pendingfalse |
| 실제 Field 보류 | 공용20.1088초 진행 | Field0초/요청5→5/다음대기2.9786초 유지, 적59마리 최대이동0, Controller시각동일. Player공용시각은 계속 진행 |
| 보류 해제 | 같은 Match 다음 예약 | 요청8회까지 정상 진행, 밀린 회차 폭주 없음 |
| CU 자동 활성화 | 새 Match에서 실제 Host gateway 제출 | AutoActivationStarted·후보2, 실제 터렛가동true 확인. 검수용 Player CU 보호/근접 warp 조건 |
| Encounter 개별 저장 | 실제world/encounter v2,2537bytes | Prepare/Apply/Rollback 왕복. 전체 월드 복원 검증과 다름 |
| 전체 저장 Gate | 실제writer 목록 | world/weather 누락 유지. 가짜 writer로 우회하지 않음 |
| 오류 | 새 Play~정상 Stop Console | Runtime error0. URP 첫 import 중 missing include/type/Renderer2D 기본재료 임시예외는 별도 rawlog로 보존 |

뒤늦은 시각 Snapshot anchor 보완을 Play 종료 전에 적용한 절차 오류가 있었다. 즉시 Stop/clean Edit Mode로 복귀했고 최종 소스의 Field시각5/rollback1과 실제 정규 생산/Field 보류를 다시 검증했다. 앞선 기록은 삭제하지 않았다.

이 범위에는 기존 Enemy 이동/공격/스탯/실제 프리셋 재설계가 없다. `EncounterSceneBinding`·환경 profile·자동 Planner·checkpoint 연결만 바뀌며 Core가 생성 타이밍을 소유한다. 실제 날씨별 편성 데이터가 없으면 기존대로 neutral이다. 공통 예산을 나누는 상황 기반 추가 출현·예고·점유 추세, 원격 Client Field 보류, 33/66/100 사건/보스는 아직 후속 범위다. B4 전체/Stage1 전체 완료로 읽지 않는다.


## B4b — 점유 추세와 공통 예산 내 예고 출현

- RegionRuntime 옆의 RegionCaptureHistory가 최근120 Field초/최대64 변경 표본만 보관한다. 첫 CU/복원/Grid·Epoch 변경은 성장이 아니며 단일 정적 구역 증가 한 번으로는 추가 출현하지 않는다.
- Collector→Processor→Planner의 기존 정상 편성을 만든 뒤, 정책이 허용하면 이미 선택된 완성 편성 하나를 총예산의 최대50% 내에서 점유 내부로 옮긴다. 새 예산·난수 재편성·추가 적 복제는 없다. 기본 순증15%/변경2회/관측30초/쿨다운90초/예고3초/Player거리8은 Inspector에서 조절한다.
- 실제 점유 외곽 근처의 비어 있는 점유 셀 안 NavMesh 영역을 고른다. 기존 RangeCircleSmooth Sprite로 주황 월드 예고를 그리며 GameAlerts도 한 번 보고한다. 원격 예고 전송이 없어 현재 1인 Host만 이 정책을 사용하며 멀티에서는 일반 계획을 유지한다.
- 준비 편성은 Source SituationEvent·원인 요청 ID·영역·비용을 잠근 채 공통 요청 서비스로 제출한다. 실행 시 Host/취소/현재 배치·동일 비용을 다시 검사한다. Field 보류 동안 deadline/표시가 멈추며 Grid 교체·Match 종료에는 제거한다. 자동 편성에 명시 World origin을 넘기는 조합은 조용히 무시하지 않고 UnsupportedOrigin으로 거절한다.
- world/encounter v3에 추가 정책 남은 쿨다운·준비 편성/영역·원인·예고를 넣었고 v1/v2 읽기를 유지했다. 복원은 새 요청 ID로 하나의 예약만 만들며 Alert는 재생하지 않는다. 전체 저장의 weather writer 누락을 우회하지 않았다.


자동 검사19개 통과. 실제 Stage_1에서 권위 점유를30→70셀로 두 번 주입한 경계 시험으로 일반6 + 예고3을 확인했다. 예고 중 개별 checkpoint apply/rollback은 생존15·대기1·예산25를 유지했고, Field 재개 뒤 Source SituationEvent 계획3/실제3·생존18·대기0·예산25로 끝났다. 자연 전체 스테이지 플레이나 Weather 포함 전체 저장의 성공을 의미하지 않는다. Scene View 주황 예고를 native PNG로 확인했고 정상 Stop 뒤 새 오류0·컴파일 정상이다.


## B5a — 개인 성장 영속화

- IGrowthCheckpointParticipant와 GrowthSaveService로 실제 PlayerExperience/PlayerBaseStats 원본을 개인 저장에 연결했다. UI는 기존 API를 유지한다. identity의 논리 Player ID로 파일을 나누고 roster 순서를 PersistenceKey에 사용하지 않는다.
- XP 기본2초 합치기, 레벨업·보상·투자/환불은 관련 두 원본을 즉시 한 캡처로 저장한다. 불변 자료만 순서대로 기존 BinaryCheckpointStore에 제출한다. 월드 복귀/정상 Match 해제는 Flush하며 디스크 실패를 숨기지 않는다. 손상 파일을 기본값으로 덮지 않는다.
- 서비스 자동 검사 최종6/6: 합치기/중요 변경 묶음, roster 변경, 디스크 실패·명시 재시도, 손상 보존, 적용 실패 원복, 지연 쓰기 역전 방지. 실제 Unity 컴파일 오류0.
- 실제 Stage_1의 기존 Player API로 XP0→100/레벨2, 보상 수령true, 공격1투자true, 사용 가능 포인트0을 확인했다. 저장 issued3/saved3/errornull. Play 완전 종료→재진입한 새 actor에서도 XP100/레벨2/공격1/보상수령true/포인트0이었다. b5a-actual-save.json, b5a-actual-reload.json에 원본을 보존했다.
- QA에서는 정규 생산 lease만 임시 종료하고 Player/CU 보호를 사용했다. 시험 전 growth 파일0을 확인했고, Stop 뒤 생성한 정확한1개 파일을 증거로 복사한 뒤 제거해 기존 사용자 성장 상태로 복구했다. 다른 Checkpoint 파일·Scene은 변경하지 않았다.
- 초기 testName 대신 빈 필터 인자를 사용한 실행은 Total0/실패이므로 검사 통과에 합산하지 않았다. 준비 테스트의 C# var 복수 선언2곳은 private 사전 compiler에서 잡아 적용 전에 수정했다. 최종6개 통과만 기준이다.
- 7008→7121 새 Console 오류0·compileFailedfalse. 현재 전체 월드 Gate는 여전히 world/weather만 누락이다.
- Host 공용 터렛/CU 성장·과거 월드 HP비율/전력 재검증은 B5b다. 실제 스킬 강화/재화 소비 원본이 없는 항목은 완료라고 쓰지 않는다. 개인 영속화가 전체 스테이지/온라인 저장 완료는 아니다.


## B5b — Host 공용 성장·캠페인·월드 접합

- FieldTurretController.Growth가 실제 제작 Definition(승급), 강화/단계·DamageBonus·CU 용량만 별도로 기록한다. 기존 업그레이드·레벨 교체·용량 사건을 구독하고 ON/OFF·피해는 성장 쓰기로 세지 않는다. 기존 OFF/냉각 조건 유지.
- 공용 귀속은 Host+CampaignSlot+Stage. world/campaign에 식별자를 저장하고 AppRoot의 이어하기가 성장 로드보다 먼저 선택한다. 새 캠페인 API는 활성 Match 중 거절. 옛 필수 ID 누락 파일은 명시 이관 오류이며 삭제/가짜 기본값 복원하지 않는다.
- 기존 Defense Persistence Prepare에서 최신 성장과 과거 전투 상태를 합친다. 현재 정상 ON을 우선, 나머지는 기존 CU 후보/논리 ID 순서. 부족한 대상만 OFF/사유를 표시하며 현재 강화·용량을 되감거나 전력 초과/환급하지 않는다. Player/CU HP도 현재 최대치에 과거 비율을 적용한다.
- 자동6/6: HP 최대치 변경/최소1/파괴/잘못된 범위, 캠페인 참조 검증. 실제 Unity 컴파일 정상.
- 실제 Stage_1 AutoActivation으로 초기2개(각15) 가동. 과거 world/field-turrets를 직접 캡처한 뒤 한 대상 OFF→HighFirepower1(비용17), CU 용량15로 변경. 개별 writer Prepare/Apply/참조 연결에서 강화1/비용17 유지, 해당 대상만 OFF, 정상 다른15 대상 ON·예약15·가용0, 부족 사유1개. 전체 Weather 포함 복원과 구분.
- 같은 실제 대상을 HP50/100에서 제작 API로 canon_stage1_lv2로 승급. 과거 lv1 월드를 적용해도 lv2/강화1/HP50/100 유지. 실제 제작 MaxHP가 lv1/lv2 모두100이므로 이 실행을 다른 MaxHP의 실제 승급 증거로 쓰지 않는다(비율 변경은 자동 검사).
- Play 완전 종료→재진입에서 lv2/강화1·CU 용량100·터렛12개·개인/공용scope2 확인. HP/ON은 성장 저장 항목이 아니므로 Scene 새 월드의 HP100/100·OFF로 시작했다. 전체 월드 이어하기 성공으로 쓰지 않는다. world/weather writer만 여전히 누락.
- 근거: b5b-runtime-baseline.json, b5b-runtime-upgrade-world-restore.json, b5b-runtime-level-ratio.json, b5b-runtime-reload.json, b5b-restore-tests.json. QA 성장 파일은 증거 복사 후 시험 전 원본 상태로 정확히 복구했다. 다른 슬롯·Scene은 보존.
- 처음 UTF8 결과를 출력할 때 cp949가 em dash를 출력하지 못했다. RPC 자체는 이미 성공했으므로 같은 강화/복원을 반복하지 않고 저장된 JSON을 파싱해 증거를 확인했다. 기존 공통 문제 해결 기록에 따라 python -X utf8을 사용했다. 시스템 코드페이지는 바꾸지 않았다.
- Debugger에 실제 성장 원본 키·요청/디스크 revision·대기/오류를 추가했다. 창 열기 RPC1회가5000ms timeout이었으나 뒤의 읽기에서 expandedtrue/창실재/성장scope2·컴파일정상을 확인했다. timeout package stack은 b5b-monitor-errors.json에 보존했다. 이1건을 게임 기능 예외0과 섞어 지우지 않았다.
- B6 구조/안전장치·B7 보스 진행·B8 원격 통합·B9 전체검수는 계속 진행한다. 실제 Weather writer와 Boss 콘텐츠는 여전히 외부 연결이 필요하다.


## B6a — 구조·안전장치의 규칙과 실제 Player 생명 port

- MissionRecoveryLedger는 Mission 실행 ID·팀 구조 잔여60초·사용 안전장치0~3·동일 실패 ID·종료 상태·마지막 유효 체크포인트 링크를 가진다. 월드 rollback용 source가 아니며 명시 새 Mission만 초기화한다. 동일 기술 실패 재시도는 또 차감하지 않고,4번째 실패는 실행 종료로 남긴다.
- MatchRecovery가 roster의 실제 Host 생존/위치 원본을 모두 확보한 뒤 관측한다. 구조자 생존·상호작용·거리3/입력 heartbeat·3초유지/HP30%를 검증한다. 여러 다운 대상을 동시에 기다려도 팀 예산은 게임초당1초다. CU 패배는 소유자가 먼저 평가하도록 계약했다.
- MissionRecoveryParticipant는 비환급 기록을 성장 저장에 연결하는 실제 codec/구독 어댑터다. 다음 B6b에서 Host scope/실패 이동에 등록한다. PlayerInfo.Rescue는 실제 생명·HP·입력 잠금만 적용하고 일반 checkpoint 복원을 구조 API로 사용하지 않는다.
- 자동11개 통과: 유지/해제/거리/입력만료·팀 공유예산·중복실패·3회차감/종료·미등록원본·시각rewind9, 저장왕복/구독해제2. 재실행은 중복 합산하지 않는다.
- 첫 실제 Play에서 새 rescue port가 기존 IPlayerInteractionActor를 상속해 단일 상호작용 원본 검증과 충돌했다. 기존 검증을 느슨하게 하지 않고 TryCaptureRescue라는 독립 port로 분리했다. 최종 자동9개 재실행 및 실제 Scene 조립 정상.
- 실제 Host Player에 HP0/사망을 적용하고 실제 Match 공용 시각으로3.015417초 유지했다. HP120/400·aliveTrue·blockedFalse·팀잔여56.984583·안전장치사용0. Field 적만 보류한 검수이며 실제 Player1명 + 시험용 아군 port1개다. 실제2인 NGO/물리 F입력 검증은 아니다.
- 앞선 검수용 Editor callback에 Action 대신 CallbackFunction이 필요해 첫 스크립트는 실행 전 컴파일 실패했다. 수정 뒤 Player를 내렸지만 새 main의 유실된 문 AnimationEvent 오류가 ErrorPause를 걸어 결과가 끝나지 않았다. 이 실행을 성공으로 쓰지 않았다. 별도271f24881 문 이벤트 수정 후 새 Play/공용시각으로 최종 성공했다.
- b6a-actual-player-revived.json, b6a-after-callback-status.json, b6a-recovery-tests-final.json, b6a-persistence-tests.json, b6a-actual-final-errors.json이 근거다. 마지막 구간7402→7409 새 오류0. 이전 실제 실패와 stack은 보존했다. callback은 스스로 해제했고 최종 Play Stop으로 위치·Field 보류가 복구됐다.
- 현재 Root가 자동으로 Player 전멸→월드 복귀를 호출하거나 실제 F입력/원격 gateway를 연결한 상태는 아니다. 이 실제 게임 조립·비환급 기록의 Host scope 등록은 B6b에서 이어서 수행한다. B6 전체 완료로 쓰지 않는다.


## B6b — 실제 Host 복귀 조립 (2026-11-14)

- MatchCoordinator가 MatchRecovery를 소유하고 Root가 CU 패배 평가 뒤 Pump한다. 실제 local Player는 F 유지 입력을 전달하고 자기 회복과 구조 입력을 중복 소비하지 않는다. 원격 실제 actor는 기존 RegisterAuthorityPlayerActor 경계에 rescue 등록을 함께 연결한다. 원격 입력 RPC/생명 복제까지 검증한 것은 아니다.
- 개인·터렛 성장 파일 schema는 그대로 유지하고, Host/campaign/Stage별 별도 recovery 슬롯에 실행 ID·팀 잔여·안전장치·마지막 유효 저장을 보존한다. 대기 시간은 1초 병합, 실패/완료/종료는 즉시 또는 Flush다. 하드 크래시에는 마지막 성공한 파일이 기준이다.
- 새 Mission / 같은 판 Retry / Resume를 구분한다. Campaign 월드 section v2에 실행 ID를 추가해 다른 실행의 월드를 잘못 이어가지 못하게 한다. v1은 명시 migration 없이 거부하며 파일을 덮지 않는다.
- 실제 저장 완료만 마지막 체크포인트로 기록한다. 전멸 중 새 캡처는 금지한다. 유효 저장이 있으면 기존 전체 writer 복원, 없으면 App 소유 Task로 Stage를 다시 열어 최신 성장·같은 실행/차감을 인수한다. 전환 실패는 추가 차감 없이 재시도 API/Debugger에서 확인한다.
- 자동: MatchRecoveryTests 11/11, MissionRecoveryPersistenceTests 2/2, GrowthWorldRestoreTests 6/6. 추가 실제 Scene 조립 컴파일 정상.
- 실제 Stage_1에서 HP400→0 피해로 1~3회 복귀: Player/Match 인스턴스 변경, 실행 4e1257d5 유지, 사용1/2/3, HP400, 오류 없음. 4번째는 새 Player 없이 HP0·IsEndedtrue·Defeat, 사용3 유지. b6b-after-first-recovery.json / b6b-safety-cycle-result.json. 오류 cursor7577→7597 신규0.
- QA 후 기존 checkpoint33개 목록/내용 해시를 정확히 복구했다. 세 개의 알려진 growth 슬롯만 보존/복구했으며 Scene은 dirtyfalse다. 관련 파일은 b6b-save-before/after와 b6b-save-cleanup.json.
- 실제 전체 월드 복원은 world/weather writer 누락으로 미통과다. 물리 F/실제 원격2인 구조·실패 입장 재시도 전체·CU동시파괴 우선순위 실제 재현은 남아있다. 이를 자동 규칙/1인 재진입 성공과 섞지 않는다.

## UI Iteration5 통합 검사

- UI 준비본8개+신규Camera partial/meta, 실제Scene marker2참조와 terrain-only bake를 별도 UI 변경으로 보존했다. Iteration5b zoom-out 중심 보간 수정은 baseline2개가 정확히 일치한 뒤 적용했다.
- 처음 Stage HUD6개 중2개는 옛 Initial_Stage에 NavMesh가 없어 실패했다. 테스트의 Scene 경로2개만 현행 Stage_1로 옮겼다. 실제 재검사6/6 (71.63초), Tactical8/8은 직전 근거다. UI Source/Asset은 Core 커밋에 섞지 않는다.
- 동기 run_tests는 RPC30초 timeout 뒤에도 계속 실행됐다. 실패 XML을 읽고 async 실행+완료 결과를 별도 확인했다. stale Temp 결과를 최신 결과로 쓰지 않았다.


## B7a — Stage 진행 정책/접점과 실제 콘텐츠 대기 (2026-11-14)

- Coordinator 소유 StageProgression, Root의 실제 점유 표본, world/stage-progress writer, Debugger 읽기 표시를 추가했다.33/66 생성 확인·고유 사건 ID·기술 재시도·동시 임계값 통과·점유 상실 뒤 사건 유지·두 실제 생성 후100% 최종 진입·등록 최종 보스 사망만 Victory로 이어진다. 중간보스 죽음은 최종 진입 조건이 아니다.
- 실제 source 계약 IFieldBossSpawnService, 최종 Scene의 ConfirmFinalBossReady, 권위 HP의 ReportStageEnemyDeath 접점을 명시했다. 구현 원본이 없으면 FieldBossContentNotConnected이며 생성/승리를 가짜로 확정하지 않는다.
- StageProgressionTests6/6:100% 동시통과 순서·실패66 동일ID 재시도/33중복금지·콘텐츠누락·현재점유와최종죽음·상실후미취소·world왕복/rollback. 실제 Scene은 Player1/기본CU1셀/정상진행·연결대기명시를 확인했다. b7a-progression-tests.json / b7a-live-stage-status.json.
- EnemyCatalog19종(개별ID는 b7a-enemy-content-audit.json), 확정 중간보스 Definition 지정 없음, Assets/Scenes 내 실제 Boss Scene 없음. 과거 Reference Boss를 실제로 대체하지 않았다. 따라서 B7 전체/보스 실제 생성·B8 Boss Scene/연출/사망 통합은 미완이다. 생성 포트 구현과 점유 밖 진입 위치를 제작 자료와 함께 연결해야 한다.
- B6b 추가: 같은 호출에서 Player/CU HP를 모두0으로 만든 실제 검수에서 Defeat·안전장치0·구조60초를 확인했다(b6b-simultaneous-defeat-result.json). UI 카메라 관측은743행/중간중심59/이탈0이며 CSV/Hmc 파일을 UI에 인계했다. 새 Mission 실행 기록의 같은 payload 중복쓰기1회를 제거했다.


## B8a — 공용/Field Client 시각 분리와 실제 전송 (2026-11-14)

- 코드 대조에서 Client의 MatchFieldReplicaScope가 Player와 Field에 같은 공용 시계를 쓰는 경로를 확인했다. Host Field 보류가 Client Field 표현에 전달되지 않을 가능성을 실제 접합 문제로 분리했다. 기존 anchor에 FieldTimeState를 추가하고 Player/낮밤은 common, 필드는 Field replica에 바인딩한다.
- 수신 쌍의 Match/descriptor/epoch·rate·availability와 공용pause→Fieldpause를 검증한다. 쌍이 준비되지 않으면 둘의 IsAvailable을 닫는다. FieldClock epoch 변경 때 revision이 감소하지 않도록 보강했다.
- wire 배치 변경으로 Core 소유 NGO 시작은 ProtocolVersion1이다. 외부에서 이미 실행한 연결은 바꾸지 않는다. 기존 Anchor Prefab·NetworkObject GUID와 UI의 Prefab 미커밋 변경은 보존했다.
- FieldTimeReplicationTests Play3/3, FieldSimulationClockTests Edit6/6. 첫 Editor 모드 조회는0개였고 이를 통과로 쓰지 않았다. Networking.Tests asmdef의 실제 PlayMode에서3개를 확인했다.
- 실제 Prefab+localhost UTP SDK2개에서 CoreProtocolVersion1·공용/Field NetworkVariable 전달·공용unpaused/Fieldpaused·Field만 revision14→15재개를 확인했다. NetworkFieldAnchorTests1/1. 실제2프로세스 Player/Steam 인수는 아니다.
- 실제 Stage_1 Host공용1.202269초/Field0초,서버시간anchorcommonpausedfalse/Fieldpausedtrue,해제후Fieldpausedfalse. b8a-live-anchor-held/resumed.json. commonGameSeconds0은 경과가 아니라 과거 공용 anchor값으로 보존된 정상 상태다.
- 검수 종료는 Playfalse/Scene clean이며 사용한 저장3슬롯만 백업·복구해 기존33개 checkpoint 해시 일치를 유지했다. 추가 출현 원격 경고와 실제 원격 구조 입력/생명은 아직 후속이다.


## B6c — 저장 누락·기술 재시도의 실패 경계 보강 (2026-11-14)

- 사망 알림과 Root Update 사이에도 다운된 원본을 안전장치 후보로 저장하지 않도록 CanCaptureWorld가 실제 roster 전체 생존을 검사한다. 정책 유효 저장과 개별 Feature의 HP0 codec은 구분한다.
- 같은 실패의 checkpoint slot/ID·run을 App에 기억한다. 파일 누락/다른 저장으로 교체는 정확히 거부하며 초기 전장으로 조용히 대체하지 않는다. Scene 소유권이 닫혀도 App 재시도 API는 같은 저장을 다시 읽고, 실제 준비 장벽까지30초/App 수명 토큰으로 제한한다. 저장이 처음부터 없을 때에만 초기 전장 재진입이다.
- 허용되지 않거나 취소된 GameFlow 이동이 기존 실행 의도를 먼저 초기화하지 않게 PrepareMission을 실제 전환 검증 뒤로 옮겼다. 이전 Scene의 늦은 오류가 새 Mission 상태를 덮지 않도록 실패ID도 확인한다.
- 자동 MissionEntry4/4와 MatchRecovery11/11. 실제 Stage_1에서 존재하지 않는 slot을 마지막 유효 저장 링크로 주입하고 사망: 안전장치1/같은실행/정지·캡처금지·누락사유가 관측됐다. 같은 Root 재시도 후에도 사용1이다.
- 실제 App.EndCurrentMatch로 Scene 소유권 종료 경계를 별도 주입한 뒤 App.CanRetrytrue와 재시도 누락사유·NoMatch를 확인했다. 실제 Weather rollback 실패를 재현했다고 쓰지 않는다. 전체 writer 복원은 여전히 world/weather 연결 대기다. b6c-missing-checkpoint-result.json.
- 새 Play의 무저장 사망 회귀는 실제 새 Player/같은run/사용1/HP400/App미완료false·오류null이다. b6c-no-save-recovery-result.json. 의도적으로 주입한 누락 오류2건은 b6c-errors-including-deliberate-missing-slot.json에 stack과 보존했고 성공0오류로 지우지 않았다. 저장33개 해시 복구 완료.


## B9a — 현행 출격 정책과 Core 임시 방문 제거 (2026-11-14)

- Product Plan4.4와 대조해 SpaceshipTravelService의 과거 Additive 임시 방문·방문자/귀환좌표·직접 순간이동/카메라 보정·위치 보고 경로를 제거했다.199→68행 수준으로 줄였고 필드 내부 진입은 공통 Alert로 안내한다. 결과 귀환은 GameFlow가 Match를 명시 종료한다.
- Scene 소비자 두 곳의 호출을 새 간단한 경계에 맞췄다. Enter 내부Scene/목적Marker, Exit origin/귀환Trigger/padding을 제거했다. Exit 목적지 이름은 missionSceneName이며 FormerlySerializedAs로 기존 두 출구의 Stage_1 경로가 실제 유지됨을 확인했다. 기존 UI Animator null-check는 별도 미커밋 변경으로 보존한다.
- 실제 Startup→Hub→문 ExitAsync→Stage_1: Hub actor1/camera1, 새 Stage actor/정상Mission/actor1/camera1. 실제 필드 문 EnterAsync 호출 뒤 이동0·동일Match/Scene수. b9a-startup-launch-guard-result.json.
- 같은 실제 실행에서 CU 파괴→Defeat→명시 ReturnToSpaceshipAsync: Spaceship/Match없음/actor1/camera1. b9a-defeat-to-hub-result.json. 실제 키보드나 물리 문 연출 인수로 확대하지 않는다.
- 관측 스크립트의 첫 PlayerActorIdentity namespace 누락은 실행 전 거절됐고 수정 후 실행했다. 제품 컴파일 오류는 아니다. 원본 실패도 b9a-observer-before-namespace-fix.json으로 보존했다.
- Stage Progress의 알려진 대기 이유는 Debugger에서 한국어 안내로 보여주며 원본 진단 키/실제 미연결 상태는 유지했다.


## B9b — 실제 원본과 중복되던 Core 상태 제거 (2026-11-14)

- MatchState는 MatchId/Phase/Result, MatchSnapshot은 여기에 Sequence를 더한 수명주기 읽기로 정리했다. 참가자 생성은 MatchConfig.Players를 읽는다. 실제 HP/전력/적/지역 원본과 연결되지 않은 초기 상태 타입6개·발행/소비 없는 사건5개 및 meta를 삭제했다. World 빈 폴더도 제거했다. 실제 Feature 저장 writer와 PlayerSceneAdapter.Bind/Unbind는 유지했다.
- 자동 검사는 AppMatchOwnership6, DynamicRegionBinding6, BattlefieldEditorDiagnostics3, FrontlineGeometryReport7, PlayerBindingCleanup6으로 고유28개 통과했다. Player 검사는 옛 Initial_Stage 대신 실제 Stage_1을 열고 NGO/actor 준비를 기다리도록 수정했다. 이전 Scene/한 프레임 가정의 실패 결과도 남겼다.
- 실제 Stage_1에서 Config 참가자1/실제 actor1, MatchState 공개 속성3개, 정규 생산 계획12/실제 생성12, 실제 HUD AutoActivation 호출 뒤 활성 터렛2개, Player HP400/CU HP1500/Running을 관측했다. Player·CU 보호와 CU 근처 위치 조정은 시험 중에만 적용하고 Play Stop으로 해제했다. b9b-live-model-smoke-fixed-result.json이 근거다.
- 첫 관측 스크립트는 사용하지 않는 Debug Spawner의 Executor를 골랐고 CU와 먼 위치에서 활성화를 요청해 성공을 놓쳤다. 이후 모든 Executor의 해당 Match 수를 합산하고 실제 CU 거리·준비 장벽을 확인했다. 최초 결과를 제품 실패 또는 성공으로 바꾸지 않고 b9b-smoke-zero-diagnostics.json/b9b-observer-live-recheck.json에 남겼다.
- core-runtime-map.md에 엔진 진입→Scene 조립→Coordinator→실제 Feature 원본과 읽기 순서를 정리했다. MatchResult.CompletedWave는 현재 항상0이지만 네트워크 codec/UI 사용이 있어 이번 삭제에 포함하지 않았다. 실제 Weather 전체 복원·Boss 콘텐츠·원격 Player 인수 대기는 유지한다.


## B9c / B7b — 실제 이동·전투와 Stage 사망 접합 (2026-11-14)

- 깨끗한 Stage_1의 기본 배속45.0166초에서 정규 요청4회·47개체·11개 스폰 지점·40개체 이동을 확인했다. 이 관측 구간에는 교전/피해/사망이0이므로 자연 전투·밸런스 통과라고 쓰지 않는다. b9c-natural-combat-stable-result.json.
- 별도 검수에서 실제 정규 생산 Slime 하나를 유효 NavMesh의 실제 가동 터렛 사거리로 옮겼다. 실제 사격→피해50→사망1/제거1, 새 오류0을 확인했다. 피해/사망 사실이나 HP를 직접 주입하지 않았다. b9c-turret-contact-result.json. CU 근처 Player 이동과 Player/CU 보호는 시험 전제이며 기본 Scene/Prefab에 저장하지 않았다.
- 실제 생명 경로에 IEnemyCombatSource.DeathObserved를 추가했다. EnemyHealth.Died→EnemyNetworkController.DeathConfirmed→EnemySpawnExecutor.ReportConfirmedDeath→Root.ReportStageEnemyDeath→StageProgression으로 연결한다. Game Debugger 진단이 게임 진행의 입력이 되지 않는다. Match 해제 때 구독을 먼저 제거한다. 일반 Despawn/Scene 해제는 사망을 발행하지 않는다.
- StageProgression 규칙6/6 재통과. 실제 검수에서는 저장으로 인수한 추적 ID 상황을 시험하기 위해 실제 Slime ID만 Field 사건에 연결했다. 실제 탄환 사망 후 FirstFieldBossDefeatedtrue, 팀 전투 성과50, 실제 위치12→11, 구독2→0을 확인했다. 남은11개체를 Despawn한 뒤 사망 수는1로 유지됐다. b7b-death-forwarding-result.json. 이 시험은 보스 콘텐츠를 선정하거나 실제33/66 출현을 검증한 것이 아니다.
- 첫45초 관측은 늦은 소스 컴파일/domain reload로 중단됐다. MatchState/Snapshot 주석·줄바꿈 수정 뒤 import가 완료되기 전에 시작한 것이 잘못이었다. 로그의 Begin MonoManager ReloadAssembly 뒤 PlayerBomb.Update35의 _charges null과 중단 시 NGO 정리 예외가 있었다. b9c-reload-before-error.log / b9c-interrupted-console.json에 call stack을 보존했다. Player를 임의 null-check하지 않고 Edit Mode import·새 계약/실행 assembly 반영·컴파일 정상 확인 후 깨끗한 새 Play에서 위 결과를 얻었다. 정상 재실행/종료에서 재현되지 않았으므로 첫 결과를 제품 수정 완료 증거로 삼지 않는다.
- 새 API 첫 컴파일에서 UnityEngine.EntityId/Domain.EntityId 이름 충돌이 나 명시 alias로 수정했다. 이 컴파일 실패도 최종 통과와 구분한다. 현재 컴파일 정상과 실제 실행 결과는 별도로 기록한다.
- Core 실행 지도 신규 링크1개를08_encounter 위치로 정정했다. 팀원별 인계는 예정/구현 혼재 본문을 현재 수정 파일·이유·남은 접합 중심으로 다시 썼고 이전 초안은 ignored 실행 폴더에 보존했다.


## B6d — 빌드용 복귀 실패 메뉴와 늦은 IO 경계 (2026-11-14)

- 코드 조사에서 같은 복귀 재시도의 실제 호출부가 Game Debugger에만 있음을 확인했다. 기존 AppScreenPresenter/MenuPrefab의 제목·주 버튼·Status만 재사용해 기술 실패 때 자동으로 메뉴를 연다. 기존 폰트/레이아웃/설정/허브 버튼을 쓰고 새 UI 에셋/별도 HUD를 만들지 않았다. 메뉴의 Esc 닫기로 정지된 전장만 남기지 않으며 실제 재시도 동안 입력/로딩을 기존 수명에 맡긴다.
- 외부 UI는 AppRoot.CanRetryMissionRecovery + RetryPendingMissionRecoveryAsync만 사용하면 된다. App이 살아있는 Scene의 실제 Restore와 Scene 종료 뒤 재입장을 선택한다. 같은 실패/실행/slot/CheckpointId를 유지하고 누락 저장을 새 전장으로 바꾸지 않는다. 직접 Stage 실행은 GameFlow가 없을 수 있어 기존 허브 귀환도 같은 성공 후 오류 닫힘 경계로 연결했다.
- 자동 MissionEntry4/4·MatchRecovery11/11 통과. 실제 Direct Stage_1과 Startup→메인→허브의 실제 출구→Stage_1 두 경로에서 기존 메뉴 Button.onClick을 확인했다. Scene 생존/해제 뒤 재시도 모두 사용1/1/1·같은 실행·정지/누락 저장 거부를 유지하고, 실제 허브 도착 뒤 오류/메뉴가 닫혔다. Startup 경로 최종 Player1/Camera1이다. b6d-recovery-menu-fixed-result.json / b6d-startup-recovery-menu-result.json. 실제 OS 마우스 입력이나 전체 Weather 복원 인수는 아니다.
- b6d-recovery-menu-fixed.png를 직접 보며 기존 메뉴 외형/문구/두 줄 안내가 표시되는 것을 확인했다. 세 문구의 font/fallback glyph 누락0이다. 처음 캡처는 메뉴 fade가 끝나기 전이어서 UI 검증 자료로 쓰지 않았다.
- 첫 관측 스크립트가 GameFlow 없는 직접 Scene을 가정하지 못해 null 예외로 중단했고, 그 중단이 진행 중 Scene travel을 취소해 Host stopped 로그1개를 만들었다. 제품 오류로 덮어쓰지 않고 원본을 남겼다. 수정한 관측은 공개 Scene/오류 상태를 사용하며 보이지 않거나 비활성인 허브 버튼을 억지 호출하지 않는다. 추가 관측 스크립트의 internal Services 접근1회도 실행 전 컴파일 거절이므로 게임 컴파일 실패와 구분한다.
- 별도 실제 BinaryCheckpointStore 검수에서 IO gate로 읽기를 잠깐 보류한 사이 NewMission 의도를 시작했다. 변경 전에는 늦은 실패가 새 LastMissionRecoveryError를 덮었다(actuallyPendingtrue/staleIgnoredfalse). App catch도 기존 실패ID 검증을 재사용하게 수정한 뒤 같은 순서에서 staleIgnoredtrue/LastErrornull/CanRetryfalse를 확인했다. 파일/새 게임/보스 콘텐츠를 생성하지 않았다. b6d-stale-recovery-before.json / b6d-stale-recovery-after.json.
- 누락 저장을 의도적으로 주입한 실행의 Error 로그2개씩은 예상된 거부 증거이며 오류0이라고 쓰지 않는다. Console ErrorPause만 시험 중 잠깐 끄고 원래 값으로 복원했으며 설정/Scene을 저장하지 않았다. 전체 Weather writer·실제 보스 콘텐츠·실제 원격 Player 인수 대기는 그대로다.

- B6d 최종 보완: 재시도 시도 ID를 두어 새 Mission 의도가 시작되면 이전 읽기를 Scene 이동 전에 OperationCanceledException으로 중단한다. 이전 finally도 새 시도의 실행 잠금을 해제하지 않는다. 이전 UI도 현재 복귀 오류가 없으면 실패 안내를 새 화면에 쓰지 않는다. 실제 BinaryCheckpointStore 재검수에서 pendingtrue/cancelledtrue/staleIgnoredtrue/LastErrornull을 확인했다(b6d-stale-recovery-owner-after.json).
- 자동 회귀 MissionRecoveryEntryTests2/2는 이전 IO의 Scene/오류 침범 차단, 이전 완료 뒤 새 작업이 계속 pending·재시도 불가인 상태, 최신 작업의 실제 누락 오류/재시도 재개를 검증한다. 저장 쓰기와 Scene 이동이 발생하면 실패한다. 기존 규칙15개와 합쳐 이번 관련 검사는17개이며 앞선 배치 수를 중복 합산하지 않는다.
- 최종 현재 소스의 직접 Stage 메뉴도 b6d-final-menu-result.json에서 사용1/1/1·같은실행·정지/누락거부·허브/오류닫힘을 다시 확인했다. Stage 열기 RPC1회는5초 응답 timeout이었으나 새 관측의 실제 Stage Root 준비와 최종 결과로 실제 로드를 확인했다. 같은 OpenScene을 중복 실행하지 않았다. CLI 세션89199는 완료 exit0을 확인했다. 이 응답 timeout은 제품 기능 통과와 별도로 남긴다.


## B9d — 실제 actor → Grid → 다음 요청의 공간 자료 대조 (2026-11-14)

- 실제 Stage_1 기본 배속·정규 생산을 관측했다. 밀도/점유/스폰 수를 직접 주입하지 않았다. Player/CU 보호와 CU 근처 이동·실제 HUD 자동 활성화만 검수 전제로 사용했다.

|요청|Field 초|입력/계획 revision|직전 실제 적|Grid 적|계획에 기록된 적|계획/실제 생성|후보 수치 불일치|
|---|---:|---:|---:|---:|---:|---|---:|
|1|0.505|4/4|0|0|0|12/12|0|
|2|15.513|25/25|12|12|12|12/12|0|
|3|30.516|54/54|24|24|24|12/12|0|

- 3회 모두44후보(수동OFF1/터렛 셀2 제외, 실제 분배 후보41)를 기록했다. 분배 수치123개·Grid 셀 기록이 입력과 모두 일치했고 범위 밖 적0·새 오류0이었다. 세 번째 요청의 후보 SpawnPoint-ad839fc0d4f0488fa47d05b01915fbd1은 적3/CellScore3/할당0으로 남았다. 이 하나의 기록만으로 전체 편성 변화의 원인을 단정하지 않는다.
- EnemySpawnExecutor.CaptureEnemyPositions → Root.RefreshDynamicInput(0.1 unscaled초 표본, 셀/개체 집합 변화 때만 revision) → AppRoot/MatchSession → BattlefieldSpatialRuntime → Collector/Planner의 같은 snapshot → 실제 실행과 기록 경로를 대조했다. 셀 내 소폭 이동만으로 revision을 늘리는 API는 아니다.
- 기존 Game Debugger가 같은3요청·동일 revision/시각·밀도/후보/계획을 보관함도 확인했다. 근거: b9d-spatial-feedback-result.json/CSV와 b9d-recorded-spawn-decisions.json. 최소 후보/정책 단위 검사 소스도 확인했으며 이번 관측 때문에 전체 검사를 다시 돌리지 않았다.
- Play 종료/Scene clean·컴파일 정상, 시험 전 저장33개 해시 복구를 b9d-save-cleanup.json으로 확인했다. 이 측정은 완성된 보스 한 사이클·실제2인·밸런스 검증을 대신하지 않는다.

## B7c — 등장 전·실제 처치 후 저장 순서 (2026-11-14)

- StageCheckpointGate를 Root에 연결했다. 33/66 등장 전에는 실제 CheckpointService의 Saved/IsCurrentScope 완료를 기다리고, 실제 HP 처치 후에는 다음 Update에서 저장해 같은 사망 스택의 Despawn 완료를 반영한다. Busy 직렬화·명시 재시도·epoch/종료 뒤 늦은 완료 무효화·Player 복귀 우선도 포함한다. 실제 보스 콘텐츠나 Weather writer를 대신 구현하지 않았다.
- Gate6/6·Stage8/8 자동 검사 통과. Stage 검사의 첫 실패는 테스트가 Session.Start보다 Coordinator.Start를 먼저 호출했기 때문이며 실제 App 순서에 맞춘 테스트 수정 전 원본도 보존했다(b7c-stage-before-start-order-fix.json).
- 실제 Stage_1에서 정규12개 생성/12개 이동, 실제 터렛 사격 피해50·HP 사망1, Stage 처치 확인·위치12→11·팀 성과50·구독2→0을 관측했다. 추적 사건에는 시험용 실제 Slime ID를 사용했으며 보스 제작 자료로 저장하지 않았다. 나머지11개 Despawn은 죽음 수를 늘리지 않았다.
- 처치 후 defeat-33 요청과 명시 재시도 모두 RequiredFeatureWritersUnavailable, 별도 누락 목록 world/weather, LastSavedData 없음이었다. 같은 실제 저장기를 사용한 Editor 전용 생성 호출 counter도0을 유지했다. 전체 월드 저장/실제 보스 생성 성공으로 세지 않는다.
- 원본 관측 success=false는 오류 코드와 누락 키를 한 문자열로 가정한 판정식 문제다. 원본 b7c-actual-death-save-result.json을 유지하고 두 필드를 따로 검사한 b7c-actual-death-save-verdict.json에12개 조건의 통과 근거를 남겼다. 이 보완은 재실행하거나 제품 결과를 덮어쓴 것이 아니다.
- Play/Pause/컴파일/dirty 모두false·Stage_1 Edit Mode, 기존 저장33개 해시 일치를 b7c-save-cleanup.json으로 확인했다. B7 실제 생성 서비스/Definition/점유 밖 진입 보장과 B8 최종 Scene 이전 필수 저장·도착/컷씬은 여전히 미연결이다.

## B7d — 지정 Field 사건의 실제 생성·복원 연결 (2026-11-14)

- 기존 EncounterSceneBinding이 IFieldBossSpawnService를 구현하고, Scene configuration의33/66 Definition·진입점을 EnemySpawnExecutor 공통 생성으로 전달한다. 일반 편성/예산을 다시 뽑지 않으며 AI/공격/스탯은 수정하지 않았다. Stage 사건 Source는 enum 끝에 추가해 기존 값의 번호를 보존했다.
- 사건에서 만든 stage/<event ID>를 Awake 전에 부여한다. 별도 완료 캐시 없이 현재 실제 추적 개체를 조회하므로 Enemy 원본 복원 뒤 같은 ID를 재사용하고 등장 전 월드로 되돌리면 실제로 다시 만든다. 정규 첫 생산 전 사건도 기존 생명/소환/통계 구독을 먼저 연결한다.
- Level의 월드 XY 점유 제외 Bounds를 RegionSceneBinding에 추가했다. 양의 면적 접촉 셀만 제외하고 Region ID/기존 지형 마스크를 보존한다. 진입점은 같은 Scene·NavMesh·Grid 안의 점유 불가 셀·주변 적 비겹침을 확인한다. 제작값 없이 임의 일반 몬스터/위치로 대체하지 않는다. 일반 SpawnPoint는 이런 셀을 NonCapturableCell로 제외한다.
- 새 영역 경계4/4 통과(b7d-region-exclusion-tests-fixed.json). 최초 CLI 인자 순서 오류는 실행 전 거절 결과로 보존했다. 실제 Stage_1 검수는 미저장 Slime 제작값과 실제 NavMesh 진입점2개만 사용했고, Player/CU 검수 보호를 사용했다. 실제33/66 사건2개 생성, 재요청2→2, Enemy 원본 복원 뒤 사건2개/동일ID, 등장 이전 원본 복귀0→같은 사건1개 재생성에 성공했다. 복원 도중 정상 생산12개가 추가돼 총14개였으며 재요청 전후14→14로 구분했다. 첫 관측의 총개체 수 가정 오류는 b7d-runtime-before-count-scope-fix.json에 보존했다.
- b7d-runtime-result.json은 success=true/신규Runtime 오류0이다. 전체 월드 저장이 아니라 실제 Enemy CaptureWorld/ApplyWorld의 한 Feature 검수다. 같은 실제 CheckpointService를 사용하는 Gate는 world/weather 누락으로 거부·개체 수1 유지를 확인했다. Weather writer를 가짜로 채우지 않았다.
- 기존 Game Debugger는 Source StageProgression의 실제 생성 요청3개를 계획1/실제1·EntityId/위치·CauseEventId와 함께 보관했다(b7d-debugger-record.json). 반복 응답은 새 생성 요청으로 세지 않았다.
- 추가 기존 RegionSpawnPolicyTests 회귀는 dirty Scene의 Save 확인창 때문에 타임아웃됐다. Cancel로 취소했고 통과로 세지 않는다. Temp의 다른 옛 테스트6개 결과도 이번 증거가 아니다. 취소 뒤 지연된 Pipeline만 Window/Pipeline Stop/Start로 복구했으며 마지막 eval은 Play/컴파일/실패false였다. Computer Use는 종료했다.
- 전체 Scene 재로드는 자동 승인 검토가 미저장 작업 유실 위험으로 거절했다. 우회하지 않고 이 검수가 만든 마커2개/추가 필드만 정확한 참조/좌표로 원복했다. Scene 파일 저장/변경 폐기/dirty 해제는 하지 않았다. 마지막 Scene dirty=true는 보존하며 임시 마커0, 원본 파일 변경 없음이다. QA의 기존 성장 슬롯3개만 복원해 저장33개 전체 해시 일치를 확인했다(b7d-save-cleanup.json).
- Field 생성 서비스 자체는 더 이상 미구현이 아니다. 실제 Stage_1의 확정 보스 Definition/진입로 지정·Weather 전체 writer와 최종 Boss Scene/전환/도착/컷씬은 여전히 제공/연결이 필요하며 B7/B8 전체 완료로 표시하지 않는다.

## B8b — 최종 진입 전 실제 저장·정지·입력 수명 (2026-11-14)

- Core FinalBossTransition과 Root.PrepareFinalBossTransitionAsync를 연결했다.100%·두 Field 생성·생존 참가자/복귀 미진행을 확인하고 공용 시간/로컬 입력을 임대한 뒤 before-final 슬롯의 실제 Saved/현재 scope 완료를 기다린다. ConfirmFinalBossReady 직접 우회는 거부한다.
- 준비 반환 뒤에도 호출자/Gameplay 취소를 전달하며, Root Update/종료에서 취소·월드 epoch 변경·실패 후 임대를 정리한다. 늦은 저장 성공은 무효다. 실제 도착/보스/연출 완료 확인 때 기존 Field만 보류하고 공용 시간·Player·낮밤은 진행한다. 다른 pause 소유자는 유지한다. Scene 로드/도착/컷씬 그 자체를 Core가 가짜로 수행하지 않는다.
- 현재 Editor assembly의 NUnit 메서드7개를 직접 실행해7/7 통과했다(b8b-direct-tests-result.json). 미저장 Scene을 보존하는 순수 Core 검사이며 Unity TestRunner 실행이 아니다. 저장 대기/실패·Saved라도 과거 scope·외부 취소·복원 epoch·Player 실패·늦은 완료/다른 pause·잘못된 보스 ID의 Field held rollback을 검증한다. 마지막 컴파일에서 새 소스보다 최신 assembly와 failed=false를 확인했다.
- 실제 Stage_1 Root/CheckpointService 관측은 b8b-root-runtime-result.json의 success=true·신규Runtime 오류0이다. 자연 진행이나 실제 Boss 콘텐츠 검수가 아니라, 실제 일반 Enemy 두 개의 ID와 시험용100%로 진입 경계에 도달했다. 저장 없이 확정하는 호출은 거부, 실제 world/weather 누락 뒤 pendingfalse·입력false→false·pauseOwners0·Field heldfalse·공용 시간 재진행·같은 Match/Scene 유지·추가 Scene 전환false·최종 ID 없음이었다. 가짜 writer는 넣지 않았다.
- 첫 관측은 읽기 IRegionGridSource에 TryApply를 호출해 실행 전 컴파일 거절됐다. 기존 MatchSession.UpdateRegionCapture API로 바로잡았다. 다음 관측은 Scene의 두 Executor 중 독립 시험용 대상을 잡아 준비 대기에서 종료됐고, 실제 EncounterSceneConfiguration.SpawnExecutor를 쓰는 최종 관측이 통과했다. 두 관측 오류는 별도 원본으로 보존하며 제품 컴파일/동작 통과와 섞지 않는다.
- Play/Pause/컴파일/실패false, 기존 저장33개 전체 해시 복구를 b8b-save-cleanup.json으로 확인했다. Scene dirty 표시는 보존했고 Scene 파일/외부 UI는 저장하지 않았다. Computer Use는 이번 배치에서 사용하지 않았다.
- 남은 B8은 실제 Boss Scene/보스/전원 도착/로컬 연출 소유자가 이 범위 안에서 기존 SceneTransitions.LoadAsync를 연결하고 실패 시 이동·Scene·카메라를 복구하는 것이다. 실제 보스 사망/결과·허브까지의 성공 인수와 전체 Weather 저장·실제 원격 Player 인수도 남아 있다. 이 저장/임대 경계 구현을 B8 전체 완료로 표시하지 않는다.

## B8c 자동 최종 진입과 실제 Scene 원복

Root Update의 단일 진입 시도, 필수 저장 후 additive 전환, 실제 참가자/생존 보스/사망 원본 검증, 실패 시 참가자 원위치와 소유 Scene 정리를 연결했다. 보스전 중 Field-only 저장을 차단하고 안전장치 복귀는 Scene 정리부터 한다. 최종 사망과 CU 파괴가 같은 프레임이면 CU 패배를 먼저 확인한다.

현재 assembly의 순수 NUnit 메서드10개·CU 우선 검사1개 직접 실행, 실제 Stage_1 Player의 Native Scene/부모/위치 원복과 Grid1→0→1, 미지정 driver의 최초 시도/재확보/명시 재시도·입력/정지 해제를 확인했다. 최종 컴파일 오류와 실제 검수의 신규 Runtime 오류는 없었다. 자연 진행이나 실제 Boss Scene 성공 검수가 아니며 formal Play coroutine은 이번 TestRunner로 실행하지 않았다. 실패한 초기 Editor-native 검사와 정리 기록은 로컬 실행 로그에 구분했다.

**실제 IFinalBattleSceneDriver 구현·보스 Scene/제작값·최종 결과 저장/허브 성공 인수는 남아 있다.** Core 기본 driver/Feature 어댑터는 Core 구현 잔여이며 단순 콘텐츠 대기로 분류하지 않는다. Weather 필수 writer와 원격 Player 인수 역시 별도다. [공개 API와 책임](../../../../02_architecture/02_match/stage-progression-api.md)을 따른다.

## B8d 기본 Scene driver와 실제 Enemy 원본

Core 기본 FinalBattleSceneDriver/SceneLayout과 Enemy FinalBattleBossSource를 추가했다. 실제 참가자 배치·공통 Enemy 생성·공용 MatchTime·선택적 Timeline/기존 CameraManager·실패 정리를 연결한다. 공통 생성기의 취소 검사는 유지하고 최종 준비에만 정지 중 생성을 허용한다. 실제 Boss 제작값을 지정한 것은 아니다.

실제 Stage_1에서 등록된 Executor prefab과 임시 Slime으로 준비 정지 중 생성·동일 ID 재사용·공용 시간 재개·실제 HP 사망·정리/취소 거부·저장 전 driver 거부를 확인했다. 실제 최종 Scene/연출 성공은 아직 검증하지 못했으며 Boss Scene·확정 Definition·Weather 필수 writer·원격 Player/결과 저장 인수는 남았다. [제작 순서와 호출 책임](../../../../02_architecture/02_match/stage-progression-api.md)을 확인한다.
