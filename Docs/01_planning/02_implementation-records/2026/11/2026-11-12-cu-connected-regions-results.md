# CU 시작 영역·동적 점유 연결 구현 결과

기준일: 2026-11-12. 기준 main `88bc2c58e` (#700 포함). 상태: 로컬 구현과 아래 QA 완료, [PR #701](https://github.com/Team-HJD-21/unity-6-the-developer/pull/701) 생성. CI/병합은 해당 PR의 실제 상태로 확인하며 실제 2인 인수와 구분한다.

## 이번에 달라진 동작

동적 지역에서 CU도 위치 노드로 참여한다. 터렛이 없어도 CU 셀 하나를 점유하며, CU와 활성 터렛 두 개가 비공선이면 들로네 삼각형을 만들 수 있다. 삼각형은 후보 셀을 계산하는 중간 자료다. 시작 셀과 후보를 합친 뒤 CU에서 상하좌우로 이어진 셀만 최종 지역 Grid에 남긴다. 과열은 점 제거 조건이 아니며 터렛 OFF/파괴가 다른 터렛의 활성·전력 예약을 해제하지 않는다.

Host는 Game Debugger에서 명시 시작 셀을 설정할 수 있다. 빈 목록은 CU 1셀 기본값이다. 목록은 CU 셀을 포함하고 4방향으로 연결돼야 한다. 범위 밖·단절된 목록은 거부한다. 적용은 명시 Reset으로만 실행하며 새 Grid 세대를 발행한다. 정책/Grid Reset은 명시 목록을 기본값으로 되돌린다. Scene/Prefab 직렬화는 수정하지 않았다.

설정은 `RegionSceneBinding`의 `world/region-start` v1 writer가 담당한다. 시작 설정 → 기존 공간 정의 → actor 복원이 끝난 뒤 조립부가 점유를 재평가한다. 기존 `world/spatial` 형식과 네트워크 지역 snapshot 형식은 유지했다. **새 필수 section이 없는 과거 Checkpoint는 적용 전에 거부한다. 저장 마이그레이션을 구현한 것은 아니다.** 실제 Weather writer가 없어 전체 전장 Save는 아직 별도 Gate다.

## 먼저 볼 코드

| 순서 | 파일 / 함수 | 확인할 내용 |
| --- | --- | --- |
| 1 | [RegionSceneBinding](../../../../../Assets/Scripts/Core/Bootstrap/SceneComposition/RegionSceneBinding.cs) `BindMatch`, `Update`, `RefreshDynamic` | 실제 Host 진입, 터렛/CU 입력 변화 감지, 최종 셀 발행. Client는 `BindReplica`만 사용 |
| 2 | [DynamicRegionCapturePolicy](../../../../../Assets/Scripts/Core/Domain/Regions/DynamicRegionCapturePolicy.cs) `EvaluateConnected` | CU 위치 노드 + 활성·비파괴 터렛 → 투영 후보 + 시작 셀 |
| 3 | [RegionGridConnectivity](../../../../../Assets/Scripts/Core/Domain/Regions/RegionGridConnectivity.cs) `RetainConnected` | 대각선·행 경계 연결을 막는 4방향 탐색. 정렬된 셀 반환 |
| 4 | [설정 API](../../../../../Assets/Scripts/Core/Bootstrap/SceneComposition/RegionSceneBinding.Dynamic.cs) `TrySetDynamicStartCells` | 권한·Reset·범위·연결 검사, 방어적 복사, 새 Grid 세대 |
| 5 | [설정 writer](../../../../../Assets/Scripts/Core/Bootstrap/SceneComposition/RegionSceneBinding.Checkpoint.cs), [조립부](../../../../../Assets/Scripts/Core/Bootstrap/SceneComposition/InitialStageCompositionRoot.Checkpoints.cs) `FinishCheckpointRestore` | 설정 적용/rollback과 모든 원본 복원 후 재평가 |
| 6 | [RegionDebugControls](../../../../../Assets/Scripts/Core/Editor/GameDebugger/Controls/RegionDebugControls.cs) `DrawStartCells` | 초안/적용 분리, Host 제한, 오류·셀 수 표시 |

자세한 호출 예시는 [지역 API](../../../../02_architecture/01_core/core-sprint-region-and-request-api.md#cu-시작-영역-설정과-저장), 조작 순서는 [Game Debugger 사용법](../../../../05_tooling/game-debugger-guide.md#cu-시작-영역-확인)에 있다.

## 실제 확인한 범위

Unity 6000.3.23f1, 원본 프로젝트의 Editor/CLI에서 실행했다. 0개 발견·비동기 제출 성공을 테스트 통과로 계산하지 않았다.

| 검사 | 결과 | 범위 |
| --- | --- | --- |
| 도메인 | 14/14 | 기존 삼각형 투영 7 + 새 CU/연결/경계/중복 ID 7 |
| 실제 바인딩·설정 writer | 6/6 | Preview Scene의 실제 Controller/Match, 권한·무효 입력 보존·Apply/Rollback·종료 해제 |
| 실제 Initial_Stage Play + Editor UI | 1/1 | Host 모드 Reset·시작 셀 적용/오류·writer 등록, 시작 셀 UI 실제 OnGUI, 명시 Fade 취소·Play 종료 |
| 관련 회귀 | 최초 48개 중 47 통과, 1 실패 | 실패는 CU가 없다는 옛 가정의 Player 통합 테스트. 원본 실패 기록 보존 |
| 승인한 Player 통합 테스트 재검사 | 1/1 | CU 정점·셀, 갱신 대기, 터렛 ON/OFF·전력 유지, 모두 OFF 후 CU 1셀 |
| 저장소 검사 | `.meta` 검사·diff 공백 검사 통과 | 새 C#과 meta 쌍. Scene/Prefab/생성 폴더 변경 없음 |

위 실행은 중복 범위를 포함한다. 48개가 한 번의 실행에서 모두 통과했다고 표시하지 않는다. 바뀌지 않은 47개를 반복 실행하지 않고 승인 후 바꾼 1개만 재검사했다.

Player 폴더에서는 사용자가 승인한 `RegionSceneIntegrationTests.InitialStage_StaticToDynamicCaptureAndReleaseKeepsRemainingTurretsActive` 하나만 바꿨다. 예전 `점유 == 0` 대기는 CU 기본 셀 때문에 즉시 건너뛰었고, 정확히 삼각형 1개/점유 0개라는 가정도 새 규칙과 충돌했다. 이제 revision 또는 새 평가 결과를 기다린다. 활성·전력 검사는 유지했다. Player 게임 코드 수정 승인으로 확대하지 않았다.

실제 Play 검사 중 포털 이동과 Editor 종료가 겹치면서 `SceneFadePresenter.BlendAsync → RunAsync → SpaceshipTravelService.EnterAsync → SpaceshipEnter.OnTriggerEnter2D` 경로의 취소 예외도 확인했다. Core 수명 취소만 정상 종료로 처리하고 파괴된 UI 접근을 막았다. 별도 HOTFIX 커밋으로 분리한다. 일반 오류와 호출자 취소는 계속 전달한다.

컴파일 최초 명령은 0오류/0경고였지만 후속 recompile wrapper에는 timeout과 임시 상태 파일 공유 오류가 있었다. 실제 assembly 로드·`scriptCompilationFailed=false`·양수 개수의 실행 테스트로 별도 확인했다. 마지막 통합 검사는 상태 파일과 원본 Editor 로그 모두 1/1 성공이며, 그 뒤 Pipeline 서버 조회는 연결 불가였다. 이를 새 게임 오류나 wrapper 성공으로 바꾸어 기록하지 않는다. 마지막 API로 읽은 상태는 Play=false, Initial_Stage dirty=false이며 이후 승인 테스트도 ExitPlayMode를 통과해 Scene 복귀 로그를 남겼다.

## 남겨 둔 범위와 이유

| 배치 | 보류 이유 / 다음 접점 |
| --- | --- |
| P1+P7a 해금/확보 분리·일반 스폰 반경 차단 | Enemy 계획·실행 소비자와 함께 바꿔야 한다. `CapturedCells` 의미만 먼저 바꾸지 않음 |
| P2a 소환 등록·P6 실제 전투 사실 | Enemy 생성/피해 적용 원본의 좁은 통지 접점 필요 |
| P6b 정적 확보·상실·다음 자동 해금 | 위 점유 의미와 실제 성과/압력 관측이 선행 |
| P7b/P8 운영 요청·환경 계획 | Enemy 계획 소비자 승인, Weather 원본 계약 필요. 기존 난이도 계산은 유지 |
| P10/P11 중간/최종 보스 | 실제 Definition/Prefab/보스 Scene·점유 대상 마스크·필수 저장 원본 필요 |
| P12 실제 전장 Save·구조 | Weather writer와 Player 다운/구조 성공 접점 필요. 시작 설정 writer만으로 전체 완료라 하지 않음 |
| P5/P13/P14/P15 | 실제 진행 원본·원격 actor·HUD 소비·완성형 통합은 기존 배치 추적 유지 |

이번에는 지형/통행 가능 마스크, 실제 두 Client의 새 CU 정책 인수, Windows Build를 실행하지 않았다. #638은 관련 구현이지만 Client/HUD 인수와 파괴/콘텐츠 조건까지 자동 종료로 처리하지 않는다. 승인된 Core 단독 범위를 넘어 다른 파트 파일을 고치지 않았다.

추가 기획 질문은 없다. 다음 착수에는 [배치별 사전 통보 범위](2026-11-12-core-progression-batch-plan.md#팀원-코드-사전-통보-목록)의 수정 승인과 제작 자료가 필요하다. 개인 원본 증거·팀원에게 보낼 문서는 `Docs/Local/Review/CoreExecution-20261112`에 두며 Git 공유에서는 제외한다.
