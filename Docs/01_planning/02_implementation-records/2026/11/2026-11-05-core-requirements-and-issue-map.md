# 이번 Core 통합 범위와 Issue 대응

**현재 판정:** [최종 로컬 안내](2026-11-05-core-local-review.md). 이슈 상태 표는 앞선 조회 기록이며 이번 정리에서 원격 이슈를 새로 조회/수정/종료하지 않았다.

**추가 승인 후속:** [낮밤·Steam·시작 우주선·로딩의 최신 결과](2026-11-05-day-night-steam-results.md)를 먼저 본다. 아래는 앞선 Core 목표의 범위/증거이며, 당시 미구현이던 낮밤 producer와 Steam SDK는 후속에서 보완했다.

기준: main `1a03e9a91` 이후 `codex/core-feature-integration-20261105`. 로컬 구현·자동 검사 기록이며 **PR/push/merge 전**이다. 이전 미완료 판정을 취소한 [재검토](2026-11-05-core-completion-reaudit.md)의 누락을 실제로 보완하고, 제품/다른 담당 의존은 분리한다.

## 스프린트·회의·대화 요구별 실제 위치

| 요구 | 현재 코드/함수 | 이번 처리·남는 범위 | Issue |
| --- | --- | --- | --- |
| 부팅→시작 버튼→우주선→Mission→귀환 | AppBootstrap, Startup composition, GameFlowController, AppGameFlowHost | 실제 Scene 경로·기존 Main 시각 요소·Startup 첫 Build Scene. 임시 내부 방문과 Match 종료 귀환은 다름 | #600/#556 |
| 현재 Core와 Scene 창구 | InitialStageCompositionRoot.Compose/ComposeAuthority, InitialStageNetworkBinding | 기존 PlayerStart/카메라/현행 EnemySpawnRoot·터렛/CU 연결. 참조를 임의 생성/치환하지 않고 준비 전에 검증 | #444/#602 |
| Host 연결과 게임 시작 분리 | TryStartHost/TryPrepareMatch/TryStartMatch, NetworkMatchCoordinator | 참가자 유무로 무조건 자동 시작하지 않음. 1인 개발 진입만 같은 API 순서 사용. Host의 출격 호출은 공유 로드·전원 준비 후 시작 | #442 |
| 협동 Scene/다음 판 | NgoSceneFlow, StartAfterSceneTravelAsync | 실제 두 프로세스에서 협동 Match3개·공유 귀환2회·Client 시계/환경 교체 검증. SOS/도중 참가/Host Migration 제외 | #442/#556/#600 |
| 실제 Player 원격 생성·등록 | RegisterAuthorityPlayerActor, PlayerActorIdentity, PlayerMatchPresence | Host의 실제 actor와 등록 lease 연결 제공. **전체 Player NetworkBehaviour/이동·공격 복제는 Player 담당**. Client 좌표를 권위 자료로 쓰지 않음 | #442/#528 |
| 전원 내부일 때 필드 정지 | MatchPresenceService, MatchTimeService, MatchFieldSceneScope | 1인은 내부면 정지, 협동은 모두 내부일 때 정지. 통신·UI·허브 이동 유지. Host 포털 이동/보고는 후속 승인으로 연결. Client 요청 승인과 제작 미션 콘솔은 별도 | #598 |
| 터렛 검증용 Core layer | TurretCommandGateway/Service, TurretInteractionAuthorization, TurretFeatureCommandExecutor, TurretMatchBinding | HUD 의도→현재 참가자·실제 Host 위치/생존/거리→터렛 API. HP/전력/생성 원본을 Core에 복제하지 않음. 렌치는 실제 Host 무기 경로에서 검증 | #495/#528 |
| CU 명시 초기화·지역 | ConfigureUnlockZones, RegionSceneBinding, MatchSpatialCheckpointParticipant | 현재 Match lease 이후 CU 위치/제작 구역 수로 정적 초기화. 동적 삼각형은 점유 셀 계산 입력이며 RegionGrid가 원본 | #495 |
| 전선·Grid·Spawn 분산 | BattlefieldSpatialRuntime, SpawnPointSpatialPolicy, SpawnDistributionHistory | 실제 현재 밀도·셀/지점 서비스 이력·전선 거리로 계획하고 성공한 생성만 이력 반영. Influence Map·최종 Director 우세 정책은 추가하지 않음 | #496/#420 |
| 정식 Encounter API | EncounterRequest/Service, EnemyNetworkTestLauncher.Submit | 자동 첫 요청·Debugger·UI·Scene 같은 경로. Host·Match·발신자·중복·실패 결과. 요청 성공과 실제 생성 수 구분 | #496 |
| 추가 스폰 | SpawnSourceRegistry, RequestOrigin/TryScheduleOrigin, SpawnCompositionPlanner.TryPlanOrigin | authored point·world point/area·등록 source 및 게임 초 예약. source 소멸은 pending 취소. 실제 지면/공중 연출 어댑터가 없으면 해당 방식 거절 | #559 |
| 날씨/시간과 Encounter | CountTick.BindScheduler, WeatherManager.Core, MatchEnvironmentSceneScope | Core 예약·초기/변경 환경 발행·Client 표현·다음 SpawnPlan 조건. **날씨 선택 난수·고유 큐 writer는 김진태 담당** | #558/#598 |
| 공용 Random | RandomService/RandomBinding, Scene RandomConsumerScope | 이름별 흐름·등록/해제·Host 권한·난수 writer. Player/Enemy/터렛 연결; 날씨 선택 교체는 김진태 담당 | #598/#602 |
| Player 공용 입력·HP·회복 | PlayerControlState, PlayerInfo, PlayerSceneAdapter | 로컬 UI/사망 잠금 분리, 공통 피해 계약, 필드 시간/Client 쓰기 거부. 새 부활/성장 정책은 만들지 않음 | #555/#598 |
| Player 무기·스킬·장치 | PlayerAttack, PlayerSkillController 및 .Checkpoint, FieldSimulationTime | 최신 입력 우선순위/제작 preview 유지. 쿨다운·확정 중 타격·탄환·장치·수리 큐·선택/충전 상태 저장 | #599/#602 |
| Enemy→Player 감속 | PlayerSlowEffects, PlayerMovement | 기존 계획 Player 항목6 보완. 가장 강한 감속 적용·독립 만료·일반 이동/Dash 적용·공용 시간·슬롯별 writer | #598/#599 |
| 터렛 시간·실제 상태 | FieldTurretController/FieldTurretBase .Simulation/.Checkpoint, 무기 writer | 전력·반환 대기/자동 큐·냉각·강화·HP·표적 예약·실제 탄환·미사일. #617 복구 우선 정책 보존 | #495/#598/#599 |
| Enemy 시간·실제 상태 | EnemySpawnExecutor.Simulation/.Checkpoint, EnemyNetworkController.Checkpoint | 최신 Runtime Variant·HP/AI/분대/경로/실제 탄환. 원거리 발사 전후·지연 이벤트 v2 추가. 상태를 생략한 부분 저장 성공 금지 | #598/#599 |
| Match Management | MatchSession/MatchCoordinator/StoryModeRules | 실제 CU 파괴→단일 패배 결과→게임 작업 중지. App 통신/결과 전달 유지. 미정 Boss 승리·보상 정책은 추가하지 않음 | #555 |
| 실제 binary 저장/복원 | CheckpointService/BinaryCheckpointStore, Feature writers | Prepare→Apply→Resolve·rollback, 논리 ID 재연결. 기본 경험치/재화 보존·재화 선택 롤백 계약. 제품 경제 원본/Backend는 별도 | #599 |
| 재실행 슬롯 복원 | AppRoot.ResumeCheckpointAsync | 실제 파일→새 Scene/새 1인 Story Match. 성공·writer 거절·읽기 취소·입장 도중 취소 검사. **운영 Scene은 Weather writer 없으므로 전체 저장 차단** | #599/#600 |
| 저장 관찰자와 실행자 | CheckpointRequestChannel/Trigger/MatchCheckpointScope | 명시 API/관찰·요청 경계까지 제공. 어느 사건에서 자동 저장/복원할지는 사용자 지시대로 선정하지 않음 | #599 |
| 공용 화면과 기존 HUD | ScreenManager/InputLockService/AppScreenPresenter/AppScreenStyle | 기존 HUD 유지·NEXON OTF/기존 sprite 재사용. 새 디자인 임의 도입 안 함. 최종 해상도·가독성은 PL 확인 | #557/#528 |
| Steam SDK·도입 설계 | SteamPlatformService, steam-api.md, IPlatformService/INetworkSession | AppID4336820·SDK/1계정·로비 확인. Steam 게임 transport/Backend/2계정 인터넷은 별도 후속 | #601 |
| Core 전체 정리·주석/문서 | core-lifecycle-and-api-guide, core-feature-integration-guide, API별 문서 | 실제 함수·수명·누가 호출/해제하는지 안내. AppServices를 전역 검색기로 만들지 않고 Feature 상태 원본 유지 | #444/#602 |
| 이전 stash/최신 팀 코드 보존 | 개인 해시3개·stash10개, 기능별 대응 기록 | 통째로 apply/pop하지 않음. 보관 Player preview/선택 로직 대신 최신 main 유지. 공용 헬퍼·partial·scoped 관측으로 대체된 항목과 미이식 테스트/진태 작업을 구분 | #602 |

