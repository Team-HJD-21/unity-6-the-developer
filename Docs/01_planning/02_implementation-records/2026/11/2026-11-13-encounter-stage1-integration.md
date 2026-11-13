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
