# NavMeshPlus 2D 컴포넌트와 연결 구조

기준일: 2026-10-11  
상태: 설치된 패키지 0.2.23의 코드 분석 및 `Initial_Stage` 현재 설정 기록. Bake 결과와 Play Mode 이동은 별도 검증 필요.

## 1. 이 문서의 범위

이 프로젝트에는 Git 패키지 `com.h8man.2d.navmeshplus`와 Unity 패키지 `com.unity.ai.navigation`이 함께 설치돼 있다. 이 문서는 **NavMeshPlus 0.2.23**의 컴포넌트를 설명한다. 분석 기준은 `Packages/packages-lock.json`에 고정된 커밋 `16283ee1040e591b9a99ddf93f37e9af93925aaf`이며, 현재 로컬 소스는 `Library/PackageCache/com.h8man.2d.navmeshplus@16283ee1040e/`에 있다. 아래 `Scripts/`와 `Editor/` 경로는 이 패키지의 `NavMeshComponents/`를 기준으로 적었다. PackageCache의 폴더명은 패키지 갱신 시 달라질 수 있다.

핵심 구분은 **경로를 만들 지형을 모으는 작업**과 **생성된 경로를 따라 에이전트를 이동시키는 작업**이다. NavMeshPlus는 주로 첫 작업에 2D Tilemap·Sprite·Collider2D 변환을 더한다. 최종 빌드와 경로 탐색에는 Unity의 `NavMeshBuilder`, `NavMeshData`, `NavMeshAgent`를 사용한다. NavMeshPlus와 Unity AI Navigation은 각각 `NavMeshSurface`, `NavMeshModifier`, `NavMeshLink`라는 동명 컴포넌트를 제공한다. 아래에서 `Surface`라고 하면 `NavMeshPlus.Components.NavMeshSurface`를 뜻한다. (`Scripts/NavMeshSurface.cs:10-23`, `:175-203`)

### 빠른 구성도

```text
NavMeshPlus NavMeshSurface
  ├─ Unity 기본 지형 수집 (Collect Objects / Layer Mask / Use Geometry)
  ├─ NavMeshExtension 호출
  │   ├─ RootSources2d: 2D 수집 시작 오브젝트 지정
  │   ├─ CollectSources2d: Tilemap·Sprite·Collider2D → 빌드 소스
  │   └─ CacheSources2d / CacheTilemapSources2d: 선택적 소스 캐시
  ├─ NavMeshModifier / ModifierTilemap / ModifierVolume: Area와 제외 규칙
  └─ NavMeshBuilder → NavMeshData → Unity NavMesh 세계에 등록
                                           └─ NavMeshAgent가 경로 사용
```

`NavMeshLink`는 두 NavMesh 영역 사이를 연결하는 별도 런타임 링크다. `AgentOverride2d`와 회전 보조 컴포넌트는 Agent 쪽 설정을 돕는다. 두 기능 모두 지형을 수집하거나 Bake하는 컴포넌트는 아니다.

## 2. Bake가 일어나는 실제 순서

1. Surface의 Inspector에서 **Bake**를 누르면 Editor의 `NavMeshAssetManager`가 비동기 빌드를 시작한다. 완료되면 씬 파일 옆의 같은 이름 폴더(이 씬에서는 `Assets/Scenes/Gameplay/Initial_Stage/`)에 `NavMeshData` 에셋을 저장하고 Surface에 연결한다. **Clear**는 Surface의 데이터를 해제하고 연결된 에셋을 정리한다. (`Editor/NavMeshSurfaceEditor.cs:264-277`, `Editor/NavMeshAssetManager.cs:30-71`, `:93-109`, `:111-164`)
2. Surface는 먼저 `Collect Objects`, `Layer Mask`, `Use Geometry`에 따라 **Unity 기본 수집기**를 호출한다. `NavMeshModifier`는 이 단계에서 markup으로 전달된다. (`Scripts/NavMeshSurface.cs:341-387`, `:397-415`)
3. 같은 오브젝트에 등록된 `NavMeshExtension`을 순서대로 호출한다. `RootSources2d`가 2D용 루트 목록을 설정하고, `CollectSources2d`가 해당 루트 아래의 2D 지형을 추가한다. (`Scripts/NavMeshExtension.cs:15-55`, `Scripts/RootSources2d.cs:21-30`, `Scripts/CollectSources2d.cs:62-92`)
4. Surface는 빌드 소스 중 설정에 따라 Agent·Obstacle 소스를 제거하고 `NavMeshModifierVolume`을 추가한다. 전체 소스의 경계를 구한 후 `NavMeshBuilder`로 데이터를 생성·갱신한다. (`Scripts/NavMeshSurface.cs:417-425`, `:443-485`, `:175-244`)
5. 활성화된 Surface는 생성된 `NavMeshData`를 Unity NavMesh 세계에 등록하고 비활성화할 때 제거한다. **Bake만으로 적의 이동 코드가 바뀌지는 않는다.** (`Scripts/NavMeshSurface.cs:108-149`)

