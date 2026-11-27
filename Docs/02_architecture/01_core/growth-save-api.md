# 개인 성장 저장

2026-11-15 Editor 시험 정책: Play Mode의 성장은 현재 Play 안에서만 유지한다. Editor는 `persistentDataPath/EditorPlayMode/<프로젝트 경로 Hash>/Checkpoints`에 저장하며, 종료 시 성장 쓰기 완료 → App/Store 정리 → 해당 임시 저장 파일 삭제 순서로 처리한다. 중단된 이전 Play의 임시 파일도 다음 시작 전에 비운다. 기존 `persistentDataPath/Checkpoints`는 Editor에서 읽지 않으며 빌드의 영구 저장으로 보존한다. 저장·복원 자체의 테스트는 한 Play 안에서 진행하고, Play 종료 후 이어하기는 현재 Editor 정책에서 지원하지 않는다.

2026-11-15 전력 시험 설정: `Stage_1`과 `Initial_Stage`의 FieldTurretController는 `maximumPower = 500`, `fixedMaximumPower = 500`으로 둔다. 최대 전력은 새 시작·성장 로드·월드 복원 모두 500이다. 과거 성장 저장의 100/1000 용량은 이 설정을 덮어쓰지 않으며, 터렛 레벨·강화는 보존한다. 가동 예약은 기존 복원 우선순위로 다시 계산하고 한도를 넘는 터렛은 OFF로 복원해 이유를 표시한다. 가용 전력은 사용량에 따라 줄어들며 HUD의 100%는 최대 용량 숫자와 구분한다.

이 고정은 시험용 Scene 정책이다. `fixedMaximumPower = 0`으로 해제하면 기존처럼 성장 저장의 용량을 복원한다. 고정 중에는 `TrySetMaximumPower`로 다른 용량을 적용하는 요청을 거절한다. 다른 시험 Scene과 신규 컴포넌트의 기본 정책은 바꾸지 않는다.

2026-11-15: Stage_1의 터렛 제작 목록이 달라져도 같은 논리 ID·같은 무기 계열의 성장은 유지한다. 새 ID는 제작 기본값, 빠진 ID의 성장 기록은 저장에 보존한다. 임의 위치 매칭·환급·파일 삭제는 하지 않는다. 이는 성장 이관이며 과거 월드 체크포인트의 배치 호환을 자동 보장하지 않는다. [실제 검사와 미연결 범위](../../03_team/00_reviews/mission-safety-audit-20261115.md)를 참고한다.

월드 체크포인트로 과거 전투에 돌아가도 최신 경험치·성장은 남긴다. 현재 실제 원본은 `PlayerExperience`, `PlayerBaseStats`, Host 전장의 `FieldTurretController` 성장 writer다. 실제 스킬 강화·재화 소비 원본이 없는 기능을 저장 완료로 표시하지 않는다.

### Stage 1 토큰과 CU 장갑

Stage 1 최종 보스의 권위 승리 경로는 로컬 프로필에 첫 처치 토큰을 한 번만 지급한다. 단독 또는 Host의 출격 전 우주선에서는 `AppRoot.Stage1TokenBalance`, `HasStage1CuArmor`, `CanBuyStage1CuArmor`를 읽고 `BuyStage1CuArmorAsync`로 선택한 Host 캠페인의 장갑을 요청한다. 연결 중인 원격 참가자나 Match가 준비·진행 중인 우주선에서는 구매할 수 없다. 지급량과 가격은 첫 검증용 1토큰이다.

우주선은 플레이어와 카메라를 연결한 뒤 토큰 파일을 비동기로 읽는다. Stage 1 출격에서는 Host가 파일 읽기를 마친 뒤에만 준비 완료를 알린다. 원격 참가자는 Host 파일을 읽지 않는다.

`Stage1TokenProgress`는 잔액, 최초 지급 여부, 연구를 구매한 캠페인 ID를 로컬 프로필의 별도 파일에 원자적으로 쓴다. 저장이 실패하면 이전 상태를 보여주며 같은 요청을 다시 시도할 수 있다. 월드 스냅샷 복원은 이 파일에 적용하지 않는다. Editor Play 종료 시 임시 저장 수명은 기존과 같다.

필드와 우주선에서 `I`로 여는 상태창의 기본 스탯 아래에 토큰 잔액을 표시한다. 우주선 상태창에는 CU 장갑 I의 효과와 1토큰 가격을 표시하고, 구매가 저장되면 같은 자리에서 구매 완료를 보여준다. 두 상태창 프리팹은 `Stage1TokenPanel`로 잔액을 표시하며, 우주선 프리팹에만 구매 버튼을 연결한다. 스킬 트리의 연구 토큰은 기존 Player 원본을 사용한다.

