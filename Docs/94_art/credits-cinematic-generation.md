# Credits 신규 Sprite 생성 명세

2026-11-13. 내장 image_gen 사용, 모든 요청 transparent_background=true. 생성 PNG 원본은 프로젝트로 복사했다. Sprite import rect는 알파24 이상 외곽의 고정 경계이며 PNG 픽셀 자체는 수정하지 않았다.

공통 금지: 텍스트·워터마크·장면 배경·과도한 네온·원본에 없는 장식 추가. 최종 텍스트는 Unity TMP가 담당한다.

## ShipSide.png

참조: `Assets/Art/Sprites/Spaceship/Spaceship_by_HSM/Idle/TopDown_Spaceship_c2_Idle_HSM.png`.

요청: 기존 탑다운 시트와 같은 우주선의 오른쪽을 향한 단일 정투영 측면. 청회색 각진 금속 외곽, 앞쪽의 낮은 청록 캐노피, 어두운 패널 틈과 환기구, 뒤의 두 엔진, 옅은 대각 금속 띠, 작은 호박색 부품을 유지. 3:1 수평 실루엣과 4–5단 픽셀 음영. 엔진 불꽃은 제외. 전체 외곽과 투명 여백을 유지하고 시트·새 무기·배경·라벨은 만들지 않음.

## Welder.png

참조: 현행 `V2/Idle/Idle_Down_0.png`, `V2/SpannerEightWay/RepairContact_UpRight.png`.

요청: 흰 우주복 주인공의 펼친 손에 잡히는 짧고 통통한 용접/각인 권총. 오른쪽을 향한 짧은 몸통과 아래 손잡이, 흰색/회색 공업용 금속, 짙은 손잡이, 짧은 구리 노즐, 작은 푸른 유리 부품. 손그림 같은 어두운 외곽선과 절제된 회색 3단 음영. 길게 뻗는 막대·라이플·플랫 UI 아이콘·실사·손·케이블·불꽃·발광 배경 제외.

## Drone.png

참조: 현행 `V2/Idle/Idle_Down_0.png`.

요청: 주인공 헬멧 폭 정도의 친근한 부유 정비 로봇 정면. 둥근 사각 흰색/회색 몸체, 큰 빈 짙은 화면과 흰 프레임, 작은 측면 추진기, 작은 구리 안테나, 접힌 작은 작업 팔. 기존 캐릭터의 두꺼운 외곽선과 간단한 금속 음영. 얼굴의 눈·입·글자와 불꽃은 생성하지 않고 화면을 비워 놓음. 사람 같은 다리·날개·광택 3D 렌더·네온·바닥 그림자 제외.

## EngineFlame.png

참조: 생성한 ShipSide.png.

요청: 오른쪽의 짧은 흰색/청록 점화부에서 왼쪽으로 뻗는 단일 수평 엔진 불꽃. 세 개의 불규칙한 픽셀 혀, 우주선 캐노피와 같은 제한된 5색 청록/파랑, 3:1 실루엣. 외부 하드웨어·연기·큰 Glow·별 입자·텍스트·검정 배경을 제외. 런타임에서 독립적으로 길이와 밝기를 변조할 단일 Sprite.

## 2026-11-13 폴리싱: 컷신 전용 플레이어 세 자세

내장 image_gen, transparent_background=true. 다음 세 PNG를 `Assets/Art/Cinematics/Credits/`에 복사했다. 원본 게임용 캐릭터 PNG와 생성 PNG 픽셀은 수정하지 않았다. 정면 → 우측 보기 → 용접 자세를 같은 선 두께와 음영으로 제작했다. 용접기는 별도 소품을 팔에 얹지 않고 두 손의 실제 파지와 함께 그렸다. 노즐 위치는 별도 Transform으로 지정하며 효과를 원화에 굽지 않는다.

### AstronautFront.png — 최종 생성 프롬프트

참조 1: `Assets/Art/Sprites/Player/Motions/V2/Idle/Idle_Down_0.png` (외형). 참조 2: `Assets/Art/Cinematics/Credits/Drone.png` (선과 음영).

