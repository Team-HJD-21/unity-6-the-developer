# Core 스프린트 — 이슈 감사와 직접 확인 안내

기준: 2026-10-15, main `1277de10`에서 시작한 `codex/core-sprint-network-regions`.

[배치·실패/수정·검증 근거](2026-10-15-core-sprint-implementation.md) · [실제 호출 API](../architecture/core-sprint-region-and-request-api.md) · [HUD](../architecture/player-ui-api.md) · [Debugger](../tooling/game-debugger-guide.md)

## 현재 결론

A의 지역/Match/요청/HUD 연결을 구현하고 실제 Scene 및 Windows Host·Client 두 프로세스로 핵심 경로를 검사했다. **전체 협동 게임 완성이나 모든 이슈의 즉시 종료를 뜻하지 않는다.** 원격 Player 생성·이동은 B, 실제 터렛 생성/표현·전투 복제는 D의 책임이다. Planner/Spawn 정책·실제 Enemy 생성은 E의 기존 구현을 사용한다.

PL 요청에 따라 화면 조작을 중단했다. 최종 마우스 조작·가독성·Scene 표시 인수는 PL이 수행한다. 자동 검증과 직접 인수를 같은 것으로 기록하지 않는다. PR/push/merge/이슈 종료는 아직 실행하지 않았다.

## 이슈별 완료 조건 대조

### #442 — 네트워크 Match 기반

| 요구 | 구현/근거 | 남은 확인 |
| --- | --- | --- |
| 연결과 Match 준비/시작 분리 | `InitialStageNetworkBinding`, `NetworkMatchCoordinator`, 실제 Scene Host 검사 | 시작 UX 제품 확정은 별도 |
| 동일 Match/Stage/Seed/참가자·지역 설정 | Descriptor 전체 binary 해시, 실제 SDK 연결→참가자 매핑 | Steam 계정 ID가 아닌 Match-local ID |
| 정상 Client 요청·무권한/다른 Match 거부 | production gateway→Host 실제 D 실행, 7종 실제 요청 검사 | 아래 개발 actor fixture 한계를 반드시 함께 기재 |
| 최소 NetworkObject 생성/상태/제거·재생성 | `NetworkMatchAnchor`, 실제 3번 Match와 서로 다른 객체 ID | 전체 Feature NetworkObject 복제는 제외 범위 |
| 종료·재시작·연결 해제 정리 | 실행 중 Client 연결 해제 후 양쪽 actor/등록/Port/지역/최소 객체 정리 | 급격한 프로세스 강제 종료·WAN 품질·Host Migration 검증은 아님 |
| 실패 중 정리·App 소유 | 선행 App/Match 예외 회귀 및 실제 두 프로세스 종료 로그 | 최종 API 리뷰 |

**판정:** Core 구현과 자동 기반 검증은 확보했다. B의 실제 원격 actor 연계와 전체 협동 완료를 이 이슈의 증거로 확대하지 않는다. 최종 리뷰 전 자동으로 닫지 않는다.

### #495 — D 터렛 Core 연결

| 요구 | 구현/근거 | 남은 확인 |
| --- | --- | --- |
| 논리 ID/로컬 핸들 분리·등록/해제 | `MatchEntityRegistry`, `TurretMatchBinding`, 실제 Factory/레벨 교체 검사 | 런타임 추가 개체는 D가 명시적 논리 ID로 등록 |
| Match/권한/구조화된 명령 결과 | 6종 `TurretCommandRequest`, Host 검증, D executor, 요청 ledger | D API 변경 리뷰 |
| 지역 Grid와 기존 D 제한의 결합 | `RegionSceneBinding`, 정적 어댑터, D control lease | 해금/정책 최종 밸런스 승인과 구분 |
| 배치/파괴 반영·과열 보존 | 배치 source와 동적 활성 source를 분리, 과열을 OFF로 취급하지 않음 | 실제 전투 상황의 표시 직접 확인 |
| 종료/재진입·중복 구독 정리 | 실제 D/Scene/Host 재시작 및 요청·UI 수명 검사 | D 담당자 연계 인수 |

**판정:** A 구현 및 실제 D 연결 검사는 확보했다. Core가 터렛 Factory/Manager/전력·열 정책을 새로 소유하지 않는다. D와 변경 계약 리뷰 후 완료 여부를 결정한다.

### #496 — Encounter 정식 요청

