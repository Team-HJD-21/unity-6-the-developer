# Startup의 빠른 Play 진입

2026-11-13 Unity 6000.3.23f1 Editor에서 반복 Play 진입의 지연을 줄이기 위해 `Enter Play Mode Options`를 활성화하고 **Reload Domain만 생략**한다. Scene Reload는 유지한다. 저장된 설정은 `ProjectSettings/EditorSettings.asset`의 enabled=1/options=1이다.

이전 Editor.log의 Domain Reload Profiling은 약 29~30초였으며 InitializeOnLoad 단계가 약 15~16초를 차지했다. 반복 Play에서는 이 초기화를 생략한다. 코드 수정 후 실제 어셈블리 컴파일/재로드까지 생략하는 설정은 아니므로 컴파일 직후의 대기는 별개다.

AppBootstrap, CameraManager, UIEscapeInput 및 Player의 기존 SubsystemRegistration 초기화 경로를 확인했다. 실제 Startup을 반복 실행해 AppRoot와 활성 AudioListener가 각각 한 개임을 확인한다. Scene 재로드를 꺼서 이전 씬 오브젝트를 재사용하지 않는다.

2026-11-13 현재 실제 Computer Use의 Play 버튼/메뉴로 실행한 진입은 약 3.35초, 다음 진입은 1.19초였다. 코드와 프리팹을 추가로 반영한 다음 첫 진입은 3.42초였다. 같은 조건의 반복 인수 결과이며 모든 플랫폼·네트워크/Steam 세션의 무결성을 보증하지 않는다. 전체 검수 원본은 `Docs/Local/Validation/StartupFix20261113`에 있다.

Main 실행 중에는 StartupControlsView가 60fps를 요청하고 비활성화 시 이전 targetFrameRate를 복구한다. 게임 전체의 품질 설정이나 vSync는 변경하지 않는다. 상세 표현·저장 위치는 [Startup 장식](../94_art/startup-atmosphere.md)을 따른다.
