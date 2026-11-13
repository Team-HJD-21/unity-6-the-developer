# HUD v6·공통 UI 적용 검증

2026-11-12, Unity 6000.3.23f1 / Windows Editor. [공통 UI 규칙](common-ui-rules.md)의 구현·검증 기록이다. 원래 HUD v6 승인을 기준으로 확장 화면을 적용했으며, 확장 화면의 최종 미감은 사용자 리뷰 대상이다.

## 적용 범위와 진입점

| 범위 | 실행 경로·편집 위치 |
| --- | --- |
| 로컬 전투 HUD | `PlayerSkillHud` → `CombatSkillPicker`, `CombatWeaponStack`, `CombatPlayerIndicators`; `PlayerSkillHUD.prefab` |
| Player/CU·상단·효과 | `InitialStagePlayerHUD`·`StagePlayerVitalsView` → `CombatVitalsGroup`, `CombatHudHeader`; `InitialStagePlayerHUD.prefab` |
| 우주선 숨김 | `CombatHudContext`가 실제 `PlayerMatchPresence.Location`을 읽고 표시·입력만 숨긴다. owner·장착·고정 상태는 유지한다. |
| 크기 | `AppRoot.DisplaySettings` → `CombatHudScaleBinding`; 설정 적용만 로컬 저장, 취소는 이전 값 복원 |
| 메뉴·설정·결과 | `ScreenManager` → `AppScreenPresenter` / `SettingsScreenPresenter` → `Core/AppScreenStyle`의 제작 프리팹 |
| 시작 화면 | 기존 배경·LogoSet·LogoText 보존, 별도 `StartupControls` Canvas의 시작/설정/종료 |
| 로딩 | `LoadingScreenPresenter` → `LoadingScreen`; 실제 진행률 또는 수치 없는 이동 막대 |
| 제작 도구 | `HudV6AssetImport`, `HudV6PrefabSetup`, `CommonUiPrefabSetup`, `CommonFeatureUiSetup`는 Editor 전용 초기 제작 도구다. 일반 수정은 프리팹 Inspector에서 한다. |

변경은 화면 표현·기존 명령 연결과 Q/E 교체 규칙에 한정한다. CU 요청은 기존 Host 명령 경계를 사용하고, 개인 메뉴가 공유 월드 시간이나 다른 소유자의 입력 잠금을 해제하지 않는다. 미구현 기능의 빈 카드·임시 버전·메뉴 사용법 문장은 표시하지 않는다.

## 시안과 실제 화면 비교

기존 게임 배경·로고·폰트·무기·스킬 원본으로 Startup/Menu/Settings/Loading 1920×1080 시안을 먼저 작성하고 실제 Game View와 비교했다. 얇은 흑연색 프레임, 작은 모서리 절개, Gravity 제목·키와 NEXON 본문, 절제된 황색 강조를 공통으로 사용한다. 배경·로고 원본은 수정하지 않았다.

실행 중 발견한 표시 문제는 다음과 같이 수정했다.

- 무기 후보의 배경·테두리·그림·키 전체에 alpha를 적용하고 선택 카드를 앞에 놓았다.
- 상태창보다 앞에 남던 필드 수리 프롬프트를 정렬 순서로 해결했다.
- 단색 패널과 게이지에 투명 여백이 늘어나던 문제를 수정했다. Filled 게이지는 원본 중심의 불투명 texel만 참조하는 별도 Sprite를 사용한다.
- 스킬 트리의 실제 스킬 4개만 표시하고 미지정 카드와 연결선을 숨겼다. 데이터 참조 8개는 보존했다.
- 메뉴에 기존 우주선 복귀를 유지하되 실제 게임 흐름에서 의미가 없으면 행·높이·키보드 순환을 함께 줄였다.
- 설정을 오간 뒤 메뉴/메인 선택이 사라지던 문제를 수정했다. 이전 화면 선택과 가려지기 전 메뉴 선택을 각각 보존한다.
- 설정/결과/로딩이 덮는 동안 필드 디버그 패널과 알림도 숨긴다.

## 자동 검사

로컬 Pipeline의 EditMode 테스트에서 검증한 묶음은 총 58개다. 각 묶음의 최신 결과를 보관한다. 전체 프로젝트 테스트·CI 빌드·Standalone 검증을 뜻하지 않는다.