토큰 파일을 읽을 수 없어도 우주선과 전장은 열린다. 장갑 소유를 확인하지 못하면 기본 CU 체력으로 출격하고, 상태창은 토큰 확인 실패를 표시한다. 첫 보스 보상 저장에 실패해도 승리와 귀환은 이어진다. 대기 중인 보상은 우주선 진입이나 상태창의 다시 시도에서 저장을 재시도한다. 저장을 마치기 전에 앱을 종료했다면 보스를 다시 완료해 지급을 요청할 수 있다. 손상된 파일은 보존한다.

출격 조립부는 체크포인트를 적용하기 전에 구매한 캠페인의 연구를 CU에 전달한다. `ControlUnitHealth`는 제작 Definition을 유지하면서 최대 체력에 10%를 올림 적용하며 기존 체력 비율과 파괴 상태를 유지한다. 현재 Host의 전력 한도와 터렛 강화 상태는 기존 설정을 따른다.

```mermaid
flowchart LR
    XP[PlayerExperience 실제 획득] --> Change[GrowthChanged]
    Stats[PlayerBaseStats 보상·투자·환불] --> Change
    Change --> Save[GrowthSaveService]
    Save --> Copy[메인 스레드에서 모든 성장 원본 복사]
    Copy --> Disk[BinaryCheckpointStore 원자적 파일 교체]
    Disk --> Load[새 Player 생성 뒤 성장부터 로드]
    Load --> World[이후 별도 월드 체크포인트 적용]
```

## 호출하는 쪽

- 게임/기존 HUD는 `AddExperience`, `TryClaimReward`, `TryInvest` 같은 기존 Feature API를 그대로 사용한다. UI에서 저장 함수를 중복 호출하지 않는다.
- `IGrowthCheckpointParticipant`는 실제 성장 원본이 구현한다. `PersistenceKey`는 `experience`, `base-stats`처럼 슬롯과 무관한 원본 이름이다. 원본 적용이 끝난 뒤 `GrowthChanged`를 보낸다.
- `InitialStageCompositionRoot.BindGrowthSaves`는 생성된 Player의 실제 writer를 개인별로 묶는다. `player/<논리 PlayerId>`를 해시한 `growth-…` 슬롯을 사용하며 매 Match ID나 roster 순서를 파일 귀속으로 사용하지 않는다. section payload의 논리 Player ID 검증은 유지한다.
- `PumpGrowthSaves`는 unscaled 절대 시각을 넘긴다. XP는 기본2초간 합쳐 캡처하고, 레벨업·보상 수령·스탯 변경은 모든 개인 원본을 즉시 한 묶음으로 캡처한다. 파일 쓰기는 불변 자료만 순서대로 처리하므로 늦은 옛 쓰기가 최신 값을 덮지 않는다.
- `FlushGrowthSaves()`는 정상 종료/월드 복귀 전에 대기 쓰기의 성공 또는 실패를 확인한다. Match 정리와 체크포인트 복원 시작에서도 호출한다. 강제 종료에는 마지막 성공한 파일만 보장하며 아직 합치던 XP는 유실될 수 있다.

저장 파일은 기존 `Application.persistentDataPath/Checkpoints` 아래의 독립 `growth-…checkpoint`다. 전체 월드 슬롯이나 `world/weather`의 준비 여부와 무관하게 개인 성장은 저장한다. 개인 저장을 전체 월드 저장으로 불러오지 않는다.

## 로드와 실패

조립부는 실제 Player identity/Feature 바인딩 뒤, 월드 복원 전에 성장 파일을 읽는다. 기본 파일 저장소는 작업 스레드에서만 IO하고 로딩 구간에서 완료를 기다린다. 이 저장소에 Unity 문맥으로 복귀해야 끝나는 IO 구현을 대입하면 안 된다. Backend로 옮길 때는 준비 단계의 비동기 로드 계약도 함께 바꿔야 한다.

모든 writer가 Prepare를 통과한 뒤 적용하고 실패 시 원복한다. 손상·원본 구성 변경·다른 Player·설정 불일치는 명시 오류이며 기본값으로 기존 파일을 덮지 않는다. 새 기능/저장 schema가 바뀌면 명시 이관이 필요하다. 디스크 오류는 `LastError`, `IssuedRevision/SavedRevision`, `HasPendingChanges`로 관측하고 실제 실패를 저장 성공으로 보고하지 않는다.

