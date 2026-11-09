# Core 잔여 구현·검증 배치

기준: `acfd430a8`, main `1a03e9a91`. [재검토의 누락](2026-11-05-core-completion-reaudit.md)과 기존 Sprint/Issue/회의·네 명 전달본을 다시 대조한다. 로컬에서 명시적으로 진행 가능한 구현·검증·구조 정리를 마쳤다. 외부 담당 구현·제품 결정과 PL 인수는 [최종 결과](2026-11-05-core-completion-results.md)에 분리했다.

## 원칙

- 진태 담당 날씨 Random 선택·Weather 고유 writer·실제 포털/콘솔 구현은 대신 하지 않는다. Core의 공용 시계/예약·환경·저장 계약과 독립 검증은 진행한다.
- 사용자의 기존3개 파일·stash10개와 최신 입력·터렛 복구·Enemy Variant를 보존한다.
- 자동 저장 사건·미정 승리/보상 정책을 임의 확정하지 않는다. 감속 중첩은 이번 대화에서 확정한 최강 효과/독립 만료를 따른다. 독립 작업을 먼저 진행하고 중요한 결정만 요청/기록한다.
- 목적별 커밋으로 남긴다. PR/push/merge는 수행하지 않는다. Issue는 실제 완료 조건/근거로 정리하며 테스트 일부를 전체 완료로 표시하지 않는다.

## 배치와 완료 조건

| 배치 | 목적 | 필요한 완료 근거 | 현재 |
| --- | --- | --- | --- |
| C0 | 최신 스프린트/기획/Issue·stash 기능 대응 | 요구별 실제 파일·함수·현황/누락·담당·검증·Issue 처분을 기록 | 요구·stash 대응 기록 완료 |
| C1 | 원거리 발사 진행·Animation Event 경계 | 발사 전/후/완료 전 snapshot, 정지/종료·Client 방어, 지연 이벤트 재개·중복 생성 없음 | 구현·격리 검사 통과 |
| C2 | Player 감속 수신·공용 시간·저장 | 확정 중첩 정책, 실제 이동 반영·만료/정지·복원·권한 검사 | 구현·격리 검사 통과 |
| C3 | binary 파일→새 Match 복원 | 실제 파일 읽기·새 Match ID/seed/원본·참조, 실패/취소/누락 writer 정리. Weather 대역과 제품 통합을 구분 | 구현·집중 검사 통과 |
| C4 | 2인 Core Scene/Match 경로 | NGO 공유 로드·readiness·Host 시작·종료/다음 판·해제, 가능한 실제 두 실행 프로세스 검증. Player 본체 구현은 제외 | 구현·실제 두 프로세스 검사 통과 |
| C5 | 전체 검토·회귀·팀 문서/이슈 | 공개 API/소유권·중복/수명·stale 문서·stash 최종 대응, 최종 시험과 제약, 팀원별 추가 통보·PL 수동 순서·Issue별 처분 | 검토·회귀·팀 문서·Issue 갱신 완료 |
| C6 | 구조와 복잡도 정리 | 실제 호출을 기준으로 불필요한 계층/변경 경로·중복 바인딩 제거, 조립부 탐색 개선, 실패 수명 일관성·직접 영향 검사 | 구현·검증·삭제/유지 판단 문서 완료 |

각 결과는 코드와 실행 근거를 함께 기록한다. 기존 실패/환경 fixture 문제/실제 제품 결함을 구분하고, 실제로 빠진 경우 보완한다. 미지원 제품 정책이나 타 담당 의존을 완료율을 위해 가짜 구현으로 채우지 않는다.

## C1·C2 1차 결과

