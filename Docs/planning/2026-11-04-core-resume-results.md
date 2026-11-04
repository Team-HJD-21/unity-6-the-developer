# 2026-11-04 Core 전용 재개 배치 결과

관련 Issue: #598 시간/필드 정지 · #599 저장 · #600 게임 사이클 · #601 Steam 설계 · #602 Core 검토/문서화.

## 이번에 실행되는 결과물

- 신규 `Assets/Scenes/Bootstrap/Startup.unity`에서 기존 Main 시각 요소로 시작하고 Start를 누르면 실제 우주선 Scene에 도착한다.
- App의 GameFlowController가 시작/우주선/Mission 입장/완전 재시작/명시 귀환을 조율한다. 파일 로드 후 실제 Match 준비가 실패하면 Faulted와 원인을 남긴다.
- Game Debugger의 Overview에 APP · GAME FLOW가 추가되어 위치·전환·실패와 명시 출격/귀환 API를 확인한다. 실제 authored 우주선 콘솔 UX와 구분한다.
- AppBootstrap이 실제 binary 저장소와 codec을 주입한다. Match 조립부는 명시 scope factory로 participant와 필수 writer 목록을 연결할 수 있다.
- 필수 writer가 없으면 직접 저장/복원과 이벤트 요청 모두 거부하며 누락 키를 조회할 수 있다. 부분 writer만으로 전체 게임 저장 성공을 표시하지 않는다.
- Core README를 현재 구조/실행 경로 기준으로 다시 작성하고 상세 함수·수명·소유권·팀 접점 지도를 추가했다.

## 원래 목표 B0~B7과 현재 상태

| 배치 | Core에서 구현/확인한 것 | 미완료·보류 이유 |
| --- | --- | --- |
| B0 기준/목록 | 현재 코드와 기존 문서 대조, main bf3db829 참조·11/3 회의 확인, 비 Core512개 파일 보존 hash 기록 | 현재 작업 브랜치에 최신 main 재병합/rebase는 하지 않음. 보류 Feature 확장과 승인된 main을 합치는 후속 통합 필요 |
| B1 구조/API | GameFlow 절차/Unity host/시작 Presenter 분리, checkpoint scope·필수 writer·App 저장소 분리, 새 API 한국어 설명 | 기존 Bootstrap→FieldTurret 구체 타입 의존은 담당 계약 대기. 전체 아키텍처 전면 교체를 수행한 것은 아님 |
| B2 시간/정지 | 기존 공용 clock/scheduler/참가자 정책 유지, 해당 순수 Core 검사 재실행 | 실제 Player 출입·Enemy/터렛 진행 정지 consumer가 보류되어 실제 전장 전체 정지 인수 대기 |
| B3 Weather | 기존 환경/예약 port와 읽기 projection 유지·실제 연결 절차 문서화 | 김진태의 Weather/Time 코드 수정·재적용 안 함. 실제 규칙/낮밤·소비 연결 대기 |
| B4 저장 | binary IO·실행/관찰 API, required writer gate, Match scope factory, App 파일 읽기/저장소 주입·종료 | Stage 조립의 실제 Player/Enemy/터렛/CU/Weather writer·복원 전후 정리 callback 합의 필요. 전체 재실행 Resume 미연결 |
| B5 게임 사이클 | 기존 시각 Startup→우주선 실제 Play 확인, 출격/준비 확인/실패 귀환·재시작 조율 API, 기존 공용 화면 route 연결 | Mission의 터렛 Match 바인딩이 HOLD. authored 허브 Player/포털/콘솔과 실제 전투 한 사이클은 별도 연결/인수 |
| B6 Steam | 기존 설계를 실제 Core 진입점·공식 auth/callback/로비 문서와 재대조 | SDK/계정·Backend·인터넷 두 실행 파일 구현은 이번 조사 범위 아님 |
| B7 검토/문서 | C#437개/asmdef13개 inventory, 참조 순환·순수 계층 import 점검, 읽는 순서·함수 흐름·이 문서 | PL의 시각/조작·Core 독해 인수, 실제 2인/제품 통합·빌드는 남음 |

## 검증 결과

Unity6000.3.23f1의 최종 선택 실행: **73/73 Passed, 실패0·skip0, 프로세스 종료0, C# 컴파일 오류0**.

- 신규 GameFlow 계약5개, checkpoint scope/필수 writer3개, Startup Scene/실제 버튼2개를 포함한다.
- 실제 시작 버튼 검사는 EditMode runner의 EnterPlayMode로 Startup을 실행해 버튼 클릭→우주선 도착→Match 미시작 상태를 확인했다.
- 저장 실행/관찰·실제 binary IO·원본 공간 participant·공용 시간/정지 정책·Scene 전환·App 종료/Match 소유권을 직접 영향 묶음으로 확인했다.
- 초기 실행의 Startup 검사 설정 오류는 수정하고 같은 묶음을 다시 실행해 통과했다. 실패 기록도 로컬 증거에 보존한다.
- 초기 제작 도구의 빈 Scene/additive 제약은 수정했다. 새 Startup 생성 성공 후 원본 Scene을 저장하지 않았음을 확인한다.
- 전체 프로젝트 모든 테스트, 실제 두 실행 파일/Steam, Mission 전투·전체 저장 복원, 새 Windows Build는 실행하지 않았다. HOLD 통합 검사를 강제로 통과시키지 않았다.

