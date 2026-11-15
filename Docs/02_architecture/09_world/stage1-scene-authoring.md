# Stage1 씬 제작과 검증

2026-11-15 진입 경로: 제품 Build Settings의 인덱스 0은 Startup, 첫 전장 항목(인덱스 1)은 Stage_1이며 `Initial_Stage`는 빌드 목록에서 제외한다. 우주선 출구·재시작·네트워크 검증 빌드는 Stage_1을 사용한다. 과거 `playModeStartScene`이 Initial_Stage로 지정돼 있으면 Play 시작 전에 Stage_1으로 교체하며 현재 편집 씬은 저장하거나 덮어쓰지 않는다. Core의 조립 로그는 실제 씬 이름을 출력한다. `Initial_Stage` 자산은 명시적 Editor 검사·참고용으로만 보존한다.

## 편집 위치

- 게임 시작 시 로드하는 제작본은 [Stage_1.unity](../../../Assets/Scenes/Gameplay/Stage_1.unity)다. 우주선 출구의 `missionSceneName`과 Build Settings도 이 경로를 사용한다. `Assets/Scenes/Reference/Stages` 아래에는 참고용 씬이 있고 [InitialStageTurretTest](../../../Assets/Scenes/Tests/InitialStageTurretTest.unity)는 독립 터렛 시험용이다.
- `Initial_Overworld_Base`는 육지 타일맵이다. `TilemapOuterBoundary`는 그 타일맵의 합성 윤곽에서 바깥 고리만 사용해 `MapBoundary`의 EdgeCollider2D를 만든다. 타일 편집이나 Undo·Redo 후 씬을 저장하고 안쪽 구멍에 경계가 생기는지 확인한다. [바닥·설치 판정 연결](../06_player/player-skill-review.md)
- `Environment_Obj`는 나무와 기물을 그린다. Tilemap Renderer는 `Individual`, `Entities / 0`을 사용하며 스프라이트의 지면 접점과 월드 Y축으로 플레이어와 정렬한다. `Tile Ground Shadows`의 시간대별 길이와 X·Y 오프셋은 [그림자 설정](weather-system-guide.md#46-stage1-태양과-타일맵-기물-그림자)에서 조절한다.
- `Navmesh2D`의 NavMeshSurface는 [Stage1 전용 NavMesh 데이터](../../../Assets/Scenes/Gameplay/Stage_1/NavMesh-Navmesh2D.asset)를 참조한다. 이동 가능한 높이·경계·장애물이 바뀌면 Surface를 베이크하고 Scene과 NavMesh 에셋을 함께 확인한다. 경계 자동 갱신과 NavMesh 베이크는 별개다.

## 런타임 연동 확인

1. Startup에서 우주선을 거쳐 Stage1에 들어가 플레이어 생성과 이동을 확인한다.
2. 해안과 기물 주변에서 발 충돌과 NavMesh 이동을 확인한다. `MapBoundary`가 외곽을 일관되게 따라가는지도 확인한다.
3. 나무 앞뒤에서 플레이어를 움직여 Y 정렬을 확인한다. 나무에 가려진 캐논과 미사일을 같은 화면에 두고 줌을 바꿔 스프라이트와 가림을 확인한다. [정렬·표적 판정](../07_turrets/turret-system-guide.md)
4. 낮 시간에 태양 위치와 그림자 방향이 함께 바뀌는지 확인한다. Inspector에서 양축 오프셋을 바꿔 그림자 위치를 확인한다.

Core·Encounter의 참조 구조와 재접합 범위는 [Encounter 런타임 가이드](../08_encounter/encounter-runtime-and-testing.md)와 [Stage1 제작 연결 원복 기록](../../03_team/00_reviews/stage1-scene-rollback-20261115.md)을 함께 본다. 씬·NavMesh 파일의 변경, 코드 컴파일, 플레이 확인은 각각 기록한다.

## Core 재접합

- `Stage Network`는 기존 NetworkManager·Transport, `Encounter Runtime`은 정식 EncounterSceneBinding이다. InitialStageCompositionRoot의 명시 참조로 연결한다. 이름만 바꾼 테스트 런처를 게임 관리자로 사용하지 않는다.
- `EnemySpawnRoot` 아래 `Spawn_Lane*`가 실제 제작 지점이다. 표시 이름과 기존 ID를 구분하며, 테스트 생성기 36개를 별도로 남기지 않는다. 신규 지점은 같은 Scene에서 자동 발견된다.
- Main Camera의 `DayNightShaderApply`에 Scene WeatherManager와 DayNight 재질을 연결한다. 타이머가 흐르는 것만으로 화면 효과 연결이 검증된 것은 아니다.
- 필드 경계는 기존 HUD의 런타임 연결을 사용한다. 전술 지도의 Scenery는 이 씬의 Environment_Obj를 지정한다. 삭제된 예전 카메라 마커의 fileID를 다시 사용하지 않는다.

## 2026-11-15 후반 생성 지점 보완

최신 실제 NavMesh의 점유 가능 셀은201개이며 구역별30/61/65/40/5개다. 기존 정식8개 지점은 구역1~4에만 있어 4번까지 확보하면 후보가 모두 사라질 수 있었다. 기존 지점을 재활성화하거나 점유 제한을 풀지 않고 EnemySpawnRoot 아래 Outer_01(월드 XY -110,79.06·반경1)을 추가했다. 5번 구역의 실제 NavMesh와 Player까지의 완전 경로를 확인했다. 현재 제작은9개 중 LaneA_02 수동 비활성1개이며 개수는 게임 규칙이 아니다. 1~4 확보 뒤 Outer_01이 남고 5번까지 확보하면 일반 스폰이0이 되는 통제 정책 검사로 확인한다. 이는 후반 전투 밸런스/자연 플레이 완료를 뜻하지 않는다.
