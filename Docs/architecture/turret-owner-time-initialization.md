# 터렛 생성·복원 시 소유 Controller와 공용 시간 연결

`FieldTurretFactory`가 만든 터렛은 전력 공급자와 시간·발사체 소유자가 같은 Controller여야 한다. Core는 Controller에 Match 시계를 연결하고, 개체 생성은 기존 FieldTurret Factory가 맡는다.

## 복원 순서

1. `FieldTurretController.PrepareRestore`가 복원할 데이터를 검증하고 적용 작업을 준비한다.
2. 적용 시 `ApplyWorld`가 기존 actor를 정리하고 `FieldTurretFactory.CreateForCheckpoint`에 **자신을 owner로 전달**한다.
3. `CreateCore`가 비활성 인스턴스를 만들고 `PrepareInitialization(data, powerSource)`를 먼저 호출한다.
4. `PrepareInitialization`은 전력 공급자와 함께 `_turretController`도 전달받은 Controller로 연결한다. 첫 Awake/등록보다 먼저 끝나야 한다.
5. `RestoreActorState`가 남은 냉각 시간을 `owner.FieldSeconds + CoolingRemaining`으로 적용한다. 이후 발사체도 같은 Match 시계·종료 수명을 받는다.

```text
Core ──BindFieldSimulation──> FieldTurretController
                                  │ Factory에 owner 전달
                                  ▼
                         PrepareInitialization
                         ├─ 전력 공급자
                         └─ 시간·발사체 Controller
                                  ▼
                         Awake / 등록 / 상태 복원
```

## #624 리뷰에서 보완한 부분

이전에는 `_configuredPowerSource`만 저장했다. `ConfigureSceneActivation`이 전력 공급자가 있다고 판단해 Controller 탐색을 건너뛰면서 `_turretController`가 비어 있었다. 그 결과 냉각·진행 여부가 Unity 시간으로 fallback하고, 새 탄환의 공용 시간 연결도 건너뛰었다.

Factory 초기화에서 전달받은 Controller를 바로 연결하도록 수정했다. 냉각·전력 규칙이나 저장 형식을 새로 바꾸지 않는다.

확인 기준은 냉각5초를 가진 실제 Factory 재생성이다. 필드 시계만 정지하고 Unity 프레임을 진행해도5초가 유지되어야 한다. 재개 후 공용 시간2초가 지나면3초가 남아야 하며, 복원 뒤 생성한 발사체의 시계·정지 상태도 소유 Controller와 같아야 한다.

이 확인은 터렛 actor 복원 경계이며 운영 전체 체크포인트/다른 Feature writer의 인수를 대신하지 않는다.

Unity 6000.3.23f1 PlayMode에서 수정 전 두 회귀가 실패하는 것을 확인했고, 수정 후 `FieldTurretInitializationTests` 전체44/44가 통과했다. 정상 냉각5초 저장을 복원한 뒤 Unity 프레임만 진행할 때의 냉각 유지와 새 발사체의 시계/물리 정지·재개를 포함한다.
