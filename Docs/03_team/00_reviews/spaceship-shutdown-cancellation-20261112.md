# 포털 이동 중 App 종료 취소 처리

기준일: 2026-11-12. 상태: Core HOTFIX 구현·로컬 Play 검사 완료. PR은 아직 생성하지 않았다.

실제 Initial_Stage 검사에서 Player가 포털에 접촉해 우주선 진입을 시작한 순간 Play가 종료되면 다음 경로로 `OperationCanceledException`이 전파됐다.

```text
MonoBehaviour.RaiseCancellation
  SceneFadePresenter.BlendAsync
  SceneFadePresenter.RunAsync
  SpaceshipTravelService.EnterAsync
  SpaceshipEnter.EnterAsync / OnTriggerEnter2D
```

[SpaceshipTravelService](../../../Assets/Scripts/Core/Bootstrap/SpaceshipTravelService.cs)의 `EnterAsync/ExitAsync`는 첫 await 전에 App 수명 토큰을 보관한다. 그 토큰이 취소된 `OperationCanceledException`만 정상 종료로 처리한다. 종료 중에는 사라지는 Player/Scene을 대상으로 이동 rollback을 시도하지 않는다. 일반 로드 실패·잘못된 목적지·수명이 끝나지 않은 상태의 취소는 계속 호출자에게 전달한다.

[SceneFadePresenter](../../../Assets/Scripts/Core/Presentation/UI/SceneFadePresenter.cs)의 finally는 UI 객체가 아직 살아 있을 때만 alpha/활성을 정리한다. 진태님 `SpaceshipEnter`·Weather·Scene·Prefab은 수정하지 않았다. 필드와 우주선의 실제 이동 정책·0.3초 Fade 단계는 바꾸지 않는다.

수정 전 실제 Play 실패와 수정 후 1/1 성공을 보존했다. 같은 Play 검사에서 `RunAsync`에 별도 호출자 토큰을 취소하면 `Task.IsCanceled=true`, 이동 미실행, Fade 비표시가 되는 것도 확인했다. 종료 취소와 일반 취소를 구별한다. 증거와 중복 범위는 [CU 배치 실행 결과](../../01_planning/02_implementation-records/2026/11/2026-11-12-cu-connected-regions-results.md#실제-확인한-범위)에 있다. 이 HOTFIX는 CU 정책 변경과 별도 커밋으로 되돌릴 수 있다.