**범위의 핵심:** `RootSources2d`는 3번의 **2D 확장 수집만** 제한한다. 2번의 Unity 기본 수집 범위는 Surface의 `Collect Objects`와 `Layer Mask`가 따로 정한다. Bake 경계(`Bounds`)가 넓다는 사실과 실제 이동 가능한 폴리곤이 넓다는 사실도 다르다. (`Scripts/NavMeshSurface.cs:341-415`, `Scripts/CollectSources2d.cs:29-59`)

## 3. 지형 생성에 사용하는 컴포넌트

### 3.1 Navigation Surface — `NavMeshSurface`

**붙이는 곳:** 씬에서 NavMesh를 소유할 오브젝트. 2D 구성에서는 `CollectSources2d`와 같은 오브젝트에 둔다.

**하는 일:** 빌드 설정과 데이터 수명을 소유한다. `Bake` 또는 `UpdateNavMesh` 때 소스를 수집해 Unity 빌더에 전달하며, 활성화될 때 저장된 NavMesh를 세계에 등록한다. (`Scripts/NavMeshSurface.cs:87-149`, `:175-244`)

| 설정 | 의미와 연결 관계 |
| --- | --- |
| Agent Type | 이 Surface로 만들어질 경로를 사용할 Agent 타입. `GetBuildSettings()`가 타입 ID로 빌드 설정을 읽는다. |
| Collect Objects | `All`은 전체 씬, `Children`은 **이 Navigation Surface 컴포넌트가 붙은 GameObject 자신과 그 자식·손자 계층**, `Volume`은 지정한 박스 안에서 Unity 기본 소스를 수집한다. NavMeshPlus 2D 수집기도 이 값을 기본 루트 선택에 이용하지만, `RootSources2d`가 2D 수집용 루트를 별도로 지정할 수 있다. |
| Include Layers | 수집할 GameObject 레이어. Unity 기본 수집과 2D Modifier 탐색에 전달된다. |
| Use Geometry | 기본 수집에서 Render Meshes 또는 Physics Colliders를 택한다. 2D 확장도 이를 보고 Sprite/Tilemap 경로 또는 Collider2D 경로를 선택한다. |
| Default Area | 기본 Area. **0 = Walkable, 1 = Not Walkable**은 이 프로젝트의 `ProjectSettings/NavMeshAreas.asset` 값이다. 2D 확장은 값이 1이 아니면 대상 Tilemap의 전체 경계 상자를 기본 Area 소스로 추가한다. |
| Center / Size | `Volume` 수집 및 수집 경계 계산에 사용한다. `All`·`Children`은 수집 결과에서 경계를 다시 계산한다. |
| Ignore NavMesh Agent / Obstacle | 수집 소스에 해당 컴포넌트가 있으면 제거한다. 장애물을 길찾기에서 어떻게 반영할지는 Bake Area 및 별도 장애물 설정과 함께 확인해야 한다. |
| Advanced의 Tile Size / Voxel Size / Min Region Area | Unity 빌드 설정의 타일·복셀·작은 영역 제거 값을 조정한다. |
| NavMesh Data | Bake한 데이터 에셋 참조. Surface가 활성화되면 Unity NavMesh 세계에 추가된다. |

근거: `Scripts/NavMeshSurface.cs:25-91`, `:151-173`, `:175-244`, `:341-425`.

### 3.2 Navigation CollectSources2d — `CollectSources2d`

