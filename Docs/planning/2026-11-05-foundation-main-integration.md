# Core 기반 main 통합 — 분리 기준

2026-11-05. 기존 임시 Foundation 스냅샷 a95f8fd3을 그대로 main에 병합하지 않고, 최신 main 3a1f2498에서 필요한 Core만 추출한다.

## 오염을 피하는 기준

- 기존 스냅샷은 오래된 main 기준으로 Feature 수정을 제외한 이력을 포함한다. 최신 main과 비교하면 승인된 팀원 파일을 과거 상태로 되돌리는 차이가 있어 그대로 PR로 사용하지 않는다.
- Feature/Map/UI, 기존 Gameplay/Spaceship Scene, Prefab은 최신 main 원본을 유지한다. 개인 폰트·DefinitionId·ShaderGraphSettings 변경과 stash는 원래 작업 공간에 보존한다.
- main의 기존 Core에 누락된 Match 진행·시간/예약·저장·환경·Scene/UI 수명 기반을 Core 경로 안에서 통합한다. #571~#574의 선행 브랜치 병합과 main 반영은 구분한다. 이미 main에 반영된 #570은 되돌리지 않는다.
- main에서 이미 제공하는 터렛 Match/권한/ID/관측 API를 사용하는 Core 소비자만 복구한다. 팀원 코드나 보관한 Feature stash는 적용하지 않는다.
- CU는 기존 IControlUnitVitals로 체력을 읽는다. 추가 파괴 이벤트 Port를 구현한 source는 즉시 알림을 구독하고, 기존 source는 Scene의 매 프레임 EvaluateProgress로 같은 원본을 확인한다.
- Startup은 Core가 제작한 새 Scene만 추가한다. 기존 Scene 본문과 기존 Build Scene 순서를 덮지 않는다.
- 현행 Enemy는 기존 App 소유 consumer와 Battlefield 스폰 경로를 유지한다. 새 Scene 설정·환경 조회·추가 스폰 context 계약은 구현한 consumer에만 연결한다. 계약 미구현 때문에 기존 Match 시작을 차단하지 않으며, 새 계약을 사용하는 실제 Feature 동작이 완성됐다고 주장하지 않는다.

## Foundation과 API PR의 경계

Foundation은 main을 대상으로 한다. 후속 #613은 이 기반 위에 Random·출입 보고 API만 추가한다. 두 PR에 같은 신규 기능을 중복 복사하거나 과거 Feature 복구/삭제를 섞지 않는다. Bootstrap 공통 파일의 후속 변경은 의존 순서대로 누적하며 후속 PR diff에서 기반 내용을 다시 표시하지 않는다.

Core Feature 계약과 실제 소비는 구분한다. Weather 시간/난수 소비, Player/Enemy/터렛의 전체 정지·체크포인트 원본 writer, 원격 Player/포털 RPC, 전체 Mission 인수는 이번 기반 통합으로 완료 선언하지 않는다.

## 검증과 검사 소스

검증 중 사용하는 신규 검사 소스는 임시로만 사용하고 최종 PR에는 넣지 않는다. main에 이미 있는 검사 소스는 유지한다.

- Unity 6000.3.23f1: Core 선행 추출 후 컴파일 오류 0.
- 선택 검사 88개 중 87개 통과, 1개 실패를 발견했다. 실패는 main Enemy에 없는 신규 설정·환경·추가 스폰 계약을 Core가 필수로 요구한 연결 문제였다. Core만 호환 수정했다.
- 수정 후 main의 Initial_Stage 제작 참조·실제 PlayMode App 생성/종료 검사 5/5 통과, 실패·제외 0, Unity 종료 0. 이전 선택 검사의 시간·저장·흐름/Startup 결과와 구분해서 보존한다.
- 검사 소스 61개 경로의 검증 당시 내용을 비공개 폴더에 보관했다. 신규 검사 파일은 최종 diff에서 제외했고 기존 검사는 보존했다. 예외는 기존 AppShutdownTests의 SceneFlow stub 네 멤버를 확장된 계약에 맞춘 변경뿐이다. 새 테스트나 검증 조건은 추가하지 않았다.
- 검사 소스 제외 후 Unity 최종 컴파일 오류 0, 종료 0. 이때 발견한 기존 SceneFlow stub의 누락을 위처럼 수정한 결과다.
- 테스트 중 자동 변경된 폰트·Network prefab 목록·ShaderGraph 설정은 main 원본으로 되돌렸다. 최종 307개 변경 경로가 Core·새 Startup·공개 문서·Build Scene 등록 범위임을 확인했다. 전체 Asset .meta 검사 오류 0, Feature/기존 Scene/Prefab/Art/Packages diff 없음, 원래 작업 공간 status 일치를 확인했다.

검증 로그/XML은 개인 `Docs/Local/Validation/FoundationSplit-20261105`에 보존한다. 이번 실행은 원격 Player/실제 Weather/전체 Mission 승패·저장 복원의 게임 인수를 대신하지 않는다.
