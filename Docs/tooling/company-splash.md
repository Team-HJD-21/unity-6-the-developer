# 회사 로고 스플래시

2026-10-28 추가. 최종 Core 작업 프로젝트 `Validation/core-sprint-20261026`에 반영했다.

- 일반 Windows 빌드의 시작 Scene: `Assets/Scenes/CompanySplash.unity`.
- 영상: `Assets/StreamingAssets/Branding/TeamHJD_Splash.mp4` — 제공 원본을 그대로 복사한 12초/1080p/무음 영상, 1,135,964 bytes.
- 재생 종료 → 기존 `SceneFlowController` → `Assets/Scenes/Gameplay/Initial_Stage.unity` → 기존 Host Match 진입.
- 검은 배경 위 16:9 비율을 유지한다. 영상 재생은 unscaled time을 사용한다.
- 준비 실패/영상 오류는 경고를 기록하고 게임으로 진행한다. 영상/RenderTexture는 해제한다.
- Editor에서는 CompanySplash Scene을 열고 Play하면 확인 가능하다. Initial_Stage 직접 Play·결과 화면의 Restart는 스플래시를 반복하지 않는다.
- `CoreNetworkBuildPipeline.BuildWindowsDevelopment`는 전용 네트워크 검증 용도라 스플래시 Scene을 제외한다. 일반 Build Settings 또는 `-buildWindows64Player`를 사용한 Windows 빌드에는 포함된다.

## 확인 결과

공식 Unity 6000.3.23f1 GPU EditMode 실제 검사 1/1 통과. 원본 영상의 실제 디코딩 프레임·무음 설정·재생 종료 후 Initial_Stage Host Match 진입·VideoPlayer 해제를 확인했다. 증거는 로컬 `Docs/Local/Validation/CompanySplash20261028/splash.xml`, `company-splash-frame.png`다.

원본 H.264의 timestamp/color-primary 메타데이터에 WindowsMediaFoundation 보정 경고가 있다. 실제 영상 프레임과 완료 전환은 정상 확인했으며 원본을 임의 재인코딩하지 않았다. 이 기록은 새 일반 Windows 빌드의 사람이 보는 전체 플레이 인수를 대신하지 않는다.

빌드 출력은 `Builds/Windows/TheDeveloper.exe`, 영상은 산출물의 `TheDeveloper_Data/StreamingAssets/Branding/`에 들어간다. 배포할 때 exe만 떼어내지 말고 `Builds/Windows` 폴더 전체를 사용한다.
