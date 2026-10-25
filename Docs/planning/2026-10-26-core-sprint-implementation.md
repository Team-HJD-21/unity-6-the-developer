# Core 5·6주차 배치 구현 기록

기준일: 2026-10-26. 목표: 기존 후속을 최신 main에 통합하고 CU 초기화·네트워크/Match 운영·Scene/UI·환경/추가 생성 경계를 순서대로 연결한다. 목표와 실제 완료를 구분한다.

## 기준과 커밋

- 시작 main: `1560df6c`. 기존 후속 source: `f00036c0`, 이미 squash된 첫 기반 이후 차이만 적용했다.
- `1663f7cf` / B0: 기존 후속 통합. 최신 main의 Scene 객체 ID 모두 유지, 중복 local fileID 0, 터렛 identity 8개. Player prefab·HUD/날씨 Scene 추가를 보존했다. 패키지 버전 변경 없음.
- `a34d91b3` / B1: [관리 책임·수명 계약](../architecture/match-management-responsibilities.md). Feature HP 원본과 Core 읽기 projection을 구분한다.
- B2: 아래 CU 초기화와 준비 검사. 각 후속 배치는 실제 구현 후 기록한다. PR/이슈 자동 종료 없음.

## B2 CU 초기화

1. 준비 전에 CU/Controller가 현재 Scene에 있고 활성인지, 지역/터렛 바인딩이 같은 Controller를 쓰는지 검사한다. CU 좌표/Grid/Inspector 구역 수를 읽기 전용으로 검증한다.
2. Host 조립은 터렛 Match lease와 Session을 먼저 만든다. 아직 Core RegionAccessPolicy를 붙이지 않은 시점에 `ConfigureUnlockZones(cuPosition)`를 명시적으로 호출한다.
3. 정적 초기 구역은 1, 총 구역 수는 D Inspector 설정이다. false/예외는 조립 실패로 전달하고 기존 공통 rollback이 Match/actor/Feature 바인딩을 정리한다.
4. Dynamic은 준비 검사를 공유하지만 정적 잠금을 적용하지 않는다. 임시 삼각형이 확정 셀을 계산하며, 이후 점령 갱신/모드 Reset은 RegionRuntime만 쓴다. Client는 Host snapshot을 연결한다.

## 이번 head 검증

Unity `6000.3.23f1`, 새 사본에서 공식 batch EditMode 실행. 원본 Editor/Scene은 변경하지 않았다. XML/로그는 로컬 `Docs/Local/Validation/CoreSprint20261026/`에 보관한다.

| 검사 | 결과 | 직접 확인 범위 |
| --- | --- | --- |
| `b0-core.xml` | 종료 0, 18/18 | 전체 스크립트 컴파일, NetworkMatchCoordinator / TurretCommandGateway / RegionGridReplica / AppShutdown |
| `b2-scene.xml` | 종료 0, 7/7 | 실제 Scene의 읽기 전용 준비, CU 누락/범위 밖·서로 다른 Controller 거부, Static→Dynamic 점령/회수, 실제 Host 시작·Enemy 생성·종료·연결 유지·새 Match 재시작 |
| `b3-network.xml` | 종료 0, 19/19 | Host 최종 결과·변조/이전 준비 거부·단일 관측·backpressure 재시도·결과 보존, codec·실제 UTP 두 SDK 인스턴스의 결과 전달/재시작, 기존 터렛 명령/종료 |
| `b4-match-fixed.xml` | 종료 0, 26/26 | 실제 Initial_Stage CU 파괴·Host 결과·Player/추가 생성 차단·Enemy/터렛 작업 정리, 중복/재진입·관측/취소 예외·기존 Scene/cleanup 회귀 |
| `.meta` 검사 | 오류 0 | 실제 Assets 전체 파일/메타 짝 검사 |

Scene 검사는 Test Framework가 PlayMode로 진입해 실제 조립/API/객체를 확인했다. 사람의 마우스 입력·해상도별 UI 외형·실제 원격 Player 이동 복제·전체 협동 완료를 의미하지 않는다. 이전 10/15 검증은 당시 source 증거로 별도 보존한다.

## 다음 구현과 미결

- B3 구현: Host 최종 결과의 Client 전달·gameplay gate, pending 재시도와 정상 종료 보류, 결과 보존을 연결했다. Lifecycle v2로 변경했으며 같은 Build가 필요하다. 한 프로세스 두 SDK 검사를 실제 원격 Player/두 실행 파일 검증으로 확대하지 않는다.
- B4 구현: 실제 CU 파괴 → MatchCoordinator → StoryModeRules의 관측값 판단 → Session 단일 완료 → 취소·Host 결과 게시·실제 Enemy/터렛 작업 정리. 완료는 Session/연결 폐기와 구분한다. 기존 CU가 파괴된 Scene에서 단순 TryPrepare만 다시 호출하는 것은 초기 상태 복구가 아니다. 재시작은 다음 Scene/UI 배치에서 명시적 재로드로 연결한다.
- B5/B6: SceneFlowController / ScreenManager의 실제 결과·로딩·전환, owner별 로컬 입력 잠금.
- B7/B8: 환경 context의 실제 Encounter 소비와 포인트 밖 origin/source의 실제 생성/추적.
- B9: S1~S3 실행 수준별 인수. 실제 Player NetworkBehaviour는 이영빈, 터렛/수리·전력은 황재동, Encounter/생성·AI는 조수빈, World/환경 표현은 김진태가 소유한다.

최종 제품의 CU 연결 점유·보스 게이트·저장/복귀 정책, OFF 터렛 타깃 가능 상태 및 수리 계약은 별도 결정/담당 연결을 보존한다. 미결을 새 임의 규칙으로 채우지 않는다.

B4 최초 검사 `b4-match.log`는 검사 코드의 HP 속성명/using 누락 2건으로 컴파일 실패했다. 수정 후 `b4-match-fixed.xml` 전체 26/26 통과했으며 실패 로그도 보존했다. 제품 예외를 무시하거나 검사를 제거해 통과시킨 것이 아니다.