**붙이는 곳:** NavMeshPlus `NavMeshSurface`와 **같은 GameObject**. 기본 클래스가 `GetComponent<NavMeshSurface>()`로 같은 오브젝트에서 소유자를 찾으므로 없으면 `NavMeshExtension requires a NavMeshSurface component`를 기록한다. (`Scripts/NavMeshExtension.cs:15-55`)

**하는 일:** Surface의 설정을 `NavMeshBuilder2dState`로 옮기고 `NavMeshBuilder2d`를 호출해 Tilemap, SpriteRenderer, Collider2D를 Unity의 `NavMeshBuildSource`로 바꾼다. 세계 경계 계산 시에는 씬의 첫 `Grid` 아래 Tilemap들의 `localBounds`도 참고한다. 이 경계 계산은 `RootSources2d`의 루트 목록과 별도다. (`Scripts/CollectSources2d.cs:29-59`, `:62-92`)

| 설정 | 실제 코드 동작 |
| --- | --- |
| Override By Grid | 타일의 스프라이트 콜라이더 모양 대신 셀 기반 형태를 쓰도록 전환한다. 프리팹 메시를 지정하지 않았다면 기본 Box 소스로 간다. |
| Use Mesh Prefab | 셀용 메시 소스를 지정한다. 코드가 프리팹의 `MeshFilter.sharedMesh`를 읽는다. |
| Compress Bounds | 소스 수집 도중 대상 Tilemap의 `CompressBounds()`를 호출한다. 이는 Tilemap 자체의 저장 경계를 바꾸므로 사용 전 결과를 확인한다. |
| Override Vector | Sprite·Collider·타일 소스의 위치에 곱하는 좌표 축별 벡터. 기본값은 `(1,1,1)`이다. |
| Rotate Surface to XY | Editor 버튼이 같은 오브젝트를 X축 `-90°`로 회전한다. 빌드된 평면을 일반적인 2D XY 맵에 맞추기 위한 조작이다. |

근거: `Scripts/CollectSources2d.cs:13-27`, `:62-92`, `Scripts/NavMeshBuilder2d.cs:293-372`, `Editor/CollectSources2dEditor.cs:22-59`.

**중요한 수집 조건:** 2D 빌더는 루트의 자식에서 `NavMeshPlus.Components.NavMeshModifier`가 달린 오브젝트를 먼저 찾는다. 해당 오브젝트의 레이어와 Agent 타입이 통과해야 하며, Render Meshes 모드라면 그 오브젝트의 `Tilemap` 또는 `SpriteRenderer`, Physics Colliders 모드라면 `Collider2D`를 읽는다. `Modifier`가 없는 Tilemap을 Root Sources 목록에 적는 것만으로는 이 2D 경로에 들어오지 않는다. (`Scripts/NavMeshBuilder2d.cs:159-212`)

### 3.3 Navigation RootSources2d — `RootSources2d`

**붙이는 곳:** Surface 및 `CollectSources2d`와 같은 GameObject.

**하는 일:** `Root Sources` 목록을 2D 빌드 상태에 기록한다. 호출 순서 값 `-1000`으로 일반 `CollectSources2d`보다 먼저 처리되도록 설계돼 있다. 목록에 적힌 오브젝트 자체와 그 하위의 2D Modifier를 탐색한다. (`Scripts/RootSources2d.cs:12-30`, `Scripts/NavMeshBuilder2d.cs:150-186`)

**제한:** Unity 기본 수집에는 이 목록이 전달되지 않는다. 예를 들어 현재 `Navmesh2D`와 `Grid`는 각각 씬 루트에 있고 `Initial_Overworld_Base`는 `Grid`의 자식이다. 그러므로 `Navmesh2D` Surface의 `Children`만으로는 Base를 Unity 기본 수집에 넣지 않는다. `RootSources2d`에 Base를 지정했기 때문에 **2D 확장 수집에는** 들어온다. 목록을 바닥 하나로 줄여도 그 바닥의 **전체 Tilemap 경계 사각형**이 생성되는 규칙까지 끄지는 못한다. (`Scripts/NavMeshSurface.cs:341-415`, `Scripts/NavMeshBuilder2d.cs:76-100`, `:171-228`)

### 3.4 Navigation Modifier — `NavMeshModifier`

**붙이는 곳:** 수집 대상 Tilemap, SpriteRenderer, Collider2D가 있는 오브젝트. 대상과 같은 오브젝트에 두면 2D 빌더의 `GetComponent` 탐색과 맞는다.

