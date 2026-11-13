# Encounter 개편 / Stage1 통합 기록

2026-11-13 · 진행 중. 이 문서는 전체 Stage1 완료 보고가 아니다.

## B0 — 실제 전장 진입

### 바뀐 동작과 진입점

- `AppScreenPresenter.StageScene`을 `Assets/Scenes/Gameplay/Stage_1.unity`로 맞췄다. 출격/실패 화면 재시작이 같은 실제 전장을 참조한다.
- `SpaceShipGrayBox.unity`의 Exit 목적지 두 곳도 같은 전체 경로로 맞췄다. Reference 폴더의 동명 Scene과 혼동하지 않는다. Scene의 나머지 오브젝트·참조·배치는 변경하지 않았다.
- 실제 흐름: `StartupControlsView.Start` 버튼 → 기존 GameFlow의 우주선 진입 → 기존 `SpaceshipExit.ExitAsync` → `SpaceshipTravelService.ExitAsync` → `GameFlowController.LaunchMissionAsync` → Stage1 조립.

### 확인한 범위

- 현재 실행 Editor Unity `6000.3.23f1`에서 CLI로 실제 Play Mode를 실행했다.
- 실제 Start 버튼의 UnityEvent를 호출하고 우주선 도착을 확인했다. 우주선에 임시 Player와 활성 Hub Camera 각 1개가 준비됐다.
- 우주선 문이 사용하는 실제 출격 API를 호출했다. 비동기 Task가 예외 없이 완료되고 GameFlow `Mission`, Scene `Gameplay/Stage_1.unity`, Running Match를 확인했다.
- Stage1에서 AppRoot 1개·등록된 Player 1명·활성 Main Camera 1개를 확인했다. 검증 뒤 Play Mode를 종료했다.
- 이것으로 지속 생산·지역 확보·구조·저장/복원·보스·협동 플레이까지 통과했다고 판단하지 않는다. 해당 배치는 이후 별도 검증한다.

### 팀원 변경과 기존 작업

- World 제작 파일 수정은 `SpaceShipGrayBox.unity`의 `returnSceneName` 두 값이다. #716에서 실제 전장 이름이 변경되어 최초 출격도 새 전장에 연결하기 위한 수정이다.
- UI 제작기와 Unity 실행 시간을 조율했다. 기존 Tactical/HUD/입력/카메라·폰트 작업과 스테이징은 포함하거나 덮어쓰지 않았다.
- 이번 목표의 필요한 팀원 코드 변경은 사용자가 승인했다. 이후 실제 변경 파일과 이유를 배치별로 추가한다.

## 현재 남은 작업

Encounter 네 Step 및 Planner 정책 연결, 점유 Region 그룹의 할당 제외와 실제 생성 재검사, 실제 전투 기반 정적 확보/해금, 지속 생산/환경·시간, 성장 저장/월드 복원, 구조·실패, 33/66/100 진행 및 실제 콘텐츠 통합이 남아 있다. Game Debugger의 기존 진단 경계를 사용하고, 실행하지 않은 검증을 완료로 기재하지 않는다.

개인 실행 로그·명령 원본은 `Docs/Local/Execution/EncounterStage1-20261113/`에 공유 제외로 보존한다. 팀 공유 문서는 확인한 결과와 사용법을 이곳 및 관련 API 문서에 반영한다.
