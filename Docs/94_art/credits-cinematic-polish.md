# Credits 컷신 폴리싱 기록

2026-11-13 후속 사용자 요청에 적용한 변경이다. 제품 전체·공통 HUD 디자인의 최종 승인을 뜻하지 않는다.

## 적용 내용

- 기존 게임용 작은 캐릭터를 큰 화면으로 확대하던 방식을 컷신용 정면·우측 보기·용접 자세 원화로 교체했다. 기존 외형을 참조하고 드론과 선 두께/음영을 맞췄다. 게임용 Motion Sprite 원본은 수정하지 않았다.
- 용접 자세는 양손과 공구를 한 원화로 제작해 파지가 어긋나지 않게 했다. 노즐 Transform에 작은 불규칙 아크와 짧은 고온 분사·탄도 불티·증기·냉각 잔불을 연결했다. 선체의 접점 반사광과 헬멧의 반사광은 별도 Shader 속성이다. 전경 금속판은 연속된 별도 Sprite다.
- 비행 배기는 두 엔진 각각에서 발생한다. 불꽃은 길이 변조와 작은 UV 변형의 위상을 다르게 준다. 통과한 뒤 잔류 궤적과 같은 방향의 마스크로 이름을 공개한다. 글자 윤곽을 따라 그리지 않는다.
- 소행성은 84개를 명도·크기·회전·속도별로 나누고 밀집 구간과 빈 통로를 만들었다. 전경의 큰 돌이 이름 앞을 지나가며 깊이와 공개 동작을 만든다.
- 터렛은 삼각 배치로 순차 투하하며 지면 그림자·제동 불꽃·착지 먼지·반동을 연결했다. 최종 사용자 정정에 따라 **기체 명판 패널은 제거**했다. 황재동 이름은 낙하 오브젝트의 자식이 아니며, 모두 착지한 뒤 앞쪽 터렛 오른편 지면에 텍스트만 Fade-in한다.
- 드론을 작은 크기로 조정하고 정면/우측 보기 자세, 바이저 반사, 두 번의 눈 깜빡임, 화면 전환을 연결했다. 얼굴 접근 때 플레이어가 화면 끝에 남지 않게 구도를 조정했다.
- 파트 길이는 총 51초, 기존 로고 4초 뒤 정적 명단이 표시된다. 최종 명단은 자동 종료하지 않는다. 기존 SFX 8개 cue에 시작/종료·피치·볼륨 envelope를 붙였다. Main 배경·로고·음악은 변경하지 않았다.

## 실제 검증

Unity 6000.3.23f1 / Windows DX11. 원본은 로컬 작업 자료 `Docs/Local/Validation/CreditsPolish20261113`에 보관했다.

- Main CREDITS를 Computer Use 실제 마우스로 클릭해 진입했다. 최종 버전은 강제 seek 없이 정상 Update 재생으로 58초에 도달했고 14개 화면과 228개 시간 표본을 저장했다. SFX 8개 cue의 실제 AudioSource 재생도 관측했다.
- 같은 적용 Scene의 Camera/Shader/ParticleSystem으로 별도 정지 화면 20장을 검사했다. 이름이 보이는 168개 표본의 picture 잘림 0, 검사 대상 주인공/비행 우주선과의 bounds 겹침 0, 마지막 명단끼리의 텍스트 bounds 겹침 0이었다. 드론 화면 안 최소 여백은 0.2168 world unit이다.
- 터렛 이름은 Image 배경 컴포넌트 0, parent는 Chapter 자체다. 착지 후 이름과 세 터렛 Sprite bounds의 겹침 0을 별도로 검사했다. 모든 픽셀과 모든 화면비의 무겹침을 보증하는 결과는 아니다.
- 용접 아크와 노즐 Transform 사이 거리 0, 금속판 윗면과 노즐의 간격 약 0.0336 world unit, helmet top과 picture 상단의 최소 여백 약 8.19%를 확인했다. 실제 화면도 확인했다.
- 같은 시점에서 renderer를 끈 RGB 대조에서 용접 입자 전체+아크는 3,231픽셀, 아크만 2,007픽셀에 차이가 있었다(채널 차이 합 9 초과). 이는 효과가 화면에 실제로 나타나는지 보는 검사이며 픽셀 수를 미술 품질 점수로 사용하지 않는다.
- Missing Script 0, BuildSettings 활성화를 확인했다. 최종 수정 코드의 Unity recompile은 오류/경고 0이었다. Standalone 빌드는 수행하지 않았다.

