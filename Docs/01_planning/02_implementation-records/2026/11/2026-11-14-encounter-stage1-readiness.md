# Encounter / Stage1 준비 상태와 남은 접합

2026-11-14 · main8fbef4a68를 포함한 feature/core 로컬 코드 기준. 원격 PR/병합이나 Stage1 전체 완료 보고가 아니다. [자동 연결 제외·명시 API 유지 결정](2026-11-14-explicit-stage-transition-scope.md)을 우선한다.

## 배치별 현재 판정

| 배치 | 현재 코드와 근거 | 남은 범위 |
| --- | --- | --- |
| B0 진입 | Startup→허브→실제 Stage_1. GameFlow/문 출격과 App·Player·Camera 단일성 실제 확인 | 전체 제품 흐름 인수와 구분 |
| B1 Pipeline | EncounterPipeline.Run에서 Collector→Processor→Planner→Executor를 직접 조율. 게임/Debugger 공통 요청 입구 | 별도 미확정 게임 규칙을 Planner에 넣지 않음 |
| B2 실제 Enemy | 일반·스킬 소환·지정 Field 사건이 공통 생성/식별/분대/NGO/HP/저장을 사용. 실제 생성·재요청·Feature 복원 검수 | 전체 월드 복원은 B5 의존. Enemy AI/성능 최적화 완료를 뜻하지 않음 |
| B3 지역 | 정적 성과/유지·다음 해금·상실·그룹 제외/반경 재검사, 동적 CU/터렛 투영을 구현. 실제51/201셀 확보와 다음 구역 해금 확인 | 실제 확보 시험은 보호/2초 조건 사용.60초 기본값·유예/상실은 별도 규칙 검사. 자연 플레이 템포는 인수 아님 |
| B4 정규 생산/추가 출현 | Field 최초0.5초/15초 생산, 현재 Grid/전선/환경·확장 이력·공통 예산/예고 사용. 실제3요청의 입력·계획 revision 일치와12/12 생성 확인 | 원격 예고가 없어 점유 내부 추가 출현은1인 Host만 허용. 멀티에서 예고 없는 내부 생성으로 대체하지 않음 |
| B5 성장/월드 | 개인/Host 성장 슬롯 분리, 기존 binary IO/필수 writer/rollback·성장6검사·개별 Feature 실제 복원 | Weather writer가 없어 전체 월드 Save/Resume는 미완. 가짜 writer로 통과시키지 않음 |
| B6 실패/구조 | 실제1인 사망·안전장치/소진·무저장 재입장·누락 파일 거부·같은 실패 재시도 검수. 구조 조회/유지/실제 Player 복귀 포트 구현 | 실제 원격 Player·구조 입력/두 실행 인수, 전체 저장 기반 복귀는 의존 미완 |
| B7 Field 사건 |33/66 사건 식별·생성 확인/중복 방지·실제 사망·등장 전/처치 후 저장 Gate와 실제 생성 서비스 구현. 시험용 Slime2개/복원 재요청 검수 | 확정 Definition/점유 밖 진입로와 Weather 저장이 없으므로 운영 중간보스 출현 완료 아님 |
| B8 최종 접점 | 명시 EnterFinalBattleSceneAsync·저장/도착/연출/실제 개체 검사·원복 API 유지. 자동 진입/승리·결과 저장 제외 검수17개와 실제 Runtime 통과 | 실제 보스전·승패/결과 설계·Boss Scene/콘텐츠는 미완/보류. 연결용 API를 보스전 완성으로 세지 않음 |
| B9 정리/계측 | 미사용 Core 상태/사건11개·임시 허브 방문 경로 정리. 기존 Game Debugger의 요청·실제 개체·밀도 기록/Profiler 구간 사용 | 실제 보스/전체 저장/원격 참가자가 없는 한 Stage1 전체 판/동일 Build 협동 인수는 불가 |

## 현재 Scene에서 확인한 실제 입력 부재

Edit Mode에서 Scene을 저장하거나 수정하지 않고 제작값을 읽었다.