> Use case: identity-preserve. Production transparent 2D game cinematic sprite, ONE full-body astronaut, directly facing viewer, neutral symmetric standing posture, arms relaxed beside torso, no props. Image 1 is the exact character identity and costume reference. Image 2 is ONLY linework/resolution/paint shading reference for a companion asset. Preserve image 1's very large plain round off-white helmet, inset nearly black visor with two diagonal white reflection slashes, short compact body, plain white sleeves with dark gray joints, simple small shield-shaped chest panel, rounded white gloves, short white boots with dark soles. Preserve recognizable proportions. Render it clearly at native large resolution with confident dark ink outlines, hand drawn flat white/light gray/charcoal cel shading matching the companion's restrained material treatment; no tiny scratch texture and no extra details. Exactly this protagonist, not a new spacesuit design. Whole character centered and completely visible with transparent padding, isolated true alpha. No environment, no lettering, no glow, no shadows on ground, no antenna, no colorful straps, no technical greebles, no gradients, no photoreal or 3D. Eyes/human face must never be visible. This is a reusable sprite for camera closeups, not a painted scene.

### AstronautLookRight.png — 최종 생성 프롬프트

참조 1: 위 `AstronautFront.png` (외형·음영 유지). 참조 2: `Assets/Art/Sprites/Player/Motions/V2/DiagonalIdle/ToolIdle_DownRight_0.png` (방향).

> Use case: identity-preserve. Make the exact SAME full-body 2D astronaut from image 1 turn modestly toward screen RIGHT in a three-quarter front view, following the facing direction in image 2. Image 1 is the edit target and invariant design/style reference; image 2 is ONLY pose/facing reference. Keep helmet size, short compact proportions, white/gray cel shaded costume, heavy dark outline width, inset black visor with two white diagonal reflections, small shield chest panel, glove and boot shapes exactly consistent with image 1. Relaxed arms, standing at same baseline with the same height, centered with transparent padding. This will be swapped with the front sprite in a short head/body turn, so do not add new costume details or change scale/proportions. One character only, complete silhouette, true transparent background. No environment, props, floor shadow, text, human face, colored straps, antenna, extra backpack equipment, 3D rendering or lens effects. Restrained hand-drawn flat shaded game sprite.

### AstronautWelding.png — 최종 생성 프롬프트

참조 1: 위 `AstronautFront.png` (동일 인물). 참조 2: 기존 `Drone.png` (소품의 선과 음영).

> Use case: identity-preserve. One production full-body transparent 2D sprite of the SAME white astronaut in reference image 1, now naturally HOLDING a compact industrial WELDING TORCH and concentrating on work. Match image 1's exact giant rounded helmet, inset black visor with two diagonal white reflections, short body, small shield chest panel, plain segmented white/gray suit and boots. Slight forward lean, helmet facing three-quarter down LEFT, BOTH mitten gloves visibly and plausibly gripping ONE short thick welding torch in front of the lower chest. Torch held diagonally DOWN LEFT, short copper nozzle sticking out clearly beyond the left glove and silhouette; nozzle endpoint must be unobstructed. Torch design: white/charcoal squat housing, small copper nozzle, dark grip. It must look like a compact maintenance tool in the same chunky linework as the suit and companion drone reference 2, not a rifle, gun, long wand or glossy realistic prop. No disconnected hands, no extra hands, no cable. Keep all limbs legible, believable relaxed bent elbows, white helmet crown fully visible. Dark confident outlines, restrained white/lightgray/charcoal cel shading, no detailed scratch noise, no photoreal or 3D. Centered complete body with transparent padding. No welding sparks, smoke, light flare, beam, environment, metal workpiece, lettering or ground shadow; these will be separate animated Unity layers. Single sprite, no sheet, true alpha background.

### HullWorkPanel.png — 최종 생성 프롬프트

참조 1: `AstronautWelding.png` (표현). 참조 2: `ShipSide.png` (선체 색). 기존 선체 이미지를 잘라 쓴 첫 시험의 어색한 구멍·환기구를 대체하는 연속된 전경 금속판이다. UI 패널로 사용하지 않는다.

> Use case: stylized-concept. Production 2D foreground game sprite, a SINGLE continuous long horizontal spaceship hull panel being welded, isolated transparent background. Image 1 is the companion character line/shading style; image 2 supplies only blue-gray hull material/colors. A plain industrial blue-gray metal slab spanning roughly 6:1 width to height, straight horizontal TOP EDGE and shallow visible upper surface, a dark thick front lip underneath, beveled corners, two restrained fasteners at far left/right, one fine panel join near center. Hand-drawn dark outlines with restrained flat cel-shaded blue-gray planes matching the astronaut. The middle upper edge must be uninterrupted metal for a welding contact point. Not a whole ship, not a UI panel or framed card. No holes, vents, pipes, sawtooth shapes, lights, glow, orange markings, tools, character, lettering, background or shadow. Complete simple silhouette with true alpha and transparent margin. Do not bake sparks, welding light, scorch or smoke into the art; those are runtime effects. Wide horizontal composition.