- 감속 연결은 기존 통합 계획의 Player 항목6이었다. 이번 대화에서 중첩은 가장 강한 효과를 적용하고 각 효과는 독립 만료하는 것으로 확정했다. 강한 효과가 끝나면 남은 약한 효과가 이어진다.
- `PlayerSlowEffects`가 Enemy의 `ISlowEffectReceiver`를 구현하고, Movement의 최종 일반 이동·Dash 속도에 한 번 적용한다. 각 감속의 남은 게임 초를 슬롯별 writer로 저장한다. 기존 제작 Player도 Awake에서 수신 컴포넌트를 확보한다.
- 원거리 적의 발사 전/후·공격 완료 대기·정지 중 도착한 이벤트를 Enemy writer v2에 기록한다. 중간 Attack trigger 프레임처럼 일관된 애니메이션 상태가 없으면 저장을 거절하고 재시도 사유를 알린다. v1의 event-timed 원거리 적은 누락 상태를 가정하지 않고 거절한다.
- 발사/폭발 완료 Animation Event는 Host·활성·공용 시계 경계를 통과한다. 정지 중 이벤트는 보류하고 재개 후 한 번 처리한다.
- 격리 Unity6000.3.23f1 검사 `effects-v3.xml`: **11/11**, 종료0. 강·약 독립 만료, 정지/사망/Client replica/취소 거절, 감속 상태 복원·rollback, 일반 이동·Dash 배율, 실제 PlantLeafer 투사체의 보류 발사·완료·중복 방지와 시계 상속, 발사 전/후 binary 상태, 종료 시계 방어, 폭발 완료 보류를 확인했다. 테스트 fixture는 제품에 추가하지 않았다.
- 이것은 C1·C2의 집중 검사다. 새 Match 파일 복원·2인 전환·전체 요구 대응은 다음 배치에서 별도로 검증한다.

## C3·C4 결과

- 파일 슬롯 저장→우주선 귀환→새 Scene·새 Match 복원 성공, 외부 writer 거절, 읽기 전 취소 **3/3** (`arrival-v4.xml`), Scene 입장 도중 취소 **1/1** (`arrival-cancel-in-scene.xml`), 모두 Unity 종료0. 실제 적의 논리 ID·Player HP/감속·필드 시각/출입 위치를 확인했다. **Weather는 테스트 전용 writer이며 제품 Weather 저장 완료 증거가 아니다.**
- Scene 로드 뒤의 비동기 Match 조립 실패를 App에 즉시 기록하고, 취소/실패한 복원 작업이 만든 Match만 정리한다. 실제 최종 코드의 호출 순서로 검증했다.
- 격리 Windows Development Build 성공. 같은 빌드 Host/Client 프로세스가 UTP로 접속하여 2인 준비→시작→공유 우주선 귀환→재출격→재시작→공유 귀환을 수행했다. 두 프로세스 종료0, Match3개 모두 다른 ID, Client 환경/시계 Match 일치, AppRoot1개, 마지막 Enemy0개. 로그의 Error/Exception 호출0. 기존 ContentCatalog fallback·미지정 NetworkPrefab 항목·터렛 런타임 ID 재배정 경고는 별도로 남아 있으며 테스트 성공을 경고0이라고 쓰지 않는다.
- 로컬 실행 근거: `Docs/Local/Work/CoreCompletion-20261105/network-cycle-v1/`. 드라이버·Weather 대역·테스트 fixture는 제품 커밋/PR에 넣지 않는다.
- C3 취소/정리는 `1730575a7`, 공유 Scene/준비와 비동기 실패 전달은 `4b28dfd3b`. 전자는 새 Match 정리, 후자는 Scene 준비 대기 오류 전달까지 보강한 연속 커밋이다.

## 추가 목표 — 구조와 복잡도 정리

사용자 추가 지시: 현재 구조에서 너무 어렵거나 과도하게 반복되는 부분, 논리상 어긋난 부분, 없어도 되는 것을 실제 호출 관계로 조사하고 정리한다. 문서의 지적만으로 끝내지 않고 안전하게 가능한 삭제/통합·이름/흐름 개선은 구현한다.

- **C6:** Core와 이번 Feature 연결의 중복 상태 원본·겹치는 scope/lease·중복 검증·죽은 API/보관 잔재·불필요한 간접 호출을 확인한다.
- 소비자/수명/권위 계약을 유지하며 가장 작은 직접적인 구조를 선택한다. 단순 파일 수 감소를 위해 실제 책임을 합치거나 공개 팀 계약을 조용히 깨지 않는다.
- 현재 구조의 문제, 실제 정리, 유지한 이유, 나중에 해야 하는 제품 변경을 나눠 기록한다. 목적별 정리 커밋과 직접 영향 검사 후 완료 판정을 갱신한다.

## C0·C5·C6 최종 결과

