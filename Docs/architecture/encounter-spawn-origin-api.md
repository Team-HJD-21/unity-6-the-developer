# Encounter 생성 origin API

현재 단계: B8 Core 경계와 기존 Enemy 생성 연결 구현·직접 검사 완료. 실제 나무 AI/지면·공중 연출은 별도 Feature다.

## 현재 구현

- `EncounterRequest.Origin`은 Automatic, AuthoredPoint, DynamicSource, WorldPoint, WorldArea를 구분한다. 기존 호출은 Automatic으로 유지된다.
- `SpawnArea`는 XY 중심·폭·높이와 표현 Z를 담는다. Grid 경계와 지형/NavMesh 유효성은 같은 개념이 아니다.
- `CauseEventId`는 요청 GUID와 별개의 원인 사건이다. origin·전달 모드·원인까지 payload에 포함해 같은 요청 ID의 다른 의도를 충돌로 처리한다.
- `EncounterSpawnSourceRegistry`는 Host Match의 논리 source ID·등록 세대·위치·취소 lease를 관리한다. source 제거와 Match 취소는 대기 수명을 닫으며 이미 생성한 적의 삭제 권한을 뜻하지 않는다.
- 같은 논리 ID를 다시 등록해도 새 generation을 받는다. 삭제 source 또는 이전 Match를 참조하는 늦은 요청은 새 source로 바뀌지 않는다.
- 공통 `EncounterRequestService`는 명시적 origin을 지원하는 `IOriginAwareEncounterExecutor`와 권위 resolver를 요구한다. 없는 연결을 자동 지점 생성으로 대체하지 않고 UnsupportedOrigin/OriginUnavailable로 거부한다. resolver가 원래 의도를 교체하거나 수명 종료 후 실행할 수도 없다.
- `EncounterSceneConfiguration`은 실제 Enemy 생성기를 제작 참조로 지정한다. 현재 Initial_Stage는 EnemySpawnRoot prefab 인스턴스를 사용한다. 기존 다른 제작 Executor는 제거하지 않았다. 재로드 뒤 App의 persistent consumer에 새 Scene 참조를 전달한다.
- standalone `EncounterRuntime`의 자동 검색은 활성 Scene에 생성기가 정확히 하나일 때만 허용한다. 여러 개면 명시 연결이 필요하다.

## 조립과 호출

Host의 `MatchSession.InitializeSpawnSources`가 Match gameplay 취소 수명에 registry를 만든다. Bootstrap이 `IEncounterSpawnContextConsumer.BindSpawnContext`로 registry·clock·seed·취소를 Enemy consumer에 전달한다. Client에는 쓰기 registry를 만들지 않는다. `AppRoot.CurrentSpawnSources`는 Bootstrap/도구 진입점이며 일반 Feature는 필요한 Port를 조립부에서 주입받는다.

`IEncounterOriginRequestTarget.RequestOrigin`은 consumer의 현재 권위 참가자로 요청을 만들고 공통 서비스를 통과한다. 명시적 요청 GUID를 재사용해야 하면 `IEncounterRequestService.Submit(request, verifiedIssuer)`을 사용한다. 호출자 입력을 검증된 원격 발신자로 간주하지 않는다. 현재 원격 임의 좌표 요청 RPC는 제공하지 않는다.

`IEncounterSpawnSourceRegistry.Register(match, sourceId, area)`가 반환한 lease를 실제 source가 수명 동안 보관한다. 이동은 `TryUpdate`, 제거는 `Dispose`다. `EncounterSpawnOrigin.FromSource(lease.Reference)`로 요청하며 실제 생성 시점의 등록 위치를 다시 해석한다. World source 없는 사건은 `AtWorld(new SpawnArea(...))`, 특정 제작 지점은 `AtPoint(id)`를 사용한다.

### 지연 요청

`TryScheduleOrigin(..., delayGameSeconds, out requestId)` 또는 Enemy consumer의 `TryScheduleEncounter(request, delay, out reservation)`을 사용한다. 기존 consumer의 Match 범위 coroutine이며 새 전역 scheduler/Spawner가 아니다.

- 대기는 Match clock을 사용하므로 Time.timeScale=0 동안 시간이 진행하지 않는다. 초기 연결 대기의 realtime과 구분한다.
- 대기 중에는 실제 Enemy가 없고 생존/공간/사망 집계에 포함하지 않는다. 실제 실행할 때 최신 Battlefield·환경·source를 조회한다.
- 동시 대기는 최대 64개다. 동일 GUID의 동일 payload/지연은 같은 핸들, 변경된 payload/지연은 거부한다. 실행 후 중복은 공통 요청 ledger가 막는다.
- source 해제·Match 완료/정리·호출자 Cancel은 대기를 취소한다. 이미 생성된 적은 그대로 두며 Match의 기존 생성/정리 수명을 따른다.
- reservation의 Result와 IsCancelled를 구분한다. 대기 등록 성공을 실제 생성 성공으로 해석하지 않는다.

## 실제 소비와 소유권

Core는 새 Spawner를 만들거나 Instantiate하지 않는다. Enemy Planner의 분대 구성·공간 배분, 기존 EnemySpawnExecutor의 NGO 생성·분대·생존/사망/제거·CaptureEnemyPositions·Match 정리를 재사용한다.

