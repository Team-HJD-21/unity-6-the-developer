# 전술 UI 에셋·폴리싱 기록

2026-11-14. 실제 Assets 입고와 URP 2D 통합 후 재생 검토를 마쳤다. native 키·held drag는 입력 도구 한계로 미검증이며 다른 관측과 구분한다. [승인 공통 UI 규칙](common-ui-rules.md)을 확장하고 [전술 동작](../02_architecture/06_player/player-ui-api.md#2026-11-13-전술-화면)과 연결한다.

## 소스와 용도

`Assets/Art/UI/Tactical`에 원본을 보존한다. 기존 `owner_player`, `owner_cu`, 공통 panel/button의 Nine-slice와 폰트는 재사용한다. 터렛/적/목표/명령을 임시 문자로 대체하지 않는다. 실제 수량·이름·제약·결과는 동적 텍스트다.

| 에셋 | 제작과 사용 |
| --- | --- |
| `TacticalSymbols-v1.png` | 내장 image_gen으로 원본 게임·기존 심볼을 참고한2×2 atlas. Cannon / Missile / 적 무리 / 목표 beacon |
| `TacticalCommands-v1.png` | 같은 심볼 계열의 power / upgrade / multiple selection / automatic power atlas |
| `glyph_*.asset`8개 | PNG 픽셀을 수정하지 않고 각 cell의 alpha24 이상 optical bbox를 참조하는 Sprite. 터렛 LOD·적 집계·목표·명령 버튼/근접 G에 사용 |
| `turret_*.png`7개 | 실제 Stage1 Cannon3·Missile3·Laser1 프리팹의 활성 베이스+헤드를 Unity 카메라로 렌더. 도크/필드 tooltip에 사용 |
| `Stage_1-overview.png` | 실제 Stage_1 Tilemap을 편집 전용 preview Scene에서 bake한512px 지형. 점유/개체/시야는 런타임 원본으로 별도 표시 |

첫 atlas 후보는 재질 잡음과 부드러운 가장자리가 있어 후속 수정본을 채택했다. 기존64px 헬멧·CU의 따뜻한 ivory/charcoal과 게임 Cannon/Missile/Slime 원본을 참조했다. 기존 PNG/GUID/게임 Sprite를 변경하지 않았다. 광택·네온·장갑 프레임·장식 회로·문자를 넣지 않는다. 아래 프롬프트와 결과는 목표 스타일의 제작 지시이며, 모든 요청이 픽셀 단위로 그대로 생성됐다는 뜻은 아니다.

## 입고·편집

PNG는 true-alpha 원본 그대로 보존한다. symbol atlas는1254×1254이며 Point/Uncompressed/no mipmaps/Clamp/NPOT None/sRGB로 입고한다. Sprite는 FullRect/pivot center/border0/PPU100이다. 작은 심볼과 원형 파츠에 Nine-slice를 적용하지 않는다. 고형 내부의 alpha253/254도 있어 alpha255 개수만으로 배경 투명도 오류를 판단하지 않는다.

`TacticalArtSetup.BakeAll`은 clean Edit Mode에서만 실행한다. 게임 원본을 편집하지 않고 임시 prefab/preview Scene을 사용·정리한다. 비활성 renderer와 range/minimap/flash/shadow/projectile은 제외한다. Rocket 원본의 뒤 animation frame에 불꽃이 있어 첫 비발사 프레임을 사용한다. 새 UI PNG의 재생성은 기존 meta/GUID를 유지한다. 일반 수정은 Style/Prefab Inspector에서 하며 제작 도구가 수동 배치를 덮을 수 있으므로 자동 실행하지 않는다.

atlas는 입고된 `Assets/Art/UI/Tactical` PNG를 정식 원본으로 재사용한다. 개인용 `Docs/Local` 생성 원본이 없어도 제작할 수 있으며, 개인 원본이 추가로 있으면 입고 PNG와 바이트가 같은지 확인한다. 두 경로 모두 없으면 실패한다. 실제 개인 원본이 없는 경로에서4개 Sprite를 읽었고 PNG SHA256/GUID가 변하지 않은 것을 확인했다. 별도2D Camera나 원본 재생성 서비스가 실행에 필요하지 않다.

지형은 Scene 경로와 현재 camera bounds가 baked 범위와 일치할 때만 사용한다. marker가 없는 Scene의 camera bounds는 기존 Region geometry다. 새 맵은 해당 authored 지형을 다시 bake하고 Style을 연결한다. 실제 이동 물체나 적의 미래 위치를 지형 그림에 굽지 않는다. 런타임에 추가 Camera/AudioListener를 만들지 않는다.

PR #722 후 Stage_1은 Scene 카메라 marker의276×240 경계를 사용한다. 현재 지형은512×445로 다시 bake했고 기존 terrain GUID를 유지했다. camera의 명시 aspect를 world width/height로 설정해 비정사각 경계의 좌우를 잘라 굽지 않는다. 표시할 때는 Scene 경로와 현재 camera bounds가 baked `TerrainBounds`와 일치해야 한다. marker 미연결 Scene은 기존 Region geometry가 camera bounds다. 지역 raster는 원래 geometry를 보존하고 camera bounds와 교차한 구간만 정확한 UV/좌표로 배치한다.

marker만 바꾼 경우 Scene instance 참조를 저장한 뒤 terrain-only 제작 메뉴를 사용한다. 전체 BakeAll/Configure는 필요하지 않다. 이 동작과 관련한 실제 Scene6/6·전술8/8 결과는 구현 기록에 구분해 보존한다.

## 실제 검토와 남은 범위

- 실제 UI bake를 확인하다 미사일 발사 불꽃이 포함된 것을 발견해 비발사 frame으로 교정했다. Cannon/Missile과 지형 PNG를 직접 검토했다.
- 실제915×515 Game View에서 reference32px가15px로 줄어 식별이 약해졌다. 먼 터렛은 최소32 화면 픽셀, 적 심볼20px·수량13px, Player/CU24px를 보존하고 라벨 간격·합침·선택 범위를 함께 보정했다. 최종 터렛 계측31.999998px,12개 활성이다. 이후1440×810/915×515와 URP960×720/1680×720 캡처에서 Player/CU 구분과 선택·도크·전체도를 다시 확인했다.
- 실제 전체 맵275×275 및 중간275×229.1666 viewport의 경계밖=false를 확인했다. 화면비별 계산 검사는4:3/16:9/21:9/32:9를 포함한다. 모든 기기·해상도의 실제 육안 검수로 확대 해석하지 않는다.
- 전술 조작 파츠는 화면 배율0.8 하한으로800px 이상 창에서16pt 글자12.8 화면 픽셀을 유지한다. 적 수량 라벨은 도크/전체도/사건/CU 버튼과 보이는 Alert를 피한다. 기존 적1개를 이동한 실제 fixture에서 Alert 교차0건을 확인했다.
- 설정 미리보기는 무기 번호를 바깥 왼쪽으로 옮기고 오른쪽 숫자 탄약12/15 한 줄·기존 SPACE 이미지를 유지했다. 설정 검사6/6 통과. URP Main 장식40초·Credits55초 이후 명단·설정/퍼즈 native 왕복을 검토했고 로딩은 실제 Restart 캡처를 검토했다. 컷씬 시간표·원본 배경/로고를 변경하지 않았다.
- 캡처는 공유 제외 `Docs/Local/Validation/Tactical20261113`의06/12~20번 PNG와 urp-main/urp-credits 폴더에 있다. 최신 네 번째 구간은 native 마우스 클릭·호버가 정상 작동했고 CU2기 일괄 ON/OFF까지 확인했다. 키 입력은 TEXT만 전달되고 held drag는 down/move가 같은 프레임에 들어오는 도구 한계가 있어 인수하지 않았다. 정상 프레임 UI 입력 모듈의 스킬 장착/G 회귀 검사는 이 native 인수와 별개다.

키보드·held drag는 11-14 사용자 지시에 따라 사용자가 직접 확인한다. 이 에셋 검수 기록은 해당 입력의 통과 판정을 포함하지 않는다. 후속 PR에는 이 규칙과 실제 구현·검증 기록을 함께 포함한다.

## 11-14 가독성 후속

지도/설정/상태는 `TacticalSymbolGraphic`의 24단위 벡터 원본이다. 기존 UGUI 메시 심볼 계열을 확장하며 폰트 문자를 아이콘으로 사용하지 않는다. 단색 얇은 선과 큰 빈 공간을 유지한다. 터렛 상태 배지는 전원·잠금·시계·전력·X·일시정지 형태로 구분하고, 색만으로 활성 여부를 전달하지 않는다. 기존 터렛 원본·공용 프레임·8종 심볼 atlas·지형 PNG와 GUID를 보존한다. 상태색은 UI에만 적용하고 월드 본체 tint를 덮어쓰지 않는다.

상시 문장 범례를 제거하고 점유 영역 자체에 채움·열린 테두리·사선·실제 진행 링을 사용하며 전선은 옅은 청록색으로 분리한다. 먼 적의 붉은 점은 개체의 실제 위치, 옆 묶음 수량은 실제 합계다. 지도 설정은 별도 큰 창을 늘리지 않고 기존 전체도 패널 안에서 표시한다. [후속 구현·검증 기록](../01_planning/02_implementation-records/2026/11/2026-11-14-tactical-readability.md)을 함께 확인한다.

아이콘 버튼은 기존 owner_player/owner_cu와 명령 atlas를 재사용하고 이전/닫기/설정/상태 등 단순 도형만 UGUI 벡터로 확장한다. 체력은 `TacticalHealthGraphic`의 얇은 분할 게이지이며 숫자만으로 대신하지 않는다. `TacticalActionHint`의 제작된 공통 hover 카드에만 행동 이름을 표시한다. 실제 피격의 잔상/흔들림은 Controller가 같은 최대 HP의 감소 표본으로 구동한다. 첫 연결이나 최대값 변경에 연출을 재생하지 않는다. 이 수정에는 새 생성형 bitmap이나 폰트 문자 아이콘이 필요하지 않았다.

사용자의 육각 격자 전환 참고는 후속 구현 승인을 받아 `TacticalModeFrame.shader/.mat`로 제작했다. 새 bitmap을 겹치는 대신 기존 UGUI shader 경로로 선을 그려 화면 해상도에서 일정한 굵기·셀 간격을 유지한다. 최초 구현은 진입0.22초/종료0.14초의 얇은 띠와 가장자리 잔상만 사용했으며(현재 규칙은 아래 11-14 재요청 항목을 따른다), 전면 색수차/왜곡/블러는 없다. 기존 HUD·전술 패널보다 먼저 그리는 Overlay Canvas(-900), `raycastTarget=false`, 로컬 Material 복제/해제로 구성한다. 정상 모드 중앙 alpha0과 실제 테두리 출력을 GPU 읽기 검사로 구분한다. .meta GUID와 Material/Prefab 연결을 함께 관리한다.

## 선택 atlas 후속 수정 프롬프트

```text
Use case: style-transfer. Image 1 is the edit target: a transparent 2x2 tactical glyph atlas (cannon turret, twin missile turret, three slime heads, waypoint beacon). Keep the SAME four recognizable silhouettes, their cell positions, their proportions, and all transparent padding. Image 2 is the exact production palette and edge treatment reference from the existing game. Make ONE targeted correction: remove every mottled material texture, gradient, bevel lighting, bloom, translucency and fuzzy edge inside the glyphs. Repaint all solid glyph surfaces in a deliberately small FLAT palette matching image 2: opaque warm ivory #eeeadd, opaque dark charcoal #202826, optionally ONE opaque desaturated gray. Broad white and dark areas must be alpha=255; empty space alpha=0, with only a very narrow antialias fringe if unavoidable. Dark outlines should be crisp square pixel steps at a consistent light weight, about one logical pixel around a 32px symbol. No soft haze outside outlines. Preserve the atlas layout and all four subjects; do not add labels, text, numbers, rings, panels, perspective, drop shadows or background. This is a small readable 2D game UI sprite atlas intended for 26-32px on-screen display, not an illustrated metal icon pack. True transparent PNG.
```

## 명령 atlas 생성 프롬프트

```text
Use case: stylized-concept. Asset type: ONE cohesive transparent 2D pixel-game COMMAND GLYPH ATLAS, a square image in a precise 2x2 equal-cell grid, no visible cell grid, one centered glyph in each cell with large transparent margins. This is the second atlas for an existing sci-fi action tower-defense UI. Image 1 is the exact current game's crisp two-tone HUD symbol style. Image 2 establishes the already selected tactical symbol family; match its silhouette readability and warm ivory/charcoal palette but use flat fills, not material texture. Top left: universal POWER TOGGLE glyph, a broken circular ring with one vertical stem, distinctly open at top. Top right: UPGRADE MODULE glyph, a short rectangular industrial microchip with three stout contact tabs on each side and ONE upward chevron cut out of its center. Bottom left: MULTIPLE SELECTION glyph, two small offset solid squares enclosed by four thin separate corner brackets, no enclosing full panel. Bottom right: AUTOMATIC POWER glyph, a small power-toggle ring surrounded by TWO short blocky curved arrow segments, clearly distinct from the plain power-toggle glyph. All four must read at 22-28 screen pixels, with a similar visual weight to the provided 64px native HUD symbol. Pixel steps and large clear negative space, a thin dark charcoal outline so ivory interiors remain visible against bright grass, no tiny details. Almost all pixels inside glyph bodies fully opaque. True alpha transparency everywhere else. Limited opaque warm ivory #eeeadd and charcoal #202826 only, at most one muted gray edge highlight. No lettering, digits, captions, labels, keyboard keys, color state, checkmarks, decorative circuits, radar grids, gears, realistic metal, soft gradients, glow, 3D perspective, background plate, drop shadows, or watermark. These will become separate cropped sprites by importer metadata; preserve equal cells and avoid bleed between glyphs.
```

## 11-14 전환/영역 재요청

사용자 후속 요청으로 전환을 전체 육각막과 중앙 파동으로 확대했다. `TacticalModeFrame` Material의 `_Cover`/`_Reveal`/`_Progress`,48px 셀 간격을 사용한다. 파동 가장자리의 작은 색 분리/육각 패턴 굴절만 있고 게임 장면 자체를 굴절시킨다고 보고하지 않는다. 전환 중800/정상-900 Canvas 순서와 입력 차단은 제작된 ModeEffect가 관리한다.

새 `TacticalTerritory.shader/.mat`는 기존 실제 지역 mask의 연속 채움 전용이다. 점유 외곽선의 월드 XY와 내부 공유 변 제거 규칙은 Field와 동일하며, 곡선화나 전선 삼각형으로 영역을 대체하지 않는다. 새 bitmap 없이 기존 UGUI shader/심볼 계열을 확장했다. Field 상태 글자는 기존 BodyFont/AlertText 외곽선 재질을 재사용하고, 문자는 사용자 요청으로 명시 허용된 짧은 상태만 사용한다. 새 이미지에 한글을 구워 넣지 않는다.

11-14 최종 사용자 조정: 전환 shader에서 검은 면을 제거하고 셀 내부는 투명하게 두며 전면 육각선만 중앙부터 걷힌다. 미점유 심볼은 끊긴 빈 영역 테두리로 변경하고 이탈만 별도 방향 화살표를 더했다. 기존 enum 순서는 보존하고 RegionExit만 마지막에 추가했다. 조작 버튼의 잠김/전원 아이콘은 기존 TacticalSymbolGraphic의 실제 자물쇠/전원 벡터를 재사용한다. action124×32, hover260×30 사각형은 기존 Prefab 객체만 조정했다. 상세는 [최신 수정 기록](../01_planning/02_implementation-records/2026/11/2026-11-14-tactical-refinement.md)을 따른다.
