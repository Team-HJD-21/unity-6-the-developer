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
| B1 | 공통 식별/요청·정리 안전성 | App/Match 정리·요청 Context/Ledger·등록 토큰과 양방향 논리 ID/로컬 핸들 대응표 구현. Feature별 명령 결과·전송은 B4 |
| B2 | 지역 데이터/Runtime·정적 어댑터 | Match/D 연결·Initial_Stage 정적 기본 시작 검증 완료 |
| B3 | 네트워크 Match 기반 | NGO·App 소유권·Scene-ready 준비/명시 시작/종료·실제 UTP 및 Windows 두 실행 파일 검증. 최소 Match 객체 생성/상태/제거·재생성 확인. 원격 정상 Feature 요청은 B4, 실제 Scene의 실행 중 연결 유실 인수는 추가 대상 |
| B4 | D/E 실제 요청 | D 지역·논리 ID/등록·명령/결과·실제 승급/강등/Factory·제어 수명·자동 큐/예약 해제 검증. 실제 UTP 터렛 gateway 구현·검증. 정식 Scene/HUD 원격 연결은 진행 중. E 타입 요청·최신 조회·중복 방지·App 연결 유지/적 제거 구현. 생성 추적 수 불일치 1회 별도 추적 |
| B5 | 동적 판정·Debugger | 셀 투영·점령 회수·Debugger/Scene 구현. 실제 모드 전환·3개 ON/1개 OFF 검사 통과 |
| B6 | 지역 상태 복제 | 초기/변경 전체 상태·Reset 세대·최신 합치기·App 수명·Client 읽기 모델 구현. 두 SDK 및 Windows 실제 Scene 두 프로세스에서 정적/동적·회수·종료/재시작과 전체 해시 일치 확인 |
| B7 | HUD | 기존 View/Presenter를 정식 명령 Port·Host 확정 표시 투영에 연결. 실제 ON/OFF·승급/강등·강화·CU·재활성화와 응답/상태 순서 검증. Client 읽기 replica는 구현, 표시 Frame 전송 및 원격 실제 actor 연결·시각 인수는 진행 중 |
| B8 | 통합 검증/문서 | 실제 두 SDK·저장 Scene 최종 8/8·연결 회귀 10/10 및 Windows 두 프로세스 2회 통과. 생성 수 불일치 1회 원인 미확정. 원격 Feature·실행 중 유실·시각 인수와 전체 이슈 감사는 미완료 |

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
- `562f8639` — 공용 bounded NGO 메시지 채널·Grid wire 값 분리.
- `17792381` — 지역 전체 상태 실제 전송·Client 적용·최신 합치기·Reset/종료 수명.
- `d263ee77` — HUD 로컬 actor의 명시적 준비·주입·해제와 Scene 연결.
- `a6aa607f` — SDK 연결 재시작 후 메시지 채널 재등록 및 옛 큐/조립 정리.
- `066af84d` — Client의 로컬 슬롯 actor 생성·공유 Match 입력 수명 바인딩.
- `60b324d0` — E의 App 연결 소유권과 Match별 적 제거를 분리.
- `98a6381a` — 정식 Host/Client Scene 조립·Inspector API·실제 Host 수명 검사.
- `cd64ae84` — Initial_Stage의 SinglePlayerHost 전환·PlayerStart 슬롯 1 연결.
- `0775a18d` — SDK Scene-ready 참가자 집합·최소 Match 객체와 실제 Scene 제작 연결.
- `bc9f3d26` — 실제 Build Settings의 Windows 빌드·독립 Host/Client 검증 도구.
- `1ee2ad40` — Match Entity 등록 토큰·양방향 대응표·명시적 논리 ID/등록 Port.
- `a34ed4d0` — 실제 D Registry/교체/Factory·Match·Battlefield/지역 입력의 논리 ID 연결.
- `34c242a7` — Scene/선택 전선의 표시 라벨만 축약. 원본·복사·Capture ID 유지.
- `71f3c0d3` — AppQuit/Editor 재생 종료에서 Scene 파괴 전 App 정리.

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
11. `NetworkMatchCoordinator.cs`: 참가자 설정 세대·준비 응답·Host 시작/종료 조율.
12. `RegionReplicationSession.cs` / `NgoRegionSnapshotTransport.cs`: Host 원본의 최신 전체 전송·Client 읽기 모델·종료.
13. `ILocalPlayerActorConsumer.cs` / `InitialStagePlayerHUD.cs`: 실제 로컬 actor의 준비·주입·대기·해제. UI는 생성자가 아님.

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
| `shared-channel-regression.xml` | 10/10 통과 | 공용 분할 채널 추출 후 기존 연결·수명 메시지 회귀 |
| `region-utp-replication.xml` | 8/8 통과 | 최대 Grid wire·bitset 검증 및 실제 UTP 초기/변경·합치기·Reset·이전 Grid/준비 세대 거부·종료/재시작 |
| `region-binding-app-regression.xml` | 19/19 통과 | App 지역 전송 Pump/정리·Client Scene 무권한/무재계산·지역 모델 |
| `local-actor-hud-scene.xml` | 4 통과 / 1 실패 | 새 iterator 테스트의 캡처가 EnterPlayMode 재로드를 넘지 못함. 실패/종료 race 증거 보존 |
| `local-actor-hud-closure-fixed.xml` | 1/1 통과 | 람다 캡처를 일반 helper로 분리한 뒤 실제 actor 교체·대기·비활성/재활성·해제 보호 확인 |
| `hud-region-scene-final.xml` | 5/5 통과 | HUD 주입·CU 버튼·실제 정적/동적 점령·Client 바인딩 최종 Scene 묶음 |
| `network-connection-restart.xml` | 13/13 통과 | 동일 Port 인스턴스로 실제 UTP 연결 종료/재접속 후 Match·지역 복제 재실행 |
| `app-exit-scene-final.xml` | 6/6 통과 | ExitingPlayMode에서 Scene 파괴 전에 App 종료 관측 및 HUD/CU/지역 회귀 |
| `network-scene-contract-compile.xml` | 12/12 통과 | 새 Scene 조립 코드 컴파일 및 기존 Encounter/Match 조율기 회귀 |
| `network-scene-host-and-local-actor.xml` | 2 통과 / 1 실패 | Host 흐름의 assertions는 통과했으나 App 종료 때 전송을 재조립하려는 오류 발견 |
| `network-scene-host-and-local-actor-repaired.xml` | 3/3 통과 | 종료 알림 수정 후 실제 Host 연결/준비/시작·적 생성/정리·연결 유지/Match 재시작과 Client 로컬 슬롯 검증 |
| `authored-network-scene-regression.xml` | 7 통과 / 1 실패 | 실제 저장 Scene에서 생성 결과 10이지만 Executor 추적 수 증가 0인 불일치 1회 |
| `authored-encounter-count-diagnostic.xml` | 1/1 통과 | 객체 ID 진단 추가 후 실패 검사 단독 통과. 원인 해결 증거로 간주하지 않음 |
| `authored-network-scene-count-diagnostic.xml` | 8/8 통과 | 같은 Scene 회귀 묶음 재검사 통과. 이전 불일치 원인은 아직 미확정 |
| `network-scene-anchor-host.xml` | 1/1 통과 | 실제 Host 최소 NetworkObject 생성·지역 메타데이터 변경·제거·새 객체 재시작 |
| `scene-ready-peer-regression.xml` | 10/10 통과 | Scene-ready 연결 추적 추가 후 NGO 소유권·수명/지역 전송·재접속 회귀 |
| `turret-identity-and-scene.xml` | 9 통과 / 1 실패 | 등록 모델 6개·기존 지역/Host Scene 통과. 새 실제 D 검사는 iterator 캡처 NRE로 중단 |
| `turret-identity-scene-closure-fixed.xml` | 1/1 통과 | 캡처 제거 후 실제 승급/강등·배치 연속성·비활성/재활성·Factory 등록/삭제·Match 해제 |
| `entity-label-formatting.xml` | 3/3 통과 | 긴 ID 표시 축약·원본 키 보존. 실제 Window 시각 인수는 아님 |

