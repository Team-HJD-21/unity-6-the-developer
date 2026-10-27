# 플레이어 장비 충돌과 표시 순서

작성: 이영빈 작업 지원 · 2026-10-27

## 본체와 에너지 막

`EnergyBarrier.prefab`의 First/Second Emitter와 `LureBeacon.prefab`의 Beacon Body에
PolygonCollider2D를 미리 배치한다. 지면과 닿는 하단 받침을 기준으로 작성했으며, 위로 솟은
기둥·안테나 전체를 이동 장애물로 사용하지 않는다. Prefab Mode에서 Edit Collider로 조정한다.

본체는 Player·Enemy 레이어를 포함하고 나머지를 제외한다. 탄환 피격용 PlayerBulletCollider와
장비 본체의 물리 충돌은 별개다. 새 탄환 피해 API는 추가하지 않는다.

방벽의 `_blocker`는 프리팹 루트에 미리 배치한 BoxCollider2D다. 초기화에서는 두 설치점 사이의
길이·폭을 적용하고 활성화한다. 기존처럼 Enemy만 막으며 플레이어와 아군 탄환은 통과한다.
본체 콜라이더와 막 콜라이더를 실행 중 AddComponent로 생성하지 않는다.

2026-10-28에는 `bomb.prefab`에도 본체 하단의 PolygonCollider2D와 동일한 초기 탈출 처리를
추가했다. 시한폭탄 본체도 Player·Enemy만 막으며 Rigidbody2D는 Static으로 고정한다.
`PlayerBombExplosion`은 점화 시 본체 충돌을 켜고 초기 겹침을 처리하며, 폭발하는 순간
Collider와 탈출 처리 컴포넌트를 끈다. 0.8초 폭발 연출이 남아 있어도 본체는 길을 막지 않는다.
폭발 피해 반경 9, 점화 시간 2.1초와 피해량은 기존 값을 유지한다.

## 설치 순간 겹침

두 스킬 모두 플레이어 위치를 사용하므로 본체에 처음부터 겹칠 수 있다.
`PlayerFieldBodyCollision.AllowInitialExit()`는 위치 확정 후 최초로 겹친 플레이어만 기록하고
본체와의 충돌을 잠시 무시한다. 한 번 빠져나오면 충돌을 복구하고 다시 들어갈 수 없게 한다.
새로 접근한 플레이어·몬스터에게는 이 예외를 주지 않는다.

기록한 플레이어의 비활성 방향 콜라이더도 참조로 보관한다. 방향 전환으로 켜진 몸 콜라이더는
다음 물리 시뮬레이션 전에 무시 설정을 다시 적용한다. Trigger 피격 영역은 이 처리에 포함하지 않는다.
장비 비활성화 시 남은 예외를 정리한다. 본체와 막은 장비 자식으로 함께 비활성화·제거된다.

`ApproachPoint`는 본체 또는 막에서 가까운 접점을 돌려주므로 기존 장비 접근·공격 코드가
단단한 본체의 중심까지 이동하려 하지 않는다. Enemy 내부 코드는 변경하지 않는다.

## 플레이어와 앞뒤 표시

`PlayerSkillDepth`는 설치한 플레이어의 활성 발밑 캡슐 중심과 장비의 접지 Collider 중심 Y를 비교한다.
플레이어가 아래면 장비 SortingGroup을 플레이어보다 한 단계 뒤로, 위면 한 단계 앞으로 표시한다.
Transform Z·Rigidbody·충돌 크기는 바꾸지 않는다.

- 방벽: 양끝 장치를 Sort At Root 그룹으로 분리해 각 접지점에서 판단한다. 막·선 효과는 루트 그룹이다.
- 비콘: 본체와 효과를 한 그룹으로 표시한다.
- 플레이어 포탑: 바닥과 회전 헤드를 그룹으로 묶어 내부의 앞뒤 관계를 유지한다.

`PlayerFieldDevice.Initialize(..., owner)`와 `PlayerTurret.AssignOwner`가 표시 기준 플레이어를 연결한다.
이 변경은 현재 설치자와 장비의 상대 표시를 조정한다. 여러 플레이어·맵 소품·몬스터까지 포함한
공통 월드 Y 정렬이나 네트워크 상태 동기화를 구현한 것은 아니다.

## 포탑 조작창과 하단 HUD

`InitialStagePlayerHUD.IsTurretPanelOpen`은 실제 포탑 View가 열렸는지 제공한다.
기존 PlayerSkillHUD 프리팹에 배치한 `PlayerCombatHudLayout`이 같은 씬의 Stage HUD를 찾아
Weapon Hotbar와 Dash Slot의 RectTransform을 이동시킨다. 실행 중 UI 오브젝트를 생성하지 않는다.

Inspector의 Visible Edge는 남길 가장자리 높이(기본 10 Canvas 단위), Slide Seconds는 이동 시간
(기본 0.16초)이다. 각 RectTransform 높이·Pivot으로 접힌 위치를 계산하고, unscaledDeltaTime을
사용한다. 무기 선택 CanvasGroup은 접힌 동안 클릭을 받지 않는다. 닫힘·선택 해제·HUD 비활성화
시 원래 위치로 복귀하며 Q/E 슬롯 위치는 바꾸지 않는다.

## 검증

- 컴파일 및 프리팹 fileID·컴포넌트 참조·레이어 마스크를 검사한다.
- `PlayerWeaponMigrationTests`는 8방향의 활성 쌍과 수동 위치 보존을 검사한다.
- `PlayerDeviceCollisionTests`는 본체 설정, 초기 탈출·방향 전환·재진입 차단, 접지 정렬,
  방벽 끝점별 정렬, HUD 접힘·복귀를 검사한다.
- 이번 작업에서는 Unity 테스트를 실행하지 않았다. 에디터에서 장비 설치 직후 탈출,
  몸 방향을 바꾸며 벽/몬스터 접촉, 장비 위아래 이동, 포탑 선택·닫기·Esc를 직접 확인해야 한다.