현재 네트워크 원격 Player 성장/Backend 인증 저장은 별도 통합 범위다. 실제 검수에서는 Stage_1에서 XP100/레벨2·보상수령·공격1 투자를 저장한 뒤 Play 완전 종료→새 actor 생성에서 동일값을 확인했다. QA 저장 파일은 원본을 보존하고 시험 후 원상복구했다.


## Host 공용 전장 성장과 월드 복귀

`FieldTurretController.CreateGrowthParticipant()`는 실제 터렛의 논리 ID별 제작 Definition(승급 레벨), 강화 종류/단계·DamageBonus, CU 전력 용량을 한 원본으로 캡처한다. 실제 강화/승급·용량 변경 사건만 즉시 저장한다. 일반 피해·냉각·ON/OFF는 성장 쓰기를 발생시키지 않는다.

성장 로드는 현재 전투 상태를 보존해 실제 터렛을 재구성한다. Cannon/Missile의 포신은 일반 실행과 같은 과열 표현과 프리팹별 비활성 색을 사용한다. Stage1 Canon·Missile LV1은 식은 포신을 기본 색으로 복원하고 LED에 해금과 전원 상태를 표시한다. 상태를 소비하는 호출부는 IsLocked와 IsActivated를 읽는다. [Stage1 LV1 전원 LED](../07_turrets/turret-system-guide.md#stage1-lv1-전원-led)

공용 파일 귀속은 `field/<Host PlayerId>/<CampaignSlot>/<StageId>`다. `AppRoot.CampaignSlot`의 기본은 `campaign-1`이고 시작/허브 흐름이 Mission 입장 전 `SelectCampaign(new CheckpointSlot("campaign-2"))`로 다른 캠페인을 고를 수 있다. 활성 Match 중 변경은 거절하며 기존 파일을 삭제하지 않는다. 개인 성장 파일은 이 캠페인 선택과 별개다.

`world/campaign`은 월드가 참조하는 캠페인 ID다. 이어하기의 `ConfigureCheckpointArrival`가 이를 먼저 읽고 최신 공용 성장→새 actor→월드 순서로 조립한다. ID가 없는 옛 전체 월드 파일은 명시 이관이 필요하다고 반환하며 현재 파일을 임의 삭제하거나 다른 성장 파일을 붙이지 않는다.

기존 `FieldTurretController.Persistence.PrepareRestore`는 과거 전투 section을 검증한 뒤 최신 성장과 합친 계획을 만든다. 실제 actor·무기 상태 적용은 기존 Defense factory/writer를 재사용한다.

- 위치·HP 비율·파괴·냉각/탄환·자동 큐는 과거 월드 상태다. 최신 Definition/강화·CU 용량은 유지한다. Player/CU도 현재 최대 HP에 과거 HP 비율을 적용하며 사망/파괴를 회복으로 바꾸지 않는다.
- 최신 비용으로 과거 ON을 복원한다. 현재 정상 ON 대상에 먼저 자리를 보장하고, 나머지는 기존 CU 후보 순서와 안정된 논리 ID 순서로 검증한다. 가용 전력에 맞지 않는 대상은 OFF로 복원하고 `GrowthRestoreWarnings`에 사유를 남긴다. 복원 후 기존 Alert로 알리며 강제 전력 초과·강화 명령 재실행·비용 환급은 하지 않는다.
- 강화는 기존 **OFF + 냉각 완료** 조건을 유지한다. 저장 도입으로 전투 중 강화 조건을 바꾸지 않는다.
- 제작 터렛 ID 집합/콘텐츠가 달라지면 명시 이관을 요구한다. 런타임 InstanceId를 영속 키로 사용하지 않는다.

Game Debugger Overview→CHECKPOINT의 ‘개인 / Host 성장 저장’에서 실제 원본 키·요청/디스크 완료 revision·대기/실패를 확인한다. 성장 저장은 성공했어도 필수 Weather writer가 없으면 전체 월드 저장 버튼은 계속 차단된다.

실제 검수: 비용15 터렛을 강화해17로 만든 뒤 CU 용량15에서 과거 ON 월드를 적용했다. 정상 가동15 터렛은 유지, 강화 대상만 OFF·부족 사유로 복원됐다. 이후 canon_stage1_lv2/강화1을 유지한 과거 월드 적용과 Play 종료→새 actor 생성 후 동일 성장 복구를 확인했다. 실제 제작 레벨1/2의 MaxHP는 둘 다100이므로, 다른 최대 HP 간 비율은 별도 자동 검사로 확인했다.
