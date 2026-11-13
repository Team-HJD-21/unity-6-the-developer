# Credits cinematic 제작 규칙과 편집 지점

2026-11-13 사용자 요청: 메인 Credits 버튼으로 진입하는 실제 2D 컷신. 공통 HUD 규칙 전체를 바꾸는 문서가 아니다.

## 장면과 담당

| 장면 | 담당 | 표현 |
|---|---|---|
| 선체 용접 | 양현석 | 용접기를 양손으로 쥔 새 자세, 노즐 끝의 아크·고온 불티·잔불·증기·국소 반사광, 컷 전환 후 완성된 선체 각인 공개 |
| 측면 우주선 비행 | 김진태 | 엔진 근접 → 전체 샷 → 가속 통과, 두 노즐의 개별 배기·형태가 변하는 불꽃, 궤적이 걷히는 방향으로 이름 공개 |
| 소행성 밀집 | 조수빈 | 크기·명도·속도가 다른 밀집 구간과 빈 통로, 전경 소행성이 이름 앞을 지나가며 공개 |
| 터렛 공중 투하 | 황재동 | 기존 터렛의 순차 낙하·제동·지면 그림자·착지 먼지와 반동. 모두 착지한 뒤 앞쪽 터렛 오른편의 지면에 이름만 표시 |
| 플레이어와 드론 | 이영빈 | 컷신용 정면/우측 보기 원화, 바이저 반사광과 시선 전환, 작은 드론의 부유·눈 깜빡임·화면 전환 후 얼굴에 이름 표시 |

각 파트는 검정 Fade-out → 다음 파트 Fade-in으로 분리한다. 마지막 파트 뒤에 기존 Gravity 로고 `The Developer`를 중앙에 표시하고, 로고가 사라진 뒤 검정 위 정적 Credits만 남긴다. 최종 명단과 역할은 [확정 역할](../03_team/TEAM_ROLE_OWNERSHIP.md)의 순서를 유지한다.

## 에셋 제작 규칙

- 우주선은 기존 HSM 시트의 청회색 선체·짙은 패널 틈·청록색 캐노피·대각 금속 띠·작은 호박색 부품을 유지한다. 원본에 없는 측면은 새 Sprite로 만든다. 원본 탑다운 이미지를 기하학적으로 찌그러뜨려 측면이라고 쓰지 않는다.
- 공구와 드론은 현행 흰 우주복의 두꺼운 어두운 외곽선, 흰색/회색 음영, 작은 구리색 부품을 따른다. 단독 도구 후보 PNG는 사용하지 않는다. 용접 자세는 도구와 두 손의 실제 파지가 함께 그려진 전용 원화다.
- 이미지 생성은 내장 image_gen으로 참조 이미지를 지정해 수행했다. 신규 PNG는 `Assets/Art/Cinematics/Credits`에 보존한다. 원본 PNG를 덮어쓰지 않는다. 구체 요청은 [생성 명세](credits-cinematic-generation.md)를 따른다.
- 배경·글자·불꽃을 캐릭터 그림에 합쳐 굽지 않는다. 드론 화면은 빈 상태의 원본을 유지하며 눈/스캔/이름은 별도 계층이다. 게임용 Motion Sprite는 보존하고 큰 샷은 별도 컷신용 정면·우측 보기·용접 자세 PNG를 사용한다.
- 투명 알파를 보존한다. 생성물의 낮은 알파 여백을 측정한 Sprite rect를 importer에 저장한다. PNG 픽셀을 자동 페인팅하거나 프레임마다 재정렬하지 않는다. 부드러운 원본과 픽셀 원본의 필터는 각자 유지한다.
- 불꽃은 독립 Sprite의 길이·밝기를 점화/가속에 맞춰 변조한다. 파티클은 작은 수량으로 사용하고 글자 앞의 먼지·광원이 가독성을 지우지 않게 한다. 헬멧을 훑는 반사 띠는 바이저 안으로 제한하며 용접의 반사광은 접점과 가까운 부위에만 약하게 넣는다.
- 용접 아크는 독립 Quad/전용 Shader다. 네 입자 계층은 짧은 고온 분사·드문 탄도 불티·작은 증기·냉각 잔불이며 `Copper nozzle endpoint` Transform을 발생점으로 공유한다. 선체 반사광도 같은 접점을 기준으로 좁게 제한한다. 단순 불투명 원형 입자 대신 전용 감쇠 Shader를 사용한다.
- 터렛 이름은 별도 패널 없이 텍스트만 표시한다. 터렛 Transform의 자식으로 두지 않고 낙하 중에는 숨긴다. 2026-11-13 후속 사용자 요청으로 기체 명판 패널 방식을 폐기했다.
- 한글 이름은 실제 NEXON 폰트로 표시한다. 글자 윤곽을 페인트처럼 따라 그리는 효과는 사용하지 않는다. 역할은 JetBrains Mono, 게임 로고는 기존 Gravity 폰트다. 장식 글자를 이미지 생성 모델에 맡기지 않는다.
- 기존 NEXON SDF에 없던 이름용 `김`, `빈`, `태` 세 글자를 추가했다. 기존 543자와 glyph index는 보존한다. 공통 UI의 폰트 선택을 바꾸는 변경은 아니다.