새 Scene Drawer 작성 중 Unity 6의 EntityId 이름 충돌을 발견해 Domain 별칭으로 수정했다. 컴파일 실패한 실행은 통과 수치에 포함하지 않는다.

전체 Player 이동/공격 복제, 터렛/Enemy 전체 상태 복제, Steam/Backend·매칭·Host Migration, Windows Build, 실제 창의 가독성/색/클릭 인수는 아직 검증하지 않았다. 이번 작업을 근거로 #442/#495/#496/#526/#527/#528을 닫지 않았다.

## 이전 체크포인트의 실행 경계

아래 로컬 Scene 설명은 정식 네트워크 조립 전 체크포인트다. 최신 추가 결과는 문서 끝의 **정식 Scene 연결** 절을 우선한다.

Initial_Stage는 아직 기존 **로컬 Match + Scene 테스트 Host 시작 정책**이다. AppServices에 NGO 어댑터를 붙일 수 있지만 이 Scene이 새 App 네트워크 소유자로 자동 전환된 것은 아니다. Launcher의 첫 시작 coroutine만 명시적으로 Host를 시작한다. 개별 정식 Spawn 요청은 Host를 열지 않는다. 테스트 Launcher의 Scene 종료 Shutdown도 정식 App 조립 전까지 남아 있다.

