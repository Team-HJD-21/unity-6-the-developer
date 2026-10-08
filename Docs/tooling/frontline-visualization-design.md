# 전선 시각화와 검증 설계

2026-10-08 · 현재 Core 계약 및 게임 기획 기준. 기하 진단과 전투 판단을 구분한다.

## 게임 디자인에서 무엇을 관찰하는가

이 게임은 탑다운 2D 타워디펜스·호드 성격이다. 아군 터렛의 배치와 사격이 적의 진입을 막고, 플레이어가 그 배치를 보조한다. 여기서 검증할 **교전 전선**은 적이 아군의 화력·충돌·목표 선택과 만나 정체하거나 밀고 들어오는 실제 플레이 현상이다. 수학적인 외곽선이 생겼다는 것만으로 그 현상을 검증하지 못한다.

현재 Core의 **기하 전선**은 다음과 같다.

1. 활성 컴포넌트이면서 파괴되지 않은 터렛의 XY 위치를 입력한다. 일시적인 과열·전력 부족으로 사격이 멈춘 터렛의 배치 점은 유지한다.
2. `BattlefieldTopologyBuilder`가 결정론적인 Delaunay 삼각분할을 만든다. 삼각형들의 집합을 Territory로 제공한다.
3. 소유 삼각형이 하나뿐인 `BattlefieldEdgeAdjacency`의 변이 `FrontlineEdges`다. 적 자체의 Territory는 없다.
4. Match가 기하·Grid 점유·Match ID·revision을 묶은 불변 `BattlefieldSpatialSnapshot`을 발행한다. Encounter는 이 자료를 소비해 스폰 지점을 판단한다.

현재 계산에는 사거리·장애물·통행 가능한 구역·전술 우세가 없다. 모든 터렛을 한 번에 Delaunay 분할하므로 멀리 떨어진 터렛 군 사이의 빈 공간도 연결될 수 있다. 도형의 채움은 **방어 가능한 영역을 보증하지 않는다**. 삼각형 연결군 번호는 기하적 연결을 계산한 Editor 진단이며, 팀의 Region/레인 ID가 아니다. 이번 작업에서 topology 정책은 바꾸지 않는다.

## 적용한 구조와 UX

| 계층 | 담당 | UI에 표시하는 의미 |
| --- | --- | --- |
| Core | 기존 topology builder / Match snapshot | 유일한 실행 계산 결과. Editor가 결과를 대체하지 않음 |
| Editor 진단 모델 | `FrontlineGeometryReport`, `FrontlineEdgeInspection` | snapshot에서 파생한 면적, 외곽 길이, 삼각형 연결군, 변의 길이·중점·안팎 법선 |
| Editor 선택 상태 | `FrontlineInspectionSession` | 정규화된 터렛 ID 쌍으로 경계 선택. 동일 Match의 새 revision에서 다시 매칭 |
| Scene 표현 | 기존 Drawer / Overlay + `FrontlineInspector` | 경계 클릭, 선택 강조, 수치 Inspector, Frame/Copy/Clear |

Scene View의 Battlefield Overlay에서 `Frontline geometry`를 펼치고 `Pick boundary edge in Scene View`를 켠다. 주황 경계선의 12 pixel 이내를 클릭하면 노란 선택선과 안/밖 화살표가 표시된다. 일반 오브젝트 선택과 경쟁하는 Unity Handle picking을 이용하며 Alt 카메라 탐색은 가로채지 않는다. `Frame`은 선택 변으로 Scene 카메라를 이동하고, `Copy`는 현재 Match/revision과 선택 기하 자료를 복사한다.

Scene View의 Gizmos와 Scene layers가 모두 켜져 있어야 표시/선택이 동작한다. 공유 Scene 도구의 Frame Grid/Frame Cell과 좌표·ID evidence 복사도 사용할 수 있다. 이들은 Scene 카메라/clipboard만 변경한다.

- 기하 전체: 면적(u²), 외곽 길이(u), 삼각형/연결군/경계 수.
- 선택 변: 양끝 터렛 ID, 길이, 중점 XY, 소유 삼각형 index, 연결군 index, 안/밖 단위 법선.
- ID 기반 선택은 같은 Match/revision 변경에서 유지하지만 경계가 사라지거나 Match가 바뀌거나 Play Mode를 종료하면 해제한다. 선택 변을 다른 변의 index로 대체하지 않는다.
- Grid cell 선택과 edge 선택은 동시에 켜지지 않는다. edge 모드를 종료하면 직전 cell 모드를 복원한다. 사용자가 이후 cell 선택을 직접 켜면 새 선택을 존중한다.
- Capture 모드는 선택선·선택 label을 숨겨 공유 화면의 가독성을 유지한다.
- `FrontlineInspector.Draw()`는 같은 read-only Inspector를 Game Debugger에서 재사용하는 연결점이다. 창을 따로 추가하거나 새 Runtime service를 만드는 방식이 아니다.