- 기존 스프린트/회의/기획·Issue18개와 실제 함수/소유자/호출 경계의 대응을 [요구·Issue 지도](2026-11-05-core-requirements-and-issue-map.md)에 정리했다. 보관 C#137경로의 현재 파일/partial·대체·제외를 기록했고 Core 보관 공개 API 이름도 비교했다. UI는 최신 main을 유지하고 진태 고유 작업·추가 보관 테스트를 제품에 통째로 재적용하지 않았다.
- 사용되지 않는 MatchState 변경 함수들과 별도 실제 규칙이 없던 MatchSimulation을 제거했다. 권한/일반 명령의 기존 호환 응답은 유지하며 실제 Feature 요청 경계와 구분했다. 투사체 중복 시간 연결과 owner 생략 시계 누락도 정리했다.
- InitialStageCompositionRoot의13개 메서드를 본문 변경 없이 Networking/Spatial partial로 이동했다. 기존 컴포넌트/Scene GUID·상태 원본은 동일하며 새 Manager를 만들지 않았다. [구조 검토](../../../../02_architecture/01_core/core-structure-review-2026-11-05.md)에 삭제/유지 근거를 남겼다.
- 확대 Core/실제 원본 회귀 `structure-core-regression-v2.xml`: **300/301**. 남은 렌치 검사는 EditMode의 null yield 대신 실제 Update 프레임2개를 기다리도록 fixture만 맞췄다. Scene 분리까지 포함한 최종 `final-wrench-and-arrival.xml`: **18/18**, 종료0(해당 렌치·파일 성공/실패/취소·감속/원거리 이벤트·생성자 생략 시계 포함). 두 실행을 하나의 전체301/301 실행으로 표시하지 않는다.
- 최신 실제 Player prefab의 두 단계 설치·무기 선택 API와 정상 해제를 사용한 `actual-player-writers-v3.xml`: **15/15**, 종료0. 폭탄·장벽·비콘·Player 터렛·소유 탄환·HP/수량/논리 ID·잠금·단일 피해·부정 자료·rollback을 확인했다. 옛 즉시 설치/무기 순환·정지 중 입력 가정을 제품 코드에 되살리지 않았다.
- 최종 Windows Build 성공(`network-build-final.exit=0`), 새 증거 폴더 `network-cycle-final`에서 Host/Client **각 종료0**. 협동 Match3개·공유 귀환2회·Client 시계/환경 교체·마지막 적0·AppRoot1개를 다시 확인했다. 이전 실행의 marker 파일을 재사용하지 않았다.
- 본인 이슈18개 본문 갱신/읽기18/18 일치, 과거 main의 삭제된 Monster 계층 버그 #403만 종료했다. 다른 코드 Issue는 PR 전 로컬 구현과 마지막 인수/외부 의존을 구분해 유지한다. PR/push/merge0.
- 현재 실제 Weather writer가 없으므로 운영 전체 Save는 여전히 차단한다. 김진태 고유 작업·Player NetworkBehaviour·미정 제품 정책을 대신 완료 처리하지 않는다. 마지막 인수/PL 확인/기능/팀 전달본은 [최종 정리](2026-11-05-core-completion-results.md)에 모았다.

## 마지막 입장 취소 재검토

- 저장 복원과 달리 일반 Mission 출격은 Match가 막 시작된 순간 취소되면 GameFlow만 Faulted가 되고 새 Match가 남았다. 실제 Scene 검사 `ordinary-cancel-before.xml` **0/1**로 재현했다.
- `AppGameFlowHost.WaitForMissionReadyAsync`의 실패 경계에 `AppRoot.EndFailedMissionArrival(expectedMatchId)`를 연결했다. 저장 입장도 같은 helper를 쓰며 해당 ID만 정리하고 결과 전송 gate는 우회하지 않는다.
- 동일 재현을 포함한 일반 출격 취소·실제 파일 입장 성공/거절/읽기 전 취소/입장 중 취소 `ordinary-cancel-after.xml` **5/5**, Unity 종료0. 앞선 두 프로세스 검증은 구조 정리까지 포함하며, 이 마지막 실패 경로 helper 이후에는 이 집중 검사로 재컴파일/검증했다. 모든 검사를 다시 돌렸다고 표시하지 않는다.
