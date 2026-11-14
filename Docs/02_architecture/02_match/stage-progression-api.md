# Stage 점유와 보스 진행 연결

`StageProgression`은 `MatchCoordinator`가 소유하고, 실제 Scene의 `RegionGridSnapshot.CaptureRatio`를 읽는다. 현재 셀 점유와 최고 도달률은 구분한다. 최고 도달률은 이미 발생한33/66 사건을 점유 상실로 취소하지 않기 위한 값이며, 일반 스폰 난이도 정책을 대체하지 않는다.

## 실행 순서

1. `InitialStageCompositionRoot.BindStageProgression`이 성장/비환급 기록을 읽은 뒤 Mission 실행 ID와 Field 시계를 연결한다.
2. Root Update가 CU 패배·구조 실패를 먼저 처리하고 현재 점유 스냅샷을 `Pump`한다.
3.33%를 넘으면 `IFieldBossSpawnService.TrySpawnFieldBoss`에 실행별 안정된 사건 ID를 준다. true와 실제 EntityId를 모두 받아야 생성 확인이다.66%도 같은 경로이며 한 번에100%가 되어도33→66 순서다.
4. 생성 실패는 같은 사건 ID로1 Field초 뒤 재시도한다. 미출현 취소·일반 적 대체·허위 성공은 없다. 이미 확인된 첫 사건은 다시 만들지 않는다.
5. 현재 점유100%와 두 실제 생성 확인을 모두 만족하면 `StageProgress.CanEnterFinalBattle`이 true다. 두 중간보스의 처치는 진입 조건이 아니다.
6. 실제 최종 Boss Scene/연출 조립부가 같은 Match에서 보스와 도착 준비를 마친 뒤 `ConfirmFinalBossReady(match, actor)`를 호출한다. 이전 Field만 보류하고 Player/공용 낮밤은 기존 공용 시계를 계속 사용한다.
7. Enemy 권위 HP 원본이 실제 사망을 확인할 때 `ReportStageEnemyDeath(match, actor)`를 호출한다. 등록한 최종 보스의 실제 죽음만 Coordinator의 Victory로 이어진다. UI·Despawn·Scene 해제는 죽음으로 제출하지 않는다.

## Enemy 생성 구현자가 지킬 경계

`IFieldBossSpawnService`는 현재 Scene에 정확히 하나이거나 실제 Encounter consumer가 구현한다. 동일 사건의 응답을 재사용하되 true는 실제 생성 사실이 확인될 때만 반환한다. 월드 rollback으로 사라진 개체의 오래된 응답을 재사용하면 안 된다. 실제 공통 Enemy 생성/NGO·식별자·관측·저장 경계를 사용하며 편성을 다시 무작위로 뽑거나 정규 예산을 임의 소비하지 않는다.

33/66 개체 Definition과 유효 진입 위치는 제작 데이터로 필요하다. 해당 시점에도 점유 밖에서 생성 가능한 NavMesh 지점을 미리 보장해야 한다. 모든 후보를 점유한 뒤 일반 포인트 중 하나를 임의 선택하는 방식은 불가하다. 생성 실패를 최종 진입이 추월하지 않지만, 이 방어 자체가 콘텐츠/위치 보장을 대신하지는 않는다.

## 저장과 표시

`world/stage-progress`는 월드 section이다. 점유율/최고율·실제 생성 ID·확인한 죽음을 같은 시점으로 복원하고 rollback한다. 개인 성장·구조 시간·안전장치는 이 section에 넣지 않는다. 저장의 실행 ID가 현재 비환급 기록과 달라지면 거부한다. 새 필수 section을 포함하지 않은 과거 저장은 명시 migration 없이는 이어가지 않는다.

HUD는 `InitialStageCompositionRoot.StageProgress`의 읽기 값을 사용할 수 있다. Game Debugger의 Checkpoint 패널은 현재/최고 점유·33/66 실제 생성·최종 진입 조건·연결 대기 이유를 표시한다. Debugger가 별도 점유율이나 생성 성공을 만들지 않는다.

## 현재 구현/검증과 미연결

정책·world codec·Coordinator/Root 연결과 자동6개를 검증했다. 실제 Stage_1 진입에서 CU1/201셀(0.4975%)·Player1·게임 진행 정상·`FieldBossContentNotConnected`를 확인했다.

현재 카탈로그에는19종의 적이 있으나33/66에 사용할 확정 Definition이 지정되어 있지 않다. 실제 Boss Scene도 제공되지 않았다. 따라서 실제 Field Boss 생성 서비스·최종 Scene/컷씬·최종 HP 사망의 Feature 연결은 아직 없다. 위 API의 존재를 실제 보스 플레이 완료로 해석하지 않는다. Reference의 과거 보스 Prefab은 현행 콘텐츠로 임의 승격하지 않았다.