E의 정식 로컬 경로는 `EncounterRequest → EncounterRequestService → IEncounterExecutor → EncounterRuntime`이다. 게임/UI/Scene/Debugger 기존 메서드는 이 경계로 변환한다. 전달된 옛 Snapshot은 권위 자료로 사용하지 않고 provider의 최신 Snapshot을 조회한다. 현재 참가자 검증은 로컬 `local-player` 바인딩이며 실제 원격 연결→PlayerId 검증·주입은 미연결이다.

다음 묶음은 작성한 네트워크·지역 전송의 정식 Scene 연결과 공용 터렛/Encounter 원격 요청이다. 로컬 actor 주입은 구현했으며 실제 Client actor 생성자와 연결하는 작업은 남아 있다. 독립 실행 파일 두 개와 실제 Feature 조립으로 확인하기 전에는 완료 처리하지 않는다.

추가로 `NetworkMatchDescriptor`와 `NetworkMatchCoordinator`, `NgoMatchLifecycleTransport`를 작성해 실제 UTP 패킷까지 검사했다. Host 설정에는 MatchId/Stage/Seed/ContentVersion·참가자/업그레이드·지역 모드/정의가 포함된다. 준비 응답은 SDK 발신 연결과 대조하고 Host만 시작/종료한다. 두 참가자 중 하나가 빠지면 현재 준비/Match를 종료한다. 도중 참가·Host Migration을 구현한 것은 아니다.

전송은 약 1KB 조각·프레임당 기본 8개 Pump, 총 메시지 최대 4MiB·조립/송신 대기 각각 최대 8MiB, 조립 최대 4개·무진행 10초/전체 120초로 제한한다. 이 제한은 게임 난이도 수치가 아니라 프로토콜 자원 한계다. 지역 소속 배열은 RLE로 전달한다. `AppRoot.AttachNetworkMatchTransport`로 연결하면 AppRoot.Update가 Pump하며 AppServices가 조율기→전송→연결 순으로 정리한다. Initial_Stage에 이 조립 경로가 자동 설치된 상태는 아니다. 전체 점령 payload는 별도 지역 복제 작업이다.

SDK 테스트는 **한 프로세스 안에서 UTP Host/Client 두 개**를 사용했다. 별도 실행 파일 2개, 최소 NetworkObject의 생성/상태/제거, 실제 Feature 조립 검증을 대체하지 않는다. 초기 두 실행의 테스트 어셈블리 참조 누락/C# 선언 오류도 수정했으며 컴파일 실패 실행을 통과 수치에 더하지 않는다.

HUD 검사는 실제 Button.onClick을 호출한 자동 검사다. 사람의 마우스 입력, 해상도별 가독성·Scene Drawer 외형을 검증했다고 쓰지 않는다. 현재 CU 입력은 D의 로컬 API를 호출하며 원격 Client 경로는 미완료다.

## 지역 복제·HUD 후속 연결 결과

`NgoMessageChannel`이 순서 보존 메시지와 최신 상태 합치기를 지원한다. `NgoRegionSnapshotTransport`는 Host의 전체 지역 프레임만 보내고 Client→Host 상태 주입을 거부한다. 지역 정의는 RLE, 점령은 bitset이며 삼각형을 보내거나 Client에서 재계산하지 않는다. 전송이 미접수되면 `RegionReplicationSession`이 최신 프레임을 보존해 다음 App Pump에서 재시도한다. 아직 보내지 않은 전체 상태만 합치며 이미 보낸 조각을 중간에 바꾸지 않는다.

App은 `AttachRegionSnapshotTransport`로 수명을 소유한다. Host는 실행 중 Match의 `session.Regions`를 `NetworkRegions.BindHostSource`로 연결하고 Client는 `NetworkRegions.SourceChanged`가 제공하는 준비된 읽기 모델을 `RegionSceneBinding.BindReplica`에 연결한다. Client 바인딩에는 MatchSession/삼각형 계산/쓰기 권한이 없고 Reset·해금은 거부한다. Match 종료는 pending·구독·Client 읽기 모델을 함께 폐기한다. 실제 UTP에서 이 흐름을 확인했지만 Initial_Stage 정식 네트워크 조립은 아직 별도다.

로컬 HUD는 `ILocalPlayerActorConsumer.PrepareLocalActor → BindLocalActor → UnbindLocalActor`로 연결한다. InitialStageCompositionRoot가 생성 전 대기와 생성 후 실제 actor를 주입한다. 다른 Match의 늦은 해제는 무시하고, actor 대기 중 다른 Player를 검색하지 않는다. HUD를 잠시 껐다 켜도 소유자 주입은 보존하며 UI 해제가 actor를 파괴하지 않는다. standalone Feature Scene의 기존 검색 호환만 남겼고 정식 조립은 이 검색에 의존하지 않는다.

다음 목표는 정식 Network Scene 조립·최소 NetworkObject 및 별도 실행 파일 2개 확인, 실제 연결 발신자와 안정적인 Feature ID를 사용하는 터렛/Encounter 원격 요청, 그 경계에 HUD 입력을 연결하는 것이다. 기존 local Scene이 이미 이 흐름으로 전환됐다고 주장하지 않는다.