## Scene과 재사용

- Scene: `Assets/Scenes/Presentation/Credits.unity`.
- Prefab: `Assets/Prefabs/UI/Cinematics/CreditsCinematic.prefab`.
- `CreditsCinematicDirector`: 장면 순서/재생 시간/페이드/로고/최종 텍스트. Update는 unscaled time을 사용한다.
- 현재 파트 길이는 9.5 / 10 / 10 / 10 / 11.5초, 로고는 4초다. 정적 명단은 55초부터 Fade-in하며 자동 종료하지 않는다.
- OST 제작 중인 타임라인은 유지한다. 비행 파트는 전체 13.25~13.75초에 짧게 후진하고 14초까지 자세를 잡는다. 기존과 같은 14초에 가속을 시작하며 14.85초에는 원래 궤적으로 복귀한다. 이름 공개(15.55초), 파트 전환, 로고와 최종 명단 시각은 변경하지 않는다.
- 소행성 파트의 `BackgroundSlime`은 기존 Slime Idle 네 프레임을 사용하는 시각 오브젝트 한 개다. 게임 AI/Collider를 만들지 않는다. Inspector의 `SlimeFrom`, `SlimeTo`, `SlimePass`로 이동 경로와 파트 내부 통과 구간을 조절한다. sorting order 10으로 모든 소행성(15 이상) 뒤에 두며 이름 위쪽을 지난다.
- 각 `CreditsCinematicChapter`: 카메라 시작/끝, 확대/회전, 담당 텍스트, 시각 rig, 파티클 시작/끝. 용접은 정면/결과 카메라, 비행은 엔진 기준 상대 오프셋/전체 카메라, 드론은 전체/얼굴 카메라로 Inspector 값을 사용한다. 실제 Player AI·Turret 전력·Match를 생성하지 않는다.
- `CreditsNameReveal`: TMP 이름의 Fade와 선택적인 RectMask2D 공개를 담당한다. 글자 윤곽 추적이나 런타임 글꼴 아틀라스 수정은 하지 않는다.
- `CreditsCinematicChapter.Sounds`: 기존 프로젝트 SFX의 시작/종료·볼륨·피치·루프와 짧은 등장/퇴장 envelope. Seek 검수 중에는 재생하지 않으며 Pause/파트 비활성화 때 정지한다. 메인 음악은 변경하지 않는다.
- 사용자 제공 OST는 [Main·Credits 음악 규칙](frontend-soundtrack.md)을 따른다. `CreditsCinematicDirector.Music`에서 Another Journey를 한 번 재생하며 55초 정적 명단 이후에도 곡 끝까지 이어진다. 컷신의 기존 타임라인은 유지한다.
- `CreditsSceneLauncher`: 원래 메인 Canvas·선택·입력 상태를 보존하고 App의 `SceneFlowController`로 Credits만 additive load/unload한다. 게임 진행 Phase나 Match를 변경하지 않는다.
- Esc는 도중에도 복귀한다. 최종 정적 크레딧에서는 왼쪽 클릭도 복귀한다. 화면에 조작 설명이나 인디케이터를 추가하지 않는다.
- 제작 메뉴: `Tools > TeamHJD > The Developer > Credits > Build cinematic scene and connect Main`. 이 명령은 해당 컷신 Prefab/Scene을 다시 작성하므로 직접 조정한 값은 별도 보존하고 실행한다. 보통 위치·시간·카메라 수정은 Prefab Inspector에서 한다.
- 기존 Main 연결을 보존하며 컷신만 재생성할 때는 같은 메뉴의 `Rebuild cinematic only`를 사용한다. 이번 폴리싱은 이 경로로 수행했다.
- Main 공통 UI 재생성 시에도 Credits 연결을 보존하도록 기존 Main authoring 경로에서 연결 도구를 호출한다. 원래 Main 배경과 로고 애니메이션은 변경하지 않는다.

검증 결과와 실제 실행 자료는 작업별 기록에서 구분한다. 이 문서의 표현 의도만으로 실제 플레이/입력/빌드 검증 통과를 선언하지 않는다.

화면비는 별도의 16:9 실시간 RenderTexture와 검은 matte/AspectRatioFitter로 처리한다. 이는 저장된 영상을 재생하는 방식이 아니라 실제 Sprite·Shader·ParticleSystem을 매 프레임 렌더하는 방식이다.

2026-11-13 최신 적용과 검증은 [후속 폴리싱 기록](credits-cinematic-polish.md)을 참고한다. [이전 검증 범위](credits-cinematic-validation.md)는 전용 용접 자세·패널 제거 전 버전의 기록이다. 검증 영상은 이 Scene의 같은 카메라와 평가 경로로 출력한 검토 자료이며 게임에 내장한 재생 영상이 아니다.

비행·터렛 장면의 추가 품질 개선은 [동일 시간 내 재구성안](credits-flight-turret-rework.md)에 정리했다. 새 시점 원화·기구 동작을 포함한 제안이며 적용 완료된 연출과 구분한다.
