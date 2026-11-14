# 대규모 몬스터 성능 트러블슈팅 기록

작성일: 2026-11-14
상태: 성능 조사·개선 기록. 이번 최적화 작업은 중단했으며, 아래 수치는 Editor 프로파일링 관측이다. 출시 빌드 성능 보증이나 완료 판정으로 해석하지 않는다.

## 목적과 요약

약 400마리의 몬스터를 생성했을 때 게임과 Unity 에디터 조작이 함께 느려지는 현상을 조사했다. 부하를 약 700마리까지 높이고 Unity CPU Profiler에서 물리, 몬스터 행동, 충돌 모양 갱신, 에디터 비용을 나눠 확인했다.

조사 중 확인한 핵심은 다음과 같다.

- 느린 프레임 하나에 물리 `FixedUpdate`가 여러 번 실행되면 물리와 몬스터 행동 비용이 함께 누적된다. 총 프레임 시간만 비교하지 않고 FixedUpdate 횟수와 메서드 호출 수도 함께 봐야 했다.
- PolygonCollider 경로를 방향·형태 전환 때만 갱신하는 중간 개선을 측정한 뒤, 최종 EnemyRoot 피격 구성은 방향별 Capsule로 전환했다. Polygon 측정값은 당시의 이력이고 현재 기본 프리팹의 적용 상태가 아니다.
- 분대의 실제 타깃 검색은 분대 단위였지만 모든 구성원이 갱신을 요청했다. 대표 구성원만 요청하도록 바꿔, 관측 캡처에서 갱신 1회당 요청을 710회에서 303회로 줄였다.
- Trigger 감지를 개체별 거리 탐색으로 대체한 실험은 물리 작업을 스크립트 작업으로 옮겼고 총비용 이득이 확인되지 않아 되돌렸다.
- 마지막 캡처에서 CPU 표본은 36.41ms였고, 계층 표에는 `PlayerLoop` 19.00ms/3회와 `EditorLoop` 17.21ms/4회가 기록됐다. 이 캡처는 여러 호출을 합산한 표본이라 19ms를 단일 게임 프레임 시간으로 단정할 수 없다. 따라서 60FPS 달성 여부나 해결된 프레임 수를 이 캡처만으로 결론내리지 않았다.

## 측정 조건과 해석

| 항목 | 기록 |
| --- | --- |
| Unity | 6000.3.23f1 |
| 작업 브랜치 | `fix/enemy-performance` |
| 부하 | 최초 약 400마리, 이후 약 700마리. 실행 중 조회에서 710마리 확인 |
| 측정 환경 | Unity Editor 실행 및 CPU Profiler |
| 비교 지표 | `Time ms`, `Self ms`, `Calls`, `GC Alloc`, 물리 FixedUpdate 횟수 |
| 프로젝트 물리 주기 | 약 0.01초. 0.02초 변경은 중간 비교 실험이었고 최종 설정으로 채택하지 않음 |
| 자동 테스트 | 대표 구성원 변경 테스트를 포함한 `EnemyAutoRetargetTests` 14개 통과 |
| 미측정 | Development Build·출시 빌드의 평균 FPS/프레임 시간 분포 및 실제 다중 클라이언트 부하 |

캡처마다 몬스터 수, 접촉 상태, 타깃 상태와 선택한 프레임 범위가 다르다. 계층의 `Time ms`는 선택 범위의 호출 누계일 수 있고, `Calls`는 그 범위에서 해당 표본이 실행된 횟수다. 서로 다른 캡처를 엄밀한 A/B 벤치마크나 변경 하나의 전체 FPS 개선율로 비교하지 않는다. 특히 `총 시간 ÷ FixedUpdate 횟수`는 실행 빈도 차이를 해석하기 위한 참고치이지 통제된 성능 시험을 대신하지 않는다.

### 확인된 병목과 변경 전후 수치

