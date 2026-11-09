> 보관 문서 · 2026-11-05 교체 전 스냅샷
> 기준 커밋: `1a03e9a9164e5db94cbc8775162467d08cf63a3b`. 당시 설명·검증 근거이며 현행 실행 지시가 아니다.

# Stage 1 기술 PoC 완료와 PR 인수

기준일: 2026-10-08 · 담당: 양현석 · 브랜치: `feature/core`

PL이 런타임을 직접 확인하고 문제가 없다고 승인했다. **양현석의 Core 공간 분석·Encounter 전달·Spawn 통합 및 Game Debugger 기술 PoC는 완료로 인수한다.** 정식 승패·성장·멀티플레이 출시 기능 완료를 의미하지 않는다. 이 기록이 이전 배치 문서의 미실행 화면 검증 및 터렛 업로드 대기 상태에 대한 최신 인수 결과다. 과거 테스트 결과는 당시의 기록으로 보존한다.

## 실행 환경과 검증 근거

실행 Scene은 `Assets/PoC/Spaceship/Scenes/Initial_Stage.unity`, Unity는 `6000.3.23f1`이다. 코드 기준은 `9fc3a458`까지의 배치 및 이후 이 인수 문서·검증된 사용자 Scene 구성을 포함하는 커밋이다. main 기준 SHA는 `e493b46a19596ae165eeb68c794e57c7064777b6`이며, 이 기록 시점에 로컬 작업은 PR 병합 전이다.

| 검증 | 결과 | 근거 |
| --- | --- | --- |
| 최종 사용자 Runtime 확인 | PL 확인 완료, 문제 없다고 승인 | 2026-10-08 현재 요청 및 제공한 PoC 영상 |
| 공간 정책·진단·등록 집중 검사 | 24/24 통과 | `Logs/spawn-distribution-editmode.xml` |
| 실제 Scene 및 기존 요청 경로 | 2/2 통과 | `Logs/spawn-distribution-integration.xml` |
| 최신 동적 Grid Scene 검사 | 1/1 통과 | `Logs/spawn-distribution-live.xml` |
| 마지막 정책·기록 회귀 | 20/20 통과 | `Logs/spawn-distribution-final.xml` |
| Player 타깃 HOTFIX | 당시 CLI 명시적 통합 1/1 통과 | [HOTFIX 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/planning/2026-10-08-enemy-movement-hotfix.md) |
| 창·차트·반복·전선 도구 | 당시 집중 검사 54/54 및 실제 Scene 1/1 통과 | [Debugger 배치 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/planning/2026-10-08-game-debugger-batch-results.md) |

여러 회귀 실행에는 같은 테스트의 재실행이 포함된다. 합산해 서로 다른 테스트 수로 표시하지 않는다. 검사 산출물은 Git에 포함하지 않는다. Windows Development Build와 별도 Client의 검증은 위 결과에 포함되지 않는다.

동일 snapshot 반복 실험에서 적합 30/30셀을 사용했고 480마리를 셀당 15~20마리로 배분했다. 최신 동적 Grid 실험은 240마리·27/30셀을 사용했다. 적합 셀별 생성 기회 분산이지 모든 SpawnPoint에 매번 같은 수를 생성하는 정책은 아니다. [분포 수정 설계](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/architecture/spatial-spawn-distribution.md)와 [검증 결과](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/planning/2026-10-08-spawn-distribution-fix.md)를 따른다.

## 완료된 양현석 기능

- App/Match/Scene 소유권과 이전 Scene의 종료 방어, immutable 공간 입력·snapshot.
- Delaunay topology·외곽 Frontline, XY 중심 기반 Uniform Grid 및 명시적 동적 점유 갱신.
- Turret snapshot 투영, Player/Enemy 위치 연결, Encounter 최신 snapshot 주입 및 자동 첫 요청.
- 공통 요청 경로, 같은 Scene 전체 SpawnPoint 등록과 복제 ID 보정, 셀 공유 이력을 사용하는 공간 분산 정책.
- Game Debugger의 분할 패널, 시간 제어, 일회/반복 요청, 선 그래프·큰 Statistics 창, Spawn 판단/생성 위치 Inspector, Timeline·조건 정지·Export/Compare 및 Scene Overlay/Gizmo.
- [팀 Core API 규약](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/architecture/core-api-usage.md), [도구 사용법](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/tooling/game-debugger-guide.md), [디자인 기준](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/tooling/game-debugger/Design.md).

## Issue 완료 판정

