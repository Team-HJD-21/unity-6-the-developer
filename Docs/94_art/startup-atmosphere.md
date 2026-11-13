# Startup 장식과 버전 표시

2026-11-13 실제 적용. 이전 `MainPolish20261113/revision3`는 Play 메모리에만 존재하는 검토 시안이었다. 이 문서는 저장된 Startup/Prefab에서 다시 실행되는 구현을 설명한다.

## 실제 저장 위치

- Scene: `Assets/Scenes/Bootstrap/Startup.unity`.
- Prefab: `Assets/Prefabs/UI/Common/StartupAtmosphere.prefab`.
- Runtime: `StartupAtmosphere`. 씬의 Controls, 로고, Credits, 보호할 RectTransform 참조를 저장한다. `StartupSceneRoot`에서 ScreenManager를 연결한다.
- Art: `Assets/Art/UI/MainAtmosphere`. 기존 승인 시안의 별똥별 PNG를 그대로 복사하고, 443×443 셀 8개로 import했다. 텍스처를 Runtime에 읽거나 새로 생성하지 않는다.
- 제작 메뉴: `Tools > TeamHJD > The Developer > Main > Apply saved atmosphere`. 편집 중인 dirty Scene은 덮어쓰지 않는다. 공통 Main 재생성 경로에서도 같은 연결을 보존한다.

## 표현 규칙

별똥별 Image 하나와 작은 별 두 개를 재사용한다. 별똥별은 네 영역 중 직전과 다른 곳에서 발생하며 0.78~1.04초 동안 이동하고 사라진다. 사용자 후속 요청으로 다음 발생 대기는 14~24초에서 **7~12초**로 줄였다. 작은 별의 발생 간격도 1.2~2.7초에서 **0.6~1.35초**로 줄였으며 수명 1.2~1.9초와 동시 최대 두 개는 유지한다.

별똥별은 승인 시안과 동일한 PNG·셀·기본 UI 블렌딩을 사용한다. 첫 적용에서 별도 alpha clip Shader가 옅은 꼬리를 제거한 차이를 발견해 제거했다. 반짝임은 Editor에서 원래 배경의 어두운 위치 5,250개를 미리 추출하고 Runtime에서 그중 UI와 겹치지 않는 곳을 고른다. 시안처럼 어두운 배경에서 작은 빛으로 읽히게 하며, Runtime 화면 읽기나 원본 PNG 편집은 하지 않는다.

로고는 원래 font atlas와 형태를 유지한다. TMP Distance Field Shader를 바탕으로 만든 별도 Shader/Material에서 글자 안에만 짧은 금속 반사 띠를 통과시킨다. 재생 중 Material 복제 한 개를 사용하고 종료 시 원래 Material로 복구한다. 원본 폰트·배경·로고 애니메이션 에셋을 덮어쓰지 않는다.

강조 효과가 동시에 몰리지 않게 광택·별똥별·반짝임의 시간을 나눈다. 로고·네 버튼·버전의 실제 Rect에 여백을 더해 전 경로를 검사한다. 모든 장식 Graphic의 raycastTarget은 false이고 CanvasGroup도 입력을 막지 않는다. 설정이나 Credits가 열리면 장식을 숨기고, 복귀 후 조용한 대기부터 다시 시작한다. 효과용 렌더 화면 읽기나 매 프레임 자산 검색은 사용하지 않는다.

## 버전과 Main 프레임 제한

실제 PlayerSettings.bundleVersion은 `0.1.0-alpha`다. `StartupControlsView.VersionLabel`은 Awake 때 Application.version을 읽어 `v0.1.0-alpha`로 표시한다. 제작 도구도 같은 값과 작은 JetBrains Mono 서식을 저장하므로 시안을 재생성해도 예전 Beta 문구가 살아나지 않는다. 임의 빌드 번호를 만들지 않는다.

Main은 `StartupControlsView.MenuFrameRate`의 기본값 60을 사용한다. Main이 비활성화되면 이전 targetFrameRate로 복구하며 게임 플레이의 프레임 설정을 이 값으로 고정하지 않는다. 기존 vSync 설정은 변경하지 않는다.

버튼 등장 완료만으로 Start Game을 자동 선택하지 않는다. 마우스 호버는 실제 포인터 진입 때 표시하고, 키보드·패드의 첫 탐색/확인 입력이 있을 때만 기본 선택을 준비한다. 이후 버튼 간 탐색과 선택 강조는 기존 EventSystem을 따른다.

## 검증 범위

실제 실행 결과는 `Docs/Local/Validation/StartupFix20261113`에 보존한다. 파일에 컴포넌트가 있다는 것만으로 정상 재생·입력·Standalone 검증 완료를 선언하지 않는다.

2026-11-13 실제 적용 후 Computer Use로 Play 버튼과 Edit/Play Mode 메뉴에서 반복 실행했다. 최초 3.35초, 다음 1.19초, 추가 반영 후 첫 진입 3.42초였고 각 실행의 AppRoot/활성 AudioListener는 각각 한 개였다. Scene Reload는 유지한다. 자세한 조건은 [Play 진입 설정](../05_tooling/startup-play-mode.md)을 따른다.

첫 실행에서는 광택 Shader의 fragment 입력을 `vertex`로 잘못 참조해 마젠타가 발생했다. 실제 필드인 `position`으로 수정한 후 정상 로고와 글자 내부 광택을 실제 화면으로 다시 확인했다. 최초의 마젠타 캡처는 검증 실패 자료로 보존하며 최종 결과로 사용하지 않는다.

최종 빈도로 48초 정상 Update 재생에서 별똥별 네 번/서로 다른 네 영역, 작은 별 동시 최대 두 개, 검사한 UI bounds 겹침 0을 기록했다. 녹화가 끝난 뒤 측정한 1,261프레임의 프레임 간격 중앙값은 16.67ms, 95% 지점은 17.17ms, 50ms 초과는 0이었다. 해당 Main 검수 조건의 결과이며 전체 게임 성능 판정이 아니다.

최종 화면은 `frequency-v2`의 PNG와 실제 Game View 24초 MP4로 기록했다. 영상은 무음 검토 자료이며 게임의 OST 연결과 별개다. Standalone 빌드는 수행하지 않았다.