| 구간 | 변경 전 관측 | 변경 후 관측 | 확인 가능한 결론 |
| --- | --- | --- | --- |
| 700마리 부하의 고정 업데이트 | 선택 표본 CPU 155.57ms, `PlayerLoop` 126.28ms. 물리 FixedUpdate 14회/58.82ms, 스크립트 FixedUpdate 14회/59.25ms. `EnemyNetworkController.FixedUpdate` 9,800회, `ExecuteBehavior` 9,800회/36.95ms | 여러 최적화 후 캡처는 부하·선택 범위·FixedUpdate 수가 달라 직접 비교 불가 | 초기 병목은 Physics2D만이 아니었다. 고정 업데이트 14회가 물리와 몬스터 행동을 함께 반복했고, 선택 표본 안에서 두 비용이 각각 약 59ms였다. 9,800회는 700개체 × 14회와 일치한다. |
| Polygon 물리 모양 재생성 | 415개체 표본에서 `SpritePhysicsShapeSync.LateUpdate` 4.02ms, `CreateShapes` 300회/3.03ms, 준비 단계 300회/2.48ms | 700개체 표본에서 Shape 동기화 0.25ms, `CreateShapes` 2회/0.07ms | 표본 간 조건은 다르지만, 재생성 호출은 300회에서 2회로 99.3% 감소했고 관측 생성 시간은 3.03ms에서 0.07ms로 낮았다. 이 수치는 해당 표본의 Shape 작업만 비교하며 전체 프레임 개선율은 아니다. |
| 분대 타깃 갱신 요청 | 3회 갱신 표본에서 `RefreshTarget` 2,130회/3.74ms, 회당 710요청 | 2회 갱신 표본에서 606회/0.09ms, 회당 303요청 | 요청 횟수/갱신은 약 57.3% 감소했다. 총 시간은 표본 조건이 달라 직접적인 절감량으로 보지 않는다. |
| 마지막 CPU 표본의 Editor 분리 | CPU 36.41ms 표본 | `PlayerLoop` 19.00ms/3회, `EditorLoop` 17.21ms/4회, `Profiler.FlushCounters` 0.17ms | 에디터 비용과 게임 루프 비용은 분리해서 봐야 한다. 호출 수가 1이 아니므로 이 합계로 단일 프레임 시간 또는 FPS를 산출하지 않는다. |

**“몇 프레임 해결됐나”에 대한 답:** 보내준 캡처는 해결된 프레임 개수를 세는 자료가 아니다. Unity Profiler가 선택한 구간의 시간과 호출 수를 보여준다. 따라서 확인된 결과는 Shape 재생성 300→2회, 분대 갱신 요청 710→303회/갱신처럼 작업별 수치로 보고한다. 같은 빌드·같은 개체 수·같은 접촉 상태에서 단일 프레임 또는 충분한 구간을 전후 비교하지 않았으므로 “몇 프레임/몇 FPS 개선”이라고 단정할 수 없다.

조사 도중 참고한 프레임들:

| 조사 시점 | 전체 CPU | 물리 갱신 횟수 | 물리 FixedUpdate 합계 | 스크립트 FixedUpdate 합계 |
| --- | ---: | ---: | ---: | ---: |
| 초기 700마리 병목 캡처 | 155.57ms | 14 | 58.82ms | 59.25ms |
| 캡슐 전환 후 공유 캡처 | 22.17ms | 3 | 7.11ms | 8.65ms |
| 분대 대표 갱신 적용 후 공유 캡처 | 18.55ms | 2 | 5.85ms | 4.52ms |
| 마지막 상위 루프 확인 캡처 | 36.41ms | 3 | 6.63ms | 6.50ms |

이 표의 시간과 횟수는 Profiler에서 선택된 범위의 합계다. 표본별 선택 범위·개체 상태·호출 수가 같지 않으므로 순차적으로 낮아진 행을 같은 조건의 전후 프레임처럼 해석하지 않는다. 마지막 표본의 구분은 [게임 루프와 에디터 비용 분리 캡처](2026-11-14-enemy-performance-evidence/05-editor-vs-player.png)에서 확인할 수 있다.

