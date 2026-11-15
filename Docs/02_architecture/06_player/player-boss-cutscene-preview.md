# 보스 진입 컷씬 미리보기

2026-11-15 사용자 요청으로 Stage 1에 연출 확인 버튼을 추가했다. 실제 보스·보스 Scene이 없는 상태에서 **플레이어 확대 → 알 수 없는 힘 안내 → 화면 왜곡·흡입 → 암전 → 원래 화면 복귀**만 확인한다. 이후 사용자 선택에 따라 Cinemachine 3.1.7과 기존 Timeline 1.8.13으로 제작했다. 사운드는 추가하지 않았다.

## 회의 근거와 범위

- [11/11 통합 회의](../../99_archive/2026-11-11/integration-review.md)는 Player의 컷씬 전환 담당과 별도 보스 맵을 기록하며, 숲 연출은 초안으로 남겨뒀다.
- [현행 실행 계획](../../01_planning/current-execution-plan.md)과 [11/12 확정 배치](../../01_planning/02_implementation-records/2026/11/2026-11-12-core-progression-batch-plan.md)는 대상 셀 100% 후 같은 Match의 참가자 전원 전환, 전환 동안 공용 시간 정지와 실제 보스 연결을 요구한다.
- 이번 버튼은 사용자 요청에 따른 **로컬 연출 미리보기**다. 점유 트리거·저장·Scene 이동·보스 생성·승리·공용 시간 정지·협동 연출 동기화는 연결하지 않는다. 따라서 위 전체 보스 진입 요구를 구현 완료로 표시하지 않는다.

## 재생과 편집

`Assets/Scenes/Gameplay/Stage_1.unity`의 `PlayerBossCutscenePreview` 인스턴스에서 시작한다. Startup → 게임 시작 → 필드 진입 후 화면 오른쪽 아래 **보스 컷씬 미리보기**를 누른다. Stage 1에 로컬 Player와 출력 카메라가 준비됐을 때만 버튼이 표시된다. 우주선에서는 숨긴다. 재생은 약 8초이며 Esc로 취소할 수 있고 완료 후 다시 누를 수 있다.

- 프리팹: `Assets/Prefabs/UI/PlayerBossCutscenePreview.prefab`
- 타임라인: `Assets/Art/UI/BossCutscene/BossEntrance.playable`
- 자막·왜곡·암전 곡선: 같은 폴더의 `BossEntrancePresentation.anim`
- 흡입 표현: `BossSuction.shader`/`.mat`
- 실행 진입점: `PlayerBossCutscenePreview.TryPlay`, `Update`, `StopPreview`

프리팹의 PlayableDirector를 선택하고 Timeline 창에서 `Camera / approach and return`의 세 샷 겹침 길이를 조절한다. `Caption / suction / fade`의 Animation Clip에서 `_letterbox`, `_captionOpacity`, `_captionCharacters`, `_suction`, `_black`, `_captureFrame` 곡선을 조절한다. 자막 문장은 `Presentation/Unknown Force` TMP Text, 확대 비율은 컨트롤러의 `_zoomRatio`다. 카메라와 입력 바인딩은 실행 중 현재 로컬 Player에 연결하므로 최종 화면 확인은 Play 모드의 버튼으로 한다.

`_captureFrame`이 처음 0.5를 넘으면 현재 월드 카메라를 RenderTexture에 한 번 렌더한다. 이후 흡입 구간은 그 장면을 왜곡한다. 자막/UI를 함께 찌그러뜨리지 않으며 6.2초부터 월드 화면으로 복귀하고 암전을 걷는다. 실제 플레이어 좌표·크기·충돌은 변경하지 않는다.

## 카메라·입력 수명

기존 `CameraManager.Current`와 같은 실제 출력 Camera를 재사용한다. 별도 출력 Camera/AudioListener를 추가하지 않는다. 다른 카메라 override나 활성 CinemachineBrain이 있으면 시작하지 않는다. 컷씬 동안만 기존 `MatchCameraFollow`를 잠시 비활성화하고 CinemachineBrain을 수동 갱신한다. Timeline의 Cinemachine Track이 확대/복귀 샷을 블렌드한다. 끝나면 Brain을 비활성화하고 원래 크기·회전·추적 상태를 복원한다. 기존 CameraManager/Core 소스는 수정하지 않았다.

App의 `LocalInput.Acquire`로 자체 입력 lease를 획득한다. 해당 Player에 연결된 HUD Canvas만 잠시 숨기고, 종료 시 이전에 켜져 있던 Canvas를 복원한다. 정상 종료·Esc·비활성화·출력 카메라 변경·Player 교체·우주선 이동·피격은 같은 정리를 사용한다. 다른 화면이 가진 입력 잠금은 해제하지 않는다. 임시 Material/RenderTexture는 정리하며, 이 컨트롤러가 추가한 Brain은 컨트롤러 파괴 시 함께 제거한다.

미리보기 중에도 공용 게임은 진행한다. 피해를 받으면 즉시 취소해 조작을 돌려준다. 정식 보스 진입의 공용 시간 정지는 Core의 전환 수명과 연결하는 별도 작업이다.

## 검증

`PlayerBossCutscenePreviewTests`는 실제 제작 프리팹과 Cinemachine/Timeline을 사용하는 Play-mode 진입 fixture로 버튼 재생, 월드 프레임 캡처, 출력 카메라 확대/복귀, 입력 lease 정리, 재재생, 피격 취소, 우주선/다른 잠금 중 시작 차단, 비활성화 정리, Player 좌표·Scene 수 보존을 검사한다. Stage 1 지형 위에 별도 Unity 카메라로 UI/왜곡 프레임도 렌더한다. 이 렌더의 카메라 보간은 화면 검토용이며 Cinemachine 실행 검증과 구분한다. 실제 Startup부터 사람의 전체 조작, 협동 전송, 빌드는 별도다.

실행 결과: Cinemachine 전환 후 3/3 통과. 이후 실제 카메라 크기 10→5.2, 자막 알파와 왜곡 곡선이 끝까지 적용되는 assertion을 추가한 재생 테스트 1/1도 통과했다. Unity 컴파일 오류 0개. Stage 1 지형을 사용하는 97프레임 별도 렌더에서 자막·레터박스·흡입·암전·복귀를 확인했다. Scene diff는 미리보기 Prefab 인스턴스 하나의 추가로 제한했다.
