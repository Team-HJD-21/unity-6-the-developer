# 플레이어 저공비행 지면 반응

2026-11-15 사용자 요청. `Player.prefab`의 `PlayerHoverFieldFeedback`이 실제 발밑 이동과 같은 Scene의 바닥 타일을 조회해 먼지와 가까운 식생의 흔들림을 표시한다. 사운드·발걸음·게임 판정은 추가하지 않는다.

## 실행과 제작 참조

- `LateUpdate` → `Tick(FieldSimulationTime.DeltaTime)`가 진입점이다. `PlayerDirectionalColliders.GroundPosition`의 실제 변화량을 사용하므로 벽을 향해 입력만 유지하거나 정지하면 새 흔적이 나오지 않는다.
- 이동/설치와 같은 `PlayerTurretPlacementRules.FindGround`에서 같은 Scene의 육지를 찾는다. 현재 Overworld 바닥 Sprite 목록과 잔디 목록을 프리팹에 지정한다. 잔디에서는 연한 황록색 입자, 흙/돌 계열에서는 조금 큰 황갈색 입자가 이동 방향 뒤쪽으로 흐른다. 지원 목록에 없는 바닥이나 바닥이 없는 물 위에서는 생성하지 않는다.
- 이동 경로를 0.22 간격으로 보간해 낮은 프레임률에서도 한곳에 뭉치지 않고 이어지게 한다. 각 흔적은 생성 당시 이동 방향을 유지하며 뒤로 미끄러진다. 속도에 따라 길이/농도를 조절하고, 0.32초 동안 길이 방향으로만 늘어지며 얇아진다. 착지 연출처럼 높이가 부푸는 동작은 없다. 정지하면 새 흔적 생성이 멈추고 남은 흔적만 사라진다.
- 현재 타일셋의 녹색 식생 Sprite를 제작 목록으로 지정한다. 플레이어 1.9 범위 내에서 실제로 배치된 해당 타일만 반응한다. 바닥 잔디 그림 전체를 기울이지 않는다.
- 미리 제작한 먼지 16개, 식생 표시 8개를 순환 사용한다. 매 프레임 오브젝트나 Material을 생성하지 않는다. 맵 탐색은 Scene 변경/지면 미확인 때, 가까운 식생 조회는 이동 중 0.1초 간격으로 한정한다. 표시용 SortingGroup은 부모 플레이어의 묶음에서 분리해 지면/원본 식생 순서를 따른다.

## 물리·수명 경계

식생은 원래 타일의 색 알파를 잠깐 숨기고 같은 Sprite·Material·색·정렬·셀 Transform으로 제작된 표시 풀에서 그린다. 밑부분을 기준으로 짧게 기울였다 복귀한다. 타일 Transform과 Collider는 수정하지 않는다. 원래 색과 TileFlags를 완료·비활성화·사망·우주선 진입 때 돌려놓는다. 중복 플레이어가 같은 셀 표시를 동시에 소유하지 않도록 셀별 표시 소유자를 구분한다. 외부에서 타일/색/Flags를 바꾼 경우 그 변경을 과거 값으로 덮어쓰지 않는다.

필드 정지 동안 진행을 멈춘다. 포털·Scene 이동처럼 큰 위치 변화에서는 기존 흔적을 지우고 새 위치부터 샘플링한다. 지면 반응은 체력·이동 속도·적 감지·물리/저장 데이터와 독립된 표현이며, 체크포인트에 저장하지 않는다.

## 아트

현재 표시 풀은 `Assets/Art/Sprites/Player/HoverWake.png`를 사용한다. 내장 `image_gen`(`art_source: host_image`)으로 만든 길고 낮은 입자 흐름이며 오른쪽이 진행 방향, 왼쪽이 흩어지는 꼬리다. 원본은 2172×724 RGBA, 완전 투명 픽셀 1,451,795개다. SHA256은 `5CB1CD6632A8FD6BA1FE10952BB366B01A8FA15DFEA0541422A89D25090BB626`이다. Unity에서는 Single/Point/최대256/압축 없음으로 가져온다. 이전 `HoverDust.png`는 보존하지만 Player 표시 풀은 새 그림을 참조한다. 식생은 기존 `Overworld_Trees.png`의 Sprite를 재사용한다. 새 맵/타일 그림과 사운드는 추가하지 않았다.

생성 프롬프트:

> Create a game-ready 2D VFX sprite for a low-hover robot moving across grass, viewed top-down. A SINGLE sparse, narrow horizontal wake of wind-swept dust grains: three broken parallel wisps that taper into tiny flecks toward the LEFT, with the slightly denser source at the RIGHT. This is continuous forward motion airflow skimming flat across the ground, NOT a landing impact, cloud, smoke puff, explosion, cone, arrow, swirl, ring, or glowing magic. Use a restrained warm ivory / muted sand 3-shade palette and crisp stepped pixel-art silhouettes matching a low-resolution top-down pixel game. Very little mass: thin irregular dashes and tiny loose grains, separated by transparent gaps; total visible shape about 8 times wider than tall. One centered coherent wake, not a sheet or four separate clouds. Fill 85 percent of canvas width, about 25 percent of canvas height with art, leave clear padding. Transparent alpha background. No outline, no ground, no character, no text, no border, no shadow, no realistic blur. Designed to remain readable at roughly 40 by 6 screen pixels. Horizontal landscape image.

