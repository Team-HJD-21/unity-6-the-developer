# 전술 화면 구현·검증 기록

2026-11-13 구현 / 11-14 폴리싱. main의 URP 2D 통합 뒤 네 번째 실행 구간에서 화면·조작·음악을 재검수했다. 구현과 아래 관측 범위를 인수 기록으로 남기며, native 키·held drag와 Client/Standalone 검증은 분리한다.

## 동작과 편집 원본

기존 터렛 클릭 패널을 제거하고 M 전술 카메라, 플레이어 중심 초기 시야, 확대·축소, 경계 클램프, 전술 화면 안 전체도와 시야 사각형을 연결했다. 터렛 선택·개별 활성화·강화·레벨 변경, CU 전용 자동/선택 명령, 피격·사거리 내 적 사건 목록과 목표 방향 표시를 제공한다. 상시 필드 미니맵과 중복 전력 요약은 추가하지 않았다.

권위 원본과 입력·편집 접점은 [Player UI API](../../../../02_architecture/06_player/player-ui-api.md#2026-11-13-전술-화면)를 따른다. 명령은 기존 채널에서 Host의 최신 점유·거리·생존·전력·냉각 조건으로 다시 검증한다. UI가 지역이나 터렛 상태를 확정하지 않는다. 점유율도 같은 actor/Match의 확정 지역 투영에서 CapturedCellCount/CapturableCellCount를 읽고, 유효 분모가 없으면 숨긴다.

얇은 기존 프레임·폰트·Player/CU 심볼을 재사용한다. 멀어지면 식별 Sprite와 적 묶음 수량으로 전환한다. 상세 터렛 이미지는 원본 프리팹의 활성 베이스·헤드를, 전체도 지형은 실제 Stage_1 Tilemap을 bake한다. [생성·입고·검수 규칙](../../../../94_art/tactical-ui-assets-2026-11-14.md)을 따른다. 프리팹4개는 실제 문서ID·본문을 유지하며 저장 순서만 정리했다.

## 실제 검증

| 범위 | 결과와 근거 |
| --- | --- |
| Unity 컴파일 | 세 번째 적용과 URP 통합 후 네 번째 적용 모두 오류0/경고0 |
| 전술 좌표·화면비·lease·프리팹 | TacticalHudTests 6/6. 4:3·16:9·21:9·32:9 계산과 실제 프리팹 참조 포함 |
| 카메라 소유·viewport 복원 | CameraManagerTests 13/13. 최상위 소유자 갱신, 중첩·종료·disable·aspect 복귀 |
| 원격 명령 권한 | TurretInteractionAuthorizationTests 4/4 |
| 실제 Scene | 세 번째 구간4/4. URP에서도 기존4개 통과. 추가 FieldHover/G 검사는 움직이는 카메라를 따라 포인터를 갱신하는 fixture 보정 뒤 별도1/1 통과. 최종5개를 한 번에 재실행한 결과는 아님 |
| Scene 검사 내용 | Host 버튼/메뉴·공용 시간/M·Esc/상태창 상호 배제/카메라·목표/확정 점유율·overflow/실제 UI 입력 모듈의 스킬 drag. 추가 검사는 실제 PlayerAttack 성공 뒤2초 tooltip 억제/복귀·G Host 명령·기존 클릭 패널 미노출 |
| 기존 피드백·설정 | CombatHudFeedbackTests 3/3, CommonUiSettingsTests 6/6 |
| 공용 Alert | 최종 저장본 CommonAlertPresentationTests 3/3. 입력 비차단·전용 폰트/재질·화면 경계와 위쪽 회피 |
| 음악 | GameplayMusicTests 8/8, GameAudioTests 6/6. [별도 실제 재생 기록](2026-11-13-day-night-soundtrack.md) 참조 |

915×515 Game View에서 먼 터렛12개의 실제 크기32px와 전체275×275·중간275×229.1666 시야의 경계밖=false를 계측했다. 이후 전술 조작 파츠만 화면 배율0.8의 가독성 하한을 적용했다. 800px보다 좁으면 창 폭에 비례해 줄이며 기존 전투 HUD 배율은 유지한다. 실제1440×810과915×515에서 정보 글자12.8 화면 픽셀, 도크·전체도·CU 명령 비겹침과 시작 점유율1/201→0.5%를 확인했다.

native 마우스로 전술 열기·휠·전체도 클릭·67m 원격 ON/OFF·강화·목표/닫기·CU 다중 선택을 관측했다. URP 최종 구간에서는 CU에서 두 터렛 선택 후 일괄 ON이 실제 Host2/2·전력65, OFF가0/2·전력0으로 확정되는 것을 확인했다. 960×720과1680×720에서 전체 맵 경계 안·정보12.8 화면 픽셀·도크/전체도/CU 버튼 비겹침을 확인했다. 21:9 RenderTexture가 Editor 패널보다 넓어1x 미리보기 좌우가 잘리는 것과 실제 전체 출력 화면은 구분해 캡처했다.

Main 설정110%/Music0·mute/취소 복원, 허브 메뉴 Continue, 무기 Hover·번호3 교체·발사15→14도 확인했다. URP 최종 구간에서 Settings Cancel/Menu Continue를 다시 native 마우스로 확인했다. 실제 Restart 중 로딩 화면의 M/G와 progress 배치를 검토했다. 설정 미리보기의 오른쪽 탄약12/15는 한 줄이며 원래 SPACE 이미지를 유지한다.

작은 창 Alert가 실제11px로 읽기 약한 것을 발견해 전술 조작과 같은 화면 배율0.8 하한(800px 미만은 폭 비례)을 적용했다. 본문22pt 기준17.6 화면 픽셀을 확보하고, 붉은 의미색을 밝은 coral로 조정했으며 전용 재질 FaceDilate0.08/기존 외곽선0.14를 사용한다. 배경·장식·설명문은 추가하지 않았다. 최종 저장 프리팹을 다시 재생·캡처했다. 활성 적1개를 Alert 밑/빈 공간으로 이동한 fixture에서 수량 라벨은 가려질 때0개, 빈 공간1개, Alert 교차0건이었다. CU 버튼과 Alert는 기존 도크/전체도/사건 목록과 함께 수량 라벨 회피 대상으로 처리한다.

Main 최초 버튼 alpha0→1, 준비 완료3.18초·초기 선택 없음·v0.1.0-alpha를 확인했다. 40초 관찰에서 별똥별/별 점멸/로고 광택을 각각 캡처했다. 캡처 중 평균 렌더 표본53.15fps는 이 Editor 실행의 관측값이며 성능 보장이나 전체 profiler 결과가 아니다. Credits는 정상 시간표에서 각 장면·로고·55초 이후 검은 배경의 명단을 캡처했고 마지막 클릭으로 Main 복귀를 두 번 확인했다. Main Intro 재개와 Credits3번 OST의 단독 재생도 확인했다. 컷씬 시간표와 원본 배경/로고는 수정하지 않았다.

공유 제외 `Docs/Local/Validation/Tactical20261113`의 iteration3-scene-tests.json, iteration3-tactical-tests.json, urp-scene-tests-first.json, urp-hover-g-tests.json, urp-alert-tests.json과14~20번 PNG에 근거가 있다. 원본 음악 관찰은 original-music-handoff.csv(7,158행/359.975초), Main/Credits는 각각 하위 폴더의 CSV/PNG로 보존한다. 캡처는 실제 Game View이며 생성 시안이 아니다.

장시간 UI 검수의 fixture에서만 정규 생산을 Dispose하고 기존 적을 비활성화했으며 Player/CU 개발 보호,13개 확보 셀과 해금 setup을 사용했다. 명령은 실제 Host 경로를 통과하지만 이 setup은 정상 진행으로13셀을 확보했다는 증거가 아니다. Alert 겹침에는 기존 적1개만 다시 활성화했다. Scene·난이도·권한 규칙을 영구 저장하지 않았고 Play 종료로 복구했다. 반환 시 Play=false, Stage_1 clean Edit Mode, 보호 표식0, 원래 Game View index1/non-maximized 및 임시 화면비3개 삭제 후 원래8개 항목을 확인했다.

## 입력 도구와 남은 인수

native 키 입력은 TEXT만 전달되는 도구 한계가 있다. 빠른 held drag도 down과 move가 같은 Dynamic frame에 들어오는 기존 원시 이벤트 증거와 같은 증상으로, 스킬 배정이 시작되지 않았다. 적을 멈추고 CanPrepare=true인 조건에서도 반복됐다. 정상 프레임의 가상 Mouse를 실제 UI 입력 모듈에 down→move→up으로 전달한 새 검사는 Q 장착까지 통과했다. Picker 장착 API를 직접 호출하지 않았다. 이 자동 검사를 실제 하드웨어 키·held drag 인수로 대신하지 않는다. 전체도 drag 시도 후 카메라 이동도 마지막 위치 클릭과 구분할 증거가 없어 native held drag 통과로 계산하지 않는다.

후속 Windows 기본 화상 키보드 경로는 앱 접근 승인 시간초과로 실행하지 못했다. 실제 키 입력이나 추가 관찰기는 실행하지 않았고, 재개 후 OSK 창0/전역 접근성 설정 변경0을 확인했다. 이 실패를 제품 입력 결함이나 자동 승인 검토의 명시적 거절로 해석하지 않는다.

URP 셰이더·폰트, CU 일괄/필드 Hover·G/Alert, 추가 화면비와 Main·Credits 음악 왕복은 위 범위에서 검수했다. Client 두 프로세스·Standalone·스피커 청감 인수는 수행하지 않았다. native 키와 held drag는 자동 검사를 포함한 구현 검증과 별도로 실제 하드웨어 인수가 남는다. 다른 세션의 이후 Encounter 변경은 이 기록의 실행 시점 이후일 수 있다.

## 기존 연결 교정과 제약

Graybox 출입구에 Animator가 없는 경로의 NearPlayer 호출만 null 검사하여 실제 필드 진입 예외를 교정했다. 잘못된33자리 GUID가 있던 PlayerAttackInputStateTests.cs.meta와 PlayerWeaponSelectionTests.cs.meta는 기존 참조가 없음을 검색한 뒤 유효32자리로 교정했다. 다른 GUID·참조는 유지했다. 병행 Core의 main 통합 중에는 Source/Unity를 인계하고 모든 기존 변경과 스테이징8개를 보존했다. 이후 사용자 마감 지시에 따라 관련 UI 변경·문서를 커밋 대상으로 분리했으며, 푸시·PR은 이번 마감 범위에 포함하지 않는다.

적은 현재 Scene의 활성·생존 개체를 최대한 공개한다. 미래 스폰이나 서버에만 있는 개체를 추정하지 않는다. 적 수는0.15초, 개체 검색은0.75초마다 갱신하며 Client는 실제 복제 계약을 따른다. 출력 카메라를 이동하므로 해당3D AudioListener 위치도 이동한다. 일괄 명령은 개별 요청 모음으로 부분 거절이 가능하다. G는 기존 F 회수와 겹치지 않는 근접 토글 시험 키다.

## PR #722 통합 후 카메라 경계·Smoothstep

사용자 추가 요청으로 main의 PR #722(8fbef4a68)을 Core가3aeb8a0d2로 통합했다. UI 기존 변경과 폰트의 추가 글리프를 보존했다. Stage_1 Scene instance의 `CameraLeftTop`/`CameraRightBottom` 두 참조를 실제 LeftTop(-138,120)/RightBottom(138,-120)에 연결했다. 최종 Scene에는 UI 관련 두 override만 추가했고, Editor 저장이 함께 직렬화한 Core 기본값 hunk는 남기지 않았다.

카메라 영역276×240과 기존 지역275×275를 분리한다. 보간 중 실제 viewport의 모든 변을 매 프레임 제한하고, 지역 texture를 늘이지 않고 실제 교차 world 구간으로 UV/위치를 정한다. 점유·명령 권한 정의는 변경하지 않았다. 지형은 원래 GUID를 유지해512×445로 rebake했다. 부모가 실제 PNG와 authoring 결과의 Scene·좌표·크기·GUID를 대조했다.

이동0.18초/zoom0.16초 Smoothstep을 연결했다. 클릭 재목표는 실제 현재 위치부터 시작하고 연속 입력은 gesture 시작 때만 easing을 시작한다. 검토 중 모서리 zoom-out의 중심만 순간 이동할 수 있는 문제를 찾아, FOV와 재중앙화가 함께 보간되도록 보강했다. 처음 framing과 원래 시점 복원은 즉시다.

공용 Unity를 소유한 Core가 통합 실행한 `TacticalHudTests`8/8과 `StageTurretCommandHudTests`6/6(71.63초)을 UI가 원본 JSON/XML·실제 Source·Scene·지형 PNG로 대조했다. Scene 검사에는 비순간이동·중간 렌더 위치·단조 수렴·모서리 zoom-out 중간 중심·이동/zoom 중 모든 변의 경계·지역 정의 불변이 포함된다. 남아 있던 과거 Initial_Stage 경로2개도 현행 Stage_1로 맞췄다. 이 자동 Scene 검사를 native held drag나 하드웨어 키 인수로 대체하지 않는다.

증거는 공유 제외 `Docs/Local/Execution/EncounterStage1-20261113/ui-i5-tactical-tests.json`, `ui-i5b-stage-tests-passed.json/xml`, `ui-i5-authoring-validation.json`이다. 추가로 Core의 정상 Scene 드라이버가 남긴 모션 CSV742data행과 Game View23프레임을 부모가 독립 감사했다. 0.001 world 허용의 경계 이탈0/이동 중심10개·zoom 중심9개/역행0/최종viewport(-138,-120,138,120)을 확인했고 실제 PNG000/003/007/022를 직접 보았다. 원본RGB가모두동일한1.194초무손실WebP도보존한다. 이 자동Scene모션을native입력통과로간주하지않는다. 이후 B6 변경과 현재 UI 변경은 별도 소유/커밋 범위로 유지한다.

사용자는 추가 OS 입력 설정 변경을 허용하지 않았다. MouseKeys·ClickLock·고정키 등 설정을 변경하지 않으며 실제 변경0이다. 기존 native 입력의 도구 한계와 OSK focus/닫기 실패를 제품 입력 결함으로 확정하지 않았다. 사용자가 OSK를 직접 닫은 뒤 창0을 확인했다.

부모의 마지막 native 키 검수는 정상 Field의 GameViewfocus/AppFocustrue/CanIssuetrue에서 supported Sky m/Escape를 각각1회 실행했다.90초1769행에서 조건을 유지한1758frame을 확인했지만 Keyboard TEXT2/키down0,실제Map상태변경없음이었다. 실제 마우스 Map 버튼 열기·전체도click 이동·닫기는 정상이며57PNG/원본RGB일치3.017초WebP를 남겼다. Source 입력 우회/가상device주입/전역설정변경 없이 Play를 정상 종료하고 Stage_1 clean/보호0을 확인했다. native키/helddrag는 검수 통과로 표시하지 않는다.

## 11-14 마감과 사용자 직접 확인

사용자가 키보드·드래그는 직접 확인하고 이번 UI 작업은 문서 정리·관련 변경 커밋으로 마감하도록 지시했다. 추가 입력 우회나 OS 설정 변경 없이 구현 작업을 마감한다. 관련 자동 검사와 실제 마우스 검수 결과는 위와 같으며, 다음 항목의 실제 하드웨어 인수 결과는 아직 기록하지 않는다.

- M 열기/닫기, Esc 복귀, WASD·방향키 카메라 이동, Home 플레이어 복귀 및 G 근접 토글. 전술 화면 중 플레이어 이동·공격이 차단되고 닫은 뒤 복구되는지 확인한다.
- 전체도·중간 버튼 카메라 드래그와 휠 확대·축소 중 경계·Smoothstep을 확인한다.
- 스킬 Hover에서 드래그 시작, Q/E 장착·교체, 영역 밖 취소와 미리보기 피드백을 확인한다.

Client 두 프로세스·Standalone·장치 청감은 이번 실행 범위 밖이다. 후속 PR에는 이 기록, Player UI/Camera/Alert API와 에셋 규칙을 함께 포함하고, 사용자 직접 확인 결과가 들어오면 검증 표를 갱신한다. 병행 Core의 우주선 기능·기존 폰트/렌더링 변경과 원래 스테이징 파일은 UI 커밋에서 분리한다.