명시 위치/영역은 요청 영역과 겹치는 최대 4,096개의 Grid 셀 후보를 만든다. 그 이상은 계획 거부이며 다른 지점으로 대체하지 않는다. 기존 적 밀도/서비스 이력·전선 정책과 완전 preset 세트를 재사용한다. 실제 World 생성도 해당 셀의 실제 서비스 부하를 기록한다.

Enemy 실행 경계는 적마다 최대 16회 후보를 검사한다. XY 요청 영역·현재 Grid·Player/Turret 점유 제외·기존 XY NavMesh 표면과 대조하고 실패 이유/실제 수를 보고한다. NavMesh 표면 적합성이 최종 목표까지 완전한 경로를 보장한다는 뜻은 아니다. Visual Z는 표현 위치이며 NavMesh 검사 Z=0과 분리한다. 기본 Z=0이고 콘텐츠의 적합한 Z/지형·목표 경로는 담당 Feature가 검토한다.

동적 source는 임시 SpawnPoint로 만들지 않는다. World 명령/실제 객체의 PointId는 비어 있고 별도 origin/source와 실제 좌표를 기록한다. 내부 `world-cell/x/y`는 Grid 후보 식별자이며 제작 지점 ID가 아니다.

| 전달 방식 | 현재 전이와 집계 |
| --- | --- |
| Immediate | 선택적 대기 → 공통 계획 → NGO 생성·공통 추적/분대 등록 → 기존 전투 참여. source가 생성 중 제거되면 이미 생성된 적은 유지하고 남은 실행은 중단하여 실제 부분 수량 반환 |
| GroundEmergence / AirDrop | UnsupportedDelivery, 실제 객체/생존 수 증가 0. 지면 출현·투하·착지/참여 전이는 아직 실제 adapter가 없으므로 즉시 지상 생성으로 대체하지 않음 |

나무를 심는 AI·나무 콘텐츠·투하 VFX/착지·최종 정책은 조수빈/김진태 Feature 범위다. 해당 adapter를 연결할 때 생성 전 대기/비참여 객체와 실제 전투 참여의 등록 시점을 합의해야 한다.

## 관측

Game Debugger Encounter의 **추가 생성 · World origin / Match 시간 지연**에서 XY·크기·Z·전달 방식·지연을 지정한다. 원본 Scene/에셋이나 적을 도구에서 직접 생성하지 않는다. 호스트의 기존 공통 요청을 사용한다.

Timeline은 RequestScheduled/RequestCancelled와 실행 사건을 구분한다. 예약은 진단 실행 번호가 없지만 GUID로 연결된다. 요청/사건 상세는 GUID·원인·origin·source generation, Statistics는 실행 당시 영역·환경·계획과 실제 객체를 표시한다. 실제 World 객체도 Scene 선택이 가능하며 Grid 후보를 실제 Scene 오브젝트로 오인하여 선택하지 않는다.

JSON과 events CSV는 새 상관관계를 보존한다. CSV 열은 뒤에 추가했고 기존 JSON의 미계측 자료를 추정하지 않는다. 기존 Run content version/seed/Build label을 함께 사용하며 전체 결정론 replay를 주장하지 않는다.

Profiler의 `TeamHJD.Encounter.Plan` / `SpawnAndCommit`으로 계획 및 실제 생성/서비스 이력 기록 구간을 관측한다. 요청별 계획/생성 시간은 Statistics/JSON에 보존하고 미실행 구간은 N/A다. `TeamHJD.Battlefield.SampleAndPublish` / `TopologyAndPublish`는 Unity 조립부의 공간 수집/발행 비용이다. Domain/Application에는 Unity 계측 의존성을 넣지 않았다.

## 현재 검증

`b8-origin-foundation.xml`: Unity 6000.3.23f1, 종료 0, 11/11. source 세대/취소·권위·origin 충돌/재전송 계약 4개, 기존 요청 회귀 6개, 실제 Initial_Stage 재시작/Hub 복귀 1개. 마지막 검사는 재로드 전후 제작된 실행 대상과 consumer 참조가 같은지 직접 확인한다. 포인트 밖 실제 생성 성공 증거로 확대하지 않는다.

`b8-origin-acceptance.xml`: 종료 0, 8/8. Core source/거부/재전송 6개, World 셀 실제 이력/분산 1개, **실제 Scene S3** 1개. S3는 SpawnPoint에서 떨어진 실제 NavMesh 위치에서 WorldPoint/WorldArea/명시 지점/source 생성, source 제거·지연 취소, TimeScale 정지 중 대기 유지·정상 지연 생성, 생성 도중 source 해제의 1마리 부분 결과·중복 생성 0, 공간·사망 추적, CU 완료 전체 정리·재시작 및 JSON 상관관계를 확인했다.

최초 S3 두 번은 EnterPlayMode domain reload를 가로지르는 검사 coroutine의 captured-local closure가 복구되지 않아 실패했다. predicate/local function을 reload 뒤 호출하는 일반 helper로 옮겼다. 제품 예외를 무시하거나 source/공통 추적 검사를 제거하지 않았다. 실패 로그/XML은 보존한다. 사용자의 화면 조작·UI 외형 인수와 두 실행 파일 네트워크 인수는 별도다.

Scene은 Unity API로 구성한 뒤 필요한 참조/필수 WeatherChangeEvent component만 보존했다. headless 저장이 만든 Canvas 크기/배율·기타 제작 값 변경은 원래 값으로 되돌렸다.