## 조사 및 조치 기록

### 1. 프로파일러에서 실제 게임 프레임을 찾기

**증상:** 게임과 Editor가 느린데 선택한 프레임에는 `Profiler.FlushCounters`, `EditorLoop` 같은 항목만 보이거나 시간이 작게 나타났다.

**조치:** 부하가 기록된 구간을 선택하고 `PlayerLoop → UpdateScene → FixedUpdate`를 펼쳐 게임 실행 비용을 확인했다. 녹화 중 상세 정보가 비어 있는 경우 녹화를 멈춘 뒤 관심 프레임을 선택했다. 최상위의 `PlayerLoop`와 `EditorLoop`도 따로 비교했다.

**결론:** 선택한 한 행의 비율이나 Editor 전체 CPU를 게임 비용으로 읽지 않는다. 측정 화면이 실제 부하 프레임을 담고 있는지 먼저 확인한다.

### 2. 물리 비용과 반복 FixedUpdate 횟수 구분

**관찰:** 초기 부하 캡처에서 물리 갱신이 12회였던 프레임이 있었고, 이후 캡처에는 20회·34회까지 기록된 경우가 있었다. 물리 계산뿐 아니라 `EnemyNetworkController.FixedUpdate`의 행동 검사도 같은 횟수만큼 늘었다.

**조치:** 총 시간 외에 FixedUpdate 횟수와 메서드 호출 수를 함께 봤다. 물리 주기를 0.02초로 바꿔보는 비교도 했지만, 시뮬레이션 빈도를 낮추는 방법을 해결책으로 확정하지 않았다. 프로젝트 설정은 약 0.01초로 유지했다.

**결론:** 느린 프레임에서 고정 업데이트가 따라잡으며 발생하는 누적과 작업 자체의 비용은 별도로 봐야 한다. 물리 빈도를 낮춰 숫자를 줄이는 것을 코드 최적화 결과로 보고하지 않는다.

### 3. 일반 몬스터의 연속 충돌 검사

**관찰:** 초기에는 `Physics2D.SolveContinuous` 및 연속 충돌 처리 경로가 크게 보였다.

**조치:** 일반 몬스터 Rigidbody2D를 `Continuous`에서 `Discrete`로 변경해 비교했다. 이후 캡처에서 연속 충돌 처리의 비중은 줄고, `FindNewContacts`와 일반 충돌 해결 비용이 상대적으로 드러났다.

**결론 및 범위:** 일반 몬스터와 빠른 투사체에 같은 충돌 모드를 일괄 적용할 필요가 없다는 방향으로 정리했다. 모든 속도·지형에서 투사체 관통이 없는지까지 검증한 것은 아니다.

### 4. 중간 구현: 애니메이션마다 PolygonCollider 모양이 재생성되는 문제

**증상 및 원인:** `SpritePhysicsShapeSync.LateUpdate` 아래에서 `Physics2D.Collider2D.CreateShapes`와 `PolygonColliderPrepareShapes`가 반복됐다. 약 415개체 캡처에서 동기화 4.02ms, Shape 생성 300회·3.03ms가 관측됐다. 스프라이트가 애니메이션 프레임마다 바뀔 때 피격 윤곽도 함께 매번 재적용하고 있었다.

**조치:** 방향별 대표 Sprite를 두고 방향 또는 충돌 형태가 달라진 경우에만 Polygon 경로를 적용했다. 이동 파라미터가 0이 된 뒤에도 마지막 방향을 기억한다. 소환사는 변태 후 형태가 바뀌므로 `FixedShape` 태그와 고정 형태 경로를 별도로 처리했다.

