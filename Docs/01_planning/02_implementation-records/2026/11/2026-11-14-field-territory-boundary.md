# Field 점유 경계 표시

## 이번에 정한 표시 규칙

- 지형 타일 위, 나머지 월드 객체 아래에 바닥 표시를 둔다.
- 점유 안쪽은 초록, 미점유 바깥쪽은 적색이다. 경계에서 멀어질수록 투명해진다.
- 적색은 미점유 표시다. 적 소유권이나 피해 발생 여부를 뜻하지 않는다.
- 점유 표시의 중심선은 지도와 같은 `RegionGridSnapshot.CapturedCells` 외곽이다. 터렛의 들로네 전선으로 대체하지 않는다.
- 안쪽의 약한 빛만 흐르며, 실제 경계 위치나 게임 상태는 애니메이션으로 움직이지 않는다.

## 연결과 책임

`TacticalCommandController.Awake` → `FieldTerritoryRenderer.Attach(this)` → 확정 `Regions` 관측 → `RegionBoundaryGeometry.Build` → 월드 XY 메시.

지도도 `RegionBoundaryGeometry.Build`의 `Start`/`End`를 자신의 화면으로 투영한다. 같은 Match·Grid·Revision에서 같은 선분을 사용한다. 내부 공유 변은 제거하고 구멍·분리 영역·대각선 접촉을 구분한다. 경계 양쪽으로 띠 폭을 주어도 중심 좌표는 바뀌지 않는다.

Field 메시는 Snapshot 세대/Revision 또는 띠 폭이 바뀔 때만 갱신한다. 시간에 따른 빛 이동은 shader가 처리한다. Raycast·Collider·추가 Camera·RenderTexture를 만들지 않는다. HUD 종료 시 메시·Material·표시 객체를 해제한다. 전술 지도가 실제 표시되는 `IsMapPresented` 동안에는 Field 표시를 숨겨 겹침을 피한다.

## 그리는 순서

`Map` (물·지형 바닥) → **`Territory`** (이번 표시) → `Default` → `Entities` (지형지물·캐릭터 등) → 기존 HUD.

11/14 작업 전 실제 Stage_1을 CLI로 읽은 결과, 바닥 두 Tilemap은 Map(-1/0), Environment_Obj는 Entities/4였으며 Map에 있는 다른 Renderer는 없었다. 그래서 기존 월드 객체의 색·정렬을 바꾸지 않고 전용 Sorting Layer를 사이에 추가한다. 새 바닥 타일은 Map, 새 지형지물/캐릭터/이펙트는 Default 또는 Entities를 사용해야 이 순서가 유지된다.

## 수정·조절 위치

- 공용 경계: `Assets/Scripts/UI/Tactical/RegionBoundaryGeometry.cs`
- Field 표시·수명: `Assets/Scripts/UI/Tactical/FieldTerritoryRenderer.cs`
- 기본 색·폭·투명도·속도: `Assets/Resources/Core/FieldTerritory.mat`
- 실제 그리기: `Assets/Art/UI/Tactical/FieldTerritory.shader`
- 공용 경계/리소스 검사: `Assets/Scripts/Features/Player/Tests/Editor/FieldTerritoryTests.cs`

Material의 `Inside width`/`Outside width`는 월드 단위다. 작은 Grid에서는 좁은 점유지를 넘어 반대편까지 칠하지 않도록 셀 크기의 45%로 각각 제한한다. Runtime에는 Material 사본을 사용하므로 Play 중 사본 조절은 원본 에셋을 변경하지 않는다.

## 검증

Stage_1 실제 정렬 조사 기록: `Docs/Local/Validation/TerritoryBoundary20261114/scene-inspection.json`.

- UI 세션과 함께 실행한 전용 Editor 검사 5/5 통과. 3×3 Grid의 모든 512개 점유 조합에 대해 닫힌 경계·중복 선분·점유 면적을 검증했다. 구멍·대각선 접촉·원점 이동/직사각 셀·리소스/정렬도 포함한다.
- 실제 Stage_1 Play의 GPU on/off 비교에서 안쪽의 초록 대비 변화 +0.03529, 바깥쪽 적색 대비 변화 +0.02353을 확인했다. 경계 위 불투명 Sprite의 RGB 최대 차이는 0이었다. 실제 Player가 경계 위에 서서 선을 가리는 화면도 확보했다.
- 첫 CU 점유 셀은 CU 모델이 넓게 덮고 있어 바닥 표본으로 사용할 수 없었다. 인접 유효 셀을 임시 점유한 뒤 노출 바닥에서 측정하고 종료 시 원래 점유/위치로 복구했다. 이는 자연 전투로 확보를 완료한 검사와 구분한다.
- 같은 실제 Match에서 임시 점유가 1→5→6셀로 바뀔 때 메시가 갱신되고, 지도 입력 Snapshot으로 만든 선분과 Field 선분이 일치하는 것을 확인했다.
- Game View 캡처는 현재 Camera 좌표와 세로 offset이 달랐다. 같은 프레임의 불투명 파란 표식으로 실제 캡처 좌표를 보정한 후 비교했다. 최종 보정값과 앞선 실패 조건 요약은 ignored 검수 폴더의 `pixel-diagnostic.json`/`QA_NOTES.md`에 기록했다. 같은 이름의 PNG는 후속 검사로 교체되었으므로 최초 실패 원본 전체 보존과 구분한다.
- 전환 전체 길이가 0.6초인데 0.45초만 기다린 최초 지도 숨김 검사와 실패 조건도 기록했다. 최종 검증은 UI 세션이 실제 마우스로 지도를 열고 닫은 뒤, 별도 즉시 반환 읽기 스크립트로 수행했다. 지도 표시 시 Field 렌더러는 꺼지고, 닫은 뒤 다시 켜졌다. 두 시점 모두 실제 TacticalMapGraphic의 캐시 선분과 Field 선분이 4/4 일치했고 Match·Grid·Revision 11도 같았다.
- 근거는 `map-open-state.json`과 `field-return-state.json`이다. 긴 RuntimeCheck 단일 호출 전체가 통과한 것으로 바꾸어 기록하지 않는다. GPU/셀 갱신 검사와 실제 클릭 기반 지도 전환 검사를 합친 결과이며, 추가 전체 재실행은 하지 않았다.

자료: `Docs/Local/Validation/TerritoryBoundary20261114/`의 `pixel-diagnostic.json`, `03-player-above-boundary.png`, `04-capture-updated.png`. 체크포인트 저장이나 Scene/Prefab 저장을 수행하지 않았고, 임시 검수 객체는 제거했다. OS 키보드/마우스 입력 인수를 대신하는 검사가 아니다.

주의할 실제 시작 상태: CU의 최초 점유 1셀은 큰 CU 본체가 덮을 수 있다. 이는 다른 월드 객체 아래에 그리기로 한 규칙의 결과다. 주변 셀을 확보하면 바닥에 드러난 경계가 보인다. 이 이유로 강제로 CU 위에 경계선을 올리거나 첫 점유 범위를 바꾸지 않는다.
