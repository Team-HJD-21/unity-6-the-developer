# Core 지역·스폰·보스·협동 복구 배치

2026-11-12 · **사용자 승인 완료 / 구현·통합 QA 전** · 코드 대조 기준 main `9acf881ba`.

이 문서는 승인한 게임 규칙과 다음 구현 순서를 기록한다. 코드가 이미 제공하는 API와 앞으로 연결할 동작을 구분한다. 아래 정책 승인은 다른 파트 소스 수정 승인이나 PR 발행 지시를 대신하지 않는다.

## 확정한 게임 규칙

| 항목 | 적용할 규칙 |
| --- | --- |
| 정적 확보 | 해금·가동·점유를 구분한다. 정적 구역마다 성과·우세와 성과 달성 후 확보 유지 시간을 합쳐 60초 누적한다. 완료 후 다음 구역을 자동 해금하며 다음 구역을 미리 점유하지 않는다. |
| 정적 상실 | 적 우세가 지속되면 구역 단위로 점유를 회수한다. 터렛 OFF·냉각·플레이어 이탈·빈 구역이라는 이유만으로 상실하지 않는다. 해금은 유지하고 재확보에는 같은 60초 규칙을 쓴다. 고립만으로 여러 구역을 연쇄 회수하지 않는다. |
| 동적 시작 | CU를 체력/전력 관리자와 별개의 위치 노드로 포함한다. CU 주변 초기 점유 셀을 제공한다. 초기 셀과 삼각형 투영 후보를 합쳐 CU 거점에 상하좌우로 연결된 셀만 실제 점유한다. 살아 있는 CU의 초기 셀은 유지하고 나머지는 활성·비파괴 노드와 연결 상태로 다시 계산한다. |
| 일반 스폰 위치 | 점유 셀 내부와 주변 SpawnPoint를 제외한다. 중심만 검사하지 않고 스폰 반경+조절 가능한 월드 좌표 여유 거리가 점유 셀에 닿는지 판단한다. 계획 뒤 점유가 바뀌면 실행 직전에도 확인한다. 수동 OFF를 점유 정책이 덮어쓰지 않는다. |
| 스폰 난이도 | 기존 점유 비례 예산·난이도 증가 방향과 편성/분포 정책을 유지한다. 위치 차단을 전체 예산 재설계와 섞지 않는다. 점유 안의 적 관측 자료는 압력 판정에 남긴다. |
| 추가 스폰 | 소환·투하 등 추가 스폰은 점유 안에서도 가능하다. Grid·NavMesh·실제 배치 안전 검사는 유지한다. 위치가 WorldArea라는 이유만으로 일반 스폰에 특례를 주지 않는다. |
| 필드 중간보스 | 33%·66%에서 각각 한 번, 점유 밖 Field에 생성한다. CutScene과 별도 Scene 전이는 없다. 이전 개체가 살아 있어도 다음 개체를 생성한다. 점유율 재상승으로 증식시키지 않는다. 중간보스 처치를 100% 최종 진입의 추가 필수 조건으로 두지 않는다. |
| 중간보스 저장 | 등장 전·처치 후 저장 방향을 유지한다. 사건과 실제 개체를 함께 복원한다. 위치가 없으면 미생성 요청을 보류하며 요청 접수만으로 생성 성공 처리하지 않는다. |
| 최종 보스 | 점유 가능한 대상 셀 100% 확보 후 자동 컷씬·별도 보스 Scene으로 1회 진입한다. 같은 Match를 유지하며 현재 참가자 전원을 함께 이동시킨다. 구조 대기/실패 처리를 먼저 끝내고 전환한다. |
| 전환과 시간 | 보스전 전 저장·Scene 준비·컷씬 동안 공용 게임 시간을 정지한다. 보스전에서는 공용 시간·Player·보스를 진행시키고 기존 Field의 생산·전투·투사체·점유 시도만 보류한다. 기존 Field CU가 보류 중인 전투에서 피해를 받지 않게 한다. |
| 협동 사망 | 한 명이 쓰러지면 구조, 전멸 또는 구조 예산 소진이면 안전장치로 복귀한다. CU 파괴는 패배를 우선한다. 겹친 실패로 안전장치를 두 번 쓰지 않는다. |
| 구조 예산 | 팀 공용 잔여 시간을 구조 대기 중에만 소모한다. 성공 뒤 잔여량을 유지하고, 새 사망·보스 처치·저장/복원으로 충전하지 않는다. 새 Mission 실행을 명시적으로 시작할 때 초기화한다. 소모한 구조 시간과 안전장치 횟수는 과거 전장 스냅샷으로 되돌리지 않는다. |
| 구조 동작 | 기존 상호작용 입력을 유지해 구조하고 성공 시 최대 HP의 일정 비율로 복귀한다. Host가 유효 참가자·거리·시간·현재 상태를 검증한다. Player의 실제 조작/표현 수정은 담당자에게 먼저 통보한다. |