**확인:** 사용자가 소환사의 변태 후 윤곽 변경을 실제 실행에서 확인했다. 이후 700개체 캡처에는 Shape 동기화 0.25ms, 생성 2회·0.07ms가 기록됐다. 서로 다른 부하·상태이므로 개선율로 비교하지 않고, 대량의 프레임별 재생성을 상태 전환 시점으로 옮긴 것으로 기록한다.

**최종 구성과의 관계:** 이 Polygon 경로는 조사 중 사용한 중간 구현이다. 이후 방향별 크기와 피격 모양을 Inspector에서 조정할 수 있도록 `DirectionalCapsuleCollider2D`와 자식 HitBox의 `CapsuleCollider2D` 방식으로 전환했다. 따라서 이 절의 측정값은 Polygon 경로를 사용하던 시점의 유효한 프로파일링 이력이며, 현재 EnemyRoot 기본 구성을 설명하지 않는다. 현재 콘텐츠 제작 절차는 [Stage 1 Enemy 가이드](../../../../02_architecture/08_encounter/stage1-enemy-content-guide.md)를 따른다.

증거: [변경 전](2026-11-14-enemy-performance-evidence/01-shape-before.png) · [700개체 캡처](2026-11-14-enemy-performance-evidence/02-shape-after-700.png)

### 5. Polygon 정밀도와 방향별 캡슐 설정

**문제:** 방향에 따라 스프라이트 윤곽이 달라 고정 Capsule 하나로는 피격 범위가 어긋났다. Polygon도 방향 변경 때만 갱신하도록 했지만, 대량 개체에서 더 단순한 충돌 모양을 적용할 수 있는지 살폈다.

**조치:** `DirectionalCapsuleCollider2D`를 추가해 방향별 `offset`, `size`, 캡슐 방향을 미리 설정하고 방향·변태 상태가 달라질 때만 적용했다. 사용자가 프리팹에서 방향별 값들을 직접 맞췄다. 가로로 긴 스프라이트를 세로 캡슐로 맞출 수 없었던 경우 Horizontal 방향으로 조정했다.

**확인 범위:** 단순화된 캡슐과 피격 범위가 스프라이트에 맞는지 시각 조정을 했다. Polygon 대비 Capsule만의 성능 차이를 분리한 통제 벤치마크는 없다. 자동 산출 값만으로 모든 프리팹의 정확도를 보장하지 않는다.

### 6. 충돌 콜백을 줄여도 물리 접촉 탐색이 남는 문제

**관찰:** `OnTriggerStay2D` 내부 일을 줄여도 `Physics2D.FindNewContacts`가 높게 남았다. HitBox만 끄기, Sensor만 끄기, 둘 다 켜기 등의 캡처를 비교했지만 실행별 FixedUpdate 수와 접촉 상태가 같지 않았다.

**조사·조치:** HitBox·발·Sensor의 역할과 Layer Collision Matrix를 확인했다. 몸통 피격 레이어의 자기 충돌은 이미 꺼져 있어 이를 원인으로 오인하지 않도록 정정했다. 활성 Collider 쌍과 발 레이어 충돌도 개별 확인했다.

**결론:** 콜백에서 return해도 Physics2D의 접촉 후보 탐색 비용이 사라지는 것은 아니다. 발끼리 충돌을 끄면 서로 밀리는 게임 동작도 달라질 수 있다. 캡처만으로 충돌 매트릭스의 개선율을 확정하지 못했고, 잔여 `FindNewContacts`는 이번 범위에서 더 변경하지 않기로 했다.

### 7. Trigger 감지를 거리 탐색으로 바꿨다가 되돌린 실험

**가설과 시험:** 몬스터마다 붙은 플레이어 센서 Trigger를 공유 후보 목록과 거리 기반 검사로 대체하면 물리 비용이 줄 것으로 예상했다. 시험 캡처에는 `DetectPlayer`가 4.79ms·3,545회로 나타났다.

