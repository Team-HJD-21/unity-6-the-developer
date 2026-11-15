# Editor 저장 초기화와 Stage 1 진입 검토

PR: 생성 후 기록

## 바뀐 동작

Editor의 XP·터렛 성장·안전장치 저장은 한 Play 안에서만 유지하고 종료하면 삭제한다. 빌드의 기존 영구 저장은 유지한다. Initial_Stage를 제품 진입 목록에서 빼고 실제 Stage_1으로 시험한다.

## 코드 진입점과 데이터 흐름

- `AppBootstrap.CreateCheckpointStore`: Editor는 `persistentDataPath/EditorPlayMode/<Application.dataPath Hash>/Checkpoints`, 빌드는 기존 `persistentDataPath/Checkpoints`다. 프로젝트별 Editor 저장을 분리하며 기존 빌드 저장을 읽거나 지우지 않는다.
- `BinaryCheckpointStore`: Editor용 `deleteOnDispose`만 시작 시 남은 임시 저장을 비우고 종료 시 checkpoint/자체 GUID 임시 파일을 삭제한다. 다른 파일·하위 폴더를 재귀 삭제하지 않는다. 기존 파일 교체와 같은 lock으로 닫고, 종료 후 대기 쓰기는 거절해 파일 재생성을 막는다.
- `AppPlayModeShutdown` → `AppRoot.Dispose` → 기존 성장 Flush/Match 정리 → `AppServices.Dispose` → Store 종료/삭제다. 런타임에는 Editor API가 들어가지 않는다.
- `EditorBuildSettings`: Startup 다음 첫 전장은 Stage_1 하나다. Initial_Stage는 목록에서 제거하되 자산은 남긴다. 기존 우주선 출구/재시작의 Stage_1 경로와 맞춘다.
- `AppPlayModeShutdown`: 과거 Play 시작 씬 지정이 Initial_Stage인 경우만 Stage_1으로 교체한다. 현재 편집 씬이나 다른 시작 지정은 덮어쓰지 않는다.
- `CoreNetworkBuildPipeline`: 실제 Stage_1으로 검증 빌드를 구성한다.
- `InitialStageCompositionRoot`: 타입명은 유지하고 로그에는 실제 씬 이름을 출력한다. 이전에는 Stage_1에서도 Initial_Stage라고 표시됐다. 사용자가 본 조건의 정확한 재현을 이 로그 문제만으로 단정하지 않는다.

## 확인과 한계

[검사 원본 요약](evidence/editor-playmode-storage-20261115.json)에 새 검사 7/7과 기존 App 종료·성장 저장·터렛 복원 15/15 결과를 기록했다.

- 실제 Startup → 우주선 → 제작된 SpaceshipExit의 요청 → Stage_1, Match 준비와 최대 전력 500을 확인했다.
- 실제 App Play에서 파일 생성 후 ExitPlayMode 뒤 삭제됨을 확인했다.
- 임시 저장의 잔여 파일 정리, 종료 대기 쓰기 차단, 빌드 기본 저장 보존, 구형 Play 시작 지정 교체/편집 씬 보존을 확인했다.
- 기존 영구 checkpoint 35개는 전후 SHA256이 같다.
- 첫 검사 컴파일 오류와 결과 없는 timeout 시도는 성공에 포함하지 않았다. 검사 코드의 비동기 예외 대기를 동기 Task 경계로 바꾼 뒤 완료된 결과만 센다.

Windows 빌드와 자연 전투 밸런스는 미실행이다. 전체 월드/날씨 writer 부재는 그대로이며, 이 PR이 전체 저장 시스템을 완성하지 않는다. UI 제작기의 별도 폴리싱·폰트·HUD 변경은 포함하지 않는다. 테스트 파일은 주 실행 흐름을 읽을 때 생략할 수 있다.

## 사용·협업 주의

Editor에서 Play를 껐다 켜서 이어하기를 확인하는 방식은 당분간 지원하지 않는다. 저장/복원 검사는 같은 Play 안에서 진행한다. 과거 빌드 저장은 보존하지만 Editor에서 사용하지 않으므로 이전 성장 때문에 시험 결과가 달라지지 않는다. 우주선/Player/Enemy의 게임 로직과 Scene 배치는 수정하지 않았다.

정책을 되돌리려면 Bootstrap의 Editor 저장 분기를 제거하고 영구 Store를 사용한다. Build 목록을 되돌리면 구형 전장 진입이 다시 가능해지므로 함께 검토한다.