## Issue를 어떻게 정리할 것인가

코드가 로컬에만 있는 Issue를 main 완료로 닫지 않는다. 다음 PR을 만들 때 아래 표의 실제 조건과 PL 확인을 함께 반영한다. 이번 목표는 PR 발행을 하지 않는 지시를 유지한다.

| Issue | 처리 방향 | 남는 이유/최종 확인 |
| --- | --- | --- |
| #403 | **종료함** | main의 해당 Monster 계층 삭제로 문제 경로 소멸, 실제 Runtime Scene/두 프로세스에서 같은 직렬화 오류 없음. 신규 기능 PR과 구분 |
| #444 | 이번 Core 정리 PR의 종료 후보 | AppServices/Factory·실제 로더/저장소와 Fake Profile/Platform 경계 문서 인수 |
| #495 | 터렛 통합 PR 종료 후보 | 실제 터렛 원본·CU 초기화·종료/재진입 근거와 PL 수동 확인 |
| #496 | Encounter 연결 PR 종료 후보 | 게임 자동 요청·Debugger·world/source 요청/결과·재시작 근거 |
| #555 | 관리 연결 PR 종료 후보 | CU 패배·작업 중지·단일 완료/정리 검사. 미정 승리 규칙을 이 Issue에 몰아넣지 않음 |
| #556 | Scene 통합 PR 종료 후보 | 공유 로드·전원 준비·재출격·실패/취소 정리 및 실제 두 실행 파일 근거 |
| #557 | 화면 PR의 PL 확인 후 종료 후보 | 자동 테스트가 해상도별 가독성·마우스 조작 인수를 대신하지 않음 |
| #528 | 유지 또는 UI 인수 뒤 종료 | **실제 Player 서버 actor와 Client 요청/HUD 인수**, 새 객체/구독 교체 확인이 필요. Core fixture 성공을 제품 Player 통합으로 쓰지 않음 |
| #558 | Core 환경 전달 PR 종료 후보 | 실제 Weather 변경→계획 조건·Client 환경 revision 검증. Weather 고유 writer 의존은 #599이며 구분 |
| #559 | 계약 범위 PR 종료 후보 | Immediate world/source와 미지원 지면/공중 어댑터의 거절을 구분. 실제 나무/공중 콘텐츠는 별도 Feature 후속 |
| #598 | 시간 기반과 실제 필드 연결 PR의 종료 후보 | 감속/지연 발사까지 검증. Host 포털 보고는 후속 구현. Client 요청·Weather 고유 구현은 구분 |
| #599 | 유지 | 운영 Weather writer 미등록. 대역을 넣은 새 Match 파일 복원 성공만으로 제품 전체 저장을 완료 처리하지 않음 |
| #600 | Core 사이클 PR 종료 후보 | 실제 Startup/우주선/Mission/귀환·새 슬롯 API 증거. Host 포털 연결 완료. 진태 제작 콘솔과 저장 준비 조건 명시 |
| #601 | 조사/설계 문서 검토 후 종료 후보 | SDK를 설치해야 닫는 구현 Issue가 아니다. PL의 SDK/접속 설계 검토가 마지막 조건 |
| #602 | 정리 문서 PR과 PL 코드 읽기 확인 후 종료 후보 | 소유권·실제 API·stash 대응·수동/외부 의존이 일치해야 함 |
| #442 | Core 기반 PR 종료 후보 | 실제 2인 설정/준비·anchor/시간/환경·Scene/다음 판 근거. 원격 Player 전투·Steam 전체를 뜻하지 않음 |
| #560 | 유지 | 팀 Milestone/Gate/담당자 인수는 코드 변경만으로 확정할 수 없음 |
| #420 | 유지 | 팀 공통 Build·최종 목표/승리·플레이 결과가 필요. Core 단독 검사는 팀 전체 Stage1 인수가 아님 |

## 이번에 임의로 추가하지 않는 것

진태의 Random 선택/Weather 고유 writer/포털·콘솔, Player NetworkBehaviour 본체, 미정 자동 저장 사건·Boss 승리·경제/성장/부활 정책, 실제 Steam/Backend·SOS·Host Migration·PvP·최종 Director 밸런싱은 완료율을 위해 대체 구현하지 않는다. 공용 API가 있고 담당 인수 또는 제품 결정이 필요한 항목과 Core에서 실제 빠졌던 감속·공격 상태·파일 복원·공유 전환 보완을 혼동하지 않는다.
