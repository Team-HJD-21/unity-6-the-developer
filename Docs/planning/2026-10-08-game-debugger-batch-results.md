# Game Debugger 확장 배치 결과

기준일: 2026-10-08 · 브랜치: `feature/core` · 기준 HEAD: `06fd1090`

[배치 계획](2026-10-08-game-debugger-batch-plan.md) · [사용/API 가이드](../tooling/game-debugger-guide.md)

## 구현 범위

| 배치 | 결과 |
| --- | --- |
| G0 | source/clock/Match/revision/request·실제 생성/제거/실패 계약, Scene 공통 snapshot 요청 |
| G1 | UI Toolkit 상위 창·USS·6개 패널. 기존 Battlefield 창/Scene Overlay 유지 |
| G2 | 배속/직접 입력/Pause/Resume/Step/Restore, 소유한 시간 복원. fixed step 자동 변경 없음 |
| G3 | authority/clock·수집 상태·터렛 HP/작동/전력·기하 지표, ID/OOB/소스/타깃 존재 검사 |
| G4 | 성공 Spawn/실제 Despawn 이벤트, 요청별 planned/actual, bounded buffer/recorder·archive, 네 source |
| G5 | 게임/실제 clock bin, 생성·제거 histogram 및 9개 기타 metric, source/preset/type/point 필터·A/B |
| G6 | 타임라인·검색·북마크·터렛 선택·실패/파괴/OOB 정지·재무장 |
| G7 | JSON/CSV·schema 검증·readonly import/compare·metadata·Capture preset, reload session 기록 보존 |
| G8 | CLI 컴파일·집계/창 구성 테스트·Initial_Stage 실제 Host/스폰/topology 연결 검증. **시각 QA/물리 충돌/영상은 미실행** |

구현의 복구 경계는 ① Runtime 계약/요청·관측 ② 수집/시간 scope/집계 ③ 창/차트/패널·Overlay ④ 테스트·문서다. 같은 순서로 적용한다. 게임 정책·UnityEditor를 Core Domain에 추가하지 않았다. Scene YAML/프리팹·팀원 터렛 정책은 수정하지 않았다. `Validation/`의 기존 파일은 건드리지 않는다. 원격 push/Issue 변경/PR은 이 배치에서 하지 않는다.

| 복구 경계 | 로컬 커밋 |
| --- | --- |
| Runtime 요청·진단 계약 | `484dd038` |
| 수집·집계·Export·시간 제어 | `84fffb43` |
| 상위 창·차트·Timeline·Compare·Overlay | `ff01a51c` |
| 테스트·문서·인수 기록 | 이 문서를 포함한 후속 커밋 |

## 코드 읽는 순서

1. `Contracts/IGameDiagnosticsSource`, `GameDiagnosticEvent`, `EncounterExecutionContext` — Feature와 Editor 경계.
2. `EnemyNetworkTestLauncher.TryRequestCurrentEncounter/TryRequestEncounter/Emit` — 네 요청의 source와 Match 수명.
3. `EncounterRuntime.TrySpawn → EnemySpawnExecutor.Execute/Observe/UntrackEnemy` — 정책 유지, 실제 사실 기록.
4. `GameDebugger/Recording/GameDebuggerSession.Update/Accept/Sample/SaveAndDetach` — 이벤트/표본/종료 기록.
5. `GameDebugRecorder.Accept`, `GameDebugStatistics.Build`, `GameDebugChart.SetData/Draw` — 중복/손실·집계·그림.
6. `GameSimulationControls`, `TimeScaleLease` — 게임 서비스와 분리된 Editor 시간 소유권.
7. `GameDebuggerWindow.CreateGUI/BuildPage`, inspection panels, `GameDebugExport` — 사용·보고 경로.

## CLI 검증 증거

Unity `6000.3.23f1` / Unity CLI / 2026-10-08. 결과 XML은 로컬 `%TEMP%`이며 Git에 포함하지 않는다.

| 검사 | 결과 | 파일 |
| --- | --- | --- |
| 전체 EditMode suite | 37개: **36 성공, 실패 0, Explicit 1 제외** | `game-debugger-final-editmode.xml` |
| 마지막 보강 후 GameDebuggerTests | **10/10 성공** | `game-debugger-final-unit.xml` |
| 명시적 Initial_Stage 통합 | **1/1 성공** | `game-debugger-final-integration.xml` |

실제 Scene 통합에서는 자동 첫 Host/Spawn 및 `SceneCollision` adapter, `RuntimeUI`, `DebugWindow`의 같은 경계에서 Normal/10 요청이 각각 성공했다. 당시 snapshot은 **삼각형 8개·외곽 6개**였다. 빈 snapshot/다른 Match 요청 거부, 실제 NetworkObject Despawn의 제거 사실, 창 없이 수집, 무장한 실패의 로컬 Pause, 정상 종료 마커·종료/reload 후 archive 보존을 확인했다. 이것은 전체 전투/승패를 인증하지 않는다.

초기 추가 테스트에 잘못된 `MonsterBase` 타입 참조가 있어 컴파일 오류를 발견하고 실제 `EnemyController`로 수정했다. 최종 검사에는 해당 오류가 없다. 전체 suite를 배치마다 반복하지 않고 순수 테스트와 명시적 Scene 검사를 좁게 수행했다.

## 마지막 사람이 확인할 화면

- 새 창의 좁은 dock/넓은 창과 USS, histogram hover/A-B 축, 종료된 기록의 배지.
- 실제 Player의 Scene 충돌과 Game UI 클릭이 올바른 source로 기록되는지.
- 시간 배속·한 프레임 Step, turret destroyed/OOB 조건 정지, Capture 화면.
- 동일 Build의 터렛 실제 전투·Enemy 타깃/피해·지역 해금·승패는 각 Owner와 통합 Gate에서 확인.

PlanId/후보 점수/처치/피해/전술 우세 지표는 없는 계약을 추정해 채우지 않았다. 본 결과가 기존 2차 문서의 “실제 생성·authority 관측 미연결” 부분을 대체하지만, B4/B5의 전체 Feature 설계·타깃·idempotency 및 전투 Gate까지 닫은 것은 아니다.
