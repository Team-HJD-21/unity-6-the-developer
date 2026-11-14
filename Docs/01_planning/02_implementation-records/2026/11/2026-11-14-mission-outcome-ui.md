# 승패·안전장치 UI와 첫 전장 진입 연출

2026-11-14 구현/검수 기록. 사용자 요청 4건을 반영한 작업이며 최종 시각 인수·전체 협동 검수와 구분한다.

## 구현과 진입점

- `AppScreenPresenter` / `AppPanelView`: 승리·패배 결과, 실제 패배 이유, 안전장치 선택·남은 수, Pause 메인 복귀를 공용 화면 수명과 입력 lease에 연결한다.
- `MatchRecovery` → `InitialStageCompositionRoot.Recovery` → `AppRoot.Recovery`: 사망 실패 인식은 선택 대기이며 차감하지 않는다. `CanUseMissionSafety`/`UseMissionSafetyAsync`에서 최초 확정하고 기존 복귀를 수행한다. `CanRetryMissionRecovery`/`RetryPendingMissionRecoveryAsync`는 이미 차감한 같은 실패의 기술 재시도다. 세 번째 사용도 허용하고 그다음 실패는 패배 처리한다.
- `MatchCoordinator` → `MatchSession` → 불변 `MatchResult.EndReason`: CU 파괴·전멸·구조 시간 소진의 실제 사유를 결과에 보존한다. 승리 UI는 기존 명시 Match 완료를 사용하며 새 승리 판정 조건을 추가하지 않았다.
- `CombatHudIntro` / `FieldEntryAnnouncement`: 중앙 문구 0.22초 등장 → **3초 유지** → 0.45초 위에서 아래로 스캔 소거 → HUD 0.6초 스캔 등장. 두 HUD가 Scene별 같은 시작 시각을 공유한다. 문구는 Skill HUD가 한 Canvas만 소유하고 입력·게임 시간을 막지 않는다. 같은 Scene의 우주선 왕복은 반복하지 않으며 Scene 재시작은 새 연출이다.

## 디자인·에셋과 편집

기존 Common Panel/Button의 얇은 모서리, 몰마루 제목, 기존 BodyFont와 금색/Cancel 색을 재사용했다. 새 래스터 그림 대신 기존 프레임과 편집 가능한 TMP/UGUI 계층을 사용한다. 승패의 가는 구분선 외에 추가 인디케이터나 조작 설명을 넣지 않았다. 남은 안전장치 수와 실제 실패 이유만 표시한다.

- `Assets/Prefabs/UI/Common/MenuScreen.prefab`: 메인 복귀 마지막 행. 허브에서는 우주선 귀환 행/간격을 함께 줄인다.
- `ResultScreen.prefab`: 승패 공통 720×440, 몰마루 제목과 이유, 처음부터/우주선 버튼.
- `RecoveryScreen.prefab`: 720×500, 전투 불능·실제 사유·남은 수/3, 사용/처음부터. ESC로 선택 대기를 닫지 않는다. 기술 오류에는 같은 주 버튼이 복귀 재시도로 바뀐다.
- `FieldEntryAnnouncement.prefab`: 1920×1080 reference Canvas, 중앙 문구와 스캔 영역. `FieldEntryLettering.mat`는 기존 몰마루 atlas를 참조하는 외곽선 재질이다.
- `HudBootReveal.shader`: 기존 stencil 스캔에 로컬 UV 선 두께를 추가했다. 작은 문구 영역에서도 약1~2px 선을 읽을 수 있게 하며 기존 전체 HUD 두께는 유지한다. 글자/이미지를 같은 경계로 자른다.
- `CommonUiPrefabSetup.ConfigureMissionScreens()` 또는 **Tools → TeamHJD → The Developer → Common UI → Update mission screens**가 해당 화면만 제작한다. 전체 Startup/Settings를 재생성하지 않는다. 로딩의 오래된 G 안내도 현재 F로 수정했다.

런타임은 제작된 프리팹을 인스턴스화하고 직렬화 참조로 갱신한다. 크기·글꼴·색·버튼·진입 유지/소거 시간은 에디터에서 수정할 수 있다. Unity 저장이 만든 YAML 문서 순서/공백 노이즈는 정리하되 모든 현재 컴포넌트 본문·fileID·참조가 동일함을 비교했다. 기존 다른 작업의 필드와 변경을 보존했다.

## 검수

자동 검사는 분할 실행한 고유 **23개 통과**다. 단일 전체 프로젝트 테스트/빌드 통과를 뜻하지 않는다.

