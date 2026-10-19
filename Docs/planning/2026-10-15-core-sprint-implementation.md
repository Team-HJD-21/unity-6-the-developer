# Core 통합 스프린트 — 배치·검증 기록

기준일: 2026-10-15

시작 코드: main `1277de10`

작업 브랜치: `codex/core-sprint-network-regions`

상태: 목표 모드 진행 중. 아래 완료 표시는 구현 단위별이며 전체 이슈/통합 완료가 아니다.

[문서 목차](../README.md) · [Core API](../architecture/core-api-usage.md) · [터렛 API](../architecture/turrets/api-reference.md) · [HUD](../architecture/player-ui-api.md)

## 확정한 원칙과 이번 답변

- 정적/동적 지역의 최종 원본은 **같은 의미의 지역 Uniform Grid 상태**다. 동적 삼각형/Polygon은 활성 터렛을 셀에 투영하는 임시 계산 자료다.
- 정적 모드가 기본이다. 정적 정책과 동적 정책은 동시에 실행하지 않는다.
- 동적 모드에서는 터렛을 지역 잠금으로 제한하지 않는다. 전력·냉각·파괴 등 기존 D 규칙은 유지한다.
- 활성 터렛 집합에서 빠진 점 때문에 면적이 사라지면 해당 점령을 회수한다. **나머지 터렛을 끄거나 전력을 반환하지 않는다.**
- 셀 판정은 양의 교차 면적을 사용하는 첫 테스트안이다. 구현의 `1e-10` 셀 면적 비율은 선/점 접촉의 부동소수 오차를 거르기 위한 수치 한계이지 밸런스 임계값이 아니다. 실제 시각 결과는 PL 확인 대상이다.
- 네트워크는 명시적 Host 시작 API를 권장안으로 채택해 기반을 작성한다. Host 혼자 시작할 수 있고, 접속·참가자 준비·Match 시작은 구분한다. Spawn 요청이 Host를 암묵적으로 시작하는 정식 경로를 만들지 않는다.
- 런타임 모드/Grid 전환은 명시적인 Reset 확인을 거쳐 새 Grid 세대로 적용한다. Dynamic 전환은 잠금을 해제하지만 자동 활성화하지 않는다. Static 전환은 첫 구역부터 적용하므로 구역 밖 활성 터렛이 꺼질 수 있음을 확인창에 알린다.
- 실제 공용 터렛 권한·참가자 이동/공격 복제와 도중 참가 정책을 위 원칙만으로 완료 선언하지 않는다.

## 이슈와 책임