NGO 연결을 다시 열면 CustomMessagingManager도 교체되므로 공용 채널이 SDK 시작/종료 콜백에서 등록을 갱신한다. 이전 연결의 미전송 조각·조립 버퍼는 폐기한다. 같은 세션/전송 객체를 유지한 실제 재접속 뒤 세 번째 Match와 지역 복제가 정상 동작함을 확인했다.

AppRoot는 OnApplicationQuit/OnDestroy에서 같은 idempotent 정리를 수행한다. Editor는 AppPlayModeShutdown이 ExitingPlayMode 알림에서 먼저 Dispose해 지연 Host 시작이 종료 프레임에 남지 않게 한다. 자동 검사에서 실제 AppRoot가 아직 존재하는 이 알림 시점에 현재 Match가 이미 해제됐음을 관측했다. 예외는 숨기지 않고 정리 후 보고한다.

## 정식 Scene 연결 — 추가 진행

`InitialStageNetworkBinding`은 실제 Scene의 NetworkManager를 App에 한 번 전달하고, 연결 후 수명/지역 전송을 연결한다. 연결 개설, 현재 접속자 집합 확정, 준비 응답, Host 명시 시작은 별도 API다. SinglePlayerHost 진입만 이 API들을 순서대로 호출한다. 참가자 수를 감시해 Match를 자동 시작하지 않는다. 현재 1~2명 cohort를 고정하며 준비 이후 새 접속은 거절한다. Steam 신원 인증이나 SOS/도중 참가 구현은 아니다.

Host는 Descriptor로 MatchSession을 만들고 지역 정의를 적용한다. Client는 같은 Descriptor의 자기 슬롯 actor와 읽기 지역 바인딩만 만든다. Client에 권한 MatchSession을 새로 만들지 않는다. **현재 각 실행 인스턴스는 자기 로컬 Player만 생성한다. 상대 Player의 실제 복제는 B의 후속 Feature 연결이 필요하며 이번 기반으로 완료됐다고 하지 않는다.**

E는 `IAppOwnedEncounterMatchConsumer`로 연결 소유권과 권한 참가자를 받는다. Host 자동 Encounter는 같은 정식 요청 서비스를 사용한다. Match 종료 때 Executor가 그 Match의 적만 Despawn하고 App 연결은 유지한다. Client가 임의로 적을 생성할 수 있도록 권한을 열지 않았다. standalone Enemy 테스트 Scene의 기존 연결 정책은 유지한다.

새 자동 Scene 검사는 실제 Initial_Stage에서 제작 연결을 주입해 Host API를 실행했다. Host 연결만으로 Match가 생성되지 않음, 준비 후 시작, 실제 적 생성, 종료 시 적/actor 정리와 연결 유지, 새 MatchId로 재시작을 확인했다. 별도 Client 실행 파일과 Scene 동기화 경로 검증은 아직 남아 있다. Inspector 버튼 외형/사람 클릭도 자동 검사 결과에 포함하지 않는다.

제작 도구 `NetworkSceneSetup`은 기존 Scene NGO를 재사용하고 PlayerStart 슬롯 1을 추가한다. Inspector용 UI 첫 컴파일에서 Application namespace 충돌이 있어 UnityEngine.Application으로 수정했다. 실패한 `network-scene-authoring.log`와 수정 후 로그를 따로 보존하며 컴파일 실패를 통과로 기록하지 않는다. 실제 Scene은 SinglePlayerHost로 저장했고 제작 저장 시 생긴 무관한 TMP 기본값 override는 제외했다. 기존 Scene/Prefab GUID와 타일맵은 변경하지 않았다.

저장된 Scene 회귀 묶음은 최종 8/8 통과했다. 다만 첫 묶음 실행에서 `EncounterRequestIntegrationTests`의 실제 추적 수 증가가 기대 10 대신 0인 불일치가 한 번 발생했다. 당시 Spawn 결과/로그는 10이었으나 실패 시 객체 ID 진단은 없었다. 진단을 추가한 단독 1/1·같은 묶음 8/8은 통과했다. **원인 재현·해결로 단정하지 않으며 생성 누계와 생존/추적 상태, 실행 순서 영향을 다음 검토에서 구분한다.** 이 결과만으로 이슈를 닫거나 PR을 만들지 않는다.

## 실제 Windows Host/Client 연결 — 추가 진행

`NetworkMatchAnchor`는 최소 Match-scope NetworkObject다. 전체 지역 원본은 기존 RegionRuntime/복제 채널에 남기고 객체는 식별자·시작 메타데이터·지역 revision/셀 수만 읽기 전용으로 표시한다. Host의 명시적 NGO 관리자에서 생성하며 Match 종료 때 구독 해제/제거한다. `NetworkSceneSetup`은 기존 DefaultNetworkPrefabs에 등록된 이 프리팹과 실제 Scene 참조를 연결한다.

