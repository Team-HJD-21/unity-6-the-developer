# Core 최종 정리와 PL 확인 순서

**추가 승인 후속:** [낮밤·Steam·시작 우주선·로딩의 최신 결과](2026-11-05-day-night-steam-results.md)를 먼저 본다. 아래는 앞선 Core 목표의 범위/증거이며, 당시 미구현이던 낮밤 producer와 Steam SDK는 후속에서 보완했다.

현재 기준: main `1a03e9a91` 이후 `codex/core-feature-integration-20261105`. 이 문서는 Core 목표 당시의 결과이며, 현재 수동 확인/미구현/전달은 [최종 로컬 안내](2026-11-05-core-local-review.md)를 기준으로 한다. **PR/push/merge는 하지 않았다.** 이전 완료 판정과 중간 실패는 지우지 않고 [배치 기록](2026-11-05-core-completion-batches.md)에 보존한다.

## 이번에 실제로 추가한 것

- Player·터렛/CU·Enemy·소유 탄환/장치의 공용 필드 시간·정지/배속·Match 종료 연결. 내부 이동과 필드 행동, 개인 메뉴와 공용 정지를 분리했다.
- 최신 터렛 관리 요청→Host 실제 actor/거리/상태 검증→기존 터렛 API와 CU 명시 초기화. 전력/수리/강화·자동 복구 정책은 기존 원본을 유지했다.
- 실제 Weather→환경 snapshot→Encounter 정책, World/source 위치 요청·게임 초 예약·source/Match 취소와 실제 생성 추적. 기본 지점 스폰도 같은 요청 경계다.
- Player 감속 수신: 가장 강한 감속을 적용하고 각 효과를 독립 만료한다. 일반 이동/Dash·공용 시간·저장에 연결했다. 원래 계획에 있던 수신 연결의 누락을 보완한 것이고, 중첩 정책은 이번 대화에서 확정했다.
- 실제 Player/CU/터렛/Enemy/Encounter writer, 논리 개체 ID·표적/미사일 예약 재연결, 남은 시간·분산 이력과 실제 살아 있는 개체를 복원한다. 원거리 발사 전후/완료 대기·정지 중 지연 이벤트는 Enemy writer v2에 포함한다.
- 실제 슬롯 binary→새 Scene/새 1인 Match 복원 API와 일반/복원 입장 실패·취소 시 해당 입장의 새 Match 정리. 누락 writer를 조용히 생략하지 않는다.
- Startup→시작 버튼→우주선→Mission→명시 귀환, Host 1~2인 공유 Scene 완료→전원 준비→시작과 다음 판. Client도 실제 수신/조립 결과로 App 흐름을 갱신한다.
- 기존 Main 시각 요소·NEXON OTF·HUD sprite를 재사용한 공용 화면, 실제 호출/수명/해제 문서와 추가 팀원 전달본.

## 구조도 함께 줄였다

`MatchSimulation`은 실제 규칙 없이 상태 전이만 전달하던 객체여서 제거했다. Session은 State의 검증된 단계/결과에 직접 연결한다. 사용되지 않는 State 변경 함수와 빈 실행/이벤트 순회를 없앴다. 일반 Submit 두 개와 전용 명령·결과·로컬 권한 타입13개를 제거하고 AppServices/Session Factory의 불필요한 권한 주입도 없앴다. PlayerSceneAdapter Bind/Unbind와 실제 Feature 요청/Host 검증은 유지한다.

큰 Scene root는 초기 조립/Update/해제를 본 파일에, 네트워크 준비/Client 읽기 복제를 `.Networking.cs`, 공간 투영을 `.Spatial.cs`, 저장 scope를 `.Checkpoints.cs`에 둔다. 같은 컴포넌트/상태이며 새 Manager가 아니다. 투사체의 중복 시간 바인딩도 한 번으로 합쳤고 owner 생략 시 Spawner의 시계를 전달하도록 고쳤다. 일반 출격 취소와 저장 입장 취소가 서로 다른 정리 결과를 내던 문제도 실제 재현 후 같은 ID 검증 helper로 맞췄다.

[삭제/유지 판단·전후 구조와 남은 호환 자료](../architecture/core-structure-review-2026-11-05.md)를 함께 보면 된다. 코드가 짧아 보이게 하려고 권위 검증·정리/rollback·서로 다른 Grid/상태 원본을 합치지는 않았다.

## 자동 확인 근거

