# Stage1 NavMesh 발밑 장애물과 점유율 UI

PR: 생성 후 같은 문서에 번호를 반영한다.

## 먼저 볼 곳

1. `Assets/Scripts/Features/Navigation/NavMeshObstacleFootprints2D.cs`: NavMeshPlus의 PostCollectSources 확장이다. 바닥 수집을 유지하고 Not Walkable Modifier가 붙은 2D 장애물의 렌더 형상만 Collider 메시로 교체한다. 임시 메시 수명은 한 Bake에 귀속한다.
2. `Assets/Scenes/Gameplay/Stage_1.unity`: Environment_Obj·터렛 Base 25개·Spaceship의 Modifier와 Navmesh2D의 확장을 연결한다.
3. `Assets/Scripts/UI/Tactical/TacticalPresentationRules.cs`, `TacticalCommandController.Intel.cs`: UI 제작기의 커밋 `625f69cb9`를 포함한다. 점유 진행을 퍼센트로 표시하고 Snapshot 읽기 흐름을 유지한다. 관련 검사와 common-ui-rules 문서도 함께 포함한다.

## 왜 이렇게 수집하는가

바닥의 CompositeCollider2D는 Outlines다. 모든 입력을 Physics Colliders로 전환하면 일부 색상 지면이 빠진다. 따라서 바닥은 Render Meshes를 유지하고, 기존 물리 충돌체가 붙은 나무 밑동·돌·터렛 Base·Spaceship만 교체한다. 실제 물리 형상·레이어·몬스터 이동 코드는 바꾸지 않는다.

현재 Stage1의 나무·돌은 개별 프리팹이 아니라 Environment_Obj의 공유 Tile/Sprite를 사용한다. 나무 Physics Shape는 이미 수관보다 작은 밑동 형상으로 제작되어 있다. 장애물 위치를 별도 수동 목록으로 복제하지 않는다.

## 변경 범위와 잠금

- 강화용 Canon LV2/LV3, Missile LV2/LV3, Laser Stage1 프리팹은 Base Modifier만 추가한다.
- hjd5031님이 잠근 Canon/Missile LV1 원본은 저장이 거절되어 수정하지 않았다. Stage_1의 25개 인스턴스 Base에만 Modifier override를 추가했다. 잠금을 강제 해제하거나 파일을 직접 덮어쓰지 않았다.
- NavMesh 데이터 경로와 GUID `aa354288c7968453ab7e90ea3c9eb2e4`를 유지해 재Bake했다.
- Scene 저장 과정에서 최신 Enemy 필드 기본값/Encounter 오브젝트와 기존 Prefab override의 직렬화도 반영됐다. 정책·배치 변경과 구분해 Scene diff를 확인한다.
- 사용자 폰트/재질 수정, staged Python cache 8개와 기존 로컬 NetworkMatchAnchor/HealOrb 프리팹 차이는 게시에 포함하지 않는다.

## 확인 결과

- Unity 6000.3.23f1, NavMeshPlus 설치본 16283ee1040e, 실제 Stage_1에서 확인했다.
- Edit Mode: 점유 지형을 포함한 색상별 바닥 표본에 보행 영역이 생성됨을 확인했다. Agent Radius 0.5, Voxel 약 0.167은 유지했다.
- Play Mode: 컴파일 실패 없음, 터렛 Base Modifier 25/25, Base 중심의 보행 영역 침범 0/25, 나무·돌 Physics Shape 중심 표본 814개 중 보행 영역 침범 0, Spaceship 중심 보행 불가를 확인했다.
- 터렛 양쪽을 연결한 실제 NavMesh 경로 25/25가 PathComplete였다. 최종 NavMesh 삼각형은 9,354개다.
- NavMesh의 실제 높이는 약 -0.0833이다. 검사에서는 높이 오프셋과 XY 침범을 구분했다.
- 전체 자연 전투·보스전·모든 좁은 길의 대형 Enemy 통행·Windows 빌드는 이번 검증 범위가 아니다. UI 담당의 커밋은 그대로 포함하며 해당 검사 전체를 이번에 다시 실행한 것으로 표시하지 않는다.
- 초기 Collider 일괄 수집 실패와 lock/CLI timeout은 통과로 계산하지 않는다. 현재 최종 설정은 Render Meshes + 발밑 장애물 확장이다.

## 재Bake와 되돌리기

Stage_1의 Navmesh2D에서 평소처럼 Bake한다. 확장이 장애물 입력을 다시 만든다. 다른 Scene도 각 Surface에 같은 설정을 넣고 별도로 Bake해야 한다. 되돌릴 때는 해당 Modifier·확장·NavMesh 데이터를 함께 되돌리고, 공유 프리팹과 Scene instance override의 범위를 구분한다.