로컬 증거: `Docs/Local/Validation/CoreResume-20261104/selected-tests-2.xml`, `selected-tests-2.log`, `selected-tests-2.exit`, `authoring-2.log`. 정적 inventory/보존 증거는 `Docs/Local/Planning/CoreResume-20261104/`.

## PL 수동 확인 순서

1. `Tools > TeamHJD > The Developer > Open Core Startup Scene`으로 시작 화면을 열고 Play한다. 기존 로고/배경/폰트/버튼 배치와 가독성을 본다. Start/Exit 외 미연결 버튼은 비활성이다.
2. Start를 눌러 SpaceShipGrayBox로 이동하는지 본다. Game Debugger Overview의 APP · GAME FLOW가 MainMenu→Spaceship으로 바뀌는지 확인한다. 실제 허브 이동/콘솔은 담당자 구현 범위다.
3. Overview의 Mission Scene 기본값을 Initial_Stage로 두고 선택 Mission 출격을 요청한다. 현재 CORE-FEATURE-HOLD가 실제 조립을 막을 수 있다. 이때 Mission 성공이 아니라 Faulted/원인이 남아야 한다. 이 Mission 실패 후 실제 귀환은 아직 수동으로 확인해야 한다.
4. `Match 종료 / 우주선 귀환`, 이어 `시작 화면으로`를 확인한다. 원격 참가자 상태에서는 기존 Host/전환 제한을 우회하지 않아야 한다.
5. README→함수/수명 지도→시간/저장 API 순서로 읽는다. 저장 scope factory는 실제 writer 계약이 준비될 때 호출하며 현재 Load 버튼으로 전체 복원이 된다고 기대하지 않는다.
6. 담당자 계약 연결 후에만 실제 두 참가자 출입/필드 정지, 환경 소비/Spawn, 전체 checkpoint 재실행을 검증한다. 과거 Scene 인수나 fixture 결과를 현재 전체 통합으로 옮기지 않는다.

## 다음 실제 연결의 소유자와 이유

| 연결 대상 | 필요한 제공/합의 | Core가 이번에 수정하지 않은 이유 |
| --- | --- | --- |
| FieldTurret | HOLD 표식의 소유/ID/Match lease/관측 API와 실제 writer | 담당자 동의 없는 수정·stash 재적용 금지. 원본 보존 |
| World/Weather | 공용 시간/예약 consumer, 환경 원본, 허브 PlayerStart/포털/콘솔 | 현재 작성 중인 Feature 코드/Scene을 임의 변경하지 않음 |
| Player/UI | 실제 권위 actor/출입 보고, Player 원본 writer와 공용 HP 관련 계약 | Player/Combat HUD 원본을 Core가 대신 구현하지 않음 |
| Enemy/Encounter | 실제 source/환경 소비·진행 정지·원본 writer/객체 재생성 | Enemy 정책·AI/Spawner는 담당자 소유 |
| Profile/Backend | 성장/재화 원본과 영속 revision, 실제 인증 | 현재 Fake에는 전체 경험치/성장 원본이 없고 Backend 미구현 |

Save/Restore 발동 사건은 계속 미선정이다. 자동 스폰·저장 규칙이나 방벽·암살자·지원형 AI 정책을 임의 추가하지 않았다. 11/3 회의의 구체 터렛 검증 지원 목록도 후속 확인 대상으로 기록한다.

## 공유 시 주의할 실제 변경

새 Startup을 Build Settings 첫 항목에 등록했다. 기존 Initial_Stage/허브 Scene 파일·GUID와 타 팀원 소스는 변경하지 않았다. 현재 Editor에서 Initial_Stage를 직접 Play하는 실행 경로는 유지된다. source 검색에서 Scene 숫자 index를 하드코딩해 로드하는 기존 경로는 발견되지 않았다.

현재 Core/App 테스트가 통과했어도 프로젝트의 기본 개발 서비스는 Fake Platform/Profile이다. 새 게임 흐름을 온라인 인증 완료나 실제 전체 게임 완주라고 표시하지 않는다. PR·구현 push·merge는 별도 요청 전까지 진행하지 않는다.

## 로컬 커밋 분리

- c176a70f: App 저장소·필수 writer·Match 체크포인트 scope 및 해당 AppRoot API.
- dad980d3: 기존 Main 시각 Startup·게임 흐름/실제 조립 확인·Debugger·관련 검사.
- 문서 정리는 위 구현 커밋과 별도 커밋으로 보관한다. 원격 구현 push/신규 PR/merge는 하지 않았다.

검증 후 생성된 새 Unity YAML/메타의 빈 값 뒤 공백만 정리했다. GUID·참조·실제 값·C# 동작은 바꾸지 않았다.
