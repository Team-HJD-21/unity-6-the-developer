# 최종 진입·승패·결과의 연결 범위 정정

2026-11-14 사용자 확정: **이번 자동 연결 제외, API 유지.** 실제 보스전·승리 조건·결과/사망 화면을 확정·제작한 것으로 진행하지 않는다.

## 바뀐 동작

| 대상 | 현재 동작 |
| --- | --- |
| 100% 점유·두 Field 생성 | 준비 상태만 읽음. Root Update 자동 보스 Scene 진입 없음 |
| 최종 등록 개체의 HP 사망 | 진행 사실만 기록. Coordinator의 자동 승리/결과 연결 없음 |
| Scene 준비 | Root.EnterFinalBattleSceneAsync(match, cancellation)를 명시적으로 호출할 연결 API 유지 |
| 결과/캠페인 저장 추가분 | 미커밋 변경을 복구 자료로 보관한 뒤 Assets/기존 파일에서 제외 |
| 기존 main UI·사망 처리 | 유지. 새 결과/Death 화면 외형 변경 없음 |

## 먼저 볼 코드

1. `InitialStageCompositionRoot.Update`: 자동 최종 진입 호출이 없다.
2. `InitialStageCompositionRoot.FinalScene`: 명시 API의 저장/Scene/실제 개체 검증과 실패 원복. 실패는 호출자의 Task 예외로 전달한다.
3. `MatchCoordinator.ConfigureStageProgression`: Stage의 최종 사망 알림을 자동 Match 완료에 구독하지 않는다. CompleteMatch는 명시 정책 API로 유지한다.
4. `StageProgression`:33/66 실제 생성·점유·등록 개체 사망 사실을 보관한다. 기존 Victory 이벤트 이름은 최종 사망 알림으로 문서화했고 게임 결과 확정으로 연결하지 않는다.

결과 저장 추가분은 개인 로컬 `Docs/Local/Execution/EncounterStage1-20261113/B8E_HELD`에 diff·원본 파일·해시 manifest로 보관했다. 새 source/test/meta6개를 Assets에서 빼고 기존5파일의 해당 추가분만 되돌렸다. 기존 staging8개와 다른 세션 변경은 보존했다. 원격 게시/PR은 하지 않았다.

## 확인 범위

- 최신 origin/main과 로컬 main은8fbef4a68(#722). feature/core에서 merge origin/main을 실행했고 이미 포함된 상태였다.
- 현재 assembly의 순수 NUnit17개 직접 실행 통과. Unity TestRunner 인수와 구분한다.
- 실제 Stage_1에서 시험용100%·실제 일반 Enemy ID 주입 후 자동 Scene 진입 없음, 명시 미지정 driver 오류/정지·입력 정리, 실제 HP 사망 후 Running Match/결과 화면 없음/동일 Match를 확인했다. 신규 Runtime 오류0.
- QA 종료 후 Play/컴파일/실패false, 기존 저장33개 해시 일치. 사용자 미저장 Scene은 유지했다.

이는 자연 진행·실제 보스전·승패/결과 디자인의 검증이 아니다. 이전 B8c/B8d 자동 조율과 B8e 결과 실험은 과거 기록이며 현재 자동 동작의 완료 근거가 아니다. [현재 API와 책임](../../../../02_architecture/02_match/stage-progression-api.md)을 따른다.