현재 접속자 확정은 SDK Scene 동기화 완료 콜백을 받은 집합을 사용한다. 승인만 된 연결과 Scene-ready 목록이 다르면 준비 API는 거부한다. 연결 전에 어댑터를 붙이는 정식 흐름을 대상으로 하며 외부 Host의 과거 원격 준비 상태를 추정하지 않는다.

Windows 개발 빌드는 실제 Build Settings의 Initial_Stage와 Spaceship을 사용한다. 최소 테스트 Scene으로 바꾸지 않았다. 개발 전용 CoreNetworkBuild + tools/Test-CoreNetworkPair.ps1이 명시적 API로 연결하고 결과를 검사한다. 첫 컴파일의 Application 이름·D Contracts 참조/using 누락은 수정했으며 실패한 authoring 로그도 보존한다.

첫 독립 실행 검증 `NetworkPair/20261015T085549Z-feafcedf`는 Host PID 22024 / Client PID 16588, 두 종료 코드 0, 같은 Build GUID `867737fe6b324c059259444c1332daf0`으로 성공했다. Client는 권한 MatchSession 없이 자기 actor 하나만 가지며 전체 Descriptor·지역 Snapshot 해시와 NetworkObjectId가 Host와 일치했다. 정적 41셀 → 동적 23셀 → OFF 후 0셀 회수, 종료 시 actor/지역/최소 객체 정리, 새 Match에서 정적 41셀·새 객체 ID 3→4를 확인했다. Client가 동적 판정 삼각형이나 쓰기 권한을 갖지 않음도 검사했다.

두 전체 endpoint 로그에서 Exception/Error/LogError/LogException signature는 없었다. 종료 때 Unity의 `GarbageCollector disposing of ComputeBuffer` 문구는 두 로그 모두 있었다. Assets/Scripts에는 해당 버퍼 사용이 없었으며 원인은 미확정이다. 이를 무경고 실행으로 표현하지 않고 그래픽 자원 종료 경고로 별도 관측한다. 이 검증은 전체 Player/Turret 전투 복제, LAN/인터넷, GUI/영상 인수를 뜻하지 않는다.

최종 실행 `NetworkPair/20261015T090046Z-17b9ee25`는 Host PID 13348 / Client PID 23468, 두 종료 0, Build GUID `c76dfcbba1144143ad1de8bc6bef29af`로 같은 여섯 단계 검사를 다시 통과했다. 전체 로그 스캐너도 오류 표식 0/ComputeBuffer 종료 경고 2를 verified.json에 기록했다. Source Scene은 기존 HUD RectTransform 값을 보존했고 실제 변경은 Match anchor 참조 한 줄뿐이다. 빌드 크기는 311,257,222 bytes다.

다음 핵심 묶음은 D의 안정적인 논리 ID 대응·Match 수명 정리·명령/구조화된 결과·실제 발신자 권한 gateway, HUD의 정식 요청 연결이다. 원격 거리가 필요한 요청은 B의 신뢰 가능한 Player 위치 Port와 연결해야 하며 Client가 보낸 좌표를 그대로 믿지 않는다. Player 전체 복제를 A가 임의로 대신 구현하지 않는다. E Encounter는 Host 시스템 요청으로 유지하며 임의 Client Spawn 권한을 열지 않는다. 실행 중 연결 유실과 앞선 생성 추적 불일치도 최종 감사에서 빠뜨리지 않는다. 아직 PR/이슈 종료는 하지 않았다.

권한 확인 질문: 공용 터렛 ON/OFF·CU 자동 활성화 요청을 모든 실제 참가자에게 허용하고 Host가 검증할지, Host만 허용할지를 PL에게 확인 중이다. 답변 전 제품 권한을 확정했다고 쓰지 않는다. 거리 제한이 있는 동작과 전역 공용 명령도 구분해야 한다.

## D 개체의 논리 ID·수명 연결 — 추가 진행

`IEntityIdentity`/`SceneEntityIdentity`는 제작 또는 권한 생성자가 부여한 논리 개체 키다. `MatchEntityRegistry<THandle>`는 일대일 대응표와 등록 토큰만 소유하고 실제 생성/삭제를 하지 않는다. 충돌·옛 토큰·다른 인덱스 해제·종료 후 등록을 검사했다. D의 원래 InstanceId·Snapshot·Definition 구조는 보존한다.

`TurretMatchBinding`이 현재 Match에서 실제 D Registry와 제작 키를 대응한다. Runtime Factory의 신규 개체는 `ITurretEntityRegistration`으로 명시적으로 편입한다. D의 LevelUpgraded/LevelDowngraded 사실에서 같은 키를 새 객체에 이전하고, Unregistered는 해당 토큰만 해제한다. Source는 교체 프레임의 최종 배치만 발행하며 동적 지역은 같은 키로 활성/파괴 상태를 읽는다. 과열을 OFF로 바꾸지 않았다.

