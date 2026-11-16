# 큰 터렛의 NavMesh 목표 투영 수정

PR: [#750](https://github.com/Team-HJD-21/unity-6-the-developer/pull/750), base `main`. 사용자의 요청에 따라 기존 Stage1 통합 PR에 별도 NavMesh 보완 커밋으로 추가한다.

## 먼저 볼 곳

1. `Assets/Scripts/Features/Enemy/Runtime/Movement/EnemyNavMeshPathfinder.cs`: 변경된 게임 코드는 이 파일뿐이다. 출발점3과 목표4의 투영 한도를 분리하고 실패 로그에 좌표/한도를 추가했다.
2. `Assets/Scripts/Features/Enemy/Runtime/Movement/EnemyMovement.cs`: 변경하지 않은 호출부. `SetDestination` → `TryUpdatePath`가 계산한 corners를 받아 이동한다.
3. [Enemy 가이드](../../02_architecture/08_encounter/enemy-system-guide.md#navmesh-출발점목표-투영): 값의 의미와 공격 사거리/Bake와의 구분을 확인한다.

## 재현과 원인

사용자가 Pause한 실제 Stage_1에서22개 적·7개 목표를 관측했다. `FieldMissileTurret LV1 (7)`을 선택한3개체만 경로가 없었다. 적의 출발점은 NavMesh에 있었고 목표도 같은 Stage_1이었다.

시설의 Transform 중심은 Base 장애물 안에 있다. 큰 미사일 터렛의 중심에서 가까운 보행면까지3보다 멀어 `NavMesh.SamplePosition`의 기존 한도3이 부족했다. 이번 오류는 잘못된 BossRoom 목표나 NavMesh 전체 미로드 때문이 아니다.

| 실제 목표 | 중심→보행면 | 기존3 | 수정4·완전 경로 |
|---|---:|---|---|
| FieldMissileTurret LV1 (7) |3.213654|실패|성공|
| FieldMissileTurret LV1 (5) |3.176170|실패|성공|
| FieldMissileTurret LV1 (2) |3.209637|실패|성공|
| FieldMissileTurret LV1 (4) |3.190498|실패|성공|

## 호출과 데이터 흐름

```text
기존 터렛 피해·체력 상실
 → DamageableTargetAdapter.Synchronize
 → TargetableComponent.RefreshTargetability
 → EnemySquad.OnTargetabilityLost
 → SetTarget / TryChangeTarget : 다음 시설 선택
 → EnemyController.AssignTarget
 → MovingEnemyController.OnTargetChanged
 → EnemyMovement.SetDestination / TryUpdatePath
 → EnemyNavMeshPathfinder.TryCalculatePath
 → XY 출발점·목표 투영 → PathComplete 검사 → corners → 실제 이동
```

로그/상태가 새 목표를 지목하므로 터렛 파괴 후 재선택에서 드러났다. 정확히 어느 이전 터렛이 파괴됐는지는 로그에 식별자가 없어 확정하지 않는다.

## 수정 범위

목표 투영만3→4로 조정한다. 출발점3, XY 평면, `NavMesh.AllAreas`, 완전 경로만 허용하는 조건은 유지한다. 실패 로그를 숨기지 않고 시작/목표 좌표와 한도를 추가해 다음 오류에서 구분할 수 있게 한다.

이번 NavMesh 보완 커밋은 Scene·Prefab·Collider·NavMesh Bake·타깃 선택·공격 수치·네트워크 권한·저장 형식을 변경하지 않는다. 기존 #750의 보스 연출/음악/UI 변경과 구분하며, 사용자 폰트/Startup·staged cache는 추가하지 않는다.

## 실제 검증

- Unity6000.3.23f1/Pipeline CLI. 원래 Pause의 위치/Collider/목표와 오류3건을 먼저 보존했다. 로그는 삭제하지 않았다.
- 수정 컴파일 후 정식 Startup→우주선→Stage1로 새 Match를 열었다. 원래22개체의 전투 진행도를 복원했다는 의미는 아니다. Scene은 저장하지 않았다.
- 실제 NavMesh9354삼각형에서 수정된 게임 함수로 기존7개 목표 모두 완전 경로를 확인했다. 위 미사일4개도 기존3 실패/수정4 성공을 대조했다.
- 실제 생성된 Slime의 `EnemyMovement.SetDestination` / `TryGetNextPathDirection`으로4개 목표 모두 HasPath·이동 방향을 확인하고 원래 목적지로 되돌렸다.
- 자동 생산을8.01실시간 초 진행해30개체 모두0.2 이상 이동했다. 자연 생산 표본은 Cannon을 목표로 했다. 미사일 검증은 위 명시적 경로/컴포넌트 검사와 구분한다.
- 수정 뒤 새 Console Error0, 컴파일 실패false. Editor는 다시 Play/Pause 상태로 뒀다.
- Windows Standalone·원격 Client·전체 전투·모든 시설의 실제 타격을 인수한 것은 아니다. 이번 게시 단계에서는 이미 통과한 같은 코드를 반복 Play하지 않고 diff와 CI를 확인한다.

원본 계측은 공유 제외 `Docs/Local/Review/NavTarget-20261116/`의 runtime-snapshot.json, path-verification.json, movement-binding-verification.json, movement-verification.json, console-before/after.json에 보존한다. 위 표와 호출 흐름은 해당 기록의 요약이다.

## 한계와 별도 관측

이 수정은 **현재 배치에서 확인한 목표 크기에 대한 한도 보완**이다. 임의 크기의 시설에 자동 대응하는 정책은 아니다. 목표에서 보행면까지4보다 멀거나 다른 원인으로 경로가 없으면 거부한다.

별개로 Slime1개체가 경로를 소진한 뒤 공격 범위에 닿지 않는 상태도 관측했다. arrivalDistance0.6, 마지막 경유점까지0.434549였지만 Collider 간격0.589843이 공격 범위0.35보다 컸다. 같은 Cannon을 향한 다른2개체는 공격 조건이true였다. 이 도착/공격 접근 문제는 이번 투영 수정으로 해결했다고 주장하지 않으며 이 PR에 섞지 않았다.

## 되돌리기

이 NavMesh 보완 커밋의 게임 코드·Enemy 가이드·검토 문서를 함께 되돌린다. #750 전체 롤백과 구분하며 Scene/Bake 복구는 필요하지 않다. 한도를3으로 되돌리면 위4개 큰 시설의 재현 조건이 다시 생긴다.
