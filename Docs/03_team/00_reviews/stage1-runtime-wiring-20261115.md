# Stage_1 정식 스폰·낮밤·필드 표시 재접합

기준: main #731 `49ee14dc3`. PR은 아직 생성하지 않았다. 사용자 승인으로 팀원 Scene을 인수한 뒤 진행한 후속 변경이다.

## 먼저 볼 곳과 실제 흐름

1. `Assets/Scenes/Gameplay/Stage_1.unity`: Stage Network, Encounter Runtime, EnemySpawnRoot, Main Camera의 참조를 확인한다.
2. `InitialStageNetworkBinding.Initialize` → 명시 `EncounterSceneBinding`에 App 연결 상태 주입 → Match 바인딩. NetworkManager·Transport는 기존 객체를 유지하고, 소비자만 별도 객체로 분리했다.
3. `MatchCoordinator/MatchEncounterProduction` → 공통 요청 → EncounterPipeline → EnemySpawnExecutor. 수동 Game Debugger 요청도 같은 실행 경계를 사용한다.
4. `InitialStageCompositionRoot.PrepareEnvironmentSources` → WeatherManager 옆 MatchDayNightCycle → Main Camera의 DayNightShaderApply → StageCameraEffectsFeature → DayNight 재질의 `_EffectBlend`.
5. `TacticalCommandController` → 같은 Region Grid snapshot → 지도/FieldTerritoryRenderer. 점유 원본은 바꾸지 않고 표현만 연결한다.

기존 Pipeline·시간·난수·지역 정책 코드는 다시 작성하지 않았다. 삭제 파일 두 개는 더 이상 참조하지 않는 옛 네트워크 시험 helper와 Controls다. AI 이동·공격·터렛 전투 규칙, UI 프리팹·폰트는 이번 변경에서 수정하지 않았다.

## 변경 내용

| 대상 | 변경과 보존한 사항 |
| --- | --- |
| 네트워크 | `Encounter Debug Network`를 `Stage Network`로 표시하고 기존 NetworkManager/Transport를 보존했다. EncounterSceneBinding을 별도 `Encounter Runtime` 객체로 옮기고 기존 Script GUID·컴포넌트 참조를 유지했다. |
| 중복 생성기 | 정식 설정이 참조하지 않는 `Encounter Debug Spawner`와 자식 36개 지점을 제거했다. 기존에는 이들도 같은 Scene의 자동 수집에 들어갔으므로 단순한 화면용 Gizmo가 아니었다. |
| 실제 지점 | EnemySpawnRoot의 8개를 `Spawn_LaneA_01` 형식으로 표시한다. 기존 ID·위치·반경과 LaneA_02 수동 비활성을 보존했다. 신규 복제 지점의 자동 등록은 유지하며 8개 고정 목록을 만들지 않았다. |
| 낮밤 화면 | 카메라에 기존 DayNightShaderApply와 WeatherManager/DayNight.mat 참조를 연결했다. 타이머는 이미 있었지만 화면 소비자가 없었다. Weather 추첨·가중치·5분 주기를 변경하지 않았다. |
| 표현 참조 | 추가 출현 예고 Sprite와 전술 지도 Environment_Obj 참조를 연결했다. 필드 경계 코드·재질·Sorting Layer는 이미 존재해 중복 추가하지 않았다. |
| 새 씬 제작 | #731에 들어 있던 Core 생산·구조·추가 스폰 설정은 유지했다. 지형·NavMesh·터렛 배치를 이전 Scene으로 되돌리지 않았다. 삭제된 옛 카메라 마커를 재생성하지 않고 기존 Grid bounds fallback을 사용한다. |

## 확인 결과

