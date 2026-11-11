# Enemy 생성 귀속 초기화 경고와 정상 로그 정리

PR: 발행 전  
작성일: 2026-11-11 · 담당: Core / 양현석  
기준 main: `ba553257c1c617eee48f33da4bbdb61a775ebcf0`

## 목적과 수정 범위

적을 만들 때 `SetSimulationMatch`가 아직 NGO에 연결되지 않은 NetworkVariable 두 개를 먼저 써서 초기화 경고가 반복됐다. 생성 전 자료 준비는 유지하고, SDK가 변수를 초기화한 `OnNetworkSpawn`에서 기존 통지 전에 반영한다. Client는 서버가 최초 Spawn 패킷에 넣은 값만 읽는다.

또한 개체마다 출력되던 NavMesh 성공 로그와 요청마다 반복되던 장문 생성 결과 로그를 제거했다. 요청 반환 문자열, 구조화된 계획/실제 결과, 통계·Timeline, 실패 경고/오류와 선택적 편성/예산 진단은 유지했다.

사용자가 이 NGO·로그 접점의 Enemy 소스 수정을 명시적으로 허용했다. 물리/Collider/CCD/AI·이동·스폰 위치/편성 정책과 Scene/Prefab/설정은 변경 범위가 아니다.

## 코드 읽는 순서와 자료 흐름

1. 기존 `EnemySpawnExecutor.Execute`와 Checkpoint 재생성 호출자는 계속 `SetSimulationMatch → NetworkObject.Spawn`을 호출한다. 호출자는 수정하지 않았다.
2. [EnemyNetworkController.Simulation.cs](../../../Assets/Scripts/Features/Enemy/Runtime/Networking/EnemyNetworkController.Simulation.cs)의 `SetSimulationMatch`는 일반 필드에 Match/Executor 값만 준비한다. 이미 Spawn된 적의 귀속 변경은 기존처럼 거절한다.
3. 기존 `EnemyNetworkController.OnNetworkSpawn → NotifySpawned`에서 서버가 준비한 두 값을 NetworkVariable에 먼저 반영하고 준비 값을 비운다. 그 뒤 `SpawnedInMatch` 구독자가 Match·owner를 읽어 시간에 연결한다. 이후 NGO가 최초 Spawn 패킷을 작성한다.
4. Client의 같은 통지에서는 서버 값을 덮어쓰지 않는다. Despawn 후 다음 Spawn에도 새로 준비한 귀속을 사용한다. 네트워크/저장 형식·공용 API 시그니처는 바꾸지 않았다.
5. [EnemyNavMeshPathfinder](../../../Assets/Scripts/Features/Enemy/Runtime/Movement/EnemyNavMeshPathfinder.cs)는 경로와 성공 여부만 반환하고 실패 진단은 남긴다. [EncounterRuntime.ExecutePlan](../../../Assets/Scripts/Features/Enemy/Encounter/EncounterRuntime.cs)은 Console에 복사하던 결과 문자열을 계속 호출자와 진단으로 반환한다.

NGO 2.13.3 패키지에서 `InternalOnNetworkSpawn → InitializeVariables → OnNetworkSpawn`과 `AuthorityLocalSpawn → SendSpawnCallForObject` 순서를 대조했다. SDK 비공개 필드를 직접 호출하거나 경고를 전역 필터하는 방식은 사용하지 않았다.

## 영향과 중요한 분기

- 기존 일반 생성·Checkpoint 재생성 API의 호출 순서를 유지한다. Spawn 전 public 자료가 실제 네트워크 값으로 반영됐다고 가정하는 새 호출자는 추가하지 않는다.
- Host의 시간 등록 통지 전에 Match·owner가 준비된다. Client는 최초 수신 값을 그대로 사용한다.
- 실패/예외 로그, 실제 생성 수·좌표·구성, Game Debugger 관측 자료는 유지한다.
- 실제 Pool 구현/종류별 reset 및 Enemy 물리·AI 최적화는 별도 범위다. 이 변경을 전체 성능 병목 해결로 쓰지 않는다.

## 확인 결과

Unity 6000.3.23f1 / NGO 2.13.3, 위 main에서 분리한 최종 작업 트리로 검사했다. 코드 커밋 후에는 검사한 소스 해시와 동일한지 대조한다. 문서 번호 보완만으로 Runtime 검사를 반복하지 않는다.

| 검사 | 실제 결과 |
| --- | --- |
| 생성 전 준비·실제 UTP Host/Client 최초 통지와 재생성 | `EnemySimulationSpawnTests` 2/2 통과. 서로 다른 귀속으로 두 번 생성하며 최초 통지 값 일치, Spawn 후 변경 거절 |
| 실제 Initial_Stage 두 독립 Play 세션 | `EnemySpawnRuntimeTests` 2/2 통과. 각 세션 자동/수동 요청 뒤 적 18개·완료 요청 2건·실제 생성 진단 18건. 전 개체의 Match/Executor/공용 시간 원본 확인 |
| 해당 두 최종 실행 로그 | NGO 초기화 경고 0줄, 경로 성공 로그 0줄, 관리 예외 0줄. 정상 결과 장문 복사 출력 없음 |
| 저장소 검사 | Python 50/50 통과, Unity `.meta` 오류 0, 변경 diff 공백 검사 통과 |
| GitHub CI | PR 생성 후 최신 head 결과를 기록 |

두 실제 Stage Match는 `5a0d866b0a44438ca7a24dd408e838d8`과 `4ded94d3f6da401b9093b9222fa8ef48`이었다. 요청의 수량 상한과 편성 단위 때문에 실제 생성 수를 고정 20으로 가정하지 않고 실제 진단 값과 생성물을 대조했다.

[공유 검증 요약·검사 이름·소스 해시](ngo-spawn-initialization-20261111.results.json)에서 위 결과를 확인할 수 있다. 재실행은 Unity CLI에서 `--filter EnemySimulationSpawnTests`와 두 `EnemySpawnRuntimeTests` 메서드를 선택한다.

원본 XML/로그·소스 SHA256·실패한 검증 준비 기록은 로컬 `Docs/Local/Review/CoreExecution-20261111/P0a/`에 보관한다. 요약 원본은 `verified.json`, 최종 XML은 `identity-runtime.xml`·`stage-entries.xml`이다. 로컬 기록을 팀원이 열 수 있는 공개 링크로 가장하지 않는다.

검사 준비 과정의 SDK internal 멤버 접근·Play 진입 전 메시지 등록·domain reload 이전 fixture/closure·한 검사 내 Play 반복 루프 문제는 검사 코드에서 보완했다. 해당 실패 XML을 성공 결과에 섞지 않았으며, 운영 코드를 null 보호나 경고 필터로 우회하지 않았다. 새 테스트는 공개 SDK와 실제 Play Mode를 사용한다.

## 남은 것과 되돌리기

- 운영 전체 Save는 별도 Weather writer 등 통합 범위다. 이 PR에서 전체 저장 인수를 완료했다고 표시하지 않는다.
- 테스트의 두 NetworkManager는 같은 Editor 프로세스의 실제 UTP Host/Client다. 두 Steam 계정의 인터넷 연결이나 독립 실행 파일 두 개의 인수와 구분한다.
- 문제가 생기면 이 PR의 귀속 초기화/로그 변경을 되돌린다. 저장 형식·Prefab 직렬화 변경은 없고, 되돌리면 기존 초기화 경고/성공 로그가 다시 발생한다.