실제 Initial_Stage에 고유 키 8개와 Core binding 참조를 저장했다. 정상 키는 유지하고 제작 메뉴에서만 누락/중복을 수정한다. 기존 Prefab GUID·타일맵·HUD 스타일은 변경하지 않았다. 신규 meta 하나의 GUID 길이 33자리 때문에 계약 소스가 컴파일 목록에서 빠진 실패가 있어 32자리로 바로잡았고 모든 새 meta의 형식을 확인했다. 실제 D 검사의 iterator 람다 캡처는 기존 재로드 문제와 같은 원인으로 일반 함수로 분리했다. 두 실패 로그/XML도 보존한다.

Windows 검증 `NetworkPair/20261015T092844Z-ecaeb39a`: Host PID 42472 / Client PID 46160, 두 종료 0, Build GUID `4086d5d0fe724daaaa94a3fdeab89b88`. Scene 원문에서 추출한 제작 키 8개, Host·Client 등록 키 정렬 해시는 모두 `iixIXoeWPs4jan4HoCEaVXiJR/+RwbXLEsQmQ6WTRPQ=`로 같았다. 기존 여섯 단계도 통과하며 종료 때 Client 대응표 Count 0을 검사했다. 오류 표식 0, 기존 ComputeBuffer 종료 경고 2다.

긴 키는 Scene/전선 패널에서만 짧게 표시하고 원본 ID·tooltip·Copy·Capture는 유지한다. 문자열 검사는 통과했지만 사람의 실제 화면 가독성을 자동 인수한 것은 아니다. 다음 작업은 D 명령/구조화된 결과·권한 gateway·HUD 연결과 명시적 Feature 실행 정리이며, 위 등록 해제를 전력/체력/업그레이드 전체 초기화로 표시하지 않는다. 공용 요청 권한 질문은 아직 답변 전이다. PR/이슈 종료는 하지 않았다.

## 터렛 명령 계약과 D 제어 수명

`TurretCommandRequest → TurretCommandService → TurretFeatureCommandExecutor → D API`를 추가했다. 요청에는 Match/요청 GUID/발신 PlayerId/논리 대상/의도만 담는다. 서버가 실제 연결로 확인한 발신자는 별도로 전달하며, 필수 `ITurretCommandAuthorization` 정책을 주입한다. 제품의 공용 요청 허용 범위는 답변 전이며 테스트 정책을 기본 권한으로 사용하지 않는다.

ON/OFF·CU 자동 활성화·레벨 승급/강등·정의 ID 기반 강화/강등을 지원한다. 전력·열·배치·프리팹 교체는 D에 남긴다. 같은 요청은 실패/거부도 기록해 재실행하지 않고 변경된 의도는 충돌로 거부한다. 자동 활성화의 Accepted는 작업 시작이며 모든 터렛의 활성화 완료가 아니다. 예외는 일부 부작용 가능성이 있어 ExecutionFailed로 확정한다.

`TurretController.BindMatchScope`는 Core가 임대한 제어 수명이다. 정식 Scene에서는 첫 Match 전·Client·Match 종료 후 직접 D 쓰기를 거부한다. 종료는 먼저 쓰기 권한과 자동 큐를 닫고 예약을 해제한다. 체력·강화·개별 열 상태를 초기화하거나 실제 D 개체를 파괴하지 않는다. 기존 D 단독 Scene에는 이 필수 수명을 임의로 강제하지 않는다.

검증 결과:

- `turret-command-service.xml`: 순수 요청 검사 8/8 통과.
- `turret-command-and-lifetime-scene.xml`: 실제 Initial_Stage 명령·승급 교체/ID·지역·Host 종료/재시작 5/5 통과.
- `turret-match-scope-regression.xml`: 기존 D 초기화/지역 어댑터 42/42 통과.

실제 Scene에 명령용 레벨/강화 카탈로그 참조도 연결했다. 현재 명령 서비스는 명시적으로 BindCommands한 경계에서만 사용한다. **Client 요청 전송·HUD gateway는 후속 연결 중이며 이미 완료됐다고 표시하지 않는다.** 권한 없는 로컬 D 호출은 이제 거부되므로 이 경계를 연결하기 전 Client 버튼을 정상 작동으로 인수할 수 없다. 새 Windows 빌드 검사는 이 단계에서 아직 실행 전이다.

## 공용 터렛 요청 전송·Host 검증 정책

사용자 답변으로 **모든 참가자가 ON/OFF·CU 자동 활성화·승급·강화/강등을 요청하고 Host가 검증**하는 정책을 확정했다. 실제 상태 변경은 항상 Host의 D API가 수행한다.

