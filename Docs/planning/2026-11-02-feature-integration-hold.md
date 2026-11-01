# Core 유지 / 미승인 Feature 수정 제외 상태

갱신: 2026-11-02 KST. 현재 작업 브랜치 `codex/core-sprint-20261026-integration`.

## 현재 적용 상태

PL 지시대로 미승인 타 파트 변경을 **현재 브랜치의 최신 tree에서 실제로 제외**했다. 단순히 stash에 복사해 두고 코드가 남아 있는 상태와 구분한다.

- 제외 커밋: `f4450fd3` — `revert(features): exclude unreviewed team edits while retaining core`.
- 제외 범위: Player101 / 필드 터렛44 / Enemy·Encounter31 / Weather9 / Spaceship2 / Combat UI·HUD11, 총198경로. 각 범위의 파일·`.meta`·검사는 기준 main `5e39728c`와 같다.
- `Assets/Scripts/Core` 아래940개 파일은 제외 직전과 **파일 hash까지 동일**하게 보존했다. App/Match/Scene 조립, 시간·예약·정지·네트워크·저장 계약/실행기 등의 Core 작업을 없애거나 main으로 돌리지 않았다.
- 사용자 기존 폰트·DefinitionId·ShaderGraphSettings 수정도 hash가 같고 Validation 폴더는 건드리지 않았다.
- 기존 구현 커밋 `b1a6bb30`은 제외 커밋의 부모로 남아 있고, 분리 stash10개도 그대로 있다. 변경 이력을 reset/rebase로 지우지 않았다.
- 제품 구현 목표는 paused. PR·push·merge는 하지 않았다.

역할 기준은 [팀 역할](../team/TEAM_ROLE_OWNERSHIP.md)이다. PL은 Core Systems & UI Architect지만 Combat HUD 세부 구현은 Player 담당 범위이므로 `Assets/Scripts/UI/Turrets`의 이번 변경도 제외했다. 외부 Feature 코드에 다시 우회 구현을 추가하지 않는다.

## 컴파일과 실제 연결 상태

**현재 컴파일이 완료되는 통합 상태는 아니다.** Core를 그대로 보존한 상태에서 Feature에 추가했던 API를 제거했으므로 Core 조립부의 참조가 남아 있다.

Unity6000.3.23f1 batch import/compile을 한 번 실행해 종료1과 중복을 제거한 C# 오류28개를 확인했다. PlayMode와 테스트는 실행하지 않았다. 이것은 최초 실패한 컴파일 단계의 오류이며 모든 후속 assembly/Scene 오류까지 조사 완료했다는 뜻이 아니다.

| 남아 있는 Core 소비자 | 제외된 Feature API/연결 | 의미 |
| --- | --- | --- |
| `TurretMatchBinding` | `RequireMatchScope`, `BindMatchScope`, `OwnsTurret`, `TryGetEntityId`, `SetEntityIdResolver`, `RefreshLayout` | Core가 미승인 Feature 확장에 직접 의존하던 부분. 기존 등록/권한·ID·해제 설계를 보존했지만 현재 Feature 구현과 연결되지 않음 |
| `TurretPresentationSceneSource`, `TurretCommandSceneScope`, `TurretFeatureCommandExecutor` | `HasWriteAuthority`, `OwnsTurret` | 읽기/명령 경계가 제외된 API를 참조. 하위 CS0165 오류도 같은 참조 실패에서 나올 수 있음 |
| `InitialStageCompositionRoot` | `BoundMatchId`, `HasWriteAuthority` | 실제 Scene의 Feature 권위/Match 소유 확인 창구 미연결 |
| `RegionSceneBinding` | `CaptureTurrets`의 resolver 인자를 받는 overload | 지역/터렛 관측 연결 계약 미연결 |
| Core 네트워크 빌드/검증 도구 | `OwnsTurret`, `TryGetEntityId` | 검증 도구도 같은 Feature 확장에 의존했으므로 현재 그대로 실행할 수 없음 |

이 표는 **팀원이 반드시 같은 메서드를 추가해야 한다는 요구가 아니다.** Core가 실제로 무엇을 필요로 하는지 설명하는 목록이며, 담당자와 계약을 맞춘 뒤 Core 측 adapter/계약을 정리하거나 Feature가 제공한 API에 연결해야 한다. 이번 제외 작업에서는 Core 코드를 수정해 숨기거나 타 파트 코드를 다시 추가하지 않았다.

## 기능 상태를 읽는 기준

| Core에 남아 있는 것 | 현재 해석하면 안 되는 주장 |
| --- | --- |
| 공용 게임 시계·취소 가능한 예약·정지/배속·참가자 정책·Host/Client anchor 코드 | 실제 Player/Enemy/터렛이 전부 같은 시계로 실행되고 정지된다는 주장. Feature 소비자 연결은 제외됨 |
| 환경 snapshot/복제/Scene 계약 | 현재 WeatherManager가 Core 시간·예약·Host/Client 환경 계약을 구현한다는 주장. Weather 변경은 제외됨 |
| 바이너리 저장소·CheckpointService/Trigger·복원 순서·정책, Core 공간 writer | Player·CU·Weather·터렛의 실제 저장·복원이 현재 활성화돼 있다는 주장. 해당 Feature writer/확장 코드는 제외됨 |
| Match/Scene/네트워크·명령·UI 아키텍처 코드 | 현재 Scene 한 사이클/2인/재실행 복원 통합 완료라는 주장 |

기존 문서의 개별 검사 결과와 Feature writer 설명은 그 당시 실험 소스의 기록이다. 현재 HEAD의 통합 검증으로 재사용하지 않는다. 실제 Core 읽기는 코드와 이 상태 문서를 함께 보고, 'Core 제공 기능 / Feature가 제공해야 할 접점 / 아직 실행 불가한 조립'을 구분한다.

## 보관 자료와 이후 작업 경계

- 로컬 상세 설명: `Docs/Local/Review/CoreSprint-2026-11-02/TEAM_HANDOFF.md` — 파트별 무엇을 바꿨고 왜 필요하다고 판단했는지, 기존 로직을 유지하는 대안과 담당자 검토 항목.
- stash hash/순서: 같은 폴더 `STASH_STACK.md`. Core는 현재 브랜치에 이미 남아 있으므로 Core stash를 중복 적용하지 않는다.
- 제외 경로·Core940개 및 사용자 파일 hash: `feature-exclusion/before.json`.
- 실제 제외 검증·컴파일 결과: `feature-exclusion/verification.json`, `compile-errors.json`, `compile.log`.

담당자 확인과 PL의 재개 지시 전에는 보관한 Feature 변경을 재적용하지 않는다. 컴파일 문제를 해결한다는 이유로 팀원 로직을 다시 수정하지 않는다. 다음 설계 논의에서는 Core의 직접 의존성도 함께 확인한다.
