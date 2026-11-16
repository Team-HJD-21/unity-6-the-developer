# 보스 진입 컷씬

Stage1의 정식 보스 진입은 **현재 점유100%**에서 Core가 자동으로 시작한다. 11/16에 일반 게임 화면의 미리보기 버튼을 제거했다. 컷씬 재생 조건은 버튼 존재 여부와 무관하다. 기존 파일·타입 이름은 Timeline·Prefab 참조 보존을 위해 유지한다.

## 실제 호출 경로

`InitialStageCompositionRoot.PumpStageBossBattle` → `EnterStageBossBattleAsync` → `StageBossBattlePresentation.PlayEntranceAsync` → `PlayerBossCutscenePreview.PlayScriptedAsync` 순서다. 수동 버튼 클릭으로 이 경로를 시험하거나 진행시키지 않는다.

Core는 공용 Match 시간과 입력을 정지하고 기존 Timeline을 재생한다. 플레이어 확대·안내·왜곡/흡입·암전·마지막 확대 샷 이후 완전 암전 안에서 BossRoom을 Additive로 로드하고 실제 Player를 도착점으로 이동한다. HUD 등장과 보스전 시작 저장이 성공해야 전투를 재개한다.

- 프리팹: `Assets/Prefabs/UI/PlayerBossCutscenePreview.prefab`. 미리보기 Button/Label 오브젝트는 없으며 기존 연출·카메라 참조는 유지한다.
- 타임라인: `Assets/Art/UI/BossCutscene/BossEntrance.playable`.
- 연출 구현: `Assets/Scripts/Features/Player/Cinematics/PlayerBossCutscenePreview.cs`.
- 진행 조율: `Assets/Scripts/Core/Bootstrap/SceneComposition/InitialStageCompositionRoot.BossBattle.cs`.
- 전체 Scene·BGM·안전장치·승리 귀환과 검증: [통합 검토 문서](../../03_team/00_reviews/boss-battle-flow-20261115.md).

## 재생과 정리 책임

`PlayScriptedAsync`는 Core가 가진 입력 잠금을 허용하고 마지막 연출 화면을 유지한다. `StageBossBattlePresentation`은 Core 암전 콜백에서 그 화면을 해제하고 기존 HUD 등장 연출을 시작한다. Scene 이동·보스 생성·승패 판단은 이 컷씬 클래스가 소유하지 않는다.

`TryPlay`/`StopPreview`는 기존 연출 검사와 명시적 코드 호출용 API로 유지한다. 독립 연출 재생은 다른 입력 잠금·우주선·사망 상태에서 거부하며 Esc/피격으로 정리한다. 정식 진입의 게임 정지는 Core 전환 범위가 소유한다. 일반 플레이에 수동 재생 버튼을 제공한다는 의미가 아니다.

`PlayerBossCutscenePreviewTests`는 프리팹의 Button 부재와 설정 유효성, 실제 Cinemachine/Timeline 재생·월드 캡처·확대/복귀·취소·입력/렌더 자원 정리·마지막 프레임 인계를 검증한다. Host의 실제100% 진행 확인과 원격2인·Standalone 인수는 구분한다.