**결정:** 물리 비용의 일부가 스크립트 비용으로 옮겨갔고 총비용 개선은 확인되지 않았다. 대체 방식은 되돌리고 플레이어 신규 감지는 `EnemyPlayerSensor.OnTriggerEnter2D/OnTriggerStay2D` 경로를 유지했다. 이미 플레이어를 추적 중인 몬스터가 반복 통지를 하지 않도록 하는 기존 조기 반환은 남겼다.

**구분:** 이미 배정된 플레이어의 근거리·원거리 이동 경로 갱신 주기를 판단하는 거리 검사는 별도 동작이며 유지했다. 되돌린 것은 새 플레이어 감지 방식이다.

### 8. 타깃 참조와 유효성 조회 반복

**문제와 조치:** 반복 조건 검사에서 타깃 컴포넌트와 Collider를 매번 다시 조회하지 않도록 `EnemyTargeting.SetTarget()`에서 참조를 저장했다. 기존 `TargetabilityLost` 이벤트를 구독해 무효화 시 참조를 비우거나 분대가 새 목표를 연결한다. 실제 피해 적용·지연 처리 직전의 유효성 검사는 유지했다. 실행 중 다시 쓰는 `FieldSimulationActor` 같은 참조도 찾은 뒤 재사용했다.

**주의점:** 이전 타깃의 상실 이벤트가 새 타깃 지정 이후 도착할 수 있어, 이벤트 대상이 현재 저장된 타깃과 같은 경우에만 지우도록 비교한다. 새 타깃 지정 때 이전 구독을 해제하고, 처음부터 공격 불가한 타깃은 저장하지 않는다.

**결론:** 캐시만 추가하고 무효화·수명 경로를 빠뜨리지 않도록 타깃 전환과 이벤트 순서를 함께 고려했다.

### 9. 한 행동 안에서 사거리 조건 반복 계산

**조치:** `ExecuteBehavior()`에서 전투 타깃 사거리를 `isInTargetRange` 지역값으로 한 번 구해 `CheckCondition(bool)`과 `MoveToTarget(bool)`에 전달했다. 이미 확인한 조건으로 내부에서 능력을 시작할 때는 재검사를 건너뛰고, 외부 공개 경로의 인자 없는 `TryUse()`는 기존 조건 검사를 유지했다.

**보존한 동작:** 능력 권한·실행 상태·쿨타임·발동 결과 검사는 그대로 둔다. 회복은 아군 대상, 소환 변태는 자체 상태 조건을 따로 확인하며, 지연 공격이나 명중 시점에 오래된 사거리 결과를 사용하지 않는다. 사거리 bool은 행동 실행 동안만 보관하고 프레임 사이 캐시로 쓰지 않는다.

### 10. Collider 정밀 거리 검사의 조기 배제

**조치:** 모든 후보에 정밀 `Collider2D.Distance()`를 하기 전에 Collider Bounds 간격의 제곱값으로 멀리 있는 후보를 제외한다. 가까운 후보만 실제 표면 거리를 검사한다.

**후속 점검:** 실행 중 약 710개체 중 99개가 플레이어를 타깃으로 삼고 있었다. 플레이어의 Collider 24개 중 Trigger가 아닌 16개에서 활성 상태는 3개였다. 방향별 활성 전환이 가능한 Collider 목록은 미리 영구 제거하지 않고 검사 시 비활성 Collider를 건너뛰도록 했다.

**관측 한계:** 단일 캡처에서 `CheckCondition`은 갱신당 약 0.64ms에서 0.61ms로 보였다. 약 4% 차이는 유의미한 개선이라고 확정하지 않았다. 이 확인은 불필요한 Bounds 접근 제거 근거이지, 사거리 전체 재설계의 성과가 아니다.

### 11. 회복 대상의 중복 검색

