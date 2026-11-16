# Stage1 보스전 표시 연결

상태: UI 소스 구현·Unity Editor/Play 검사 완료, 실제 Stage1 통합 연결 검증 대기. Scene·보스 생성·최종 진입 판정·저장·BGM은 Core 통합 작업이다. 이 문서만으로 실제 게임 완료를 뜻하지 않는다.

## 진입점과 소유 범위

`StageBossBattlePresentation`은 `IStageBossBattlePresentation`의 MonoBehaviour 구현이다. Stage_1에 두고 기존 `InitialStagePlayerHUD`, `PlayerSkillHud`, `PlayerBossCutscenePreview`, `FieldEntryAnnouncement` 프리팹을 Inspector에 연결한다. 입력은 기존 `ILocalInputConsumer.BindInputBlocker`로 받는다. HUD/Preview 미지정 시 같은 Scene의 기존 인스턴스만 찾으며 다른 플레이어를 전역에서 임의 선택하지 않는다.

- `PlayEntranceAsync(token)`: 기존 Preview Timeline을 명시적으로 재생한다. Core가 이미 획득한 입력 잠금과 `CanMove=false`는 이 scripted API에서만 허용한다. 일반 버튼의 생존·필드·로컬 카메라·입력 조건은 유지한다. 마지막 암전/포착 화면은 Task 완료 뒤에도 유지해 다음 Fade 직전에 필드가 번쩍이지 않게 한다.
- `ShowBattleIntroAsync(boss, displayName, token)`: Core가 지정한 실제 `EnemyHealth`를 기존 `CombatBossView.BindBattle`로 연결한다. `보스를 처치하세요` → 기존 문구 Fade/3초 유지/스캔 지움 → 두 HUD의 기존 스캔 등장을 재생한다. 이 재생은 Core 입력 잠금 중에도 진행하며 게임 시계를 변경하지 않는다. 완료 뒤 이 컴포넌트의 입력 lease만 해제한다.
- `PlayVictoryAsync(message, holdSeconds, token)`: 기존 문구 프리팹, 기존 출력 카메라의 `PushView`로 플레이어를 확대한다. 기본 확대0.45배·전환0.75초·문구 Fade-in0.22초이며, `holdSeconds=5`는 Fade-in 이후5초 유지다. Task가 끝나도 문구·확대 시점·HUD 숨김과 자체 입력 잠금은 다음 Fade 인계까지 유지한다.
- `ResetBattle()`: 진행 Task 취소, 자체 HUD/입력/카메라 lease와 포착 Texture/재질 정리, 보스 바인딩/점유 숨김 해제, 기존 자동 Skill HUD 바인더와 전술 UI의 이전 활성 상태를 복원한다. 반복 호출할 수 있으며 다른 소유자의 잠금·HUD 숨김·카메라 lease를 해제하지 않는다.

## Core 호출 순서

1. 기존 로컬 actor를 Stage HUD에 정상 바인딩한 상태에서 Core가 시간·입력을 잠근 뒤 `PlayEntranceAsync`를 기다린다.
2. Core가 Fade-out → additive BossRoom 로드·같은 actor 이동·카메라 연결·실제 보스 생성을 소유한다. 기존 Stage HUD 참가자 바인딩을 제거하거나 다른 actor로 교체하지 않는다.
3. 완전 암전 콜백에서 `ShowBattleIntroAsync` Task를 시작한다. 첫 동기 구간에서 입장 마지막 화면을 해제하므로 Core는 이어서 Fade-in한 뒤 Intro Task 완료를 기다린다. 완료 후 Core 자신의 시간·입력 잠금을 해제한다.
4. 실제 보스 사망 뒤 Core가 다시 잠그고 `PlayVictoryAsync("보스를 처치하는데에 성공했습니다", 5, token)`을 기다린다.
5. 문구·확대 시점을 유지한 채 Core가 Fade-out·우주선 복귀를 수행한다. 가려진 상태에서 `ResetBattle`하고 Fade-in한다. 실패·취소·Scene 해제에도 Core는 Reset을 호출한다.