공용 게임 시간은 기존 정지·배속을 따른다. 보스전 중 Field 보류를 공용 시계 전체 정지로 대신하지 않는다. 입력 해제·거리 이탈·구조자 사망/종료 시 구조 요청 수명을 닫으며, 피격마다 무조건 구조를 취소하는 별도 규칙은 추가하지 않는다.

### 승인한 시험 기본값

| 값 | 기본값 | 구분 |
| --- | --- | --- |
| 정적 확보 | 구역별 60초 | 기존 확정 규칙 |
| 확보 시도 활동 중단 유예 | 180초 | 기존 조절 가능한 시험값. 지속 교전 중인 시도를 시작 180초에 무조건 종료하는 뜻이 아님 |
| 확보 완료 후 상실 | 연속 열세 30초 | 이번에 승인한 조절 가능한 시험값. 열세 해소 시 초기화 |
| 동적 초기 점유 | CU가 있는 유효 셀 1개 | 이번에 승인한 기본안. 맵의 명시적 시작 셀 집합으로 조절 |
| 협동 구조 예산 | 팀 공용 60초 | 이번에 승인한 시험 총량 |
| 구조 입력 유지 | 3초 | 추가 답변으로 승인한 시험값 |
| 구조 성공 HP | 최대 HP의 30% | 추가 답변으로 승인한 시험값 |
| 안전장치 | 3회 | 기존 제품 규칙 |

시험 기본값은 이번 개발·QA 기준이며 출시 밸런스 인수 완료가 아니다. 일반 스폰의 추가 여유 거리와 성과/압력 가중치는 데이터·Game Debugger에서 조절한다. CU가 Grid 밖/점유 불가 위치이면 임의 셀을 점유시키지 않고 설정 오류를 표시한다.

**분모도 구분한다.** 보스 진행률은 점유 가능한 대상 셀 중 확보한 비율이다. 기존 Enemy 예산의 `CalculateTotalBudget`는 `Geometry.TotalCellCount`를 분모로 쓰므로 그대로 유지한다. 새 대상 셀 자료를 추가했다고 예산 분모까지 바꾸지 않는다. 해금이 섞였던 입력을 실제 점유로 분리하는 영향은 Enemy와 함께 인수한다.

## 현재 코드와 먼저 맞춰야 할 것

- `RegionSceneBinding.TryUnlockThroughZone`는 현재 해금 셀도 `CapturedCells`에 넣는다. 이 값을 그대로 스폰 차단에 연결하면 확보 전 구역까지 차단된다. `RegionAccessPolicy.CanUseAt`·복제·Checkpoint·Enemy 소비를 함께 정리해야 한다.
- `SpawnPointPlanningSession`은 Player/Turret 객체가 있는 셀 등을 제외하지만 영토 점유 셀과 스폰 반경의 겹침은 검사하지 않는다. `EnemySpawnExecutor.Execute`도 일반 지점 반경의 실제 위치를 추첨한다. 두 단계가 같은 제한을 사용해야 한다.
- SpawnPoint는 Scene에 배치한 지점을 요청 때 다시 수집한다. 점유가 바뀔 때 자동으로 이동하거나 외곽 지점을 새로 생성하는 시스템은 현재 없다. 후보가 없으면 점유 안으로 우회하지 않고 사유를 보고한다.
- `StoryModeRules.Evaluate`의 현재 판정은 CU 파괴 패배다. 중간보스 사건·최종 보스 승리까지 구현됐다는 뜻은 아니다.
- `AppRoot.ReplaceGameplaySceneAsync`는 현재 Match를 끝낸다. 보스 추가 Scene을 여는 데 그대로 사용하지 않는다.
- `PlayerInfo`에는 `onDeath`와 생존/피해 처리가 있지만 일반 협동 구조 성공 API는 없다. Checkpoint 복원을 일반 구조 API로 오용하지 않는다.
- 실제 Weather writer 등 필수 저장 원본은 담당자 제공/연결이 필요하다. 가짜 writer로 저장 Gate를 통과시키지 않는다.