| 이슈 | 책임 |
| --- | --- |
| [#442](https://github.com/Team-HJD-21/unity-6-the-developer/issues/442) | 공용 연결 수명·준비·Match 시작/종료와 권한/식별자. 최소 객체 검증은 전체 Player 복제가 아님 |
| [#495](https://github.com/Team-HJD-21/unity-6-the-developer/issues/495) | D 실제 처리기에 Core 요청·지역 결과를 연결. 전력·공격·생성자/매니저는 D 소유 |
| [#496](https://github.com/Team-HJD-21/unity-6-the-developer/issues/496) | E Encounter 정식 요청·결과·중복 실행 경계. Planner/Executor는 E 소유 |
| [#526](https://github.com/Team-HJD-21/unity-6-the-developer/issues/526) | 공통 지역 공간 정의·상태·Snapshot·수명과 정적 어댑터 |
| [#527](https://github.com/Team-HJD-21/unity-6-the-developer/issues/527) | 동적 셀 판정·Debugger 모드/표시·실제 지역 복제 |
| [#528](https://github.com/Team-HJD-21/unity-6-the-developer/issues/528) | 기존 HUD 보완. CU 자동 활성화·공통 요청·로컬 actor 연결 |

## 배치와 현재 결과

| 배치 | 구현 범위 | 현재 상태 |
| --- | --- | --- |
| B1 | 공통 식별/요청·정리 안전성 | App/Match 정리, 요청 Context/중복 Ledger 구현. 최종 Feature별 결과·전송 연결은 B4 |
| B2 | 지역 데이터/Runtime·정적 어댑터 | Match/D 연결·Initial_Stage 정적 기본 시작 검증 완료 |
| B3 | 네트워크 Match 기반 | NGO 어댑터·App 소유권·타입 시작 설정·준비/시작/종료 조율·실제 UTP 전송 구현. 두 SDK 인스턴스의 시작/종료/재시작 확인. 정식 Scene 조립·별도 두 실행 파일 검증 미완료 |
| B4 | D/E 실제 요청 | D 지역 접근·Match 수명 연결, E 타입 요청/결과·최신 조회·중복 방지·실제 Host 검증 완료. 원격 요청·터렛 명령 gateway 미완료 |
| B5 | 동적 판정·Debugger | 셀 투영·점령 회수·Debugger/Scene 구현. 실제 모드 전환·3개 ON/1개 OFF 검사 통과 |
| B6 | 지역 상태 복제 | Client 읽기 모델의 세대/revision 검증 구현. 실제 payload 전송·Host/Client 적용 미완료 |
| B7 | HUD | 정책/점령 표시·CU 근처 자동 활성화 구현·실제 Scene 검증. 공용 네트워크 요청/로컬 참가자 주입 미완료 |
| B8 | 통합 검증/문서 | 변경 단위 검사 진행 중. 실제 두 인스턴스·전체 Scene 통합·화면 인수는 미완료 |

## PR·커밋 경계

PR은 아직 생성하지 않는다. 기반 코드와 실제 Scene 사용 전환을 구분하고 의존 관계와 실제 검증을 보고 분리한다. 번호가 앞선 배치 전체가 끝나기 전에도 독립적인 기하 계산처럼 준비된 부분은 별도 커밋으로 진행한다.

- 기반 PR 후보: App/Match 정리 안전성, 지역 데이터·정적 변환, 공용 요청/준비 계약, NGO 연결 Port와 어댑터.
- Feature 연결 PR 후보: D/Match 바인딩 및 E 정식 요청·결과 연결. D의 API 변경은 검토를 요청한다.
- 지역 비교 PR 후보: 동적 셀 판정, Runtime 정책 선택과 Scene/Debugger 연결, 이후 실제 지역 복제.
- HUD PR 후보: 기존 B Prefab/View/Presenter를 유지하면서 CU 입력과 정식 요청·로컬 actor 연결.
- 별도 수정: 비활성 레벨 프리팹의 교체 초기화 실패는 지역 계산과 무관한 D 경로이므로 독립 커밋으로 남긴다.

현재 기록된 커밋:

- `b2547133` — App 종료·Match 교체 실패 정리와 회귀 검사.
- `4270e4e4` — 지역 Grid 상태/Runtime·정적 변환 어댑터.
- `2466f5b8` — 준비 barrier·요청 중복 기록 및 연결 Port.
- `6a110b0f` — 활성 터렛 삼각형의 지역 셀 투영과 점령 회수 검사.
- `af61b298` — 비활성 승급 교체 오브젝트 활성화. D 냉각 후 승급 회귀 수정.
- `5e840daf` — NGO 어댑터 및 1인 Host 시작/종료 검사.
- `15f393bb` — Match 지역과 D 접근 정책·수명 연결.
- `ae178372` — Debugger/Scene 정책 전환·표시·실제 Scene 바인딩.
- `c27a4a6c` — 정식 Encounter 요청·결과·중복 방지·실제 Host 검사와 Match 해제.
- `44138408` — AppServices 네트워크 소유권.
- `0395da2b` — HUD 지역 안내·CU 자동 활성화 및 프리팹/Scene 검사.
- `71d98bdb` — Encounter 실행 도중 Match 종료 시 이미 폐기된 Ledger 재접근 방지.
- `0d7cdd5e` — 지역 Client 읽기 모델과 세대/revision/정의 상충 검증.
- `7d7aff88` — 연결 Dispose 전에 Match 소유자에게 종료 통지.
- `d49ec4de` — Host 확정 설정/슬롯과 준비·명시 시작·종료 조율.
- `6dd051d7` — RLE/bounded codec·조각 조립·실제 NGO 수명 메시지 전송.
- `7bf062eb` — App 소유 전송의 프레임 Pump와 종료 순서.

추가 커밋과 마지막 검증 결과는 해당 배치 검증 후 기록한다. 앞선 테스트 결과를 이후 미검증 코드 전체의 완료 근거로 재사용하지 않는다.

## 코드 읽는 순서

1. `Assets/Scripts/Core/Domain/Regions/RegionGridDefinition.cs`: 공간과 셀의 정적 소속. 점령 여부는 여기 없음.
2. `RegionGridState.cs` / `RegionGridSnapshot.cs`: 셀 점령 변경과 불변 결과. Polygon을 보관하지 않음.
3. `Assets/Scripts/Core/Application/Regions/RegionRuntime.cs`: Match/Grid 세대·revision 검증, 명시적 Reset, 변경 통지와 종료.
4. `Assets/Scripts/Features/Turrets/Core/TurretRegionGridAdapter.cs`: D의 별도 Grid 값을 Core 정의로 변환. 터렛 입력도 실제 값만 복사.
5. `DynamicRegionCapturePolicy.cs` / `TriangleGridRasterizer.cs`: 임시 들로네 계산과 양의 교차 면적 셀 선택.
6. `RegionAccessPolicy.cs`: Static은 Grid의 사용 가능 상태를 읽고 Dynamic은 지역 잠금을 적용하지 않음.
7. `Assets/Scripts/Core/Bootstrap/SceneComposition/RegionSceneBinding.cs`: 실제 D 상태를 Match 소유 지역 Runtime에 연결. 정책 변경·활성 입력 갱신·정리 담당.
8. `Assets/Scripts/Core/Application/Networking/MatchReadyBarrier.cs`: Host가 고정한 참가자 집합과 설정 세대의 준비 응답. 자체 서버 시작 기능은 없음.
9. `Assets/Scripts/Core/Application/Requests/MatchRequestLedger.cs`: 동일 요청의 진행/확정 결과. 발신자 인증이나 전송 기능은 아님.
10. `Assets/Scripts/Core/Infrastructure/Networking/NgoNetworkSession.cs`: 실제 NGO 시작·종료. 전달된 NetworkManager를 사용하며 자신이 연 연결만 닫음.

공간 정의에 재사용한 `BattlefieldGridConfiguration`은 좌표/분할 값만 가진 불변 타입이다. 지역 상태가 Battlefield의 Enemy/Player 점유 상태를 참조·변경하는 것은 아니다. D의 `TurretGridConfiguration`도 명시적 값 변환을 거쳐 사용한다.

## 수명·사용 규약

- RegionRuntime은 MatchSession이 소유한다. Scene 바인딩은 종료 알림에서 D 정책과 전역 Registry 구독을 먼저 해제하며 Runtime을 별도 Dispose하지 않는다.
- `MatchSession.Disposing`에서 한 Feature가 실패해도 다른 Feature 및 Match 자원 정리는 끝까지 실행하고 오류를 모아 보고한다.
- 점령 갱신은 새 셀 집합을 검증한 뒤 반영한다. 유효하지 않은 입력으로 일부 셀만 변경하지 않는다. 변경이 없으면 revision과 통지를 늘리지 않는다.
- Grid 설정/모드 Reset은 새 GridId를 발급한다. 같은 Match의 과거 Grid 결과도 적용되지 않는다.
- 지역 변경 관측자는 이미 확정된 사실을 받는다. 관측자 실패는 다른 관측자를 건너뛰거나 상태를 롤백하지 않으며, 통지 중 점령 재변경은 거부한다.
- 준비 barrier는 Host의 확정 참가자 집합을 받는다. 참가자 구성이 바뀌면 새 설정 세대와 barrier를 만들고 이전 준비 응답을 재사용하지 않는다.
- 요청 Ledger는 Unity 메인 스레드의 소유 경계에서 사용한다. 검증된 발신자를 먼저 확인하고 등록한다. 용량이 차면 거부하며 이전 실행 기록을 퇴출해 중복 실행 가능성을 만들지 않는다.
- 실제 전송과 권한을 연결하지 않은 Port/모델 테스트로 네트워크 완료를 선언하지 않는다.

## 검증 증거와 한계

로그/XML은 ignore 대상 `Docs/Local/Validation/CoreSprint20261015/`에 보관한다. 동일 테스트의 재실행은 더하지 않고 마지막 유효 결과를 사용한다.

| 검사 | 결과 | 의미 |
| --- | --- | --- |
| `foundation-editmode.xml` | 14/14 통과 | 초기 지역/종료/소유권 단위 |
| `static-adapter-playmode.xml` | 4/4 통과 | D 테스트 어셈블리의 정적 지역 변환 |
| `regions-editmode.xml` | 22/22 통과 | 동적 셀 판정과 점령 회수 포함 |
| `network-contracts-editmode.xml` | 7/7 통과 | 준비·발신 연결 매핑·요청 기록. 실제 패킷 전송은 아님 |
| `ngo-session-playmode.xml` | 4개 실패 | 새 테스트가 NetworkConfig를 초기화하지 않은 fixture 오류. 결과 보존 |
| `ngo-session-repaired-playmode.xml` | 4/4 통과 | 실제 NGO/UTP Host의 명시적 시작·종료·외부 연결 소유권 |
| `bindings-repaired-editmode.xml` | 30/30 통과 | Match 정리 실패와 지역 수명/공통 계약 회귀 |
| `turret-region-integration-playmode.xml` | 41 통과 / 1 실패 | 기존 냉각 후 승급의 비활성 교체 프리팹 초기화 실패. 실패 결과 보존 |
| `turret-region-repaired-playmode.xml` | 42/42 통과 | 수정 후 D 회귀·정적 접근·동적 회수/전력 유지 |
| `initial-stage-regions.xml` | 1개 실패 | EnterPlayMode 도메인 재로드 후 Editor 전용 SetUp 재실행. fixture 수정 |
| `initial-stage-regions-repaired.xml` | 1/1 통과 | 실제 정적 시작·동적 무잠금·3개 ON/1개 OFF·남은 활성/전력 유지 |
| `encounter-requests.xml` | 5/5 통과 | 최신 조회·권한·중복·부분 실패·재진입 |
| `encounter-actual-host.xml` | 1/1 통과 | 실제 10개 생성·재전송 추가 생성 0·변조/Unbind 후 거부 |
| `cu-hud-and-app-network.xml` | 4/4 통과 | 프리팹·CU 근처 버튼·동적 안내·App 네트워크 Dispose |
| `core-contract-regression.xml` | 41/41 통과 | 지역 Client 읽기 모델 포함 공통 회귀. 실제 패킷 검사는 아님 |
| `request-replica-final.xml` | 12/12 통과 | 마지막 요청 종료 재진입 보완 및 지역 Client 모델 |
| `network-match-coordinator.xml` | 12/12 통과 | 준비/시작/종료·cohort 변경·위조/옛 응답·전송 실패 모델 |
| `ngo-match-lifecycle-compiled.xml` | 4 통과 / 1 실패 | 실제 메시지 reader의 전체 Length를 payload 크기로 오인해 완전 조각 거부 |
| `ngo-match-lifecycle-payload-fixed.xml` | 5/5 통과 | 수정 후 codec/분할 메모리 제한 및 실제 UTP Host/Client의 설정·준비·시작·종료·재시작 |
| `networking-final-playmode.xml` | 10/10 통과 | 연결 소유권/종료 통지와 실제 수명 메시지 전송 최종 묶음 |
| `network-app-final-editmode.xml` | 15/15 통과 | 최종 조율기·참가자 슬롯 정렬·App 프레임 Pump/Dispose |

새 Scene Drawer 작성 중 Unity 6의 EntityId 이름 충돌을 발견해 Domain 별칭으로 수정했다. 컴파일 실패한 실행은 통과 수치에 포함하지 않는다.

전체 Player 이동/공격 복제, 터렛/Enemy 전체 상태 복제, Steam/Backend·매칭·Host Migration, Windows Build, 실제 창의 가독성/색/클릭 인수는 아직 검증하지 않았다. 이번 작업을 근거로 #442/#495/#496/#526/#527/#528을 닫지 않았다.

## 현재 실행과 다음 연결의 정확한 경계

Initial_Stage는 아직 기존 **로컬 Match + Scene 테스트 Host 시작 정책**이다. AppServices에 NGO 어댑터를 붙일 수 있지만 이 Scene이 새 App 네트워크 소유자로 자동 전환된 것은 아니다. Launcher의 첫 시작 coroutine만 명시적으로 Host를 시작한다. 개별 정식 Spawn 요청은 Host를 열지 않는다. 테스트 Launcher의 Scene 종료 Shutdown도 정식 App 조립 전까지 남아 있다.

E의 정식 로컬 경로는 `EncounterRequest → EncounterRequestService → IEncounterExecutor → EncounterRuntime`이다. 게임/UI/Scene/Debugger 기존 메서드는 이 경계로 변환한다. 전달된 옛 Snapshot은 권위 자료로 사용하지 않고 provider의 최신 Snapshot을 조회한다. 현재 참가자 검증은 로컬 `local-player` 바인딩이며 실제 원격 연결→PlayerId 검증·주입은 미연결이다.

다음 묶음은 작성한 네트워크 조율/전송의 App·Scene 소유권 연결과 공용 터렛/Encounter 요청이다. 이후 지역 payload·Reset 세대의 실제 전달 및 Client 적용, HUD의 실제 로컬 actor 주입을 연결한다. 각각 두 인스턴스로 확인하기 전에는 완료 처리하지 않는다.

추가로 `NetworkMatchDescriptor`와 `NetworkMatchCoordinator`, `NgoMatchLifecycleTransport`를 작성해 실제 UTP 패킷까지 검사했다. Host 설정에는 MatchId/Stage/Seed/ContentVersion·참가자/업그레이드·지역 모드/정의가 포함된다. 준비 응답은 SDK 발신 연결과 대조하고 Host만 시작/종료한다. 두 참가자 중 하나가 빠지면 현재 준비/Match를 종료한다. 도중 참가·Host Migration을 구현한 것은 아니다.

전송은 약 1KB 조각·프레임당 기본 8개 Pump, 총 메시지 최대 4MiB·조립/송신 대기 각각 최대 8MiB, 조립 최대 4개·무진행 10초/전체 120초로 제한한다. 이 제한은 게임 난이도 수치가 아니라 프로토콜 자원 한계다. 지역 소속 배열은 RLE로 전달한다. `AppRoot.AttachNetworkMatchTransport`로 연결하면 AppRoot.Update가 Pump하며 AppServices가 조율기→전송→연결 순으로 정리한다. Initial_Stage에 이 조립 경로가 자동 설치된 상태는 아니다. 전체 점령 payload는 별도 지역 복제 작업이다.

SDK 테스트는 **한 프로세스 안에서 UTP Host/Client 두 개**를 사용했다. 별도 실행 파일 2개, 최소 NetworkObject의 생성/상태/제거, 실제 Feature 조립 검증을 대체하지 않는다. 초기 두 실행의 테스트 어셈블리 참조 누락/C# 선언 오류도 수정했으며 컴파일 실패 실행을 통과 수치에 더하지 않는다.

HUD 검사는 실제 Button.onClick을 호출한 자동 검사다. 사람의 마우스 입력, 해상도별 가독성·Scene Drawer 외형을 검증했다고 쓰지 않는다. 현재 CU 입력은 D의 로컬 API를 호출하며 원격 Client 경로는 미완료다.
