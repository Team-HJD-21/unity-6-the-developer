# 전술 UI 후속 PR 검토 지도

## 반영 범위

최신 main `b1adbacaf`를 부모로 `8bcd0693b`와 `c5e354a07`의 UI 코드·테스트·폰트·렌더 설정·관련 문서만 추출했다. head는 `codex/ui-tactical-transitions`, base는 `main`이다. 다른 세션의 Encounter 런타임 분리(PR #728)와 진행 중인 네트워크/Enemy 변경은 포함하지 않는다.

공유 feature/core에 이미 업로드된 NetworkMatchAnchor/PartyMushroomHealOrb 프리팹 변경은 이번 PR에서 제외한다. 모든 Scene/Prefab 및 해당 meta는 main 그대로 유지한다. 앞선 구현 기록의 전체 업로드 범위와 이번 PR 반영 범위를 구분한다. 기존 Stage_1 원복을 되살리지 않으며 팀원의 EnemyTest Scene/Prefab 변경도 되돌리지 않는다.

## 먼저 볼 파일과 실행 흐름

1. `TacticalCommandController`: 첫 필드 진입 문구/스캔 완료 전 지도 버튼·M·직접 Open 호출을 함께 막는다.
2. `TacticalModeEffect` → `HudPresentationVisibility`: 진입은 중앙에서 바깥, 종료는 가장자리에서 중앙의 역방향 파동이다. 기존 구간 시간에 5차 SmoothStep을 사용하며 전체 전환 동안 표시 scope로 HUD를 숨긴다. 완료·취소·비활성화·Scene 해제에서 scope를 해제한다.
3. `PlayerSkillHud`, `CombatVitalsGroup`, `AlertStripView`, `CombatExperiencePopup`, 커서/필드 상태·경계가 표시 scope를 읽는다. Player/CU 수치·알림 수명·게임 시간·입력 권한 원본을 바꾸지 않는다.
4. `TacticalHudView.Awake`: 정렬 상속을 끄고 지도 Canvas10을 지정하여 상단/스킬20·체력30 아래에 표식과 지도 패널을 놓는다. 전환막800/-900과 지도 안 버튼의 기존 순서는 유지한다. 제작 메뉴는 실행하지 않았으며 편집기 제작 기본값 코드만 같은 상수를 참조한다.

Font8개의 동적 atlas/table 직렬화는 코드 흐름 검토에서 생략할 수 있다. Font 원본과 동적 모드는 유지하며 Shader 오류 없음/로드를 확인했다. `TheDeveloper2DRenderer.asset`은 이전 로컬 post-process 참조 제거를 포함한다. 제작된 새 이미지나 Scene/Prefab 재생성은 없다.

## 검증 근거와 한계

공유 검수 브랜치에서 Unity6000.3.23f1 컴파일 오류 없음, 표현/보간/scope 검사10/10을 확인했다. Stage 검사13개는 전체12/13 뒤 표시 반영을 기다리도록 테스트 대기를 고친 1개를 개별1/1로 재검사하여 고유13개 통과를 확인했다. 이후 표시 순서/체력바·닫기 버튼 EventSystem raycast 1/1 및 전환·메뉴 취소1/1을 확인했다. 단일 전체 실행 통과나 새 main 분리 스냅샷의 별도 Unity 전체 실행으로 보고하지 않는다.

정상 Play의 OS 마우스 열기/닫기, 전환55개 렌더 표본의 HUD 노출0건, 상단/체력바 강제 겹침 Canvas600/10 화면 대조를 수행했다. 런타임 QA 배치/피해 보호/Alert/XP는 Play 종료로 제거했다. Python50/50과 meta 누락0건을 확인했다. Standalone/실제 원격 멀티플레이 인수는 미실행이다.

구현별 상세 기록은 2026-11-15 tactical-entry-exit/tactical-transition-hud/tactical-draw-order 문서를 따른다. CI 결과와 원격 changed-files의 Scene/Prefab0건 여부는 PR 본문에 별도로 기록한다.
