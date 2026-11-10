# Unity 패키지와 CLI 확인

기준 Editor는 Unity `6000.3.23f1`이다. 이 저장소의 `Packages/manifest.json`과 `packages-lock.json`은 AI Assistant `2.19.0-pre.2`, Input System `1.17.0`, Unity Pipeline `0.7.0-exp.1`을 고정한다. 팀원은 프로젝트를 연 뒤 Package Manager의 복원과 스크립트 컴파일이 끝날 때까지 기다린다.

## 확인 순서

1. Unity Editor Console에서 빨간 Compile Error가 없는지 확인한다. 특히 `UnityEngine.InputSystem`을 찾지 못한다면 `manifest.json`의 Input System과 Package Manager 복원 상태를 먼저 확인한다.
2. 프로젝트 경로에서 `unity pipeline list`를 실행한다. 프로젝트 행의 Pipeline 버전과 서버 연결 가능 여부를 확인한다. Editor가 열린 상태에서 `unity status`가 연결된 Editor를 보여주는지도 확인한다.
3. Pipeline이 설치됐지만 연결되지 않는다면 Editor의 컴파일·도메인 재로드가 끝났는지 확인한 뒤 다시 조회한다. `/api/exec` 요청 중 `Thread was being aborted`가 뜨면 요청이 재로드 도중 중단됐을 수 있다. 그 로그만으로 게임 코드의 컴파일 성공이나 실패를 판단하지 않는다.
4. AI Assistant는 각자 자신의 Unity 계정과 서비스 접근 권한으로 확인한다. 로그인/API 접근 실패는 게임 스크립트의 Compile Error와 별개다.

`unity test`와 `unity run`은 별도의 batch Editor를 실행한다. 이 명령이 Unity Licensing Client 연결 오류로 끝난 경우 테스트는 **실행되지 않은 것**이다. 테스트 성공으로 기록하지 말고 라이선스 연결을 복구한 뒤 다시 실행한다.

## 저장소에 포함하지 않는 것

AI Assistant의 개인별 Editor 설정인 `ProjectSettings/Packages/com.unity.ai.assistant/Settings.json`과 계정 인증정보, 토큰, 사용자별 CLI 설정은 커밋하지 않는다. 패키지 버전과 팀 공통 절차만 공유한다.