| 요구 | 구현/근거 | 남은 확인 |
| --- | --- | --- |
| 게임·UI·Debugger 공통 요청 경계 | `EncounterRequestService`, 실제 Launcher 호환 진입점 변환 | 최종 E 계약 리뷰 |
| 최신 Host provider·발신자/Match 확인 | 요청 payload에 Snapshot 없음, provider에서 현재 값 조회 | Client의 임의 Enemy 생성은 허용하지 않음 |
| 동일 요청/반복 생성 구분·부분 실패 | GUID ledger, 구조화된 상태/수량·불명확 실행 수 구분, 서비스 회귀 | E의 최종 PlanId/재시도 정책 확정과 구분 |
| 자동 요청과 연결 소유 분리 | Match 시작 cadence, App 연결 유지, Match별 적 정리 | 게임 진행/웨이브/승패 완성은 제외 범위 |
| 실제 E 생성 | 최신 Scene 2/2에서 10개 생성·재전송 0개·변조/권한 거부·종료 정리 | 과거 추적 수 불일치 1회 원인은 미확정 |

**판정:** A 정식 경계와 E 실제 실행 연결은 구현했다. 과거 한 번의 생성 결과 10/추적 증가 0은 후속 검사에서 재현되지 않았으나 해결 원인도 확정하지 못했다. 성공 재검사로 해당 실패 기록을 지우지 않는다. E 리뷰 및 불일치 처리 결론 전에 무조건 종료 가능한 이슈로 분류하지 않는다.

### #526 — 공통 지역 Grid

공간 정의/소속과 상태/Runtime/정책/불변 Snapshot을 분리했다. 정적 CU 거리 구역을 어댑터로 보존한다. 월드 XY·중심 원점·비정사각 셀 변환, 잘못된 입력의 원자적 거부, 이전 Match/Grid/revision 거부, 무변경 통지 억제, 명시적 Reset과 종료/재진입을 검사했다. Battlefield Player/Enemy 밀도 Grid와 지역 점령 Grid는 서로 다른 원본이다.

**판정:** 구현·자동 데이터/수명 검증 완료. D의 계약 리뷰를 거쳐 종료 후보로 판단할 수 있다. Profile 저장/최종 밸런스까지 완료한 것으로 확장하지 않는다.

### #527 — 동적 셀 점령·Debugger·지역 복제

활성·비파괴 터렛→임시 들로네→양의 면적으로 겹치는 셀→단일 지역 Runtime으로 연결했다. 0/1/2점·중복/공선·경계 선/점 접촉·비정사각 셀·Grid 밖 입력과 OFF/파괴 제외를 검사했다. 과열은 활성점을 유지한다. OFF 후 점령만 회수하고 다른 터렛의 활성/전력을 보존한다. 동적 모드는 지역 잠금을 적용하지 않는다.

Static 기본값, 확인 후 모드/Grid Reset, 새 Grid 세대, Scene 확정 셀/임시 삼각형 구분을 구현했다. 실제 Client는 Host의 전체 결과를 받아 읽고 자체 권위 삼각분할/쓰기를 하지 않는다. Windows 두 프로세스에서도 동일 Grid/revision/전체 상태와 재시작을 확인했다.

**판정:** 구현·기하/Scene/복제 자동 검증 완료. Debugger 확인창·색/레이어/셀 표시의 실제 사용성 인수는 PL에게 남긴다.

### #528 — 기존 HUD의 정식 입력/표시

기존 HUD/Prefab/View·Presenter를 유지했다. ON/OFF·승급/강등·강화·CU 자동 입력은 같은 Host 경계를 거친다. Player는 Match 로컬 actor를 명시 주입한다. Host 표시 Frame/Client replica와 결과 순번을 함께 기다리고, 대기/거부/부분 실패·비활성/재활성·교체·종료·오래된 결과를 구분한다. CU 생존/전력/자동 작업/냉각, Dynamic 안내를 기존 화면에 연결했다.

실제 버튼 자동 호출, 멱등 주입/구독 해제와 Scene 검사를 수행했고 두 실행 파일에서 전체 표시 Frame의 binary 해시도 비교했다. 지역 점령 표시는 Frame이 참조한 모드/GridId/revision이 같은 Snapshot만 결합한다.

**판정:** A 입력/표시 구현과 자동 검증 완료. 사람이 직접 확인하는 조작/해상도별 가독성 및 B/D Feature 연계 인수는 미완료다. 원격 Client에 실제 Host actor가 아직 없으면 AuthorityActorUnavailable이며 정상적인 안전 거부다.

## 실제 두 프로세스 증거의 범위