## 기존 배치에서 완료한 것

| ID | 완료 범위 | 남은 경계 |
| --- | --- | --- |
| P0a | [#691](https://github.com/Team-HJD-21/unity-6-the-developer/pull/691): NGO 초기화 경고·정상 로그 정리, QA/CI 후 main 반영 | Enemy 물리·AI·Pooling 완료가 아님 |
| P3 | [#693](https://github.com/Team-HJD-21/unity-6-the-developer/pull/693): Client 낮밤 잔여시간·phase anchor | Weather writer와 실제 환경 편성은 별도 |
| P4 | [#694](https://github.com/Team-HJD-21/unity-6-the-developer/pull/694): 로컬 카메라 선택·복귀·수명 | 실제 보스 컷씬/Weather 소비자·시각 인수는 별도 |

이 완료 범위를 다시 첫 구현 순서에 넣지 않는다. 최신 통합으로 영향이 생긴 경로에만 회귀 검사를 적용한다. 현재 원격 Issue 상태를 새로 조회하거나 종료한 목록은 아니다.

## 11월 12일 Core 선행 실행 결과

팀원 원본을 보류한 실행에서는 P9의 CU 공간 노드·시작 셀·4방향 연결·Host 설정 UI·시작 설정 writer를 먼저 구현했다. 정적 `CapturedCells` 의미와 Enemy 소비자를 함께 바꿔야 하는 P1을 분리해 강행하지 않았다. P9는 기존 지역 snapshot 형식을 유지하므로 이 범위에서 독립 검증했다. [실행 결과와 남은 연결](2026-11-12-cu-connected-regions-results.md)을 참고한다.

Core 도메인 검사 14/14, 바인딩·설정 저장 검사 6/6, 실제 Initial_Stage Play·Editor UI·취소 검사 1/1이 통과했다. 관련 회귀 48개 중 기존 CU 규칙을 가정한 Player 테스트 1개가 실패했으며, **사용자가 해당 테스트 한 개만 수정하도록 승인한 뒤 재검사 1/1이 통과**했다. Player 게임 코드·Scene·Prefab은 수정하지 않았다. 새 기능의 실제 두 Client 인수, 전체 전장 Save, 물/통행 가능 대상 셀 입력은 이 결과에 포함하지 않는다.

## 다음 실행 순서와 PR 경계

기존 P 번호는 요구 추적용으로 유지한다. 아래 PR 묶음은 기능 단위의 목표 순서이며 최종 PR 개수를 고정하지 않는다. 계획 작성 당시에는 문서만 갱신했고, 이후 착수한 P9의 실제 결과는 위 실행 기록과 구분한다. 팀원 원본 수정이 필요한 배치는 승인 전까지 보류한다.

| 순서 | 배치 / PR로 만들 결과 | 주요 Core 진입점·호출부 | 선행과 다른 파트 경계 | 통과 기준 |
| --- | --- | --- | --- | --- |
| 1 | P1+P7a: 지역 사실 분리와 일반 스폰 제한 | `RegionSceneBinding.BindMatch/TryUnlockThroughZone`, `RegionAccessPolicy.CanUseAt`, 지역 snapshot/codec/Checkpoint, Encounter 요청 | Enemy 후보/실행 소비 수정 사전 통보. Level의 대상 셀 정의 필요. 의미 전환은 같은 완결 PR에서 연결 | 해금만 하면 사용 가능·미점유·스폰 유지, 확보 뒤 반경+여유 거리 차단, 실행 직전 변경·전부 제외·Client·Save 왕복 |
| 2 | P2a: 일반/소환 공통 수명 | 실제 생성 → Match/시간/Grid/관측/저장 등록 → 제거/해제 | Enemy `EnemySummonGenerator.Spawn` 등 사전 통보. 이동·편성은 Enemy 소유 | 소환도 누락 없이 등록/저장/제거, 사망과 Despawn 구분, Match 종료/NGO 귀속. 실제 Pool은 별도 |
| 3 | P6a: 실제 전투·위협 관측 | Stage 조립·등록 수명 → 구역별 관측 → Debugger/Timeline | Enemy 실제 적용 피해, Player 피해 경계. Turret 기존 상태/체력 알림을 먼저 재사용 | 무효 피해·회복·과거 Match·중복 사망 제외, 실제 게임 성과/압력/위치 관측. 가짜 테스트 자료만으로 인수하지 않음 |
| 4 | P6b: 정적 확보·상실·재확보·다음 해금 | 관측+공용 clock → 구역 시도/상실 → 확정 셀 → 다음 해금 | 1·3. 터렛 해금/전력 원본을 Core가 복제하지 않음 | 60초·확보 유지·180초 시도 유예·연속 열세 30초 구분, OFF/냉각/이탈만으로 상실하지 않음, 재확보·해금 유지·복원 |
| 5 | P9: CU 시작 셀·동적 연결 | `DynamicRegionCapturePolicy.Evaluate`, `RegionSceneBinding.RefreshDynamic` | 1. CU 위치는 기존 조회 사용, 초기 셀/맵 제작 값은 Level 인수. 정적 Stage 1 전체의 선행이 아님 | CU 1셀·CU+2 비공선 노드·4방향 연결·고립 후보·OFF/파괴 회수·모드 Reset·맵 끝 |
| 6 | P7b·P8: 운영 요청 주기·환경 정책 | Match 소유 scheduler → Encounter, 확정 환경 → 두 계획 경로 | Enemy 계획 접점 승인. Weather 선택/Queue는 진태 담당 | Debugger 없이 진행·정지/종료/복원 예약 정리·상황별/고정 경로 일관성·중복 배율 없음 |
| 7 | P12a: 실제 전장 저장 원본 완성 | `BindCheckpoints/RegisterCheckpointParticipant`, `CheckpointService` | 1·2·4 + 실제 Weather/신규 Enemy writer. 진태 구현을 대신 수정하지 않음 | 필수 원본 누락 시 차단, Prepare/Apply 실패와 원상 복구, 일반/소환 개체·진행·환경의 실제 Scene 왕복 |
| 8 | P10a: 33%·66% Field 중간보스 | 확정 점유 → 1회 사건 → 생성 요청/실제 개체 → 처치 → 저장 | 4·7. Enemy 본체/Definition·점유 밖 위치·수명 연결 | 각각 1회, 동시 생존, 재상승 중복 없음, 위치 없음/실행 실패 재시도, 등장 전/처치 후 저장·복원 |
| 9 | P12b: 협동 구조·안전장치 | Player 사망 사실 → 공용 구조 예산/유효 요청 → 구조 또는 1회 복귀 | 7, Player 구조 성공/입력 접점 사전 통보. 실패 판정 뼈대는 앞서 준비 가능 | 3초/30%·공용 60초 잔여 유지·전멸·CU 우선·동시 실패 1회 차감·0회/첫 저장 없음·복원 예산 비환급 |
| 10 | P10b+P11: 100% 최종 보스 전환·승리 | `MatchCoordinator.EvaluateProgress`/Story 규칙 → 저장 → Scene/입력/카메라 → Boss 사망 → `MatchSession.Complete` | 7·8·9, 실제 Enemy Boss·Level 맵·Player 컷씬. P4 관리부 재사용 | 같은 Match·전원 이동·구조 대기 경합·전환 실패 복구·전환 정지/보스 진행/Field 보류·중복 승리 차단 |
| 11 | P5+P12c: 로컬 진행 원본·이어하기 | Profile API → 실제 재화/해금/미션 원본 → Startup/GameFlow → 저장/재개 | 각 실제 원본 연결 통보, 7·9. Backend 구현으로 표시하지 않음 | 정상 종료/재개·깨진 저장·실패 UI·재정산 방지·현재 재화 보존과 전장 rollback 분리 |
| 병행 | P14: HUD / Audio / 입력별 작은 PR | Host 진행 snapshot → 기존 HUD·Alert·표현 / 기존 로컬 입력·카메라 | 각 소비 자료가 준비되면 연결. 기존 폰트/에셋 사용. Player 표현 변경 사전 통보 | 점유/확보/상실/33·66·100 목표·구조 잔여·실패 사유가 읽히고 Runtime/Debugger 값 일치 |
| 별도 Gate | P13: 실제 통신·Steam/Client | 기존 Network Match·Steam adapter와 실제 Player 연결 | Player 실제 원격 actor·Steam transport·계정 준비. Backend 별도 | 동일 Build의 실제 Host/Client 인수. 로비 성공을 게임 통신 완료로 표시하지 않음 |
| 마감 | P15: Core 정리·완성형 통합 | 실제 호출·Scene/Prefab 참조·공개 계약·저장/통신 형식 감사 | 위 기능의 실제 인수 뒤. 삭제에 타 파트 영향이 있으면 별도 보고 | 미사용/중복 제거와 문서/코드 일치, Startup→허브→Field→중간/최종→결과/재도전, Build/자원 정리 |

실제 2인 구조/동시 이동 인수에는 P13의 실제 원격 Player 연결이 필요하다. Host 1인 경로와 정책 fixture 검사를 실제 협동 완료로 바꾸어 쓰지 않는다. 이 외부 입력은 통보 후 연결하며 Core 준비 전체의 불필요한 선행으로 만들지 않는다.

P0는 이번 확정 정책·계획 문서 갱신이다. P2b 실제 Enemy Pool과 팀원에게 인계한 물리/AI 최적화는 이 자동 구현 순서에 포함하지 않는다. 요구 자체가 사라진 것은 아니다.

P1에서 `CapturedCells` 의미만 바꾸고 Enemy 소비를 나중에 붙이는 깨진 중간 버전을 main에 보내지 않는다. Core schema·계약 준비를 먼저 하더라도 의미 전환과 소비자는 위 1번 완결 PR에서 함께 인수한다. 임시 호환 필드/관리자를 영구적으로 하나 더 두는 방식은 피한다.

P12a의 필수 저장 Gate는 실제 보스 생성/전환보다 앞선다. Writer가 없을 때 순수 상태/요청 계약 준비는 가능하지만, 보스전 전 저장과 전체 복귀가 동작한다고 표시하지 않는다. 협동 구조 상태 기초도 보스 전환 준비와 함께 설계하되 일반 구조 동작의 완결 인수는 9번으로 분리한다.

## 팀원 코드 사전 통보 목록

아래는 **수정 필요 보고**다. 이 문서 작성에서 해당 소스는 변경하지 않았다. 실제 구현 전 최신 main과 다시 대조하고 통보/승인을 받은 범위만 수정한다.

| 파트 | 실제 파일·함수 | 왜 연결해야 하나 | 수정 범위 |
| --- | --- | --- | --- |
| Enemy / 조수빈 | `Assets/Scripts/Features/Enemy/Encounter/Planning/SpawnCompositionPlanner.cs`의 `TryPlan/TryPlanOrigin`, `SpawnPointPlanningSession.cs`의 생성자·후보 제외 | 일반/명시 지점 요청에 실제 점유+반경 제한을 적용 | 후보 자격과 자료 전달만. 기존 예산 증가·분대·AI·분포 순위를 재설계하지 않음 |
| Enemy / 조수빈 | `Assets/Scripts/Features/Enemy/Encounter/EncounterRuntime.cs`의 `RequestSpawn`, `Spawning/EnemySpawnExecutor.cs`의 `Execute` | 같은 점유 기준을 계획과 최종 실행에 전달. 계획 후 점유 변경도 차단 | 최신 Match/지역 확인·실제 결과/제외 사유. 반경 계산·생성 책임을 Core가 대신 가져오지 않음 |
| Enemy / 조수빈 | `Assets/Scripts/Features/Enemy/Runtime/Health/EnemyHealth.cs`의 `TryTakeDamage`, 기존 `Died` | 점유 성과에 실제 적용 피해가 필요. 사망 알림은 기존 이벤트 재사용 | 유효 피해 사실을 원본 적용 지점에서 통지. 피해 규칙·HP 원본·AI를 변경하지 않음 |
| Enemy / 조수빈 | `Assets/Scripts/Features/Enemy/Runtime/Networking/EnemySummonGenerator.cs`의 `Spawn`, 관련 Executor Checkpoint | 소환 적도 Match·Grid·공용 시간·저장·종료 수명에 등록 | 공통 등록/해제와 실제 저장. 소환 선택·분대 구성·포화도 제품 규칙은 유지 |
| Player / 이영빈 | `Assets/Scripts/Features/Player/PlayerInfo.cs`의 `ApplyDamage/Die`, 새 구조 성공 접점 | 실제 피해 관측과 다운/구조 후 생존·입력 복귀를 연결 | 기존 `onDeath`·잠금 API부터 재사용. HP 원본은 Player 유지. 새 복귀 API/죽음 표현 변경 전 통보 |
| Player / 이영빈 | `PlayerInfo.Checkpoint.cs`, 실제 HUD/입력·컷씬 소비 코드와 Prefab | 다운/구조 상태·Scene 이동·연출 완료를 실제 actor/화면에 반영 | 기존 Checkpoint가 표현 상태를 충분히 복원하는지 먼저 확인. 새 컷씬 파일/함수를 이미 존재하는 것처럼 가정하지 않음 |
| World/Level / 김진태 | `Assets/Scenes/Gameplay/Initial_Stage.unity`, 실제 보스 Scene·도착점·점유 가능/초기 셀 설정 | 점유 분모·CU 위치·실제 보스 전장 데이터가 필요 | Scene/맵 데이터 변경도 사전 통보. 없는 Boss Scene 경로나 마스크를 임의 생성해 완료 처리하지 않음 |
| World/Weather / 김진태 | 실제 Weather writer 및 Scene 효과 소비 접점 | 보스전 전 저장/복원에 실제 날씨 원본 필요 | 기존 약속대로 김진태 구현을 기다리고 Core 계약·검증을 제공. Queue/Shader/CountTick은 수정하지 않음 |
| Defense / 황재동 | `FieldTurretController.SetRegionAccessPolicy`, `FieldTurretInstanceRegistry.HealthChanged/SnapshotChanged`, `ControlUnitHealth` 조회/이벤트 | 해금 원본·터렛 상태·CU 위치/파괴를 Core에서 읽음 | **현재는 원본 수정 예정 없음.** 기존 API 구독/어댑터 우선. 부족한 정보가 실제 확인되면 파일·함수를 다시 보고 |

Core만의 함수 추가라도 외부 소비자의 의미가 바뀌면 통보 대상이다. 관측 DTO나 새 서비스 이름을 다수 먼저 확정하지 않고 기존 소유자·구독·공용 시간·Checkpoint 실행기를 재사용한다.

## 남은 질문과 자료

- 기존 5가지와 추가 구조 동작 질문은 사용자 답변으로 정리됐다. **현재 배치 착수 전에 답을 받아야 하는 추가 기획 질문은 없다.**
- 실제 중간보스/최종 보스 Definition·Prefab, 보스 Scene/도착점, 점유 가능 셀과 실제 시작 셀 데이터, Weather writer, Player 구조/연출 접점은 팀원 통보·입력이 필요하다. 질문이 끝났다는 이유로 제공 완료라고 표시하지 않는다.
- 성과/압력 가중치와 스폰 여유 거리 등은 조절 가능한 시험값으로 구현·관측한다. 실측에서 진행 불능/왜곡이 발견되면 재현 근거와 함께 질문한다.
- 최종 출시 Stage 순서/Ending·Backend 서비스·실제 온라인 완성 등 제품 전체 후속은 남아 있다. Stage 1 지역/스폰 배치의 필수 답변으로 다시 요구하지 않는다.

## 근거와 검증 경계

- 해금/가동/점유 분리·상실 방향: [10/17 회의](../../../../99_archive/2026-11-05/planning/2026-10-17-regular-meeting.md).
- CU 초기 점유·보스전 전후 저장·공용 구조 시간: [11/6 회의](../../../01_meetings/2026/11/2026-11-06-planning-review.md). 당시 25% 기반과 미정 수치는 이번 승인으로 보완한다.
- Stage 구간별 방식: [11/10 기록](../../../../99_archive/2026-11-10/weekly-progress.md). 1–3 정적 CU 거리, 4–6 송전탑, 7–8 동적 구분을 유지한다.
- 자동 컷씬·별도 Boss Scene: [현재 실행 계획](../../../current-execution-plan.md)의 11/11 근거. 같은 Match·필드 보류·이번 수치는 11/12 사용자 승인이다.
- 이번 확인은 문서·실제 함수/호출부 정적 대조와 링크 검사다. 새로운 Unity 컴파일/Play Mode/Build를 수행한 결과가 아니며 기능 완료나 Issue 종료를 선언하지 않는다.