## PREVIEW / LIVE / 과거 요청은 다르다

Edit Mode PREVIEW는 동일한 **Core 계산기**로 현재 Scene 터렛의 배치를 검사하지만 실행 Match는 아니다. 플레이어·적의 실제 이동·사격·타깃 선택은 검증하지 않는다. LIVE는 현재 Match가 발행한 snapshot을 읽는다. 면적이나 revision이 변한 이유를 보려면 터렛 등록/파괴·복구와 Grid 이동 관측을 함께 비교한다. revision이 증가했다고 기하가 반드시 바뀐 것은 아니다.

Statistics의 Spawn inspection / `GameDebugSpawnMap`은 **요청 당시 보존된 snapshot**과 후보/계획/실제 스폰을 보여준다. 이 Scene 도구는 **현재 기하**를 선택·검사한다. 서로 다른 시점의 자료를 섞지 않기 위해 Match/revision과 PREVIEW/LIVE를 표시한다. 과거 요청 지도는 최대 32개 기록의 별도 목적이며 이 도구가 Scene을 과거 상태로 되감거나 Runtime을 재실행하지 않는다.

## 엔진 레퍼런스와 채택 이유

Unity는 Scene View용 panel/toolbar Overlay를 제공하므로 설정·선택 Inspector를 Scene 가까이에 둘 수 있다. 이번에는 기존 Overlay를 재사용한다. [Unity 6: Create your own overlay](https://docs.unity3d.com/6000.0/Documentation/Manual/overlays-custom.html)

Scene 커스텀 입력과 Handles는 `SceneView.duringSceneGui`에서 처리하고 실제 그리기는 Repaint 이벤트에 한정한다. 변 클릭 거리는 `HandleUtility.DistanceToLine`이 제공하는 화면 pixel 거리로 검사해 줌 수준에 따른 월드 거리 임계값 혼란을 줄인다. [Unity: duringSceneGui](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneView-duringSceneGui.html), [Unity: DistanceToLine](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/HandleUtility.DistanceToLine.html)

UE Visual Logger는 대상 목록·선택 시점 snapshot·분류된 메시지·시간 탐색을 묶어 실행 상태를 재검토한다. 이 UI 패턴을 참고해 현재 선택 Inspector와 요청 당시 지도를 분리했다. UE API나 매 프레임 전체 Actor 기록을 Unity에 그대로 옮긴 것은 아니다. [Epic: Visual Logger](https://dev.epicgames.com/documentation/en-us/unreal-engine/visual-logger-in-unreal-engine)

## 실사용 시험 순서

1. Edit Mode에서 비공선 터렛 3개 이상을 놓고 PREVIEW의 삼각형/외곽선·면적/외곽 길이를 확인한다. 같은 위치 중복이나 공선 입력은 별도 진단한다.
2. 경계 하나를 선택한다. 양끝 ID와 Scene 위치가 일치하고 바깥 법선이 Territory 밖을 가리키는지 확인한다. 확대/축소, Alt 탐색, cell 선택 전환을 시험한다.
3. Play Mode에서 같은 배치를 LIVE로 관측한다. 과열 중 배치 점은 유지되고 파괴·복구 시 실제 Core 결과가 바뀌는지 확인한다.
4. 터렛 위치 변경/파괴로 선택 경계가 사라지면 선택이 해제되는지, 유지되는 ID 쌍은 새 revision의 위치를 사용하는지 확인한다.
5. 반복 Encounter 요청 중 현재 전선과 요청 당시 Spawn inspection을 비교한다. 점유 셀·후보 제외 사유·실제 생성 지점을 함께 보고 적이 이동·교전하는 영상으로 판단한다.
6. Play 종료/Match 교체 뒤 이전 선택이 남지 않는지 확인한다. 실제 교전 전선의 형성·가시성·재미는 화면 관측과 팀 리뷰로 판단한다.

순수 기하/선택 모드 테스트와 실제 Scene 클릭·스크린샷 QA를 구분한다. 전술 압력, 방어 우세 색상, 영향력 지도, 영역별 밸런스 점수는 없는 Runtime 계약을 추정해 표시하지 않는다.
