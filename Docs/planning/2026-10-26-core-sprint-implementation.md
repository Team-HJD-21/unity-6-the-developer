# Core 5·6주차 배치 구현 기록

기준일: 2026-10-26. 목표: 기존 후속을 최신 main에 통합하고 CU 초기화·네트워크/Match 운영·Scene/UI·환경/추가 생성 경계를 순서대로 연결한다. 목표와 실제 완료를 구분한다.

## 기준과 커밋

- 시작 main: `1560df6c`. 기존 후속 source: `f00036c0`, 이미 squash된 첫 기반 이후 차이만 적용했다.
- `1663f7cf` / B0: 기존 후속 통합. 최신 main의 Scene 객체 ID 모두 유지, 중복 local fileID 0, 터렛 identity 8개. Player prefab·HUD/날씨 Scene 추가를 보존했다. 패키지 버전 변경 없음.
- `a34d91b3` / B1: [관리 책임·수명 계약](../architecture/match-management-responsibilities.md). Feature HP 원본과 Core 읽기 projection을 구분한다.
- B2: 아래 CU 초기화와 준비 검사. 각 후속 배치는 실제 구현 후 기록한다. PR/이슈 자동 종료 없음.

## B2 CU 초기화

1. 준비 전에 CU/Controller가 현재 Scene에 있고 활성인지, 지역/터렛 바인딩이 같은 Controller를 쓰는지 검사한다. CU 좌표/Grid/Inspector 구역 수를 읽기 전용으로 검증한다.
2. Host 조립은 터렛 Match lease와 Session을 먼저 만든다. 아직 Core RegionAccessPolicy를 붙이지 않은 시점에 `ConfigureUnlockZones(cuPosition)`를 명시적으로 호출한다.
3. 정적 초기 구역은 1, 총 구역 수는 D Inspector 설정이다. false/예외는 조립 실패로 전달하고 기존 공통 rollback이 Match/actor/Feature 바인딩을 정리한다.
4. Dynamic은 준비 검사를 공유하지만 정적 잠금을 적용하지 않는다. 임시 삼각형이 확정 셀을 계산하며, 이후 점령 갱신/모드 Reset은 RegionRuntime만 쓴다. Client는 Host snapshot을 연결한다.

## 이번 head 검증

Unity `6000.3.23f1`, 새 사본에서 공식 batch EditMode/PlayMode 실행. 원본 Editor/Scene은 변경하지 않았다. XML/로그는 로컬 `Docs/Local/Validation/CoreSprint20261026/`에 보관한다.

| 검사 | 결과 | 직접 확인 범위 |
| --- | --- | --- |
| `b0-core.xml` | 종료 0, 18/18 | 전체 스크립트 컴파일, NetworkMatchCoordinator / TurretCommandGateway / RegionGridReplica / AppShutdown |
| `b2-scene.xml` | 종료 0, 7/7 | 실제 Scene의 읽기 전용 준비, CU 누락/범위 밖·서로 다른 Controller 거부, Static→Dynamic 점령/회수, 실제 Host 시작·Enemy 생성·종료·연결 유지·새 Match 재시작 |
| `b3-network.xml` | 종료 0, 19/19 | Host 최종 결과·변조/이전 준비 거부·단일 관측·backpressure 재시도·결과 보존, 기존 터렛 명령/종료의 계약 검사. 실제 UTP/codec 실행은 아래 B7 증거로 정정 |
| `b4-match-fixed.xml` | 종료 0, 26/26 | 실제 Initial_Stage CU 파괴·Host 결과·Player/추가 생성 차단·Enemy/터렛 작업 정리, 중복/재진입·관측/취소 예외·기존 Scene/cleanup 회귀 |
| `b5-scene-final.xml` | 종료 0, 10/10 | 실제 CU 완료 대기·새 Scene/CU/Match/Enemy 연결·Hub 복귀·이전 객체 폐기·입력 복구, 전환 취소/중복/실패·지연 driver 연결·App 정리 |
| `b6-screens-fixed.xml` | 계약/종료 8개 통과, UI 사례 1개 실패 | stack·focus/back·중복/이전 결과·Presenter 실패/재진입·입력 정리. UI는 마지막 Hub EventSystem 검사 가정으로 실패 |
| `b6-ui-flow-final.xml` | 종료 0, 1/1 | 실제 UGUI Resume·CU 결과·Restart·Loading·새 Match/CU·결과에서 Hub 복귀·Hub 메뉴/Resume |
| `b7-environment-fixed.xml` | 종료 0, 11/11 | 환경 불변성·권위·과거/다른 세대 거부·요청 상관관계 및 기존 요청 회귀 10개, 실제 Initial_Stage 환경 소비/생성 1개 |
| `b7-network-and-capture.xml` | 종료 0, 1/1 | 실제 날씨·Enemy 정책 소비·NGO 5마리 생성, 사용 환경/규칙 JSON 보존 |
| `b7-utp-play.xml` | 종료 0, 7/7 | PlayMode 실제 UTP 두 SDK의 초기/변경 환경·stale/이전 세대 거부·종료/3회 재시작(연결 재시작 포함)·최종 결과/지역 전달 및 codec |
| `.meta` 검사 | 오류 0 | 실제 Assets 전체 파일/메타 짝 검사 |

Scene 검사는 Test Framework가 PlayMode로 진입해 실제 조립/API/객체를 확인했다. 사람의 마우스 입력·해상도별 UI 외형·실제 원격 Player 이동 복제·전체 협동 완료를 의미하지 않는다. 이전 10/15 검증은 당시 source 증거로 별도 보존한다.

## 다음 구현과 미결

