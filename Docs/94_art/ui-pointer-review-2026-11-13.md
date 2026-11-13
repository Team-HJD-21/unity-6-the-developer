# 무기명·필드 커서 후속

2026-11-13 사용자 요청의 구현과 컴퓨터 유즈 검수 기록이다. 앞선 [UI 리뷰](ui-review-2026-11-13.md)의 가상 Mouse/Keyboard 검사와 이번 OS 입력 검수를 구분한다.

## 구현

- 무기 카드 아래에 기존 Panel 바탕·한 겹 테두리·NEXON 본문을 재사용했다. 교체 후 1.4초, 무기/번호 호버 중에는 해당 이름을 표시한다. 0.18초 smoothstep으로 나타나고 사라지며, 전환 반전은 현재 알파에서 이어간다. 이름·유지/전환 시간은 `CombatWeaponStack` Inspector에서 편집한다.
- 기존 `Assets/Art/UI/Cursors/target_a.png`의 작은 흰색 픽셀 조준선을 선택했다. 새 bitmap 생성이나 원본/import 변경은 없다. `Common/CombatCursor.prefab`의 별도 Overlay Canvas로 32px를 유지하며, 작은 어두운 그림자로 밝은 바닥에서도 구분한다. GraphicRaycaster 없이 모든 그림의 raycastTarget을 껐다.
- `CombatFieldCursor`는 기존 로컬 HUD actor·필드 위치·게임 입력 잠금·스킬 선택/설치·UI raycast와 주변 10px를 읽는다. 상단 정보와 무기명도 일반 포인터 영역이다. 메뉴는 화면에서 실제로 사라질 때까지 일반 포인터를 유지한다. 조작 권한·사격 성공 판정은 변경하지 않는다.
- `PlayerAttack.ShotFired`는 실제 탄환 생성에 성공한 한 번의 ranged 발사 후 발생한다. 산탄 pellet별로 발생하지 않으며, 입력 차단·빈 탄창·재사용 대기·생성 실패·Spanner 공격은 발사로 표시하지 않는다. 커서는 성공 이벤트에만 0.16초의 작은 확장과 절제된 금색 반응을 준다. owner 교체/비활성/파괴 시 구독과 커서를 정리한다.
- `HudPointerReviewSetup`은 무기명·커서만 프리팹에 연결하며, 필요한 한글은 HUD 전용 본문 SDF에 미리 준비한다. 기존 제작 도구의 재생성 경로에도 연결했다. 사용자 원본 NEXON SDF는 수정하지 않는다.

## 실제 OS 입력에서 관측한 범위

Windows 컴퓨터 유즈의 `@oai/sky`와 동일한 Windows backend를 사용하는 `cua_repl`로 Unity 창을 직접 조작했다. 키·마우스를 InputSystem.QueueStateEvent로 주입하지 않았다. 상태 확인·GameFlow/출구/메뉴의 검수 상태 준비와 촬영에는 Unity CLI를 사용했다.

| 항목 | 관측 |
| --- | --- |
| Main | 실제 Play/Start/Settings 클릭, Settings 호버 강조, 버전 유지 확인 |
| Settings | 마우스 슬라이더 조작으로 85→103%, 취소 버튼으로 복원. 필드에서도 85→104% 미리보기·취소·85% 복귀 확인 |
| 무기 | 번호 클릭으로 Sniper, 휠로 Spanner, 번호로 DefaultGun 전환. 한 장·번호·회전·아래 이름과 호버 유지 확인 |
| 스킬 호버 | 실제 마우스로 반원을 열고 PlayerTurret 설명·Hover 강조 확인 |
| 사격/커서 | 실제 필드 클릭으로 탄약 15→14→13과 산탄 생성. native-shot-observed의 실제 연속 프레임에서 Pulse 1→.88→.59→.28→.04→0 확인 |
| 메뉴 버튼 | 준비된 필드 메뉴에서 Settings·Cancel·Continue 실제 클릭. 메뉴에서는 조준선 대신 일반 포인터, UI 클릭 중 탄약 유지 확인 |
| 우주선 | 방문 내부에서 HUD/조준선 숨김과 일반 포인터 복원 확인 |

