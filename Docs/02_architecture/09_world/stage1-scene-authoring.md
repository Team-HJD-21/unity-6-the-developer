# Stage1 씬 제작과 검증

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