| 확인 | 결과와 한계 |
| --- | --- |
| Core 확대 회귀 | 구조 정리 후 **300/301**. 마지막 렌치 검사1개는 EditMode에서 실제 Update 프레임을 기다리도록 fixture만 보완해 후속 검사에서 통과. 제품 기대값/피해량은 낮추지 않음 |
| 최종 직접 영향 | 파일 입장 성공/거절/읽기 취소/입장 중 취소, 실제 렌치/탄환, 감속·발사 상태/중복 방지·생성자 생략 시계 상속 **18/18**, Unity 종료0. Scene partial 분리까지 포함 |
| 마지막 취소 보완 | 일반 출격의 시작 직후 취소 누수0/1 재현→수정 후 같은 검사와 실제 파일 입장4개 **5/5**, Unity 종료0. 두 프로세스 최종 실행 뒤 추가한 실패 경로 helper의 직접 검증이며 전체 회귀 재실행으로 합치지 않음 |
| 실제 Player/CU writer 확대 | 최신 두 단계 배치/무기 선택 API와 정상 해제 순서를 쓴 실제 prefab 검사 **15/15**, 종료0. 폭탄/장벽/비콘/Player 터렛·소유 탄환·단일 피해·HP/논리 ID/수량/잠금·부정 자료/rollback 포함 |
| 실제 World/source·환경 | 실제 source 제거/예약 취소·부분 생성/재전송·Grid/사망/제거·CU 완료·새 Match 집중1/1. 날씨 정책으로 요청10→실제5, 환경 변경 자체는 새 요청 없음도 확대 회귀에서 확인 |
| Editor 창 | nographics에서 그래픽 장치 오류4개를 분리하고 D3D11에서 해당 창을 포함한19개 검사 통과. 도구 오류를 제품 성공으로 숨기지 않음 |
| 두 실행 프로세스 | 같은 Windows 개발 Build에서 UTP Host/Client, 협동 Match3개·공유 귀환2회·Client 시계/환경 세대 교체·AppRoot1개·마지막 Enemy0. 양쪽 종료0. **구조 정리 후 최종 Build·새 증거 폴더의 두 프로세스 재검사도 동일하게 통과** |
| 저장 경계 | 실제 file→새 Match 검사는 외부 **Weather 대역**을 사용함. 운영 Scene의 실제 Weather writer 완료로 표시하지 않음 |
| Repository | Python50/50, .meta 오류0. Python 최초 temp 권한 실패는 프로젝트 안의 실행별 tempfile로 해결했으며 전역 설정을 바꾸지 않음 |
| 보존/코드 품질 | 개인3파일 SHA256·stash10개 원본/순서 보존. 보완19파일 서식 Roslyn 토큰 동일. 별도 상태 원본/전역 Service Locator 추가 없음 |

각 실행의 XML/로그와 중간 실패·fixture 차이·최종 근거는 Git 제외 `Docs/Local/Work/CoreCompletion-20261105`에 보관했다. 한 번에 전부 통과한 실행으로 합쳐 쓰지 않는다. 기존 Player/Enemy 회귀의 main에서도 있던 prefab 경로/기대값·물리 대기 실패는 기존 비교 기록에 남아 있으며, 팀 전체의 모든 검사가 녹색이라는 보고가 아니다.

## 현재 수동 확인과 미구현의 구분

[최신7단계·남은 Core 구현·다른 팀원 담당](2026-11-05-core-local-review.md)을 따른다. 이 결과 문서 작성 뒤 낮밤/실제 Steam SDK/Host 포털/0.9초 페이드/날씨 HUD를 구현했다. 예전 미구현 목록을 그대로 현재 상태로 읽지 않는다.

현재 운영 전체 Save는 실제 Weather writer 부재로 차단된다. 원격 Player 본체와 미션 콘솔은 해당 담당자 연결이고, Core에 남은 Steam 게임 transport·Client 출입 승인·Client 남은 시간 동기화와 구분한다. 자동 저장 사건·Boss 승리 등 정책 미정은 PL이 결정하기 전까지 의도적으로 만들지 않았다.

## 팀원에게 추가로 알려야 할 것

기존 네 명 전달본을 보존하고 추가본을 `Docs/Local/Review/CoreCompletion-2026-11-05/by-owner`에 각자 보낼 수 있게 나눴다. 파일·왜 넣었는지·Core 어느 API에 연결되는지만 적었다.

- Player/UI: 실제 시간/입력·HP/무기/스킬/장치 writer와 감속 수신 추가, 최신 입력/preview 유지, 실제 서버 원격 actor 등록 계약.
- FieldTurret/CU: 기존 전력/자동 복구 원본 유지, 공용 시간·실제 탄환/미사일/예산/큐 복원, CU 명시 초기화와 Host 검증.
- Enemy/Encounter: 실제 환경/origin/예약과 생성 수명/원본 writer, 원거리 발사 상태 v2·Animation Event 정지 방어, **관측 EntityId가 이제 논리 ID라는 변경**.
- World/Weather: CountTick/환경 Core bridge, 실제 Weather writer에 남은 예약을 재등록할 API, 진태 고유 구현을 대신하지 않은 경계. 공유 Scene 출격 API까지 Core에서 연결.

## Issue와 PR 준비 상태

본인 이슈18개를 최신 구현/검증/외부 의존으로 갱신하고 읽기 일치를 확인했다. main에서 문제의 구형 Monster 계층이 제거되어 소멸한 **#403만 종료**했다. 나머지는 로컬 구현을 main 완료로 닫지 않고 [Issue별 종료 후보/유지 이유](2026-11-05-core-requirements-and-issue-map.md)를 남겼다. 특히 #599는 실제 Weather writer, #528은 실제 Player/HUD 인수, #420/#560은 팀 전체 인수/결정이 필요하다.

목적별 커밋을 유지했다. PR을 나눌 때 공용 계약/시간→Feature 연결→실제 저장/참조→App/Scene/화면→문서 순서를 따른다. 이번 후속의 감속, 원거리 상태, 입장 실패/취소, 공유 Scene, 진단 ID, 서식/구조 정리도 별도 검토할 수 있다. **지금은 PL이 Core를 읽고 수동 확인하는 단계이며 PR은 올리지 않는다.**
