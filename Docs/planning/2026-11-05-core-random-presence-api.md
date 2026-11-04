# 11월5일 Core 범용 Random / 출입 보고 API 제공

## 제공 결과

진태의 날씨/시간/우주선 연결 작업에 앞서 Core API를 구현했다. Weather·Spaceship·Enemy·터렛·HUD 소스와 Scene/Prefab은 수정하지 않았다.

1. [범용 Random 사용법·주입 예제·기존 교체 목록](../architecture/core-random-api.md)
2. [출입 보고·Host 권한·actor 등록·거부 결과](../architecture/core-presence-report-api.md)

Random은 정수·실수 범위, 확률, 목록/가중치 선택, 셔플, XY 원판 좌표를 같은 함수 세트로 제공한다. 이름별 독립 상태와 Match/App 수명을 갖는다. 게임 난수는 Host Match, 로컬 효과는 App의 PresentationRandom을 사용한다. Core writer로 난수 상태의 바이너리 저장/복원과 실패 백업을 제공한다. 자동 저장 사건은 추가하지 않았다.

출입은 실제 Host actor 이동 완료 후 TryReportLocation으로 보고한다. Core가 전원 내부 여부로 필드 정지/재개를 판단한다. Client 로컬 호출을 전송하는 RPC는 추가하지 않았다. 원격 이동 요청은 기존 Player/포털의 Host 검증·이동 완료 경로에서 연결해야 한다.

## 검증 기록 — 테스트 소스 제외 전 e87bdf91 기준

- Unity 6000.3.23f1 컴파일 오류 0.
- 선택 EditMode 60/60: 새 Random/보고 계약과 기존 위치·시간·저장·App 종료 검사. 실패/제외 0.
- 추가 3/3: Match의 Random 소유/완료 후 차단 2개, 빈 Scene에서 실제 PlayMode 출입 보고·정지·actor 파괴 해제·난수 복원 1개. 실패/제외 0.
- 두 실행 모두 Unity 종료 코드 0. 테스트 실행 중 오류를 무시하거나 테스트를 제외하지 않았다.
- **PR 전 최종 검수:** 이번 API C# 21개 파일의 압축 문장·메서드 간격·들여쓰기를 정리했다. Roslyn 토큰 종류/값 비교가 21/21 일치하여 실행 의미 변경이 없음을 확인했다. 새 파일/대상 파일은 UTF-8/LF·마지막 개행으로 기록했고 혼합 개행/NUL/후행 공백을 확인했다.
- **서식 정리 후 단일 재실행:** 컴파일 오류 0, 기존 API/실제 PlayMode 포함 63/63 통과, 실패/제외 0, 종료 코드 0.
- **문서 예제:** Random 사용 문서 5개 코드 블록과 Core README·PR 예제를 문서의 인자 문맥으로 감싸 실제 Unity/Core DLL에 대해 컴파일했다. 7/7 성공, 컴파일러 종료 코드 0.
- 기능 소스에서 기존 난수 직접 소비는 6개 파일 7개 호출부. 교체 목록만 작성했고 변경하지 않았다.
- 기존 사용자 폰트·DefinitionId·ShaderGraphSettings의 시작 시점 hash 일치. Feature/Map/UI/Scene/Prefab diff 없음.
- 검증 XML/로그/프로세스 종료 기록은 Git 제외 Docs/Local/Validation/CoreSharedApis-20261105에 보관한다.

실제 Weather 선택/예고, authored 우주선 포털 이동, 원격 Player 생성/RPC의 연결 검증은 이번 API 단위 검사와 구분한다. 최신 main의 승인된 연결은 선행 #615에서 유지하며, API PR에서 Feature 실험/stash를 되살리지 않는다.

## 후속 — API 테스트 소스 제외

PL 요청으로 이번 PR에 추가했던 NUnit 테스트 7개와 해당 .meta 7개를 제외했다. 기존 프로젝트의 테스트는 삭제하지 않았다. 위 63개 통과 기록은 삭제 전 검수 커밋의 증거로 보존하며, 제거 이후 새로 63개를 실행했다고 표기하지 않는다. Core의 실제 API 구현은 변경하지 않았다.

## 전달 순서와 PR 범위

- Core: API와 사용법을 먼저 제공한다. 전체 게임 판정용 난수 진행과 정지 판단을 소유한다.
- 진태: IRandomConsumer로 난수를 주입받아 Weather에서 사용하고, 실제 Host 이동 완료 지점에서 출입 결과를 보고한다. 자신의 날씨 원본과 남은 전환은 별도 writer로 연결한다.
- Enemy/Encounter·FieldTurret·Player/UI: 난수 교체 목록을 참고해 각자 기능에서 호출부를 교체한다. 게임/연출 흐름을 혼용하지 않는다.

이 작업은 Random과 출입 보고의 두 커밋으로 구분하며 같은 API 제공 PR에 포함할 수 있다. 전체 시간/안전장치/GameFlow Issue를 이 변경만으로 완료 처리하지 않는다.

PR 검수 보강은 별도 후속 커밋으로 묶는다. Random 도입 이유, 주입/선택/셔플/연출 예제와 교체 전 확인 사항을 확장했으며 Docs 목차·Core README·API 규약에서 접근할 수 있다. PR 상단에는 이 문서를 반드시 읽도록 명시하고 실제 호출 예제를 싣는다.

## 최신 main 위의 PR 정리

선행 [#615](https://github.com/Team-HJD-21/unity-6-the-developer/pull/615)는 최신 main 3a1f2498 위에 필요한 Core 기반만 통합한다. 과거 a95f8fd3 스냅샷을 통째로 main에 합치지 않아 팀원 변경을 되돌리지 않는다. [#613](https://github.com/Team-HJD-21/unity-6-the-developer/pull/613)은 이 선행 기반을 base로 하는 API 추가분만 유지한다. 순서는 #615 → #613이며 아직 병합하지 않는다.

기존 API 코드·검증 증거는 보존하고 문서의 최신 main/선행 PR 설명만 조정했다. Feature/기존 Scene/Prefab/패키지 변경과 신규 검사 파일은 API diff에 포함하지 않는다. 선행 기반 반영 뒤 #613의 base를 main으로 전환해 검토·CI를 확인한다.

### 정리 후 현재 구성 검증

선행 기반 #615 위 API 최종 구성으로 Unity 6000.3.23f1 컴파일 오류 0, 기존 App 종료·Initial_Stage 제작/실제 PlayMode 검사 7/7 통과, 실패·제외 0, 종료 0. 임시 API 검사 63개의 이전 기록을 현재 다시 실행한 것으로 표시하지 않는다. API 추가분 41개 경로는 Core와 공개 문서로 한정하며 신규 테스트/Feature/Scene/Prefab/Packages 변경은 없다. 새 API 클래스 12개가 기존 검수 소스와 동일함을 대조했다.
