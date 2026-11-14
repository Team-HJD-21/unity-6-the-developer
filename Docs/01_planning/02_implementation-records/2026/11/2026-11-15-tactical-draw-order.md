# 지도 표식과 HUD 표시 순서

사용자 확인: 터렛 등 지도 표식이 HUD 위에 겹친다. 표식은 HUD 아래에 그려야 한다.

## 변경

- 기존 전술 프리팹의 Canvas 제작 순서는600이었고 전투 HUD는20/30이다. 전술 View는 체력 HUD의 자식으로 생성되므로 정렬 상속도 명시해야 한다. `TacticalHudView.Awake`에서 `overrideSorting=true`, 순서10을 적용한다.
- 월드 터렛/적/Player/CU 아이콘, 사거리·체력·수량과 지도 패널 모두 스킬·상단 정보·체력 HUD 아래에 놓인다. 지도 안에서는 기존 패널/버튼이 월드 표식보다 위에 그려지고 raycast도 유지한다.
- 전환막의 독립 Canvas800/-900과 여백 Canvas-1000, 공용 메뉴/Alert의 별도 Canvas는 변경하지 않는다. 기존 HUD 숨김 scope도 유지한다.
- `TacticalHudSetup`의 제작 기본값도 같은 상수를 사용하도록 코드만 수정했다. 제작 메뉴는 실행하지 않았고 Scene/Prefab 파일은 수정·재저장하지 않는다. Stage_1은 팀원 제작 중이므로 그 파일의 이전 변경을 되살리지 않는다.

## 검증

`MapMarkersStayBelowCombatHudAndDoNotStealItsClicks`는 실제 Stage_1 정상 필드 진입 뒤 런타임 Canvas 순서, 지도 표식들의 순서, 체력바·지도 닫기 버튼의 실제 EventSystem raycast 우선순위를 검사한다. 직접 마우스 입력 검수와 구분한다. Scene을 정상 Play에서 읽지만 Scene/Prefab 제작·저장 API는 사용하지 않는다.

결과와 검수 산출물은 Git 제외 `Docs/Local/Validation/TacticalDrawOrder20261115`에 기록한다.

- Unity6000.3.23f1 컴파일 오류 없음. 새 표시 순서/실제 raycast 검사1/1과 기존 전환·연속 반전·메뉴 취소 검사1/1을 각각 통과했다. 전체 프로젝트/Standalone/물리 마우스 검수를 의미하지 않는다.
- 정상 Play에서 기존 터렛 glyph 풀의 두 이미지를 검수용으로48px 확대하여 상단 타이머와 체력바에 겹쳐 놓고 Canvas600/10 렌더를 대조했다. 순서10에서는 시간 문구와 체력바가 아이콘 앞에 그려졌다. 표식 위치를 매 프레임 갱신하는 WorldGraphic은 이 대조 동안에만 런타임 비활성화했다가 복구했다. 첫 대조는 매 프레임 비활성화되는 풀 이미지 때문에 강제 겹침이 보이지 않아 증거로 사용하지 않았다. 새 에셋이나 제품 배치를 만든 검수가 아니다.
- 런타임 정렬값은 지도10/overrideSorting=true, 상단 HUD20, 체력 HUD30이었다. Play 종료·Startup dirty=false로 복원했으며 Stage_1 blob은 수정 전후 모두 `d293f49d21887f1edbab6f2906616a2c86a5f7f8`이다. Scene/Prefab 파일 diff0건을 확인했다. 기존 Font atlas와 staged Python cache는 보존했다.

사용자의 후속 전체 업로드 승인에 따라 위 코드·테스트·규칙·구현 기록과 현재 `CombatHudBodySDF.asset`의 동적 glyph/atlas 직렬화를 함께 커밋한다. Font 원본 GUID와 동적 생성 모드는 유지한다. 자동 생성 Python cache8개는 업로드에서 제외하고 기존 스테이징 상태를 보존한다. 이번 커밋은 Scene/Prefab 변경을 추가하지 않는다.
