# 플레이어 터렛 슬롯·피격·수리

2026-11-14 작업 기준. 플레이어 보조 터렛은 전력을 사용하지 않으며 필드 터렛의 전력·승급 Registry에는 등록하지 않는다.

## 주요 실행 경로

- `PlayerSkillController.TryUse(PlayerTurret)` → `PlayerTurretSkill.IsSelecting` → `PlayerTurretSlotPanel`의 1·2·3번 카드 → `TrySelectSlot(index)` → 기존 주변 8칸 프리뷰 → `TryConfirmPlacement`.
- 슬롯 창이 열린 동안 기존 반원 스킬 선택창의 하단 마우스 호버 자동 열림은 보류한다. 슬롯 선택을 설치 클릭으로 재사용하지 않는다.
- `PlayerTurretHealth : IDamageableTarget` → 프리팹의 기존 Enemy `DamageableTargetAdapter` / `TargetableComponent` → Enemy 분대 타깃 조회와 근접·투사체 피해. Enemy 내부 구현은 수정하지 않는다.
- `PlayerTurretRepair` → 같은 Scene·Match 시계·권한·거리·장애물 검사 → `PlayerTurretHealth.Repair(초당 HP, 경과 시간)` → 기존 R 안내·스패너 모션·금속 효과. 필드 터렛의 Repair/Restore 경로도 유지한다.

## 고정 슬롯

`PlayerTurretInventory`가 번호별 `Available`, `Deployed`, `Rebuilding` 상태와 체력 비율을 소유한다. 설치 actor의 `PlayerTurret.SlotIndex`가 해당 슬롯을 연결한다. 현재 프리팹은 세 슬롯, 최대 체력 100, 설치 간격 3초, 파괴 복구 30초다.

- 살아 있는 터렛 회수: 같은 번호가 즉시 사용 가능 상태가 되고 남은 체력을 보관한다. 설치 간격은 우회하지 못한다. 회수 상태에서 자동 회복하지 않으며 재설치 시 그 체력으로 시작한다.
- 설치 중 수리·피격: 실제 체력이 슬롯에 반영된다. R 수리는 기존 설정인 초당 최대 HP의 10%를 사용한다.
- 파괴: actor의 타깃·충돌을 해제하고 해당 슬롯만 복구 시간을 진행한다. 복구 후 최대 체력으로 재설치할 수 있다. 파괴 actor를 R로 복구하는 별도 경로는 제공하지 않는다.
- 스킬 사용으로 상태 창을 여는 데는 수량·시간을 소모하지 않는다. 설치 중·복구 중 슬롯과 설치 간격 중에는 카드 선택을 거부한다. 같은 스킬 키·우클릭·Esc, 다른 스킬 사용·장착 변경·조작 차단은 선택/프리뷰를 취소한다.
- 세 슬롯의 체력은 같은 판의 상태다. 새 Player 스킬 인스턴스는 최대 체력으로 시작하며 영구 성장이나 계정 저장에는 기록하지 않는다.

## 피해·물리·권한

공통 피해 API는 정수 피해를 받으며 기존 Player 터렛의 실제 체력 원본은 float를 유지한다. `CurrentHealth`는 남은 HP를 올림하여 계약에 제공한다. 수리는 실제 float 체력을 회복하고 최대 체력을 넘지 않는다. 슬롯 UI는 이 원본의 비율을 표시한다.

본체는 `PlayerFeetBox`·`EnemyFeetBox`를 막고, `EnemyProjectile` 접촉도 허용해 투사체의 공통 피해 수신자로 연결한다. 나머지 접촉 제외 설정과 아군 탄환 통과를 유지한다. 기존 Player 장치 우회 공격 Registry에 중복 등록하지 않아 Enemy 기본 공격과 임시 공격이 겹치지 않게 한다.

피해·수리는 네트워크 실행 중 서버 권한에서만 적용하며 필드 정지·종료 시계를 따른다. 설치 actor는 `FieldSimulationTime.BindSpawned`로 설치자의 Scene·Match 시계·종료 수명을 받는다. 이 연결은 NGO 원격 설치 명령·actor 복제·클라이언트 UI 동기화를 구현한 결과가 아니며 다중 클라이언트 인수는 별도다.

## 체크포인트

터렛 스킬 section만 schema **2**로 변경한다. 슬롯 번호·상태·체력·복구 시간, 설치 actor의 슬롯 번호와 실제 체력, 상태 창/프리뷰 선택을 함께 기록한다. 슬롯과 actor 수·번호·체력이 불일치하거나 슬롯이 중복되면 적용 전에 거부한다. 기존 schema 1을 잘못 읽지 않고 버전 불일치로 거부하며 다른 스킬 section 버전은 유지한다. Apply/Rollback에서는 이전 소유자를 먼저 해제해 actor 제거가 슬롯을 다시 소비하지 않도록 한다.

## 검증 진입점

`PlayerTurretInventoryTests`, `PlayerTurretSlotRuntimeTests`, 기존 `PlayerTurretRepairTests`, `PlayerTurretPlacementTests`, `PlayerSkillRuntimeTests`를 사용한다. 새 통합 검사는 실제 Player·HUD·터렛·Enemy 투사체 프리팹과 Enemy 근접 능력으로 상태 선택·체력 승계·피격·R 수리·체크포인트를 검사한다. 자동 검사와 실제 Initial_Stage 수동 플레이·Host/Client 검증은 구분한다.
