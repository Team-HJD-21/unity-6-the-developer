# 우주선 내부 방 이동

이 문서는 `SpaceShipGrayBox`, `LeftRoom`, `RightRoom` 사이의 현재 씬 이동 흐름을 설명합니다. 여기서 말하는 내부 이동은 우주선에서 방으로 들어가거나, 방에서 우주선으로 돌아오는 이동입니다. 미션 출격·귀환 흐름은 [우주선 이동 API](spaceship-travel-api.md)를 참고합니다.

## 씬 구성

- `SpaceShipGrayBox`는 `LeftRoom`, `RightRoom`을 이름으로 지정합니다.
- 각 방 씬은 Build Settings에 등록되어야 Additive로 로드할 수 있습니다.
- 우주선의 좌우 문에는 `SpaceshipRoomDoor`가 붙습니다. `roomOnRight`가 해당 문의 목적 방을 고릅니다.
- 각 방의 `ToSpaceshipDoor`에는 `SpaceshipReturnDoor`가 붙습니다.

## 우주선에서 방으로

`SpaceshipRoomDoor.OnTriggerEnter2D`는 Trigger에 들어온 Collider가 `PlayerFeetBox` 레이어의 Player인지 확인합니다. 이동 중이 아니면 목적 방을 선택하고 화면을 암전한 뒤 다음을 수행합니다.

1. 목적 방이 로드되어 있지 않으면 Additive로 로드합니다.
2. 목적 방에서 `ToSpaceshipDoor`를 찾아 Player의 X 위치를 문 기준으로 배치합니다. 왼쪽 방은 `door.x - 0.3`, 오른쪽 방은 `door.x + 0.3`이며 Player의 Y는 유지합니다.
3. 우주선 씬 루트에서 `SpaceshipHubCompositionRoot`를 찾아 목적 방 씬으로 옮깁니다. 이 Hub가 시작 우주선에서 만든 Player와 카메라를 소유하므로, 루트 이동 후 우주선 씬을 언로드합니다.

방 문 Inspector의 `blackoutSeconds`는 화면이 어두워지는 시간과 다시 밝아지는 시간을 합한 값입니다. 씬 로드와 언로드가 끝날 때까지 검은 화면을 유지합니다.

## 방에서 우주선으로

`SpaceshipReturnDoor.OnTriggerEnter2D`는 같은 Player Collider 조건을 확인한 뒤 복귀를 시작합니다.

1. 우주선 씬을 Additive로 로드합니다.
2. 현재 방이 왼쪽인지 오른쪽인지에 따라 `LeftDoor` 또는 `RightDoor`를 찾습니다.
3. Player를 우주선 문의 안쪽으로 이동합니다. 왼쪽 방에서 돌아오면 `LeftDoor.x + 0.3`, 오른쪽 방에서 돌아오면 `RightDoor.x - 0.3`이며 Y는 유지합니다.
4. 현재 방 씬을 언로드합니다.

복귀 문에도 `blackoutSeconds`가 있으며, 복귀 씬 로드·방 언로드를 암전 동안 수행합니다. 복귀 중에는 `SpaceshipReturnDoor.IsReturning`으로 우주선 문이 새 방 이동을 시작하지 않도록 막습니다.

## 현재 범위와 확인 사항

- 두 방이 동시에 열린 상태를 일반 이동 경로로 사용하지 않습니다. 우주선에서 선택한 문은 해당 방을 로드하고 우주선을 언로드합니다.
- 이 이동은 Player Transform을 명시적으로 이동시킵니다. 씬 이동만으로 Player가 문 사이를 걸어가는 구조는 아닙니다.
- 실제 왕복, 시작 허브에서 만든 Player·카메라의 수명, Trigger 위치와 Player Collider의 방향 교체는 Unity PlayMode에서 별도로 확인해야 합니다.
- 변경된 씬의 YAML에만 의존하지 말고, 각 씬 Inspector에서 문별 `roomOnRight`, 목적 씬 이름, `ToSpaceshipDoor` 연결을 함께 확인합니다.