**하는 일:** Agent 타입별 적용 여부를 정하고, `Override Area`를 켰다면 기본 Area를 다른 값으로 바꾸며, `Ignore From Build`면 소스를 제외한다. 활성 Modifier는 등록 목록에 들어간다. Surface의 기본 수집에서는 markup으로, NavMeshPlus 2D 수집에서는 탐색 기준으로 사용된다. (`Scripts/NavMeshModifier.cs:12-53`, `Scripts/NavMeshSurface.cs:341-369`, `Scripts/NavMeshBuilder2d.cs:159-212`)

**주의:** `Override Area = Not Walkable`은 **그 Modifier의 소스 Area**를 바꾼다. Surface의 `Default Area`가 Walkable이면 2D 빌더는 이 설정을 읽기 전에 해당 Tilemap의 전체 경계에 Walkable Box를 추가한다. 따라서 Modifier Area만 바꿔도 넓은 이동 가능 사각형이 남을 수 있다. (`Scripts/NavMeshBuilder2d.cs:171-185`, `:214-228`)

### 3.5 Navigation Modifier Tilemap — `NavMeshModifierTilemap`

**붙이는 곳:** Tilemap 오브젝트. `[RequireComponent]`로 Tilemap과 `NavMeshModifier`를 요구한다.

**하는 일:** `TileBase` 에셋별 `TileModifier` 목록을 저장한다. 타일 항목마다 `Override Area`와 Area를 지정하면 수집기가 개별 타일 소스에 적용한다. Agent 타입 필터도 별도로 갖는다. Editor에는 사용된 타일을 목록에 넣는 기능과 중복 항목 경고가 있다. (`Scripts/NavMeshModifierTilemap.cs:12-80`, `Scripts/NavMeshBuilder2d.cs:293-338`, `Editor/NavMeshModifierTilemapEditor.cs:24-56`)

**적용 순서:** Surface Default Area → 오브젝트 Modifier의 Override Area → 개별 타일 Modifier의 Override Area 순으로 타일 소스 Area가 결정된다. 다만 앞에서 생성한 전체 경계 Box의 Area는 Surface Default Area를 유지한다. 현재 바닥의 `TileModifiers` 목록은 비어 있어 개별 타일 덮어쓰기는 없다. (`Scripts/NavMeshBuilder2d.cs:171-185`, `:293-338`; `Assets/Scenes/Gameplay/Initial_Stage.unity`의 `Initial_Overworld_Base`)

### 3.6 Navigation ModifierVolume — `NavMeshModifierVolume`

**붙이는 곳:** 박스 형태의 Area를 지정할 위치. Surface에 붙일 필요는 없다.

**하는 일:** `Center`, `Size`, Area, 적용 Agent 타입을 갖는다. Surface는 이를 `ModifierBox` 빌드 소스로 추가한다. Tilemap이나 Sprite와 달리 자체 지형을 수집하는 방식이 아니라 지정한 공간에 Area를 찍는 역할이다. (`Scripts/NavMeshModifierVolume.cs:10-53`, `Scripts/NavMeshSurface.cs:279-318`)

**범위 주의:** Surface가 `Collect Objects = Children`이면 **Surface의 자식에 있는 Volume만** 모은다. `RootSources2d`에 있는 별도 오브젝트의 Volume이 자동 포함되는 구조는 아니다. (`Scripts/NavMeshSurface.cs:286-296`)

### 3.7 Navigation Link — `NavMeshLink`

**붙이는 곳:** 끊어진 NavMesh 영역을 연결할 위치.

**하는 일:** 시작점·끝점, 폭, 양방향, 통과 비용, Area, Agent 타입을 설정한다. 활성화될 때 Unity `NavMesh.AddLink`로 등록하고 비활성화 시 제거한다. `Auto Update Positions`를 켜면 Transform 변경을 추적한다. **지형 Bake 소스를 추가하는 컴포넌트는 아니다.** (`Scripts/NavMeshLink.cs:14-69`, `:90-149`)

**2D 주의:** 기본 시작·끝점은 로컬 Z축 방향이므로 XY 맵에서는 Inspector/Scene 핸들로 두 끝점의 실제 위치와 방향을 확인해야 한다. (`Scripts/NavMeshLink.cs:14-44`)

## 4. 선택적 갱신·캐시 컴포넌트