보스 생존과 체력은 주어진 `EnemyHealth.CurrentHealth/MaxHealth`만 읽는다. 이름으로 일반 적을 보스로 추정하거나 체력을 복제하지 않는다. 보스전 중 점유 수치·제목·구분선은 숨기고 기존 타이머를 가운데로 배치한다. 전술 화면과 필드 확보 상태는 전투 동안 중지한다. 새 폰트·디자인·Canvas 제작 자산·전역 Manager를 만들지 않는다.

## additive HUD 경계

`InitialStagePlayerHUD.BindBattleActor`는 이미 바인딩된 같은 actor와 `PlayerActorIdentity`만 허용한다. Skill HUD는 전투 동안 기존 `PlayerSkillHudSceneBinding`을 이전 활성 상태와 함께 보관해 중지하고 `PlayerSkillController.BindBattleHud`로 명시 연결한다. 일반 `BindHud`와 `CombatBossView.Bind`의 같은 Scene 조건은 유지한다. 배틀 전용 바인딩도 양쪽 Scene이 실제 로드돼 있어야 한다. Reset 시 cross-scene Skill HUD 연결을 해제하고 이전 자동 바인더를 복원한다.

## 검증

Unity6000.3.23f1의 일반 Editor에서 각 클래스를 별도로 실행했다. 최종 `StageBossBattlePresentationTests` **2/2**, `PlayerBossCutscenePreviewTests` **5/5**, `MissionUiPresentationTests` **3/3** 통과, 실패/생략0이다. 실제 프리팹·additive Scene의 같은 로컬 actor·실제 EnemyHealth의 피해 후75% 표시·Core 잠금 중 두 HUD 스캔 완료·일반 씬 제한·점유 숨김을 확인했다. 승리 확대4.5/기존10, 문구와 HUD 숨김의 Fade 인계 유지, 취소 시 다른 입력/HUD lease 보존과 기존 Preview의 피해/허브/중복 실행 제한도 확인했다.

초기 fixture의 첫 Play 진입 대기와 비동기 완료 확인은 고정2프레임 가정 대신 실제 초기화·완료를 기다리도록 보완했다. 보스 피해는 실제 Player 공격의 성공·원본 HP15/20을 먼저 확인한 뒤 HUD 갱신을 검사한다. 여러 이름을 `|`로 묶은 Pipeline 부분 문자열 필터는0건이어서 통과로 세지 않고 위3클래스를 개별 재실행했다. 제품 입력 조건이나 연출 시간을 완화하지 않았다.

같은 승리 문구를800×600/1920×1080 Preview Scene에서 기존 프리팹으로 렌더해 직접 확인했다. 기존 폰트44pt·선호 폭638.01·한 줄이며 잘림/넘침/양쪽 장식선 겹침이 없다. Edit Mode Preview의 Awake 초기화 누락은 검사 helper에서만 보완했고 제품 코드를 바꾸지 않았다. 프리팹·Scene·폰트 파일을 저장하지 않았다.

검사 뒤 원래 `Assets/Scenes/Tests/EnemyTest.unity` 편집 상태로 복귀했다(Play=false, dirty=false). 기존 폰트·사용자 Startup/BossRoom·Core 작업을 포함한 타 변경23개의 파일 해시와 staged patch가 검사 전후 동일하다. 근거는 Git 제외 `Docs/Local/Validation/BossBattlePresentation20261115`의 `battle-final.json`, `preview-final-rerun.json`, `mission-final.json`, `announcement-preview.json`, `victory-800.png`, `victory-1920.png`다. 실제 Stage_1→BossRoom→우주선 전체 흐름, 실제 Runtime 보스·안전장치·BGM, 사람의 수동 인수와 Windows 빌드는 Core 통합 검증으로 구분한다.