| 검사 | 통과 |
|---|---:|
| MatchRecoveryTests: 사용 확정 전0회, 중복 확정/기술 재시도,3회 허용/다음 실패, 구조 경계 | 12 |
| MissionRecoveryPersistence/Entry: 기록·과거 비동기 복귀 취소 | 4 |
| MissionUiPresentationTests: 선택 Back 차단·입력 lease, 글자/버튼 간격, 단일 문구 참조 | 3 |
| Completed 사유 codec: 새 사유·옛 payload·잘린/잘못된 enum | 1 |
| CombatHudPresentationTests: 기존 스킬 호버/배치/취소·무기/탄창 | 3 |

실제 Unity6000.3.23f1/DX11에서 직접 Stage 실행과 Startup→Start Game→우주선 출격 서비스를 통한 Stage 진입을 따로 확인했다. 실제 PNG는1692×952, reference1920×1080이다.

- 실제 `PlayerInfo.ApplyDamage`로 사망을 유발하고 **OS 마우스로** 안전장치 사용: 선택 전3/3·사용0 → 같은 실행 ID·사용1·남은2/3·HP400·입력 해제. 피해 유발은 QA 호출이며 적에게 자연사한 장면 검수라고 쓰지 않는다.
- 두 번째 선택에서 OS 마우스로 처음부터: 새 실행 ID·사용0·남은3/3. 실제 결과 화면에서도 재시작을 확인했다.
- 명시 `CompleteMatch(Victory)`의 승리 UI·우주선 버튼과 실제 `ControlUnitHealth.ApplyDamage` CU 파괴의 패배 사유를 확인했다. 자연 플레이의 최종 보스 클리어 검수는 아니다.
- 누락 저장 링크를 QA 조건으로 설정한 뒤 OS 마우스로 사용/같은 복귀 재시도: 사용1·남은2/3·같은 pending 실패·오류/정지 유지, 추가 차감0, 초기 전장 성공으로 대체 없음. 명시 처음부터 뒤에는 새 실행·3/3·오류 없음.
- 우주선과 필드에서 메인 복귀 버튼을 OS 마우스로 클릭했다. Startup 도착 뒤 Screen 없음·로컬 입력 잠금 없음·옛 복귀 오류 없음. 필드4행/허브3행의 간격과 실제 화면을 확인했다.
- 정상 프레임 연속 캡처로 중앙 문구·소거선·하단부터 켜지는 HUD를 비교했다. 초안에서 문구 소거선이 너무 얇았던 부분과 배경 위 글자 가독성을 보강한 최종본을 다시 캡처했다.

최종 출격의2,670개 Editor update 관측 표본에서 문구/HUD 동시 표시0건, 두 HUD의 Progress 최대 차이0이었다. 같은 게임 프레임의 중복 관측도 포함하므로2,670개의 서로 다른 렌더 프레임을 뜻하지 않는다. 캡처11장은 정상 Game View 렌더를 기록했다. 전체 Assets `.meta` 검사0오류와 scoped diff 검사를 확인했다.

OS Escape는 이 컴퓨터 유즈 경로의 기존 입력 전달 제한으로 메뉴 열림을 확인하지 못했다. 메뉴 열기는 정식 `Screens.ShowMenu()`로 재현하고 버튼 클릭은 OS 입력으로 확인했다. 물리 키보드·누른 채 드래그를 통과했다고 보고하지 않으며 OS 입력 설정은 바꾸지 않았다. 의도한 누락 저장 로그 때문에 Unity Error Pause가 걸린 때에는 에디터 재생만 재개했고 기존 Error Pause 설정을 보존했다.

원본 결과/캡처는 Git 제외 `Docs/Local/Validation/MatchUi20261114`에 있다. 구현·검수 후 Play를 종료하고 Startup의 미저장 변경이 없는지 확인한다. 기존 staged 파일과 다른 파트의 변경을 보존하며 이번 요청에서 커밋·푸시는 수행하지 않는다.

## 남은 경계

실제2인 Player 복제·원격 안전장치 선택 RPC/남은 수 Snapshot은 기존 Core에 없으며 이번에는 Host 선택을 구현/검수했다. 생존 동료가 있는 개인 다운을 팀 실패로 바꾸지 않는다. 전체 checkpoint 복원은 필수 `world/weather` writer 미연결과 구분한다. 새 codec은 옛 사유 없는 완료를 읽지만 구형 reader가 새 tail을 읽는 양방향 호환은 아니므로 협동에는 같은 빌드를 사용한다. Standalone 빌드·모든 화면비/해상도의 수동 인수는 이번 검수 범위가 아니다.