| 묶음 | 수 | 주요 경계 |
| --- | ---: | --- |
| `CombatHudRulesTests` | 22 | Q/E 교체·교환·중복 금지, 영역·보간 규칙 |
| `PlayerSkillPrefabTests` | 14 | 실제 제작 프리팹·원본 스킬/장치 참조 |
| `PlayerWeaponHudTests` | 2 | 기존 4개 무기 슬롯·선택 연결 |
| `PlayerStatusPanelTests` | 9 | 상태/스킬 트리 데이터·기존 동작 |
| `CommonUiSettingsTests` | 6 | 유한 배율·범위·저장/취소, 화면 lease, 실제 제작 화면 |
| `CombatHudPresentationTests` | 2 | 실제 부채꼴 raycast, hover·drag·swap·cancel, 입력 차단·XP |
| `LocalPlayerHudBindingTests` | 1 | 로컬 owner 연결·해제·수명 |
| `StageTurretCommandHudTests` | 2 | 실제 Host 버튼·구독, 메뉴 중 월드 시간·다른 lease, 최종 응답·설정 덮임·키보드 선택 복구 |

설정 덮임 검사는 TestRunner가 `LateUpdate` 전에 다시 실행되는 경우를 확인해 실제 화면 갱신을 기다리도록 수정했다. 실행하지 않은 프레임의 값을 화면 결과로 판정하지 않는다.

최종 재가져오기 뒤 컴파일 오류 0, 대상 프리팹 11개의 Missing Script 0, 전체 Assets meta 검사 0 error, 저장소 Python 검사 50/50, 변경 문서의 상대 링크 151개와 `git diff --check`를 확인했다. Python fixture는 기본 sandbox TEMP의 하위 생성 권한 오류를 분리하고, 해당 프로세스에서만 작업공간 내부의 임시 폴더를 사용해 동일 검사를 통과했다.

## 직접 단계별 입력·실제 Scene 관찰

자동 NUnit 결과와 별도로 실제 Startup, Initial_Stage, SpaceShipGrayBox에서 입력을 한 단계씩 전달하고 모델·Game View를 읽었다. CLI의 Editor 전용 `HudPlayInput`은 가상 Mouse/Keyboard 상태를 **정상 Input System 프레임**에 전달한다. UI의 `onClick`/장착 핸들러를 직접 호출하지 않는다. OS 물리 키보드 전송이 검증됐다는 의미는 아니다.

| 검수 | 관측 결과 |
| --- | --- |
| 스킬 4종 호버 | 드래그 전부터 해당 원본 그림·설명 표시, 4구획 배치·안쪽 빈 영역 분리 |
| Q/E | 새 스킬은 대상만 교체, 반대 슬롯 스킬은 교환, 예고 중 원본 유지, 실제 drop 뒤 반영 |
| 취소·재드롭 | 영역 밖 금지 배지, 우클릭·Escape·실제 입력 lease 상실 취소, 기존 장착 유지, 같은 슬롯 재드롭으로 중복 없음 |
| 장착 피드백 | 큰 디스크·목적지 키·화면 중앙 X 기반 영역 예고, 실제 성공 후 fade·기본 슬롯 복귀 |
| 무기 | 카드 클릭·숫자·Tab·마우스 휠 선택, 앞뒤 순서와 카드 전체 반투명·회전, UI 위 휠의 게임 조작 차단 |
| 탄약·대시·XP | 실제 사격 5→4, 재장전 뒤 5; 회전 화살표 관찰, 무한 탄약 숨김; 실제 SPACE 대시; 실제 XP debug 버튼 +10으로 모델·월드 팝업 확인 |
| 독립 바 | Player/CU 각각 고정·해제·호버 수치, CU HP/전력 공유; 실제 CU 1500→750 피해로 100%/50% 길이 확인 |
| 효과 | 실제 slow 2개·최강 배율 .5, 호버 50%, 만료 뒤 숨김. 없는 효과별 시간은 표시하지 않음 |
| 상태·트리·포탑 | 실제 상태창·스킬 트리 버튼/스크롤, 실제 스킬 4개, 기존 수리·활성화 등의 Host 버튼 검증과 분리해 외형 확인 |
| 개인 메뉴·디버그 | 실제 Escape·Resume·Settings, 월드 시계 진행, 실제 Reset/Day–Night/Auto 버튼과 Host 최종 피드백 |
| 설정 | 75/110% 실시간 미리보기, Escape 취소로 85% 복원, 실제 적용 저장·기본값 키보드 조작, 선택 복귀 |
| 우주선 방문 | 모든 필드 HUD 숨김, 메뉴·설정·Resume 가능; 정상 출구 복귀 후 동일 actor·E 장착·CU 고정 상태 복구 |
| Startup/허브 | START GAME 선택·Enter로 실제 우주선 도착, 두 버튼 메뉴와 키보드 순환 확인. 배경/로고 유지 |
| 실제 결과 | CU 파괴 이벤트로 실제 패배·완료 웨이브 표시. 재시작 클릭 후 새 Match·Player 재연결·CU1500 복구. 결과와 개인 메뉴의 복귀 클릭 후 실제 허브·Match 종료·필드 HUD 제거 |
| 로딩 | 실제 시작/전환 중 제작 화면, 실제 Scene 진행률 확인; 별도 격리 표시에서 불명 진행률은 빈 수치·이동 막대 확인 |