- `EncounterSceneConfiguration.fieldBoss33`/`fieldBoss66`: 둘 다 비어 있음.
- `fieldBossEntrances`:0개. 지정 생성 서비스만으로 실제 콘텐츠/위치를 대신하지 않는다.
- Root `_finalBattleSceneDriver`: 비어 있음. 승패/보스 설계가 정해지기 전에는 자동으로 채우지 않는다.
- WeatherManager1개, 해당 원본의 체크포인트 writer0개. 실제 저장 서비스의 누락 키도 `world/weather`였다.
- 실제 Player prefab은 `Assets/Prefabs/Player/Player.prefab`, 루트 NetworkObject/NetworkBehaviour는 없음. 논리 roster나 로컬 SDK2개 전송을 실제 원격 Player 통합으로 해석하지 않는다.

GitHub의 열린 PR 목록은 확인 시점0개였다. [#599 전체 저장](https://github.com/Conquer-or-Be-Condemned/CBC/issues/599)과 [#680 Boss map 초안](https://github.com/Conquer-or-Be-Condemned/CBC/issues/680)은 Open이다. Issue 본문의 과거 원거리 공격 저장 미완 기록은 현재 파일과 대조했으며, 현행 `EnemyProjectileAbility.Checkpoint.cs`는 실행/발사 대기·고정 방향·지연 이벤트를 캡처/복원한다. 과거 본문만으로 같은 구현을 다시 만들지 않는다. 전체 Save/새 Match 통합의 미완 판정은 유지한다.

## 원본이 준비되면 붙일 곳

| 원본 | 제공/확정할 내용 | Core 연결 |
| --- | --- | --- |
| Weather | 실제 현재 날씨·예고 큐·필요 난수의 capture/검증/apply/rollback writer. 김진태 작업 유지 | Root.RegisterCheckpointParticipant / 필수 world/weather. 이후 실제 파일 Save→새 Match Resume 검수 |
| Field 사건 | 확정 EnemyDefinition 두 개·카탈로그 등록·유효한 점유 제외 NavMesh 진입로 | EncounterSceneConfiguration → IFieldBossSpawnService → 공통 EnemySpawnExecutor. 저장 Gate를 건너뛰지 않음 |
| 원격 Player | 실제 권위 actor/식별·이동/생명·요청 송신자와 로컬 입력 연결 | 현재 Match actor scope·구조 service·Host 검증 포트. Core가 로컬 복제 actor를 원격 동기화로 위장하지 않음 |
| 최종 게임 진행 | 보스전·승패·사망/결과 경험 확정과 실제 Scene/콘텐츠 | 명시 Scene/완료 API만 준비. 사용자 확정 전 자동 연결은 하지 않음 |

이 입력들이 없는 동안 일반 생산·그룹 차단·지역 확보·개별 성장/Feature 검수는 가능하다. **전체 월드 저장/복원과 실제 Stage1 완성 판정은 할 수 없다.** 승인된 최종 정책을 다시 묻거나 가짜 콘텐츠로 빈자리를 메우지 않는다.

## 검증과 데이터 보존

최신 자동 연결 제외 검수는 실제 일반 Enemy HP 사망 뒤 Match가 Running이고 결과 화면이 없음을 확인했다. 시험용100%와 추적 ID를 사용했으므로 자연 진행/보스전 인수가 아니다.17개 순수 NUnit은 현재 Editor assembly에서 직접 실행한 것이며 Unity TestRunner 실행과 구분한다. 기존 저장33개 전체 해시와 사용자 dirty Scene·staging8개를 보존했다.

세부 경로/관측은 [통합 기록](2026-11-13-encounter-stage1-integration.md), [Core 지도](../../../../02_architecture/01_core/core-runtime-map.md), [Stage API](../../../../02_architecture/02_match/stage-progression-api.md)를 따른다. 로컬 원본 측정/오류/복구 자료는 `Docs/Local/Execution/EncounterStage1-20261113`에 보관한다.
