# 검증과 제한 사항

최종 갱신: 2026-10-26
상태: 통합 시 확인할 항목. 체크리스트 자체가 테스트 통과 기록은 아니다.

냉각·강화 제한의 코드 기준은 main `8d08001a`에 병합된 PR #525다.
Initial 플레이어 UI의 조작 검증 목록은 [Player UI API](../player-ui-api.md#확인-순서)에 있다.

## 발사체 이름 변경 검증

2026-10-26 로컬 검증: `TowerBullet` / `TowerMissile`을 `FieldTurretBullet` / `FieldTurretMissile`로 변경했다. 두 `.meta`는 기존 파일과 동일하고 직렬화된 필드 및 공격·추적 로직은 유지했다. 기존 타입에는 `MovedFrom` 정보를 제공한다. Unity 6000.3.23f1 컴파일과 기존 EditMode 테스트 14개가 통과했다. 대상은 `MissileReservationLifecycleTests` 10개, `PlayerSkillPrefabTests.SupportTurret_UsesIndependentCannonProjectile` 1개, `ProjectAssetMigrationTests.CurrentScenes_DoNotContainMissingScripts` 3개다. 미사일 테스트는 PlayMode에 진입해 검사하며 실제 화면의 전투 인수를 뜻하지 않는다.

## main 병합 후 회귀 검증

2026-10-25 로컬 검증: main `1560df6c` 병합 충돌에서 FieldTurret 명칭, 시간 기반 수리 API와 main의 정수 회복 API·원거리 선택/조작 거리 구분·설치 충돌 검사를 함께 유지했다. Unity 6000.3.23f1 컴파일 및 `TeamHJD.Game.FieldTurrets.Tests` PlayMode 테스트 77개가 통과했다. 정수 수리로 최대 체력에 도달한 뒤 시간 기반 수리를 호출해도 이전 소수점 회복량이 남지 않는 회귀를 포함한다.

별도 EditMode 실행에서 `StageControlUnitHudTests`, `PlayerTurretPlacementTests`, `PlayerWeaponMigrationTests.SpannerTargetingRejectsRearDistantAndBlockedTargets`, `RegionSceneIntegrationTests` 총 32개가 통과했다. 총 109개 항목을 분리 실행했으며 화면 렌더링 인수·입력 장치별 수동 플레이·다중 클라이언트 검증은 포함하지 않는다.

## FieldTurret 명칭 변경 검증

2026-10-24 로컬 검증: Unity 6000.3.23f1에서 변경된 `TeamHJD.Game.FieldTurrets.Tests` PlayMode 테스트 74개가 통과했다. 초기화·냉각·자동 활성화·업그레이드·지속 수리·미사일 예약·지역 입력 어댑터를 포함한다.

별도 EditMode 실행에서 기존 통합 테스트 15개가 통과했다. 대상은 `ProjectAssetMigrationTests`의 카탈로그 GUID 및 씬 Missing Script 검사, `PlayerSkillPrefabTests`, `StageControlUnitHudTests`, `RegionSceneIntegrationTests`다. 씬 검사에는 Initial_Stage·SpaceShipGrayBox·EnemySandbox가 포함되고, HUD/지역 테스트는 실제 Initial_Stage에 진입해 CU 자동 활성화와 Static→Dynamic 지역 전환·점령 회수를 검사한다. 총 89개 항목을 분리 실행했으며 화면 렌더링 인수와 네트워크 다중 클라이언트 검증을 뜻하지 않는다.

파일 327개 이동과 관련 `.meta` GUID 179개 보존을 검사했다. 직렬화 파일 79개의 GUID 참조와 수치 설정이 유지됐고, 터렛 프리팹 24개에서 Missing Script가 없었다. 터렛 영역 밖의 코드/asmdef 26개는 새 타입·네임스페이스·경로를 참조하는 기계적 변경만 포함한다. 이 결과는 실제 화면의 선택 창·사거리 렌더링이나 플레이어 수리 도구 연결 검증을 대신하지 않는다.

## FieldTurretTest에서 확인

공통 기능은 왼쪽 패널에서, 개별 기능은 맵의 터렛을 클릭해 여는 전용 창에서 확인한다. 다른 터렛 선택, 창 드래그·닫기, UI 클릭의 맵 선택 차단, 승급·강등 후 같은 ID의 창 유지도 확인 대상이다. 기본 모드에서 선택한 터렛의 최소·최대 범위가 표시되고 선택 전환·해제 시 이전 범위가 사라지는지 확인한다. 전체 표시 모드에서는 모든 범위를 유지하며 `Selected Range Only`로 돌아오면 선택한 터렛만 남아야 한다.

1. 등록된 터렛의 서로 다른 ID를 확인한다.
2. Activate 요청 전후 활성 상태와 가용 전력 및 예약 전력을 비교한다.
3. Damage와 Destroy 후 파괴 상태 및 활성화 거부를 확인한다.
4. Restore 후 최대 체력과 비활성 상태를 확인한다.
5. 터렛을 끄고 냉각이 끝난 뒤 세부 업그레이드와 되돌리기를 수행해 공격력·요구 전력·외부 사거리를 확인한다.
6. 같은 비활성·냉각 완료 조건에서 레벨 승급과 강등 후 같은 ID의 선택 창이 교체된 터렛을 표시하는지와 컨트롤러 소속을 확인한다.
7. Lock과 Unlock 후 요청 결과 및 스냅샷을 확인한다.
8. ControllerSnapshot의 전체 집계와 종류별 활성 수를 비교한다.
9. 활성 상태와 비활성 냉각 중에는 두 업그레이드 경로가 거부되고 객체·단계·전력이 유지되는지 확인한다.
10. 자동 활성화 중에는 켜기·끄기·승급·강등·세부 변경·잠금·복구 버튼이 비활성화되고 직접 API 호출도 거부되는지 확인한다. 지속 수리 API도 `AutoActivationInProgress`로 거부되며 적의 피해와 자동 처리 내부 활성화는 계속 수행되는지 확인한다.

## 지속 수리 API 검증

2026-10-23 로컬 검증: Unity 6000.3.23f1 컴파일 통과. PlayMode에서 `FieldTurretRepairTests` 25개와 기존 `FieldTurretInitializationTests` 38개를 분리 실행해 통과했다. 수리 테스트는 30/50/60/120회 갱신의 1초당 회복량, 소수점 누적과 호출 중단, 최대 체력 제한, 잘못된 인자, 활성·OFF 냉각 중 상태·전력 유지, 체력 이벤트와 스냅샷, 잠금·파괴·비활성·자동 활성화 중 거부, 치명타 이벤트 안에서의 수리 거부, 초기화·레벨 교체의 누적량 초기화를 검사했다. 검증 결과는 플레이어 무기·입력과 실제 Stage 연결 완료를 뜻하지 않는다.

플레이어 담당자는 [지속 수리 API](api-reference.md#플레이어의-지속-수리)를 연결한 뒤 다음을 실제 Stage에서 확인한다.

1. 유효한 도구를 들고 대상 범위 안에서 입력을 유지할 때만 `Repair(초당 HP, 경과 시간)`를 호출한다.
2. 입력 해제·도구 변경·대상 이탈 후 추가 회복이 없는지 확인한다.
3. 10 HP/초 예시에서 5초 동안 50 HP를 회복하고 최대 체력을 넘지 않는지 확인한다.
4. 켜진 터렛과 OFF 냉각 중인 터렛도 상태·전력 변화 없이 수리되는지 확인한다.
5. 잠금·파괴·자동 활성화 중에는 거부되고, 파괴 후 복구는 기존 `Restore()`로 분리되는지 확인한다.
6. 기존 `HealthChanged` / `SnapshotChanged`로 HP 표시를 갱신하며 일반 수리에서는 `Restored` 연출이 실행되지 않는지 확인한다.

이번 작업은 터렛 측 API를 제공하며 FieldTurretTest 전용 수리 버튼이나 플레이어 입력은 추가하지 않는다.

## 미사일 타깃 예약 회귀 검증

`MissileTargetReservationTests`는 PlayMode 테스트 어셈블리에서 예약 배타성, 같은 목표의 복수 발 예약, 소유권 이전, 중복 반환, 목표 파괴·비활성화와 Play 세션 초기화를 검사한다. `MissileReservationLifecycleTests`는 EditMode 목록에서 선택하며 각 테스트가 빈 씬에서 PlayMode에 진입해 실제 `FieldTurretMissile` 컴포넌트의 폭발 시작·비활성화·파괴·초기화 실패·수명 종료를 검사한다. 테스트 종료 시 PlayMode를 빠져나온다. 실행 결과는 별도 확인한다.

2026-10-20 로컬 검증: Unity 6000.3.23f1 스크립트 컴파일 통과. `MissileTargetReservationTests` 7개 통과. `MissileReservationLifecycleTests`는 9개 통과 후 타이머 테스트의 대기를 실제 프레임 대기로 수정하고 해당 1개를 재실행해 통과했다. 최종적으로 신규 17개 항목을 분리 실행해 통과했으며 아래 실제 FieldTurretTest·Initial_Stage 전투 검증을 대신하지 않는다.

FieldTurretTest PlayMode에서는 다음을 추가 확인한다.

1. 적 한 마리를 두 발이 추적할 때 첫 발이 폭발해도 다른 미사일 터렛이 해당 적을 바로 예약하지 않는다.
2. 터렛을 끄면 미발사 예약만 반환하고 이미 발사한 미사일의 예약은 유지한다.
3. 비행 중인 미사일을 비활성화하거나 제거하면 다른 터렛이 해당 적을 다시 선택할 수 있다.
4. 목표가 제거되거나 비활성화되면 비행 중인 미사일이 새 목표를 예약한다.
5. 미사일의 코루틴을 중단해도 최초 직진·유도 전환과 10초 자폭은 진행한다. 폭발 피해는 시작 후 0.1초가 지난 물리 갱신에서 한 번 적용한다.

## 다른 영역과 통합할 때

- 소비자 asmdef에서 Core와 Contracts 참조를 확인한다.
- EnemyController와 EnemyHealth 및 네트워크 실행 조건을 확인한다.
- 터렛과 CU 및 UI가 같은 컨트롤러를 보는지 확인한다.
- Additive 씬에서 예산 공유 여부를 명시한다.
- 등록 해제 후 ID 조회 실패와 이벤트 구독 해제를 처리한다.
- 기존 Laser를 새 터렛 생명주기와 동일하게 취급하지 않는다.

## 아직 보장하지 않는 것

NGO 권한과 동기화 및 Network ID 연결은 별도 작업이다. 복구 비용과 시간 및 해금 조건은 최종 정책이 아니다. 읽기 전용 스냅샷 제공만으로 AI와 전선의 실제 연동 검증이 완료되지는 않는다.

전체 설정법과 Mermaid 실행 흐름 및 기술 부채는 [Turret System Guide](../turret-system-guide.md)를 참고한다.