검토 MP4는 같은 Scene의 카메라/VFX로 1280×720, 24fps, 65초를 출력했다. 효과음은 동일한 authored clip/cue/피치/envelope를 48kHz stereo로 혼합했으며 Main 음악은 제외했다. 8개 cue, 최대 PCM 진폭 0.1234로 clipping은 없었다. 실제 Scene에서는 AudioSource를 사용하며 이 영상을 재생하지 않는다.

Unity VideoPlayer로 0..1559 전체 1,560프레임과 끝 도달을 확인했다. 오디오 트랙 1개, stereo/48kHz를 조회했다. 네 동일 시각 원본 대비 디코딩 영상의 평균 RGB 채널 차이는 1.051 / 1.118 / 1.437 / 0.081이었다. MediaFoundation color primaries 0 경고는 남아 있어 모든 디스플레이/OS 색관리 검증을 뜻하지 않는다. Shader 메시지 목록은 비어 있었다.

마지막 명단에서 실제 마우스 클릭으로 복귀한 뒤 Credits Scene unload, MainMenu Phase, 입력 잠금 해제, 화면 스택 0, 컷신 AudioSource 0, 임시 VideoPlayer 0을 확인했다. Main Scene·배경·원래 캐릭터/우주선·폰트·ProjectSettings 등 직전 검수에서 보호한 원본 10개는 모두 같은 SHA256이었다. 기존 미커밋 HUD 변경은 보존했고 커밋·푸시는 하지 않았다.

실제 Esc 키 검증은 이전 작업에서 Computer Use의 Escape가 Keyboard TEXT 이벤트로만 전달되는 제한을 확인했다. 이번 작업에서 재검증 통과로 바꾸지 않았고 제품 코드에 문자 입력 우회를 추가하지 않았다.

## 2026-11-13 후속: OST 시간 보존과 배경 슬라임

- 비행 파트 안에서 13.25~13.75초에 0.55 world unit 후진하고 잠깐 자세를 잡는다. 14초의 기존 가속 시작을 유지하고 14.85초까지 원래 궤적으로 복귀한다. 준비 중 엔진 불꽃의 길이와 밝기도 낮아진다. 실제 Scene의 40Hz 위치 표본에서 변경 구간 밖의 원래 궤적과 최대 차이는 약 0.0000115 world unit이었다.
- 기존 Slime Idle 네 Sprite를 사용하는 배경 슬라임 한 마리를 소행성 파트에 추가했다. sorting order 10으로 소행성(15 이상) 뒤에 놓고 이름 위쪽으로 통과시킨다. 원본 PNG·importer와 적 AI는 수정하지 않았다.
- Main CREDITS를 실제 마우스로 클릭해 정상 Update로 58초까지 재생했다. 15개 화면을 저장했고 55초 이후 정적 명단을 확인했다. 추가 슬라임의 렌더 켜기/끄기 비교 14표본 중 11표본에서 실제 화면 차이가 있었으며, 이름의 실제 텍스트 bounds 안에서 생긴 슬라임 픽셀은 0이었다. 화면 밖 표본은 가시성 통과로 세지 않았다.
- 파트 길이·로고 길이·파트 Fade·역할 공개 시작·모든 효과음 cue 데이터를 적용 전후 대조해 동일함을 확인했다. Main Scene, Credits Scene, Director 소스의 SHA256도 보존했다. Prefab에는 배경 슬라임과 그 참조를 추가했다.
- 적용 구간 9.5~29.5초의 검토 MP4는 20초/1280×720/24fps다. Unity VideoPlayer에서 0..479 전체 480프레임과 끝 도달, stereo 48kHz 오디오 트랙 한 개를 확인했다. 기존 authored 효과음만 포함하며 제작 중인 OST는 건드리지 않았다. 정지 평가로 만든 검토 영상과 정상 Update 재생 기록은 별도로 보관한다.
- 큰 품질 개선은 [비행·터렛 재구성안](credits-flight-turret-rework.md)에 정리했다. 새 시점 원화와 기구 동작은 제안 상태이며 이번에 구현한 것으로 보고하지 않는다. 로컬 검수 원본은 `Docs/Local/Validation/CreditsFlightAnticipation20261113`에 있다. Standalone 빌드는 수행하지 않았다.

## 편집과 원화

[편집 지점](credits-cinematic.md)과 [원화·참조·최종 생성 프롬프트](credits-cinematic-generation.md)를 따른다. 신규 PNG 네 개는 `Assets/Art/Cinematics/Credits/AstronautFront.png`, `AstronautLookRight.png`, `AstronautWelding.png`, `HullWorkPanel.png`이며 내장 image_gen을 사용했다. 원본 생성 PNG의 alpha를 유지한 채 Sprite rect를 importer에 저장했다. 컷신만 재작성할 때는 `Rebuild cinematic only` 메뉴로 Main 연결을 보존한다.