`TurretCommandGateway`는 준비 세대/Match별 요청 경계다. Host는 실제 SDK 연결→참가자 매핑을 사용하고 payload 발신자와 대조한다. Client는 서버 연결·세대·Match·요청 GUID·명령 종류·대상이 모두 맞는 결과만 확정한다. Pending은 기본 64개, 서버 응답 대기도 같은 제한이며 전송 압력이 있으면 실행 전 거부하거나 확정 결과를 보존해 다음 Pump에 보낸다. InProgress는 확정 응답이 아니다. 응답 미도착은 실행 실패로 만들지 않으며 TryRetry는 같은 의도/GUID만 재전송한다. 자동 재전송·새 GUID 재실행·낙관적 D 상태 변경은 하지 않는다.

`NgoTurretCommandTransport`는 기존 제한 바이트 채널을 이용한다. 명령 최대 8KiB, Client→Host 요청/Host→Client 결과만 허용하고 명령을 합치지 않는다. `TurretCommandCodec`는 버전·길이·UTF-8·열거형·필드 의미·bool·후행 바이트를 검증한다.

- `turret-command-gateway.xml`: 초기 서비스/gateway 12/12 통과.
- `turret-command-utp-repaired.xml`: 실제 UTP/재접속은 통과했으나 빈 논리 대상의 default/null과 빈 문자열 비교 불일치로 codec 1개 실패(총 2 통과/1 실패). 자동 CU 응답 상관관계에도 영향을 주는 결함이므로 DTO의 선택 ID를 정규화했다.
- `turret-command-utp-normalized.xml`: 수정 후 codec/실제 UTP 3/3 통과. CU 자동 요청 왕복도 추가 확인했다. 실제 D/별도 두 프로세스 검증은 아직 아니다.
- `turret-gateway-and-authorization.xml`: InProgress 처리·권한 상태 재확인·서버 actor 등록/거리 정책 포함 15/15 통과.

`PlayerInteractionRegistry`는 Host가 명시적으로 등록한 제공자에서 **명령 검증 순간**의 위치/생존/조작 상태를 읽는다. `TurretInteractionAuthorization`은 그 값과 서버의 실제 터렛/CU 위치를 사용한다. 원격 actor가 없으면 AuthorityActorUnavailable이다. Client payload 좌표·다른 참가자 좌표·PlayerStart를 대신 넣어 통과시키지 않는다. B의 Host 측 원격 actor 연결 Port와 정식 Scene/HUD 조립은 다음 작업이다. 아직 이 결과로 #442/#495/#528을 닫지 않는다.

### App 전송 소유권과 실제 B actor 조회 연결

`AppServices.AttachTurretCommandTransport`와 `AppRoot.NetworkTurretCommands`를 추가했다. Initial_Stage가 실제 연결 후 한 번 부착하고 App Update가 Pump한다. Match별 gateway는 이 전송을 빌려 쓰며 전송 자체는 App 정리에서 폐기한다.

B의 `PlayerControlState`는 `IPlayerInteractionActor`를 구현한다. 현재 실제 actor의 XY·HP·CanAct를 읽으며, `PlayerInteractionActorBinding`은 명시적으로 주입한 actor의 PlayerActorIdentity와 제공자 하나를 검사해 registry에 등록한다. actor 검색·생성·복제는 이 바인딩의 책임이 아니다. actor 비활성/파괴·권한 유실·lease 해제 시 조회를 닫는다.

`turret-app-and-actor-scene.xml` 4/4 통과: App Pump/중복 부착 거부/실패 중 전체 Dispose와 실제 Initial_Stage Player 조회·입력 차단·권한 유실·lease 해제, 기존 명령/Host 재시작을 확인했다. 이 시점에는 Match별 명령 gateway의 자동 Scene 조립과 HUD 명령 입력 연결이 아직 남아 있다. 원격 B actor 복제를 구현한 것으로 간주하지 않는다.

### 실제 Scene의 명령 scope 자동 조립

`InitialStageNetworkBinding.OnStarted`는 Scene의 D/Player/지역 조립 후 `TurretCommandSceneScope`를 만든다. Host는 현재 로컬 actor를 등록하고 여섯 공용 명령을 모든 참가자에게 허용하는 거리 정책·D 서비스·gateway를 구성한다. Client는 권위 서비스 없이 requester gateway만 구성한다. App은 실제 전송을 Pump하고 Scene은 제한된 미전송 응답을 Pump한다. Ended/Scene 해제는 gateway와 actor 제공자를 닫은 뒤 나머지 Feature를 정리한다.

`InitialStageNetworkBinding.TurretCommands`가 입력 소비자용 Port다. `RegisterAuthorityPlayerActor(matchId, playerId, actor)`는 B가 Host에 실제 생성한 참가자 actor를 명시적으로 연결하는 API다. 반환 lease는 교체/Despawn 시 해제한다. 현재 Host가 갖지 않은 Client actor를 자동 검색하거나 PlayerStart로 위조하지 않는다. CU의 `IControlUnitInteractionTarget`은 실제 생존/활성 상태와 위치를 조회한다.