검증 진입점은 `PlayerHoverFieldFeedbackTests`다. 독립 Player/Tilemap fixture와 실제 Field Scene에서의 시각·멀티플레이 인수는 구분한다.

초기 가시성 보정 기록: Stage_1의 육지 48,774셀은 모두 지원 목록에 들어 있었으나, 카메라 orthographic size 18에서 작은 폭·낮은 알파·녹색 tint가 겹쳐 먼지가 거의 보이지 않았다. 폭/높이/농도를 높여 보이게 한 뒤, 사용자가 착지처럼 보인다는 피드백을 주어 현재의 이동 방향 입자 흐름으로 교체했다. 기존 먼지의 대비 픽셀 수치는 새 그림의 검증 수치로 사용하지 않는다.

2026-11-15 초기 검증: `PlayerHoverFieldFeedbackTests` 4/4 통과. 실제 제작 Player 프리팹과 Tilemap에서 이동/정지/바닥 없음, 식생 표시 복구와 Collider bounds/타일 Transform 보존, 우주선/사망 정리, 필드 정지/포털 이동을 확인했다. 바닥 RuleTile·기존 수풀·플레이어를 Unity 카메라로 60프레임 렌더했고 최대 먼지 6개/식생 2개가 표시되며 종료 후 원래 셀 알파 1로 복구됨을 확인했다.

이동형 흔적으로 교체 후 검증: 같은 테스트에 경로 보간·방향 전환·높이 감소 검증을 추가해 5/5 통과했다. 실제 Stage_1 맵에서 임시 Player를 오른쪽/위/왼쪽으로 이동 후 정지시키며 카메라 size 18로 100프레임을 렌더했다. 최대 9개 흔적, 정지 후 0개를 확인했다. GIF는 30fps 정상 속도이며 1920×1080 게임 화면의 중앙 640×480을 확대 없이 잘라 확인했다. 실제 Field Scene 수동 조작·멀티플레이·빌드는 실행하지 않았다.

## 나무 접촉 시 떨어지는 잎사귀 (2026-11-18)

흔들림을 시작한 타일에서 `TreeLeafBurstFx.Emit`을 한 번 호출한다. Player 쪽 변경은 이 호출과 기존 `Clear()`에서 효과를 정리하는 호출 두 곳이다. 별도 Scene 연결이나 Player 프리팹의 표시 오브젝트 추가 없이 첫 접촉 때 재생 풀이 준비된다.

- [TreeLeafBurstFx](../../../Assets/Scripts/Map/TreeLeafBurstFx.cs)가 [PNG](../../../Assets/Resources/TreeLeafFall.png), [프레임 JSON](../../../Assets/Resources/TreeLeafFallFrames.json), [2D Lit Material](../../../Assets/Resources/TreeLeafFallMaterial.mat)을 공용으로 읽는다.
- PixelFX로 만든 녹색 잎 4장 또는 5장 연출 중 하나를 고른다. 각 클립은 96×96, 37프레임, 15fps다. 좌우반전과 재생 속도를 조금씩 바꾼다. 나뭇잎이 기울고 뒤집히며 떨어지고 바닥 근처에서 사라진다.
- 타일의 실제 Sprite 크기와 셀 Transform으로 발생 위치·크기를 맞춘다. 플레이어가 이동해도 이미 발생한 잎은 해당 나무의 월드 위치에 남는다. 나무와 같은 Sorting Layer / Order의 독립 SortingGroup으로 Y 정렬하고, 원본보다 아주 조금 앞쪽에서 그린다.
- 표시 풀은 플레이어당 최대 16개다. 동일 위치는 2 게임 초 동안 중복 발생을 제한한다. 필드 일시정지 때 애니메이션도 멈추며, 기존 Player 정리 경로에서 함께 숨긴다. 타일 Transform과 Collider, 게임 난수·체력·판정에는 쓰지 않는다.
- 제작 소스와 재생성 방법은 [tools/PixelFx](../../../tools/PixelFx/README.md)에 있다. 원본 PixelFX와 Sprite Fusion의 MIT 고지는 함께 보존한다.

검증: `TreeLeafBurstFxTests`의 중복 생성·종료, 이동 중 월드 위치와 정렬 유지, 풀 상한·정리, 필드 정지 경계 4개가 통과했다. Scene을 저장하지 않는 직접 fixture 호출로 검사했다. 실제 Stage1 `Environment_Obj`의 `(93, -17, 0)` 나무 옆으로 플레이어를 이동시키자 한 묶음이 생성되는 것을 Game 뷰에서 확인했다. 정지 후 3초 관측에서 활성 묶음은 0, 원본 타일 알파는 1로 복구됐다. 이 검증은 로컬 Host의 화면이며 원격 Client와 Player 빌드는 별도다.