| 검사 | 관측한 결과 |
| --- | --- |
| Unity 컴파일 | 오류 0, 경고 0 |
| 정적 검사 | Unity .meta 검사 오류 0, 갱신한 사용법 문서 링크 누락 0, diff 공백 검사 통과 |
| 실제 Scene 제작 참조 | Stage1EncounterAuthoringTests 4/4 통과 |
| 바인딩·연결 소유권·이전 저장 형식 | EncounterRuntimeSeparationTests 6/6 통과 |
| 실제 게임 진입 | Startup → 우주선 → Stage_1 정상 진입. 아래 설명한 별도 QA 캠페인 사용 |
| 자동 생산 | 첫 실행에서 15/15, 15/15 계획·실제 생성. 재진입 후에도 15/15, 15/15 |
| 수동 요청 | Normal 상한 10으로 9/9 생성. 분대 1세트 단위의 기존 정책 결과이며 10마리 강제 생성으로 바꾸지 않음 |
| 지점 | 등록 8개. LaneA_02의 ManuallyDisabled 유지. Debug 지점은 후보에서 사라짐 |
| 낮밤 화면 | Night 전환 후 실제 렌더 재질 생성·`_EffectBlend=0.526913643`, Day 복귀 후 0. Day/Night 화면을 각각 캡처해 확인 |
| CU 자동 활성화 | CU에서 2유닛 떨어진 실제 Player로 기존 HUD API 요청 수락. 이후 터렛 25개 중 가동 3개 확인 |
| 지도·필드 경계 | 같은 GridId 확인. CU 점유 1셀·경계 4변 표시. 지도 표시 중 필드 띠 숨김 확인 |
| 복귀·재진입 | 우주선 복귀 시 Enemy 0·Host 유지·NetworkManager 1개. 재진입 후 NetworkManager 1개·Executor 1개·새 Match 생산 정상 |
| Match 해제 | 적 76 → 0, 바인딩 해제, Host/Listening 유지 |
| 정리 | Play 종료·Startup 복귀. 원래 저장 35개 모두 해시 일치로 복구, QA 전용 파일은 검수 폴더로 보관 |

낮밤은 디버그 Phase 전환으로 실제 source·날씨 알림·렌더 경로를 확인했다. 이번 검수에서 실제 10분을 기다려 두 자동 경계를 모두 관측한 것은 아니다. 원격 2인 접속과 최종 보스·승패는 이번 검수 범위가 아니다.

## 별도 문제: 기존 성장 저장으로 새 맵 진입

처음 기존 캠페인으로 진입하면 다음 예외가 발생했다.

```text
InitialStageCompositionRoot.ComposeAuthority
  → BindGrowthSaves
    → GrowthSaveService.Load / Restore
      → FieldTurretController.GrowthParticipant.PrepareRestore:177
        InvalidDataException: The authored turret set changed; growth migration is required.
```

새 제작 Scene의 터렛 목록과 예전 Host 전장 성장 저장의 대상 목록이 달라 기존 검증이 복원을 거부한다. 데이터 손실을 막기 위해 이 검증을 우회하거나 기존 저장을 지우지 않았다. 실제 실행 검수는 공개 API `AppRoot.SelectCampaign`으로 별도 `qa-stage1-wiring-20261115` 슬롯을 선택해 진행했다. 기존 저장을 사용하는 정상 시작 경로까지 해결되었다고 읽으면 안 된다.

후속에는 기존 ID가 유지된 터렛의 성장을 어떻게 옮길지와 삭제·추가·정의 변경된 터렛의 복원 정책을 확인해야 한다. 임의 초기화나 자동 환급을 이번 패치에 섞지 않는다.

또한 첫 Startup의 MenuSoundtrack.PlayScheduled에서 기존 FMOD 출력 장치 초기화 오류가 1회 발생했다. 씬 진입을 막은 성장 저장 오류와 구분한다. QA 캠페인 실행·복귀·재진입에서는 새 게임 예외가 발생하지 않았다.

## 검수 자료

로컬 보관: `Docs/Local/Review/Stage1HandoffPreparation-20261115/`.

- `authoring-tests.json`, `separation-tests.json`
- `runtime-spawn-and-phase.json`, `runtime-night-render.json`, `runtime-day-and-map.json`, `runtime-map.json`
- `runtime-hub-return.json`, `runtime-reentry.json`, `runtime-cleanup.json`
- `runtime-errors-initial.json`, `checkpoint-restored.json`, `scene-edit-report.json`
- `stage1-day.png`, `stage1-night.png`, `stage1-map.png`

로컬 검수 자료와 원본 저장 백업은 커밋 대상이 아니다. PR 작성 시 필요한 캡처만 첨부하고 이 문서에 PR 번호를 추가한다.