검사 `turret-command-scene-scope.xml`은 4 통과/1 실패였다. 이전 fixture는 없는 대상을 D 처리기의 TargetUnavailable로 기대했지만 새 실제 정책이 그 전에 Unauthorized/InteractionTargetUnavailable로 거부했다. 기대값을 이 명시적 계약에 맞춘 `turret-command-scene-policy-aligned.xml`은 5/5 통과했다. 거리 밖 거부·동일 GUID 재전송·승급/강화·자동 작업·종료 후 차단·재시작과 기존 지역/ID 검사를 포함한다.

**이 시점의 남은 경계:** 실제 HUD 버튼은 아직 이 requester로 교체 전이다. B의 원격 Host actor 생성/이동 복제, D의 Client용 실제 터렛 상태 읽기 연결은 별도 Feature 협의/통합 대상이다. Client의 로컬 D 초기값을 Host 확정 상태로 간주하지 않는다. 새 Windows 두 프로세스 및 연결 유실 검증도 후속으로 수행한다.

## HUD 정식 명령·표시 투영 연결

`InitialStagePlayerHUD : ITurretCommandConsumer`와 `StageTurretCommandBinding`을 추가했다. 정식 Match는 ON/OFF·승급/강등·강화·CU 버튼 모두 requester를 사용한다. Port가 대기/해제 상태면 로컬 D 변경으로 되돌아가지 않는다. 별도 D Feature 테스트 Scene의 명시적 standalone 동작만 기존 로컬 경로를 유지한다.

`TurretPresentationSceneSource`는 Host의 D 실제 상태에서 읽기 전용 `TurretPresentationFrame`을 만든다. 전력 예산·CU 체력/가용성·자동 작업·터렛 상태/정의·레벨·강화·냉각·표시 수치를 담고 최대 0.1초 주기로 조회한다. 내용이 같으면 새 revision을 만들지 않는다. 비어 있는 표시 이름은 정의 ID로 대체하고 UI 이름만 최대 256자로 제한한다. 실제 생성자/원본/공격 규칙·Client의 D 런타임을 변경하지 않는다. Client용 `TurretPresentationReplica`는 준비 세대·revision·같은 revision의 내용 충돌·확정 순번 역행을 검사한다.

서버 결과와 표시 Frame은 도착 순서가 다를 수 있다. UI는 적용/접수 성공 및 부분 부작용 가능성이 있는 ExecutionFailed에서, `ConfirmedCommandSequence`가 해당 `ExecutionSequence` 이상인 Frame까지 기다린다. 그 전에는 해당 대상의 새 입력을 막고 같은 GUID의 명시적 재전송만 허용한다. 순수 거부는 즉시 표시한다. UI 미확정 의도는 최대 64개다. 같은 Port 재주입은 멱등이며 다른 Port 교체에는 명시적 해제가 필요하다.

HUD 비활성/재활성화는 요청 추적을 보존하고 Presenter/View 구독은 재구성한다. 실제 파괴·Match 해제는 요청/상태 구독을 정리한다. `TrySelectTurret(entityId)`는 튜토리얼/명시적 입력에도 기존 Scene/거리 검사를 적용한다. `RetryPendingTurretCommand(entityId)`는 이전 GUID를 유지하며, CU 요청은 빈 EntityId로 지정한다.

검증(서로 겹치는 사례는 합산하지 않는다):

- `turret-hud-authority-binding.xml`: 기존 CU·로컬 actor 교체/재활성화·명령 Scene 4/4.
- `turret-hud-confirmation-and-buttons.xml`: DTO/replica·도착 순서·실제 터렛 버튼 7/7.
- `turret-hud-partial-failure-and-scope.xml`: 부분 실패의 상태 대기·gateway·replica 11/11.
- `turret-hud-cu-availability.xml`: 실제 명령/버튼·CU 비활성/복구 투영 2/2.
- `turret-hud-final-binding.xml`: 멱등 주입·재전송/구독 해제·실제 버튼/재활성화 최종 5/5.

**남은 A 구현:** 표시 Frame의 Host→Client 실제 전송/정식 조립·독립 실행 파일 검사. 현재 Client replica는 아직 전송을 받지 않아 대기 표시하며 로컬 D 초기값을 진짜처럼 보여주지 않는다. **B 연계:** Host 측 실제 원격 Player actor와 이동/상태 제공자. **D 연계:** 실제 터렛의 원격 생성/레벨 외형/발사체 등 Feature 표현·전투 복제. 표시 Frame 전송을 D에게 넘기는 것은 아니며 A가 다음 배치에서 연결한다. Client의 기존 로컬 사거리 원은 D 표현 복제 전까지 잘못 그리지 않도록 숨긴다. 아직 이슈/PR을 완료 처리하지 않았다.
