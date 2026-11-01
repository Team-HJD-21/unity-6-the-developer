# Match 환경 API

현재 구현: B7 (2026-10-26). 날씨와 시간대는 독립 정보다. 환경 변경은 상태 변경이며 Spawn 명령이 아니다.

2026-11-02 시간 기반 이관: 권위 Initial_Stage의 `IMatchClock`은 `MatchTimeService`의 공용 시계로 연결했다. 네트워크 Host는 NGO ServerTime을, standalone은 Unity double 실시간을 사용한다. 완료 후 예약 차단까지 실제 Scene에서 확인했다. 현재 WeatherManager는 `IMatchScheduledEnvironmentFeature`로 공용 scheduler를 받고 CountTick이 제작 간격을 등록한다. 정식 Match에서는 polling 호환 경로가 동시에 전이하지 않는다. Client 시각 복제·참가자 필드 정지는 후속이다. [시간 API와 실제 이관 범위](match-time-api.md)를 따른다.

## 소유권과 수명

- World의 `Map.WeatherManager`가 현재 날씨와 예고를 결정한다. `CountTick`은 해당 Feature에 간격 진행만 요청한다.
- scheduler를 제공한 Host 바인딩에서는 CountTick이 간격 예약을 등록하고 lease/취소 토큰으로 해제한다. 긴 프레임의 반복은 한 번으로 합친다. standalone·scheduler 없는 호환 바인딩만 기존 TickWeather 진행을 사용한다.
- `InitialStageCompositionRoot`가 같은 Scene의 활성 `IMatchWeatherSource` / `IMatchTimeOfDaySource`를 각 1개 이하로 연결한다. 모호한 중복 source는 초기화 실패다.
- `MatchEnvironmentSceneScope`가 Host의 Match clock·seed·gameplay 취소 lease를 연결한다. 완료 후에는 진행하지 않고 Session 폐기 시 구독과 lease를 정리한다.
- `MatchEnvironmentRuntime`은 확정 정보를 불변 projection으로 보관한다. HP·날씨 효과·Enemy 알고리즘을 대신 소유하지 않는다.
- `EnvironmentReplicationSession` / `NgoEnvironmentTransport`가 초기·변경 전체 상태를 같은 준비 세대의 Client에 전달한다. 미전송 상태는 최신 값으로 합친다. Client World에는 읽기 lease를 주어 자체 Random/타이머를 막는다.

## 읽기와 실제 소비

`AppRoot.CurrentEnvironment`는 현재 snapshot이다. `MatchSession.Environment` / `AppRoot.NetworkEnvironments.Source`는 현재 값과 `Changed` 통지를 제공한다. snapshot에는 MatchId, revision, 확정 시각, 날씨 ID, 시간대 ID, 복사된 날씨 예고가 있다. 없는 축은 `HasWeather/HasTimeOfDay=false`이며 낮/맑음으로 추정하지 않는다.

`IEncounterEnvironmentConsumer.BindEnvironment`를 먼저 연결하고 공통 `EncounterRequestService.Submit`을 사용한다. 서비스는 권위 측의 현재 Battlefield와 환경을 한 번 조회하고 같은 Match인지 확인한 뒤 `IEnvironmentAwareEncounterExecutor`로 전달한다. 재전송은 기존 결과와 사용 환경 revision을 재사용한다.

Enemy의 `SpawnCompositionPlanner`가 선택적인 `EncounterEnvironmentProfile`을 소비한다. 규칙은 weather/time 필터, preset override와 0~1 budget scale로 구성한다. 가장 구체적인 일치 규칙을 선택하고 같은 구체성이면 제작 순서가 우선이다. 요청 예산을 초과하지 않으며 기존 공간 배분·완전 preset 단위 정책을 유지한다. Profile 미지정은 중립이다. 최종 규칙·수치는 Enemy/밸런스 담당이 제작한다.

## 네트워크와 관측

환경 채널은 `teamhjd.core.environment.v1`이다. 서버 발신, 준비 Descriptor, Match, revision, clock을 검증한다. 같은 revision의 충돌·과거 상태·다른 준비 세대는 거부한다. 종료 시 읽기 모델은 비워지고 다음 Match에 재사용하지 않는다.

Game Debugger Overview는 **현재** 환경, Statistics의 요청 상세는 **판단에 사용한** 환경 revision/규칙을 표시한다. JSON에는 요청 당시 날씨·시간대·예고를 복사한다. 이전 기록은 미계측으로 남긴다. 기존 Run의 content version/seed/Build label과 함께 해석한다. Build label은 사용자가 명시해야 하며 Seed만으로 전체 게임의 결정론 replay를 보장하지 않는다.

## 검증과 한계

- 실제 Initial_Stage의 Weather → Match → Enemy Planner → NGO 생성 경로를 실행했다. 별도로 주입한 검증용 rain/0.5 규칙은 10 한도에서 실제 5마리를 생성했다. 날씨 변경만으로 요청 수가 늘지 않았으며 JSON 보존도 확인했다.
- 계약 검사는 불변성, 미제공 축, 권위 종료, observer 실패, 과거/다른 Match·준비·시계 역행, 요청 중복을 확인했다.
- 실제 시간대 source는 현재 없다. 시간대 계약이 존재하는 것을 실제 주야 기능 완료로 표시하지 않는다.
- 실제 날씨 전투 효과·최종 밸런스·나무/투하 생성 콘텐츠·두 실행 파일 Player 복제는 이 배치의 완료 주장이 아니다.
- 재현 증거와 실제 UTP 검사 결과는 [배치 기록](../planning/2026-10-26-core-sprint-implementation.md)에 누적한다.
