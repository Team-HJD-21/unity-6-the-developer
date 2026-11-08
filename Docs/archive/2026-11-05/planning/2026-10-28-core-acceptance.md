> 보관 문서 · 2026-11-05 교체 전 스냅샷
> 기준 커밋: `1a03e9a9164e5db94cbc8775162467d08cf63a3b`. 당시 설명·검증 근거이며 현행 실행 지시가 아니다.

# Core 5·6주차 수동 인수 기록

기준일: 2026-10-28 KST. 상태: 현재 로컬 통합본의 PL 수동 확인 완료, 분할 PR 준비. 팀 공통 Build·전체 협동·Release Gate와 구분한다.

## 인수 대상과 근거

- 대상 코드: `34e048fa`, 실제 작업 프로젝트 `D:/Dev/Unity/Unity_3D_CBC`, 브랜치 `codex/core-sprint-20261026-integration`.
- 최신 main `7802e0c6`과 기존 전체 Core 통합을 포함한다. CU 초기화 PR #569는 이미 main에 반영됐다.
- PL 양현석이 현재 프로젝트에서 앞서 요청한 수동 확인을 모두 수행했고 이상이 없어 보인다고 보고했다. 개별 입력 시각·해상도·영상·측정값은 따로 제공되지 않았다.
- 수동 확인 범위: Initial_Stage 진입·정적/동적 지역, 공용 터렛 요청/HUD, 메뉴·결과·재시작/복귀, 환경 표시, World origin/지연 생성과 진단이다. 시간대 source가 없거나 환경 Profile이 중립인 현재 설정도 함께 해석한다.
- 자동 증거: 실제 작업 폴더에서 메뉴/결과·CU 초기화 7/7. FieldTurret 통합 후보의 실제 Scene 연계 14/14, Network/수리 47/47, 초기화 호환 6/6. 동일·겹치는 검사를 합산해 고유 검사 수로 표현하지 않는다.

초기 도구 자동화에서 물리 Escape 입력을 확인하지 못했던 기록은 당시 이력이다. 이후 사용자 수동 확인 보고를 인수 근거로 추가하며, 도구가 직접 키 입력 검증에 성공했다고 소급 수정하지 않는다.

## 완료 수준

| 항목 | 현재 판정 |
| --- | --- |
| 선택한 Core 기능 구현·기술 검증 | 완료. [배치 인수](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/planning/2026-10-26-core-sprint-handoff.md)와 실제 코드 참조 |
| 현재 프로젝트의 PL 수동 조작 인수 | 사용자 보고 기준 완료 |
| 분할 PR별 head 구성·검증 | 발행 전 수행. 전체 후보 성공을 각 head의 성공으로 복사하지 않음 |
| Code Owner/관련 Feature 검토 | 각 PR 리뷰에서 확인 |
| 실제 원격 Player 생성·이동/전투 연결 | Player/UI 연계 후 별도. 개발 actor fixture 성공으로 대신하지 않음 |
| 최신 동일 Windows Build의 팀 공통 플레이 | 별도. 이번 사용자 로컬 확인을 #420의 팀 관찰로 확장하지 않음 |
| Weather 시간 서비스 개편 | 설계·협의 중. 현재 Weather 수정은 팀원이 진행하므로 이번 작업에서 Runtime 코드를 바꾸지 않음 |

## PR와 이슈 처리 원칙

1. 이미 main에 반영된 #569를 다시 제출하지 않는다.
2. 기반/지역·터렛, Match 운영, Scene/UI, 환경, 추가 스폰의 선행 관계를 유지한다. 공용 파일을 파일째 복사해 후속 기능을 앞 PR에 섞지 않는다.
3. #555/#556/#557/#558/#559는 해당 구현·직접 영향 검증과 리뷰가 포함될 때 종료 후보다. #526/#527과 #444도 각각 지역 기반·선택 서비스 범위를 만족한 PR에서 판단한다.
4. #442의 실제 Player 연계, #495의 FieldTurret·Enemy/Encounter 상태·타깃 계약, #496의 Enemy/Encounter 소비 계약, #528의 실제 Client HUD는 해당 남은 조건을 구분해 관계를 정한다. 수동 인수만으로 자동 종료하지 않는다.
5. #420은 팀 Build 기록, #560은 PL/Owner의 설계·Gate·전파 검토까지 추적한다. 다른 담당자의 작업은 임의로 닫지 않는다.
6. 사용자 글꼴·개인 문서·검증 로그·과거 worktree를 분할 PR에 넣지 않는다. 이번 기록은 기능/검증의 출처이며 새 원격 발행이나 병합 완료 기록은 아니다.

## 별도 후속

현재 시간 기반은 `MatchCoordinator.Tick(Time.deltaTime)`과 `IMatchClock`이다. 낮·밤·날씨를 위한 공용 서버 시계와 타이머 등록 구조는 후속 설계이며, 기존 PR에서 이를 구현했다고 쓰지 않는다. 현재 Weather/Encounter 계약은 [환경 API](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/architecture/match-environment-api.md), 관리 책임은 [운영 책임](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/architecture/match-management-responsibilities.md)을 따른다.

10/28 후속 설계에서 사용자가 확정한 정책은 두 가지다. 낮밤·날씨 시간은 Host 게임 정지·배속을 따른다. 환경 전환은 우선 다음 Encounter/SpawnPlan 조건만 바꾸며 전환 자체로 추가 스폰을 요청하지 않는다. SDK의 서버 시간과 게임 시간은 구분하고, 실제 clock/timer 구현과 Match/행성 시간 수명은 별도 협의/개발한다. 팀원이 수정 중인 Weather/CountTick Runtime은 이번 준비 작업에서 변경하지 않았다.

사용자의 다음 공유 계획은 10/28 수요일 Weather, 10/31 토요일 GameManagement, 10/31~11/1 주말 Network다. ServerTick/시간 서비스는 김진태와 협의 예정이다. 이는 이번에 사용자가 보강한 계획이며 10/27 회의의 확정 발언으로 소급 기록하지 않는다. 문서 준비와 실제 통보·전파 완료도 구분한다.