hover 위치 이동에는 backend에 별도 move API가 없어 게임에서 사용하지 않는 가운데 버튼 클릭을 사용했다. 입력장치는 native Mouse/Keyboard만 연결된 상태였다. 긴 검수용 적 정리는 해당 Play 세션의 정상 서버 Despawn 경로만 사용했고 제품 Scene에 저장하지 않았다. 출입구 자동 재진입을 피하기 위한 actor 위치 변경도 검수 상태 준비다.

## 완료로 표시하지 않은 실제 입력

**실제 키보드와 스킬 장착 드래그는 이번 컴퓨터 유즈 경로에서 통과하지 않았다.**

- `Escape`·숫자키 등 컴퓨터 유즈 입력에 대해 Unity native Keyboard의 관측 구간에는 TEXT 이벤트만 들어오고 키 상태 이벤트가 확인되지 않았다. Game View 포커스와 Play 진행을 대조했고, 두 공개 컴퓨터 유즈 경로에서 같은 현상을 확인했다. backend 내부 원인은 확정하지 않았다.
- 실제 drag의 Mouse trace는 Dynamic frame 79252에서 `(650,162)` 왼쪽 누름과 약 1ms 뒤 `(1089,606)` 이동을 처리했고, 해제는 frame 79254였다. EventSystem이 누름 프레임에 이미 목적지 위치를 읽어 시작 스킬을 잡지 못했으며 장착은 None/None을 유지했다. 공개 Windows API에는 누름/이동을 분리하거나 drag 시간을 조정하는 기능이 없어 추가 반복으로 성공을 주장하지 않았다.
- 이 제한을 맞추려고 제품 입력 판정을 바꾸지 않았다. 앞선 정상 프레임 가상 입력 회귀는 별도 근거이며 실제 키보드·held drag 검수를 대체하지 않는다. OS 입력의 이 두 항목은 다른 입력 backend 또는 실제 사람이 확인할 수 있는 환경에서 남은 인수 항목이다.

진단 중 최초 읽기 콜백이 TEXT 이벤트를 state로 읽어 낸 예외와 Editor Error Pause는 검수 코드 오류였다. state/delta-state만 읽도록 정정하고 콜백을 종료했다. 제품 오류로 분류하지 않는다.

## 최종 검사와 보관

- 최종 관련 자동 회귀는 `CombatHudPresentationTests` 2개·`PlayerWeaponHudTests` 2개, 합계 4/4 통과했다. 무기 계층 검사는 TMP가 새 atlas에 자체 생성하는 SubMeshUI를 제외하고 제작된 오브젝트의 identity 집합이 유지되는지 비교한다. 커서가 UI raycast를 가로채지 않는지도 검사한다.
- 실제 무기명 기록 147프레임에서 alpha .08→.27→.55→.82→.98→1과 호버 이탈 후 0을 확인했다. 최종 메뉴 닫기 147프레임 중 Panel alpha>.001인데 조준선이 켜진 프레임은 0이었다. 발사 커서 기록은 146프레임이다. 촬영 시작은 실제 native Mouse 누름을 읽어 결정했으며 입력은 생성하지 않았다.
- 컴파일 오류/경고 0, 새 meta 누락·GUID 중복·대상 프리팹 Missing Script 0, `git diff --check` 통과. 원래 메인 배경/로고 직렬화 블록과 사용자 NEXON 파일 해시를 보존했다. 진단 코드 오류 이후 새 오류 로그는 없었다.
- 검수 종료 후 Startup 편집 모드·Scene 수정 없음·원래 Free Aspect를 확인했다. 일반 커서는 visible이며 runtime 커서 인스턴스는 0이었다. HUD 설정 키는 원래의 부재 상태이고 Play 세션의 이동·적 정리·콜백은 남기지 않았다. 부수적인 ThinMulmaru 캐시만 정리했다. 커밋/푸시/PR은 하지 않았다.

실제 화면·GIF·원본 PNG/JSON은 로컬 `Docs/Local/Validation/HudV6` 검수 자료에 보관하며, 공유용 로컬 뷰어는 `hud-pointer-review-20261113` 아티팩트로 제공한다. 실패한 native drag와 녹화 시작이 늦었던 초기 무기 교체 기록을 성공 영상으로 사용하지 않았다.