- B3 구현: Host 최종 결과의 Client 전달·gameplay gate, pending 재시도와 정상 종료 보류, 결과 보존을 연결했다. Lifecycle v2로 변경했으며 같은 Build가 필요하다. 한 프로세스 두 SDK 검사를 실제 원격 Player/두 실행 파일 검증으로 확대하지 않는다.
- B4 구현: 실제 CU 파괴 → MatchCoordinator → StoryModeRules의 관측값 판단 → Session 단일 완료 → 취소·Host 결과 게시·실제 Enemy/터렛 작업 정리. 완료는 Session/연결 폐기와 구분한다. 기존 CU가 파괴된 Scene에서 단순 TryPrepare만 다시 호출하는 것은 초기 상태 복구가 아니다. 재시작은 다음 Scene/UI 배치에서 명시적 재로드로 연결한다.
- B5 구현: SceneFlowController / UnitySceneFlow / 단독 Host NgoSceneFlow의 실제 재로드·Hub 복귀와 owner별 입력 잠금. [Scene API](../architecture/scene-flow-api.md)의 범위·commit·다중 참가자 제한을 따른다.
- B6 구현: ScreenManager의 실제 UGUI 결과/로딩/메뉴·Presenter/focus/back와 owner별 입력 수명. Session/Network 결과를 같은 불변 자료 경계에 연결하고 실제 버튼으로 S1 재시작/Hub 복귀를 확인했다. [화면 API](../architecture/screen-management-api.md) 참조. 사람의 외형/입력 인수와 다중 참가자 이동은 별도다.
- B7 구현: 실제 Weather/CountTick을 Host Match clock/취소 수명에 연결하고 불변 초기·변경 환경과 Client 읽기 replica, 실제 Encounter 정책 소비·Debugger 기록을 추가했다. [환경 API](../architecture/match-environment-api.md) 참조. 시간대 원본 없음/중립 기본값/명시적 검증용 규칙과 최종 밸런스는 구분한다.
- B8 기반 구현: [origin/source 계약과 명시 Scene 실행 대상](../architecture/encounter-spawn-origin-api.md). source 세대/취소·origin 충돌/재전송, 실제 Scene 재로드의 실행 대상 연결 11/11 통과. 포인트 밖 실제 Enemy 생성/공통 추적은 다음 단계이며 아직 완료가 아니다.
- B9: S1~S3 실행 수준별 인수. 실제 Player NetworkBehaviour는 이영빈, 터렛/수리·전력은 황재동, Encounter/생성·AI는 조수빈, World/환경 표현은 김진태가 소유한다.

최종 제품의 CU 연결 점유·보스 게이트·저장/복귀 정책, OFF 터렛 타깃 가능 상태 및 수리 계약은 별도 결정/담당 연결을 보존한다. 미결을 새 임의 규칙으로 채우지 않는다.

B4 최초 검사 `b4-match.log`는 검사 코드의 HP 속성명/using 누락 2건으로 컴파일 실패했다. 수정 후 `b4-match-fixed.xml` 전체 26/26 통과했으며 실패 로그도 보존했다. 제품 예외를 무시하거나 검사를 제거해 통과시킨 것이 아니다.

B5 실제 재로드 검사에서 B4의 결과 대기 경로를 보완했다. 완료 후 D 제어 lease를 정상 해제했는데 TurretCommandSceneScope.Pump가 이를 권한 손실 오류로 보고 Match를 종료했다. 이제 실행 중 gameplay에만 제어 lease를 요구하고, 완료 뒤에도 gateway/표시 전송 pump는 유지한다. CU 파괴 뒤 한 프레임을 실제로 기다리는 회귀를 추가했다. B4 PR에는 이 후속 수정도 함께 포함해야 한다.

B5 최초 컴파일 검사는 검사에서 내부 AppServices를 조회해 실패했다. 좁은 Scene/Input 공개 Port로 수정했다. 후속 Scene 검사에서 위 Pump 오류와 “Executor는 1개”라는 잘못된 검사 가정을 구분했다. 실제 main에는 같은 Scene의 제작 Executor가 두 개였고, 진단은 양쪽 `InScenePlaced/DestroyWithScene=true`를 확인했다. 최종 검사는 이전 객체 폐기·제작 개수/새 Scene·실제 새 Enemy 생성을 요구한다. 실패 로그/XML을 보존했으며 오류 무시나 Executor 임의 삭제는 하지 않았다.

B6 `b6-screens.log`는 Unity Application 이름 충돌과 interface에 없는 접속 목록 조회 2건으로 컴파일 실패했다. SDK 조회를 Bootstrap의 NGO 경계로 수정했다. 그 뒤 UI 경로는 모두 실행됐지만 “Hub에는 기본 EventSystem이 있다”는 마지막 가정으로 실패했다. 실제 Hub 메뉴 open 시 Scene 전용 fallback 생성·중복 없음·Resume를 확인하도록 검사했고 `b6-ui-flow-final.xml` 1/1 통과했다. 통과한 계약 8개를 불필요하게 다시 실행하지 않았으며 실패 로그를 보존했다. 기존 Presentation/UI 폴더 GUID도 유지했다.

B7 최초 로그는 UI가 snapshot을 source처럼 조회한 컴파일 오류 1건으로 실패했다. 수정 후 위 계약/실제 Scene 검사가 통과했다. 전송 검사 assembly는 PlayMode이므로 EditMode 필터로 실행되지 않았음을 실제 testcase 목록으로 확인했다. 따라서 B3의 “19개에 UTP/codec 포함” 설명을 정정했다. `b7-utp.xml`의 0개 실행을 성공 증거로 세지 않으며, 실제 전송은 `b7-utp-play.xml` 7개 중 해당 UTP 사례로 확인했다. 모두 한 프로세스의 두 SDK이며 두 실행 파일·실제 원격 Player 인수는 아니다.