아래는 기본 정적 맵 Bake에 필수인 구성품이 아니다. 소스를 캐시하거나 런타임 변경을 반영하려는 경우 사용하는 확장이다. 코드에 API가 있어도 **변경 직후 NavMesh가 자동 재생성된다는 뜻은 아니다.**

### 4.1 Navigation CacheSources2d — `CollectSourcesCache2d`

**붙이는 곳:** Surface와 같은 GameObject. 초기 소스 목록, 오브젝트→소스 조회표, 소스 경계를 보유한다. `AddSource`, `UpdateSource`, `RemoveSource`와 `IsDirty`를 제공하고, `UpdateNavMesh()`를 호출하면 캐시된 소스로 `NavMeshBuilder.UpdateNavMeshDataAsync`를 실행한다. (`Scripts/CollectSourcesCache2d.cs:8-22`, `:39-130`)

**연동:** 수집 전에 조회표와 2D 빌더 상태를 준비하고, 수집 뒤 소스 목록과 경계를 저장한다. 호출 순서 값은 `-1000`이다. `AddSource(GameObject)`는 상태가 준비된 뒤의 사용을 전제로 읽히며, 호출만으로 NavMesh 갱신이 실행되지는 않는다. 이 컴포넌트를 현재 씬에 붙이기 전에 별도 샘플에서 추가·수정·삭제 동작을 확인하는 편이 안전하다. (`Scripts/CollectSourcesCache2d.cs:24-49`, `:99-130`)

### 4.2 Navigation CacheTilemapSources2d — `CollectTilemapSourcesCache2d`

**붙이는 곳:** Surface와 같은 GameObject. 대상 Tilemap, `NavMeshModifier`, `NavMeshModifierTilemap` 참조를 지정해야 한다.

**하는 일:** Bake 후 타일 좌표→빌드 소스 인덱스를 저장한다. `Tilemap.tilemapTileChanged` 이벤트에서 캐시된 Area를 바꾸며, `UpdateNavMesh()`를 호출하면 캐시 소스로 비동기 갱신한다. 이 코드는 이벤트 안에서 인덱스를 조회하므로 캐시에 없는 새 타일·삭제 타일의 동작은 사용 전 확인이 필요하다. 이벤트가 일어났다는 이유만으로 `UpdateNavMesh()`를 자동 호출하지 않는다. (`Scripts/CollectTilemapSourcesCache2d.cs:11-26`, `:30-94`)

## 5. 에이전트 쪽 컴포넌트

### 5.1 Unity `NavMeshAgent`

경로를 요청하고 NavMesh 위에서 이동시키는 Unity 컴포넌트다. NavMeshPlus가 자체 Agent나 별도 경로 탐색 알고리즘을 제공하는 구조는 아니다. Surface의 Agent Type과 Agent의 타입을 일치시켜야 한다. 2D 빌드는 이동 가능한 평면을 만들지만, 기존 적 코드의 직접 위치 이동을 자동으로 교체하지 않는다. (`Scripts/NavMeshSurface.cs:25-27`, `Scripts/AgentOverride2d.cs:17-34`)

### 5.2 `AgentOverride2d`

**붙이는 곳:** `NavMeshAgent`가 있는 적 오브젝트. 시작 시 Agent의 `updateRotation`과 `updateUpAxis`를 `false`로 바꿔 3D 기본 회전·축 보정을 멈춘다. 매 프레임 `IAgentOverride.UpdateAgent()`를 호출할 수 있게 연결점도 제공한다. 이 컴포넌트 자체가 목적지를 정하지는 않는다. (`Scripts/AgentOverride2d.cs:17-34`)

### 5.3 `AgentRotate2d`

같은 오브젝트의 `AgentOverride2d`에 `RotateAgentInstantly` 전략을 등록한다. Agent에 경로가 있고 코너가 둘 이상이면 다음 코너를 향해 Transform의 Z축을 즉시 돌린다. 새 목적지를 정하거나 위치를 움직이지 않는다. (`Scripts/AgentRotate2d.cs:5-14`, `Scripts/RotateAgentInstantly.cs:21-38`)

### 5.4 `AgentRotateSmooth2d`