**조치:** `allyHitboxLayers`로 후보 레이어를 제한하고 `HashSet<EnemyHealth>`로 같은 아군의 여러 Collider 중복을 걸렀다. 유효한 회복 대상과 Collider 참조를 저장한다. 대상을 잃으면 즉시 다시 찾고, 대상이 없는 동안에만 기본 0.2초 간격으로 검색한다. 검색 간격과 회복 쿨타임은 구분한다.

**검증 범위:** 반복 탐색·중복 처리를 제한하는 구현을 확인했으나, 이 변경만의 독립적인 Profiler 시간 개선 수치는 없다.

### 12. Animator에 같은 상태를 매번 쓰는 문제와 복원

**조치:** `EnemyAnimation`이 마지막 방향, 이동 여부, 애니메이션 속도를 기억하고 값이 바뀔 때만 Animator에 쓴다. `OnEnable()`에서 실제 Animator 값을 다시 읽고, 체크포인트 복원 후에도 캐시를 동기화해 캐시와 실제 상태가 어긋나지 않게 했다.

**검증 범위:** 불필요한 Animator 쓰기를 줄이는 동작과 활성화·복원 경로를 정리했다. 독립 Profiler A/B 수치는 없다.

### 13. 경로 이동에서 중복 위치·거리 작업

**조치:** `EnemyMovement.TryGetNextPathDirection()`에서 위치를 한 번 읽고 거리 제곱과 도착 거리 제곱을 비교한다. 이미 정규화된 다음 방향을 `Move()`에서 다시 정규화하지 않도록 했다. 타깃 종류 판정과 Match 시계 참조도 재사용한다.

**주의점과 보존 사항:** 제곱 거리는 비음수 거리 비교에서 도착 경계 판정을 근사로 바꾸지 않으며, 부동소수점이므로 비트 단위 동일성을 주장하지 않는다. AddForce와 외부 충돌로 밀리는 동작은 유지했다. 이 최적화만의 개별 시간 수치는 없다.

### 14. 분대마다 타깃 재검색을 대표 구성원이 요청하도록 변경

**증상:** 한 캡처에서 `RefreshTarget`이 2,130회·3.74ms였다. 실제 검색은 분대 단위였지만 모든 구성원이 FixedUpdate에서 갱신 메서드에 들어왔다.

**조치:** `EnemySquad`가 멤버 등록·제거 및 `EnemyController` 활성 상태 변경 때 대표를 갱신하고, 해당 몬스터에 `IsSquadRepresentative`를 표시한다. `EnemyNetworkController`는 대표 구성원만 주기적 `RefreshTarget()`을 요청한다. 대표가 비활성화되거나 분대를 나가면 다음 활성 구성원이 승계한다. 기존 타깃 상실 이벤트의 즉시 재검색은 유지한다.

**회귀 검증:** 대표 교체 시 기존 타깃·공격 인원 배정·재검색 시각을 초기화하지 않는 두 테스트를 추가했다. 기존 테스트 3개는 게임 기본 공격 인원 상한 50과 다른 테스트 가정 10 때문에 실패했다. 제품 설정은 바꾸지 않고 테스트 fixture에서만 상한 10을 지정했다. `EnemyAutoRetargetTests` 14개가 통과했다.

**호출 수 결과:**

| 지표 | 변경 전 | 변경 후 |
| --- | ---: | ---: |
| 물리 갱신 수 | 3 | 2 |
| `RefreshTarget` 호출 수 | 2,130 | 606 |
| 물리 갱신 1회당 호출 수 | 710 | 303 |
| 관측된 메서드 총 시간 | 3.74ms | 0.09ms |

`(710 - 303) / 710 ≈ 57.3%`로, 해당 캡처에서 **갱신 요청 호출 수**가 감소했다. 분대 검색 그 자체가 57% 빨라졌다거나 전체 게임 FPS가 57% 개선됐다는 뜻은 아니다. 1인 분대에는 대표 공유로 인한 호출 감소가 없다. 시간 값은 서로 다른 프레임 관측이므로 순수 개선량으로 비교하지 않는다.

