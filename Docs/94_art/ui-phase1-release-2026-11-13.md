# UI 1차 작업 마감

2026-11-13 사용자가 현재 작업을 통과 처리하고 PR 생성과 main 병합을 요청했다. 이 기록은 해당 UI 전달 범위를 설명하며 전체 게임의 출시·온라인·빌드 인수를 뜻하지 않는다.

## 전달 범위

- 전투 HUD, Q/E 드래그 배정과 피드백, 피격·타이머·쿨타임·커서, 무기 한 장/번호와 오른쪽 숫자 탄약 표시.
- 해상도/HUD 크기 설정, 공통 버튼·패널·툴팁, 우주선 내부 Field HUD 숨김과 메뉴, 로딩 화면.
- 공통 텍스트 Alert 큐와 스킬 위 배치, 스킬·터렛 호출부, 좁은 Hub 귀환 보완 및 관련 테스트.
- Startup 배경/로고 보존, 로고 이후 버튼 등장, 자동 Start Game 강조 제거, `v0.1.0-alpha`, 저장된 별똥별/별 반짝임/로고 광택. 별똥별 대기 7~12초, 작은 별 발생 간격 0.6~1.35초.
- Main Credits 버튼과 별도 실시간 2D Credits Scene, 다섯 담당 파트의 연출·파티클·카메라·글자 전환, 마지막 로고와 정적 명단.
- Main Theme 1회 후 Main Ambient 반복, Credits 전용 OST 연결. 음원 원본과 기존 Credits 파트 시간은 유지한다.
- 편집 가능한 Scene/Prefab/Style 및 제작 도구와 공통 시각 규칙.

## 통합 방법

원본 `feature/core`의 미커밋 작업과 Unity 상태를 보존하기 위해 별도 `codex/ui-phase1` worktree에서 최신 main `79379c5ee`에 UI 변경을 3-way 적용했다. Mulmaru의 atlas와 글리프/빈 rect는 함께 유지하고, Build Settings에는 main의 Stage1과 이번 Credits를 모두 남겼다.

`Initial_Stage`에서는 HUD Canvas의 additional shader channels override 한 곳만 옮겼다. 로컬 적/네트워크 프리팹 변경, 맵·날씨·렌더링 관련 Scene 변경과 ThinMulmaru의 실행 중 글리프 캐시는 이번 PR에 포함하지 않는다. 해당 파일은 원래 작업 폴더에 보존한다.

## 검증과 한계

기존 실제 Unity 검수 기록은 [최초 HUD/공통 화면](ui-validation-2026-11-12.md), [후속 HUD 수정](ui-review-2026-11-13.md), [피드백](ui-feedback-review-2026-11-13.md), [포인터](ui-pointer-review-2026-11-13.md), [무기/탄약](ui-ammo-dock-review-2026-11-13.md), [Credits](credits-cinematic-validation.md), [Startup](startup-atmosphere.md)에 있다. 자동 입력/API 확인과 OS 마우스·키보드 관측은 각 기록에서 구분한다.

마지막 숫자 탄약 버전은 사격/재장전 정상 141프레임, 15→14→13→12→15 및 발사 피드백/재장전 44개 회전 각도, Sniper 5발과 Spanner 무한 표시를 확인했다. 해당 마지막 단계의 OS 입력·스킬 picker는 재검수하지 않았다. Startup 자동 강조 수정은 Unity 재컴파일 오류/경고 0, 실제 재생의 무선택·기본 색상을 확인했다. 실제 게임패드 검증을 뜻하지 않는다.

별똥별 최종 빈도는 정상 재생 48초에 서로 다른 네 구역에서 네 번, 보호 UI bounds 겹침 0을 확인했다. 24초 실제 Game View 검토 영상은 무음이다. OST 전곡 경계의 실제 청음 검증과는 별개다.

PR 준비 worktree의 저장소 Python 검사 50개와 전체 Assets meta 검사(누락 0)를 통과했다. 활성 Build Scene 7개의 파일/meta GUID와 포함 파일의 충돌 표시·새 Asset meta 누락 0도 확인했다. 원본에서 수행한 Unity 컴파일·기능 검수와 최신 main을 적용한 worktree의 검증을 혼동하지 않는다. 새 worktree에서 Unity 전체 재가져오기·전체 EditMode·Standalone 빌드·원격 Client·모든 화면비 검증은 수행하지 않았다. 필수 GitHub Repository checks 결과는 PR에서 확인한다.

원본 캡처·영상·JSON·실패 기록은 ignored `Docs/Local/Validation`에 보존한다. PR에는 대표 실제 화면 일부만 함께 전달한다.
