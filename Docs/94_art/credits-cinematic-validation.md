# Credits 컷신 검증 범위

2026-11-13, Unity 6000.3.23f1 / Windows DX11.

Main의 CREDITS를 실제 마우스로 클릭해 Scene에 진입하고 정상 Update 재생으로 다섯 파트·기존 로고·마지막 정적 명단까지 확인했다. 정적 명단에서 실제 마우스 클릭으로 복귀한 뒤 Credits Scene unload, 입력 잠금 해제, MainMenu Phase 보존을 조회했다. 원본 기록은 로컬 작업 자료 `Docs/Local/Validation/CreditsProduction20261113`에 있다.

이름이 보이는 182개 시간 표본에서 화면 잘림 및 이름과 주인공/비행 우주선 bounds 겹침 0, 최종 명단끼리의 텍스트 bounds 겹침 0이었다. 별도로 실제 정지 화면 16장과 정상 재생 화면 14장을 확인했다. 모든 픽셀과 모든 화면비를 완전히 검증했다는 뜻은 아니다. 화면비 처리는 고정 16:9 실시간 렌더와 AspectRatioFitter/검은 matte를 사용한다.

같은 시점의 실제 카메라 렌더에서 효과를 끈 대조를 수행했다. RGB 채널 차이 합 9를 넘는 픽셀 수는 용접 불티 2,079, 헬멧 반사광 2,234, 착지 먼지 3,548, 엔진 불꽃 10,023이었다. 43초 위치에서 오래된 착지 먼지가 남지 않는 것도 확인했다. 생성된 파티클 개수만으로 시각적 성공을 판단하지 않았다.

Missing Script 0, Credits Scene의 BuildSettings 활성화를 확인했다. 검토 영상은 같은 Scene/카메라/Shader/ParticleSystem으로 1280×720, 24fps, 74초를 출력했다. 게임에서는 이 영상을 재생하지 않는다. 검토 MP4는 무음이며 Scene의 비행 소리는 별도 AudioSource다.

검토 MP4는 Unity VideoPlayer로 0..1775 전체 1,776프레임과 재생 끝 도달을 확인했다. 네 시점의 디코딩 결과와 동일 시각 원본의 평균 RGB 채널 차이는 0.917 / 0.909 / 0.984 / 0.095였다. Windows MediaFoundation의 color primaries 0 경고는 남아 있다. 모든 OS/디스플레이 색관리까지 검증한 결과는 아니다.

실제 Esc 입력 검사는 Computer Use의 Escape 요청이 Unity InputSystem에서 Keyboard TEXT 이벤트로만 관측되어 통과로 기록하지 않았다. 제품 코드에 도구용 문자 입력 우회를 넣지 않았다. Standalone 빌드와 다른 OS 검증도 수행하지 않았다.

기존 Main 배경·로고 애니메이션·Spaceship Scene은 보존했다. NEXON SDF는 이름용 김/빈/태를 추가하며 기존 543자와 glyph index를 보존했다. Inspector 편집 지점과 재생성 주의 사항은 [제작 규칙](credits-cinematic.md)을 따른다. 이 기록은 공통 HUD 디자인의 새 승인이나 팀 전체 빌드 통과 선언이 아니다.

검수 종료 시 Play Mode 종료, Startup 단일 Scene/dirty=false, VideoPlayer 임시 객체 0, 기존 Console Error Pause=true 복원을 확인했다. 최종 recompile 조회는 up_to_date, 오류/경고 목록 0이었다. Unity가 저장한 Scene YAML의 빈 값 뒤 공백은 일반 git diff --check에 걸리며, 코드·문서 검사와 구분했다. 해당 YAML 전체를 공백 정리하지 않았다.