증거: [변경 전](2026-11-14-enemy-performance-evidence/03-refresh-before.png) · [변경 후](2026-11-14-enemy-performance-evidence/04-refresh-after.png)

### 15. 마지막 병목에서 게임 비용과 Editor 비용 분리

**마지막 선택 표본 관측:**

| 최상위 표본 | 시간 |
| --- | ---: |
| 전체 CPU | 36.41ms |
| 게임 `PlayerLoop` | 19.00ms |
| `EditorLoop` | 17.21ms |
| `Profiler.FlushCounters` | 0.17ms |

`PlayerLoop` 계층에는 물리 FixedUpdate 6.63ms/3회, 스크립트 FixedUpdate 6.50ms/3회가 표시됐다. `EnemyNetworkController.FixedUpdate`는 5.10ms/2,139회, `ExecuteBehavior`는 4.26ms였다. 이는 선택된 호출 범위의 합계이며, 캡처의 호출 수가 1이 아니므로 단일 프레임 16.7ms 예산과 직접 비교하지 않는다.

**결정:** 전체 CPU 표본 36.41ms를 전부 게임 코드 비용으로 취급하지 않고, Editor 비용과 게임 비용을 구분했다. 게임 `PlayerLoop`도 여러 호출의 합계이므로 이 수치만으로 목표 프레임 달성 여부를 선언하지 않았다. 동일 부하의 Development Build 측정은 남아 있으며, 이번 최적화 조사는 여기서 멈췄다.

## 적용·되돌림·미확인 사항

| 상태 | 내용 |
| --- | --- |
| 적용 | 일반 몬스터 Continuous → Discrete 충돌 설정 조정 |
| 과거 중간 적용, 현행 기본 구성에서는 대체됨 | 방향·형태가 바뀔 때만 Polygon 모양 갱신, 소환사 고정 변태 형태 처리. 해당 시점의 Profiler 기록은 위 이력 절 참고 |
| 현행 적용 | `DirectionalCapsuleCollider2D`와 HitBox `CapsuleCollider2D`, 프리팹별 방향 크기·오프셋 설정 |
| 적용 | 타깃 참조 저장과 상실 이벤트 무효화, 행동 내 사거리 결과 재사용 |
| 적용 | 회복 대상 필터·중복 제거·검색 간격, Animator 쓰기 캐시, 이동 계산 중복 제거 |
| 적용 | 대표 구성원만 분대 타깃 재검색 요청, 비활성 Collider Bounds 접근 생략 |
| 되돌림 | 새 플레이어 감지를 Trigger에서 개체별 거리 탐색으로 바꾼 시도 |
| 비교 실험 | 물리 주기 0.02초, HitBox/Sensor 비활성, 충돌 레이어 변경. 통제된 최종 A/B로 확정되지 않음 |
| 미완료 | 실제 Player 빌드의 평균·상위 프레임 시간, 다중 클라이언트 부하, 60FPS 목표 검증 |

AddForce 이동, 타깃 상실 이벤트, 기존 분대 우선 타깃 동작 전체를 이번 성능 작업에서 새로 구현했다고 해석하지 않는다. 이번 기록은 반복 실행 비용을 조사하고 일부 경로를 줄인 변경을 다룬다.

## 팀원이 이어서 검증할 때

