# 전술 전환 중 HUD 숨김과 SmoothStep

사용자 요청: 지도↔필드 전환에서는 모든 HUD를 숨기고 파동을 부드럽게 가감속한다. 앞선 전장 진입 중 지도 차단·종료 방향 반전도 유지한다. 사용자는 마지막에 이번 작업 외의 현재 소스·에셋·문서 수정도 모두 GitHub에 올리도록 승인했다. 자동 생성 Python 캐시는 제외한다.

## 구현과 검토 순서

1. `TacticalModeEffect.SetOpen`이 `HudPresentationVisibility.Suppress()`의 표시 전용 scope를 얻는다. 정상 종료·Reset·비활성화·파괴에서 해제하고 연속 반전은 같은 scope를 유지한다. 여러 scope 중 하나를 정리해도 다른 화면의 숨김을 풀지 않는다. Domain Reload가 꺼진 새 Play도 정적 scope를 비운다.
2. `PlayerSkillHud`의 Canvas/GraphicRaycaster와 `AreCombatControlsVisible`, `CombatVitalsGroup`의 alpha/자물쇠/상세, `AlertStripView`, `CombatExperiencePopup`, `CombatFieldCursor`, 디버그 메뉴가 이 표시 상태를 읽는다. `IsFieldVisible`이나 Player/CU 원본·알림 큐·수명·입력 lease를 덮지 않는다. 기존 고정 체력바도 종료 후 그대로 복구된다.
3. `TacticalHudView`는 클릭이 Controller.Update 뒤에 온 첫 프레임에도 지도 버튼·목표·지역 상태·호버/프롬프트를 숨긴다. `RefreshFieldStatus`의 기존 퇴장 fade 잔상도 전환 중에는 즉시 0으로 처리한다. `FieldTerritoryRenderer`의 필드 경계 띠도 숨긴다. 별도 전환 Canvas 자체는 유지한다.
4. Cover/Reveal/Progress 보간은 5차 SmoothStep(`6t⁵−15t⁴+10t³`)을 사용한다. 기존에도 기본 3차 SmoothStep이 있었으며 선형 코드였다고 기록하지 않는다. 진입0.12/0.06/0.42초와 종료0.30/0.06/0.12초 및 반대 파동 방향은 그대로다.

추가 래스터·프리팹·Scene 제작은 필요하지 않다. 기존 투명 육각 셰이더와 Inspector 시간 설정을 사용한다. 공용 scope는 Core Presentation에 위치하며 Application/Domain에 전술 UI 의존성을 추가하지 않는다.

## 검증

검수 산출물: Git 제외 `Docs/Local/Validation/TacticalHudSuppression20261115`. 자동 입력·직접 API 호출·OS 마우스 입력·실제 Scene 렌더를 구분해 최종 결과를 기록한다.

- Unity6000.3.23f1에서 실제 새 코드/테스트 반영과 `scriptCompilationFailed=false`를 확인했다. 보간·scope·기존 전술 표현 검사10/10 통과.
- 실제 Stage_1 검사13개는 전체 실행12/13, 새 HUD 검사에서 EditMode coroutine이 LateUpdate 표시 반영 전에 재개되는 문제를 수정한 후 해당 검사1/1로 통과했다. 따라서 고유13개 통과를 분할 실행으로 확인했으며 단일 전체13/13이라고 보고하지 않는다. 진입 중 M/버튼/직접 호출 차단, 역방향 종료·연속 반전·실제 메뉴 lease 취소, 터렛/핑/사거리/카메라/기존 스킬 드래그 경로가 포함된다. 제품 표시 조건은 실패를 피하도록 완화하지 않았다.
- OS 마우스로 지도 열기·닫기를 실행했다. 최종 정상 렌더92개 표본 중 전환55개에서 스킬 Canvas·체력/자물쇠·알림·활성 XP·필드 상태/목표/호버/버튼·조준선·지도 Command 노출0건이다. 진입/종료 중간 Game View 이미지를 직접 확인했으며 배경과 육각 파동만 보였다. 정상 완료 뒤 해당 HUD 복구도 확인했다. 물리 키보드/드래그의 추가 인수는 수행하지 않았고 기존 자동 검사와 구분한다.
- 첫 native 계측은 비활성 pooled XP의 남은 내부 alpha까지 노출로 집계했다. 최종 계측은 activeInHierarchy와 alpha를 함께 기록했다. 비활성 객체는 렌더되지 않으며 제품 코드를 이 오탐 때문에 변경하지 않았다. QA용 장시간 Alert·반복 XP·피해 보호는 런타임에만 적용했고 Play 종료로 제거했다.
- Python 저장소 검사50/50, 전체 Unity `.meta` 누락0건. 변경된 동적 TMP Font8개는 모두 원본 Font/재질 연결이 유효하고 shader 오류가 없었다. NetworkMatchAnchor/PartyMushroomHealOrb 프리팹의 missing script0건과 2D Renderer asset 로드도 확인했다. Standalone 빌드·실제 원격 멀티플레이 검증은 이번 실행에 포함하지 않았다.
- Play 종료 후 Startup·Scene dirty=false로 복원했다. 새 Scene/Prefab/Material은 저장하지 않았다. 사용자 승인에 따라 기존 동적 Font atlas/table 직렬화, NetworkObject hash/프리팹 플래그, 2D Renderer의 기존 post-process 참조 제거까지 이번 소스·문서와 함께 업로드 범위에 포함한다. 해당 변경을 이번 HUD 코드가 만든 것으로 설명하지 않는다. 기존 staged Python cache8개는 제외하고 보존한다.