같은 오브젝트의 `AgentOverride2d`에 `RotateAgentSmoothly` 전략과 `angularSpeed`를 등록한다. 다음 경로 코너가 바뀌면 코루틴을 시작해 회전한다. 코드상 비공개 클래스이고 `[AddComponentMenu]`가 없어, 사용 가능 여부와 회전 결과를 별도로 확인해야 한다. 새 코너를 만났을 때 기존 회전 코루틴을 명시적으로 중지하지 않는다. (`Scripts/AgentRotateSmooth2d.cs:5-15`, `Scripts/RotateAgentSmoothly.cs:26-55`)

두 보조 컴포넌트 모두 목적지 지정·이동·공격 판단을 담당하지 않는다. 둘을 한 Agent에 동시에 붙이면 하나의 `agentOverride` 필드에 각각 전략을 할당하므로 `Start` 호출 순서에 따라 마지막 할당이 적용된다. (`Scripts/AgentOverride2d.cs:19-34`)

현재 [적 이동 코드](../../Assets/Scripts/Features/Enemy/Monster/EnemyController.cs)는 `Transform`의 XY 위치를 직접 갱신한다. [런타임 Slime 프리팹](../../Assets/Prefabs/Enemies/Runtime/Slime.prefab)에는 `Rigidbody2D`, `NetworkTransform`, 서버에서 실행되는 AI가 있다. NavMeshAgent를 적용한다면 **이동 위치의 권한을 한 경로로 정리**하고, 네트워크 위치 동기화와 공격 거리·4방향 애니메이션을 함께 검증해야 한다. 현재 패키지가 설치됐다는 사실만으로 Slime 경로 이동은 활성화되지 않는다. (`Assets/Scripts/Features/Enemy/Monster/EnemyController.cs:75-125`, `Assets/Scripts/Features/Enemy/Networking/EnemyNetworkController.cs:29-37`)

## 6. 코드 내부의 연결 부품

| 내부 타입 | 역할 | 근거 |
| --- | --- | --- |
| `NavMeshExtension` | Surface와 같은 오브젝트에서 소유자를 찾고, `CollectSources`·`CalculateWorldBounds`·`PostCollectSources` 훅을 제공한다. | `Scripts/NavMeshExtension.cs:9-55` |
| `NavMeshExtensionsProvider` | 확장 목록을 `Order` 순서로 보유해 Surface가 순회하도록 한다. | `Scripts/NavMeshExtensionsProvider.cs:10-55` |
| `NavMeshBuilderState` | 빌드 한 번 동안 루트 목록, 경계, 좌표 변환, 확장별 상태를 전달한다. | `Scripts/NavMeshBuilderState.cs:7-60` |
| `NavMeshBuilder2dState` | Sprite·Collider 메시 캐시와 Area·레이어·수집 옵션을 보유한다. | `Scripts/NavMeshBuilder2d.cs:12-27`, `:34-78` |
| `NavMeshBuilder2d` | Modifier를 거쳐 Tilemap 셀·SpriteRenderer·Collider2D를 `NavMeshBuildSource`로 변환한다. | `Scripts/NavMeshBuilder2d.cs:148-368` |
| `NavMeshAgentAttribute` / `NavMeshAreaAttribute` | Inspector에서 Agent 타입·Area 필드를 선택하게 하는 속성. 경로 계산은 하지 않는다. | `Scripts/NavMeshAgentAttribute.cs`, `Scripts/NavMeshAreaAttribute.cs` |

`RootSources2d`와 두 캐시 확장은 모두 `Order = -1000`을 사용한다. 여러 개를 함께 붙일 때 동순위 실행 순서는 코드에서 안정적으로 정의되지 않았으므로 초기화·캐시 결과를 확인해야 한다. 또한 `NavMeshExtensionsProvider.Remove()`에는 확장 비교 대신 대입이 사용된다. 확장을 런타임에 제거·교체하는 구성은 이 구현을 검토하고 별도로 시험해야 한다. 이는 **코드상 위험**이며 현재 씬에서 발생한 오류로 확인된 것은 아니다. (`Scripts/NavMeshExtensionsProvider.cs:29-55`)

## 7. 현재 `Initial_Stage`에 대입하면