1. 같은 빌드 모드와 같은 부하 조건에서 400·700개체를 각각 기록한다. Editor Profiler와 Development Build 결과를 섞지 않는다.
2. 캡처별 `PlayerLoop`, `EditorLoop`, 물리 FixedUpdate 횟수, `Physics2D.Simulate`, `EnemyNetworkController.FixedUpdate`를 기록한다.
3. `Calls`와 `Time ms`를 같이 보고, 대표 구성원 조정은 `RefreshTarget calls ÷ 물리 갱신 횟수`로 비교한다.
4. 현행 Capsule 구성은 동일 개체 수·방향 전환 조건에서 `DirectionalCapsuleCollider2D`의 형태 적용과 Physics2D 비용을 확인한다. Polygon 경로를 별도 시험하는 경우에만 `SpritePhysicsShapeSync.LateUpdate`와 `CreateShapes` 수치를 사용하고, Capsule 결과와 혼용하지 않는다.
5. Collider나 물리 레이어를 바꾸는 시험에서는 몬스터 밀침, 플레이어 접촉, 투사체 관통 등 게임 동작 회귀를 별도로 확인한다.
6. 테스트 통과, Unity Editor 플레이, Development Build, 실제 네트워크 플레이는 각기 다른 검증으로 기록한다.

## 관련 코드와 자료

아래 코드는 조사 시점의 구현 위치다. 현재 작업 트리에 남아 있는 변경은 main/PR 반영과 구분해서 확인한다.

| 코드 | 관련 내용 |
| --- | --- |
| [EnemySquad.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Targeting/EnemySquad.cs) | 대표 선정·승계, 분대 타깃 갱신 |
| [EnemyController.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Controllers/EnemyController.cs) | 대표 상태와 활성화 생명주기 |
| [EnemyNetworkController.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Networking/EnemyNetworkController.cs) | 대표의 재검색 요청, 서버 행동 실행 |
| [EnemyAutoRetargetTests.cs](../../../../../Assets/Scripts/Features/Enemy/Editor/Tests/EnemyAutoRetargetTests.cs) | 대표 교체·재검색 간격·배정 회귀 |
| [EnemyTargeting.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Targeting/EnemyTargeting.cs) | 타깃 저장·무효화 이벤트 |
| [MovingEnemyController.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Controllers/MovingEnemyController.cs) | 행동 조건·사거리 결과·이동 경로 |
| [EnemyAbility.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Abilities/EnemyAbility.cs) | 후보 거리 사전 배제·비활성 Collider 검사 |
| [EnemyHealAbility.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Abilities/EnemyHealAbility.cs) | 회복 대상 검색·중복 제외·재검색 간격 |
| [EnemyAnimation.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Presentation/EnemyAnimation.cs) | Animator 캐시 및 활성화 동기화 |
| [EnemyNetworkController.Checkpoint.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Networking/EnemyNetworkController.Checkpoint.cs) | 복원 후 Animator 캐시 동기화 |
| [EnemyMovement.cs](../../../../../Assets/Scripts/Features/Enemy/Runtime/Movement/EnemyMovement.cs) | 위치·거리 조회 및 이동 계산 |
| [SpritePhysicsShapeSync.cs](../../../../../Assets/Scripts/Shared/Physics/SpritePhysicsShapeSync.cs) | Sprite 방향·형태에 따른 물리 모양 변경 |
| [DirectionalCapsuleCollider2D.cs](../../../../../Assets/Scripts/Shared/Physics/DirectionalCapsuleCollider2D.cs) | 방향별 Capsule 설정 적용 |

Profiler 이미지: [Shape 변경 전](2026-11-14-enemy-performance-evidence/01-shape-before.png) · [Shape 변경 후](2026-11-14-enemy-performance-evidence/02-shape-after-700.png) · [대표 갱신 전](2026-11-14-enemy-performance-evidence/03-refresh-before.png) · [대표 갱신 후](2026-11-14-enemy-performance-evidence/04-refresh-after.png) · [게임/Editor 분리](2026-11-14-enemy-performance-evidence/05-editor-vs-player.png) · [초기 700개체](2026-11-14-enemy-performance-evidence/06-early-700.png)

성능 최적화는 이 기록 시점에 중단했다. 다음 재개 시 비교 조건을 먼저 고정하고, 게임 빌드의 기준 성능과 목표 프레임 시간을 합의한 뒤 남은 병목을 다시 측정한다.