`NetworkPair/20261015T112511Z-af50a905`는 실제 Initial_Stage와 production requester/transport/Host 검증/D executor/HUD replication을 사용한다. Host interaction actor만 명시적인 **개발 검사 fixture**다. Client 좌표를 신뢰하거나 생산용 권한 검사를 생략하지 않았다. 이 fixture는 `--core-network-role` 개발 인자가 있을 때만 생성하며 일반 게임 경로에는 없다.

따라서 Core API의 정상 원격 요청 통과를 확인했지만 **현재 일반 게임에서 상대 Player까지 자동 생성/복제한다는 증거는 아니다.** B는 실제 Host 측 원격 actor에서 `RegisterAuthorityPlayerActor`를 호출하고 반환 lease를 actor 교체/Despawn 때 해제해야 한다. 실제 원격 터렛 외형·발사/피해/상태 복제도 표시 Frame만으로 대체되지 않는다.

## PL 직접 확인 순서

1. `Assets/Scenes/Gameplay/Initial_Stage.unity`를 열어 Play. 기본 Static의 첫 지역 잠금/해금, Player/CU 체력·전력 표시와 Console을 본다. 연결만 켠 뒤 Match 시작을 나눠 보려면 재생 전에 NetworkBinding Entry를 Manual로 설정한다.
2. `Tools > TeamHJD > Game Debugger` → Battlefield의 지역 Grid에서 Dynamic으로 Reset을 확인한다. 동적 모드 안내와 지역 잠금 해제를 확인한다. 전력/열/냉각 제한은 계속 적용되는 것이 정상이다.
3. 서로 일직선이 아닌 터렛 3개를 ON. 확정된 점령 **셀**과 임시 판정 삼각형의 다른 표시를 본다. 하나 OFF 시 해당 면적은 회수되지만 다른 두 터렛과 예약 전력이 유지되는지 확인한다. 과열만으로 점이 빠지면 오류다.
4. Player를 실제 대상/CU 가까이 이동해 ON/OFF·승급/강등·강화·자동 활성화를 조작한다. 거리 밖/죽음/입력 차단·전력/냉각 거부, 진행·완료/쿨다운 문구를 확인한다. UI가 결과 전 성공으로 뒤집히지 않아야 한다.
5. HUD를 숨겼다 켜고 레벨 교체·Match 종료/재시작·Play 종료를 확인한다. 선택/중복 클릭/오래된 피드백/Missing Reference가 남지 않아야 한다. 이 변경의 최종 마우스/해상도 인수 결과를 배치 문서에 추가한다.

위 조작을 위해 Editor를 새로 종료하거나 저장된 Scene/Prefab을 다시 생성할 필요는 없다. Runtime 정책 변경은 에셋의 영구 변경과 구분한다. Manual Entry 등 authoring 변경을 저장할지는 의도적으로 결정한다.

## PR 준비·복구 경계

현재는 하나의 작업 브랜치에 배치별 커밋을 보존했다. **별도 PR 브랜치 분리·push·PR 생성/병합은 하지 않았다.** 구현 커밋이 시간순으로 교차하므로 경로별로 임의 추출한 부분을 바로 독립 빌드 가능하다고 약속하지 않는다.

리뷰 순서는 ① App/지역·요청 순수 기반 → ② NGO/Match/최소 객체·지역 복제 → ③ 실제 D/E Scene 수명·명령/권한 → ④ 기존 HUD/표시 전송 → ⑤ 제작 Scene/검증·사용 문서다. PR을 나눌 때는 각 선행 경계를 포함한 stacked base를 만들고 각 경계의 compile/핵심 검사를 다시 확인해야 한다. 별도 배포 가능한 독립 기능 묶음이라고 표현하지 않는다.

D 비활성 레벨 교체 수정 `af61b298`은 독립 HOTFIX 성격이다. 제외/되돌리려면 해당 D 수정이 다른 변경으로 대체됐는지 확인해야 하며, 그대로 제거하면 비활성 프리팹 승급 실패를 다시 만들 수 있다. 승인 없이 커밋을 없애거나 Force Push하지 않는다.

최근 검증 커밋: `ecf454a0` HUD 명령/표시, `ce373aaa` 실제 표시 전송, `9ff8a9cb` 지역/HUD 참조 일치, `ee5d5d4b` 다른 Match 명시 거부, `9357071b` 실제 두 프로세스 명령/표시/연결 해제 검사. 이전 배치 커밋과 실패 실행은 배치 기록을 따른다.