| Issue | 판정 | 근거 또는 잔여 범위 |
| --- | --- | --- |
| #388 | 완료 | App/Match 수명·의존성 금지·Legacy 책임 대응·구현 파일과 후속 경계가 Charter/Convention/Core README에 있음. M1 구조 기준이며 영구 API 불변 약속은 아님 |
| #412 | 완료 | Skeleton·asmdef·Fake/Content/Presentation 경계가 구현됨. 전투·경제 규칙은 원래 제외 범위 |
| #443 | 완료 | Match-owned topology/frontline·adapter·읽기 전용 snapshot·degenerate 정책 및 실제 터렛 기하 확인 |
| #458 | 완료 | Grid 좌표/경계/ID/빈 입력/OOB·갱신 정책·Encounter 조회 및 집중/Scene 검증 |
| #445 | 완료 | 실제 계약과 일치하는 예제·명명·권한·종료·취소·PoC 구분을 공유 가이드에 기록. #442 미결은 미결로 표시 |
| #389, #446, #460 | 이미 Closed | 기존 수명주기·Editor 기반 완료. 이번 확장을 과거 검증으로 소급 기록하지 않음 |
| #420 | 열어 둠 | PL의 양현석 기술 PoC 인수는 완료. 팀 공통 Windows Build·플레이 관찰 기록은 별도. 원문 임시 승패는 최신 Sprint의 미정/제외 기준과 불일치하므로 현재 기준을 Issue에 명시 |
| #459 | 열어 둠 | 양현석 전달·공간 소비/실제 생성은 확인. 조수빈 소유 전체 인수에는 공식 Plan 중복 실행 방지/권한 및 Enemy 상호작용·종료 범위의 담당자 확인이 필요 |
| #441 | 열어 둠 | PL이 전역 Legacy 제거를 별도 범위 확정까지 보류함. Singleton 자동 생성 억제는 전체 제거가 아님 |
| #442 | 열어 둠 | Core 전체의 2인 Host/Client 권한·ID·상태·생성/제거 검증 미완료. 로컬 Authority와 Enemy Host Spawn으로 대체 불가 |
| #444 | 열어 둠 | Bootstrap의 Profile/Platform/SceneFlow는 Fake. 실제 서비스 선정·사용·실패/종료 구현은 별도 |
| #403 | 열어 둠 | Enemy Legacy animator 중복 직렬화 범위이며 Player 타깃 누락 HOTFIX와 다른 결함. 이 작업에서 전체 대상 회귀를 인수하지 않음 |

완료와 main 반영은 다르다. 완료 Issue를 닫아도 현재 feature/core 변경은 PR 검토·필수 checks·승인 후 main에 병합해야 팀 공용 기준이 된다. 다른 Owner의 전체 Feature를 양현석의 기술 인수만으로 닫지 않는다.

## HOTFIX 제거와 교체

**`dcb583d1`은 임시 HOTFIX이며 이번 PR에서 제외하거나 이후 되돌릴 수 있는 별도 커밋이다.** 수정 대상은 Player prefab의 `TargetableComponent`/`PlayerTargetableAdapter`와 해당 테스트·원인 기록이다. 현재 준비 단계에서는 보존하며 임의로 삭제하지 않는다.

HOTFIX는 Player 실제 HP를 Enemy 타깃 후보에 연결한다. 대체 adapter 없이 제거하면 모든 터렛이 꺼졌거나 파괴된 상황에서 살아 있는 Player를 후보로 찾지 못해 적이 대기할 수 있다. Player/Enemy Owner가 정식 타깃 계약으로 교체할 때 동등한 체력·생존·구독 해제 동작을 유지한다. 커밋을 제외하는 PR은 현재 포함 상태의 테스트·영상 결과를 그대로 제외 상태의 검증으로 주장하지 않는다.

## PR와 첨부 자료

본 PR은 `feature/core → main`으로 Core 통합·Runtime 관측·도구가 함께 동작하는 결과를 제출한다. 이미 분리된 구현 커밋과 별도 Scene authoring/문서 인수 커밋을 유지한다. 개인 `YGDocs/`, `Docs/Local/`, `Validation/`, `Logs/`와 녹화 원본은 제출하지 않는다.

| 첨부 | 용도 | 표현할 범위 |
| --- | --- | --- |
| `녹화_2026_10_08_07_08_32_795.mp4` | PL 제공 PoC 실행 영상 | 그리드 기반 스폰·전선 형성의 사용자 Runtime 확인 |
| `스크린샷 2026-10-08 064800.png` | Game Debugger 4분할 사용 예 | Edit Mode PREVIEW 화면. LIVE 생성/사망 통계의 증거로 쓰지 않음 |

두 자료는 기본 인수 설명에 충분하다. 선택 보강은 LIVE `Statistics → Spawn decisions / distribution`에서 후보 Cell service·planned/actual·생성 좌표를 함께 보여주는 사진이다. #420의 공통 Build 검증은 사진 추가 대신 실제 Build/commit·참여자·플레이 결과 기록이 필요하다. GitHub 첨부는 PR 작성 시 원본을 업로드해 얻은 URL을 사용하며 로컬 파일 경로를 원격 본문에 넣지 않는다.