2026-10-11에 다시 읽은 저장된 씬은 `Navmesh2D`에 Surface, `CollectSources2d`, `RootSources2d`가 붙어 있다. Surface는 `Collect Objects = Children`, `Default Area = Not Walkable`, `Use Geometry = Render Meshes`, Layer Mask는 전체 레이어다. Root Sources는 `Initial_Overworld_Base` 하나를 가리킨다. Base의 Modifier는 `Override Area = Walkable`이고 ModifierTilemap의 타일별 목록은 비어 있다. 현재 Surface의 `NavMeshData` 참조도 비어 있어 저장된 씬에 Bake 결과가 연결돼 있지 않다. (`Assets/Scenes/Gameplay/Initial_Stage.unity`의 `Navmesh2D` 및 `Initial_Overworld_Base`)

**이전의 넓은 사각형 원인:** Base Tilemap의 저장 경계는 `Origin (-133,-114)`, `Size (269,243)`이다. 앞선 설정에서 `Default Area = Walkable`이었기 때문에 2D 빌더가 **그 타일맵의 넓은 경계 Box를 Walkable 소스로 먼저 추가**했다. 현재 `Default Area = Not Walkable`로 바뀌어 이 생성 조건은 꺼져 있다. `CollectSources2d`의 세계 경계 계산은 첫 `Grid`의 여러 타일맵 범위를 참조하지만, 경계 계산만으로 Walkable 소스가 생성되는 것은 아니다. (`Scripts/NavMeshBuilder2d.cs:171-228`, `Scripts/CollectSources2d.cs:29-59`)

**현재 설정의 별도 오류 가능성:** `CollectSources2d`의 `Use Mesh Prefab`이 `Grid` GameObject를 참조한다. 그러나 `Grid`에는 `MeshFilter`가 없고, 2D 빌더는 `Override By Grid` 값과 관계없이 `Use Mesh Prefab`이 비어 있지 않으면 `GetComponent<MeshFilter>().sharedMesh`를 읽는다. 따라서 이 참조는 Bake 도중 null 참조 오류를 낼 수 있다. 메시 프리팹을 의도한 것이 아니라면 `Use Mesh Prefab`을 비우고, 셀 모양을 쓰려면 `Override By Grid`를 켠 후 다시 Bake해 확인한다. (`Scripts/NavMeshBuilder2d.cs:293-315`; `Assets/Scenes/Gameplay/Initial_Stage.unity`의 `Grid`와 `Navmesh2D`)

| 확인할 질문 | 해당 설정·코드 |
| --- | --- |
| 다른 오브젝트가 Unity 기본 수집으로 들어오는가? | Surface의 `Collect Objects`, `Layer Mask`, `Use Geometry`; `RootSources2d`는 이 단계에 적용되지 않음 |
| Base 경계 사각형이 생성되는가? | Surface `Default Area`가 1이 아닌지, Base에 Modifier가 붙었는지, `AddDefaultWalkableTilemap` 호출 여부 |
| 어떤 타일이 막히는가? | 오브젝트 Modifier의 Area와 `ModifierTilemap`의 타일별 목록, 실제 Bake 결과 |
| 동적 장벽·터렛이 길을 막는가? | Bake 당시 소스와 런타임 갱신·장애물 처리 정책. 현재 정적 Bake만으로 동적 오브젝트 변화는 반영되지 않음 |

### 현재 구성에서의 검증 순서

1. 현재 설정의 `Use Mesh Prefab = Grid` 참조를 제거한다. 셀 단위 모양이 목적이라면 `Override By Grid`를 켠다. 그다음 Bake가 완료돼 `NavMeshData` 참조가 생기는지와 Scene 뷰의 실제 이동 가능 폴리곤을 확인한다. (`Scripts/NavMeshBuilder2d.cs:293-368`)
2. 바닥의 타일 칸을 확인한 뒤, 물·환경 장애물이 어느 Area여야 하는지 정한다. 단지 화면에 보인다는 이유로 장식용 Tilemap을 장애물로 단정하지 않는다.
3. 막힌 곳, 바닥 경계, 스폰 지점, 목표 지점을 Scene 뷰와 Play Mode에서 확인한다. Slime의 실제 우회 이동은 적 코드가 Agent를 사용하도록 연결한 뒤 별도로 확인한다.

이 문서는 **패키지의 현재 구현을 설명하는 자료**다. 위 검증 순서는 패키지 코드에서 도출한 방법이며, 맵의 정답 Bake 설정이나 완성된 적 이동 구현을 선언하지 않는다.
