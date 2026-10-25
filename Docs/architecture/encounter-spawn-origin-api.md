# Encounter 생성 origin API

현재 단계: B8 기반 계약과 Scene 실행 대상 연결 구현. 실제 포인트 밖 Enemy 생성 연결은 다음 단계이며, 이 문서의 목표를 완료로 해석하지 않는다.

## 현재 구현

- `EncounterRequest.Origin`은 Automatic, AuthoredPoint, DynamicSource, WorldPoint, WorldArea를 구분한다. 기존 호출은 Automatic으로 유지된다.
- `SpawnArea`는 XY 중심·폭·높이와 표현 Z를 담는다. Grid 경계와 지형/NavMesh 유효성은 같은 개념이 아니다.
- `CauseEventId`는 요청 GUID와 별개의 원인 사건이다. origin·전달 모드·원인까지 payload에 포함해 같은 요청 ID의 다른 의도를 충돌로 처리한다.
- `EncounterSpawnSourceRegistry`는 Host Match의 논리 source ID·등록 세대·위치·취소 lease를 관리한다. source 제거와 Match 취소는 대기 수명을 닫으며 이미 생성한 적의 삭제 권한을 뜻하지 않는다.
- 같은 논리 ID를 다시 등록해도 새 generation을 받는다. 삭제 source 또는 이전 Match를 참조하는 늦은 요청은 새 source로 바뀌지 않는다.
- 공통 `EncounterRequestService`는 명시적 origin을 지원하는 `IOriginAwareEncounterExecutor`와 권위 resolver를 요구한다. 없는 연결을 자동 지점 생성으로 대체하지 않고 UnsupportedOrigin/OriginUnavailable로 거부한다.
- `EncounterSceneConfiguration`은 실제 Enemy 생성기를 제작 참조로 지정한다. 현재 Initial_Stage는 EnemySpawnRoot prefab 인스턴스를 사용한다. 기존 다른 제작 Executor는 제거하지 않았다. 재로드 뒤 App의 persistent consumer에 새 Scene 참조를 전달한다.
- standalone `EncounterRuntime`의 자동 검색은 활성 Scene에 생성기가 정확히 하나일 때만 허용한다. 여러 개면 명시 연결이 필요하다.

## 다음 연결과 소유권

Core는 새 Spawner를 만들거나 Instantiate하지 않는다. Enemy Planner의 분대 구성·공간 배분, 기존 EnemySpawnExecutor의 NGO 생성·분대·생존/사망/제거·CaptureEnemyPositions·Match 정리를 재사용한다.

명시 위치/영역은 Feature의 제한된 후보·지형 검사 후 실행해야 한다. 동적 source를 임시 SpawnPoint ID로 위장하지 않는다. GroundEmergence/AirDrop은 전달 모드 계약이며 실제 대기/착지 연출이 없으면 명시적으로 미지원 처리한다.

다음 단계에서는 실제 생성·공통 추적과 source 제거/pending 취소를 검증하고 Game Debugger에 요청 GUID·원인·origin/source·계획/실제 결과를 연결한다. 나무를 심는 AI·나무 콘텐츠·투하 VFX/최종 정책은 담당 Feature 범위다.

## 현재 검증

`b8-origin-foundation.xml`: Unity 6000.3.23f1, 종료 0, 11/11. source 세대/취소·권위·origin 충돌/재전송 계약 4개, 기존 요청 회귀 6개, 실제 Initial_Stage 재시작/Hub 복귀 1개. 마지막 검사는 재로드 전후 제작된 실행 대상과 consumer 참조가 같은지 직접 확인한다. 포인트 밖 실제 생성 성공 증거로 확대하지 않는다.

Scene은 Unity API로 구성한 뒤 필요한 참조/필수 WeatherChangeEvent component만 보존했다. headless 저장이 만든 Canvas 크기/배율·기타 제작 값 변경은 원래 값으로 되돌렸다.