평상시 전투와 안정된 조작 검수 환경을 구분했다. 긴 드래그/애니메이션 관찰 때만 런타임 검수 세션의 Enemy launcher 예약을 중지하고 Host despawn을 사용했다. 제품 Scene에 저장하지 않았다. 결과·재시작·복귀의 최종 검수는 launcher가 활성인 정상 세션에서 수행했다. 이동 API·실제 피해/slow 등은 상태 준비에 사용했고, 해당 작업 자체를 마우스 조작으로 보고하지 않는다.

실제 입력 뒤 연속 Game View RT를 읽어 반원 등장 약 .22초, 성공 디스크 소멸, 무기 카드 alpha·회전, 약 1.5초 재장전 회전, XP 팝업을 관찰했다. `Time.timeScale=1`, 정상 프레임을 사용했다. 기록 PNG는 너비 960으로 축소한 실제 프레임이며 시안으로 대체하지 않았다.

검증 화면은 1920×1080, 1366×768와 16:10 Game View(실제 915×572), HUD 75/85/100/110% 조합을 포함한다. WXGA 100%에서 실제 Q→E 드래그 예고·완료도 확인했다. 모든 해상도·4K·울트라와이드·실제 게임패드의 인수를 선언하지 않는다.

## 보존·한계

두 기존 HUD 프리팹의 원래 fileID는 모두 유지했다. Startup에서는 기존 StandaloneInputModule 하나를 InputSystemUIInputModule로 교체했다. 메인 Background·LogoSet·LogoText의 GameObject/컴포넌트 데이터는 원본과 동일하다. 프리팹 블록 순서·행 끝 공백만 정리한 전후에 모든 직렬화 블록의 내용이 같음을 대조했다. 새 에셋의 meta와 중복 GUID도 검사했다. 시작 전부터 있던 사용자 NEXON SDF 변경은 원래 파일 해시 그대로 보존했다.

점유 가능 영역의 확정 분모와 보스 presentation 원본은 아직 없어 `--%`와 숨김을 사용한다. 기능이 없는 것을 임의 수치로 채우지 않았다. 기존 Startup About/Notice는 실제 listener가 없는 stub이므로 보존하되 숨겼다.

출입 트리거 근처에서 Match 종료를 강제로 발생시키면 기존 `SpaceshipEnter`가 종료된 위치 보고에 다시 접근하는 오류를 관측했다. UI 외의 이동/종료 경계 문제이며 이번에 권한 검사를 우회해 수정하지 않았다. 안정된 필드 위치의 동일 결과·재시작·복귀 검수는 통과했다. 검수 중 launcher를 비활성화한 채 Scene을 교체하면 persistent NetworkManager의 필수 consumer 검사에 걸리므로 해당 준비 환경을 전환 인수로 사용하지 않았다.

스크린샷·프레임 JSON·테스트 원본은 작업 환경의 ignored `Docs/Local/Validation/HudV6`에 보관했다. 코드/에셋이 존재한다는 사실, 자동 검사, 실제 Scene 관측과 사용자 최종 미감 승인은 서로 구분한다. 이번 작업으로 원격 Client·동시 Scene 이동·Standalone 빌드까지 재검증하지 않았다.

종료 시 Play를 끄고 Startup 편집 화면으로 돌아왔다. 검수 가상 입력과 캡처 callback을 해제하고, Game View를 원래 Free Aspect로 복구했다. 원래 없었던 HUD 크기 저장 키는 검수가 만든 85% 값임을 확인한 뒤 제거했으며, 다음 실행은 제품 기본값 85%를 사용한다. 런타임 날씨가 바꾼 머티리얼 값도 작업 전 값으로 복구했다. 기존 사용자 변경은 유지하며 커밋·푸시·PR은 만들지 않았다.
