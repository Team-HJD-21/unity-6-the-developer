# Unity Runtime 통합 검사 실행 기준

갱신: 2026-11-02. Unity 6000.3.23f1 / 설치된 Test Framework 3.5.0 기준.

- EditMode 검사에서 `EnterPlayMode`를 반환해도 테스트의 실행기는 EditMode runner다. 일반 `WaitForSeconds`와 `WaitForFixedUpdate`를 실제 게임 coroutine 대기로 취급하지 않는다. 시간 경과는 `Time.timeAsDouble`, 물리 step은 `Time.fixedTimeAsDouble`의 변화로 확인하면서 `yield return null`을 반환한다. 진행 중단으로 영구 대기하지 않도록 별도의 realtime timeout을 둔다.
- `WaitForSecondsRealtime`처럼 IEnumerator인 yield는 설치본의 별도 경로로 처리된다. 모든 yield가 같은 문제라고 일반화하지 않는다. 설치된 Test Framework 소스와 [Unity의 Edit/Play 검사 안내](https://unity.com/how-to/automated-tests-unity-test-framework)를 함께 확인한다.
- 독립 탄환/물리 fixture는 빈 Scene에서 시작한다. 직전 검사에서 열었던 실제 Stage가 자동 부팅되어 새 테스트에 섞이지 않게 하며 종료 후 이전 Scene setup을 복원한다. 실제 Stage와의 통합 검사는 해당 Scene을 명시적으로 로드한다.
- Additive 검사는 활성 Scene과 actor의 소속 Scene을 별도로 확인한다. Player는 PlayerStart의 Match Scene에 속해야 한다.
- `SerializedObject`는 직렬화된 제작 값 검사에 사용한다. private 비직렬화 Runtime 필드는 직렬화 속성이 존재한다고 가정하지 말고 공개 관측값 또는 제한된 테스트 reflection으로 확인한다. 테스트 편의를 위해 Runtime 상태를 Inspector 저장 데이터로 바꾸지 않는다.
- 스폰 분산은 현재 적 수뿐 아니라 선행 자동 요청의 셀별 서비스 이력을 포함한다. 반복 구간의 생성 수만 비교할 때 시작 부하의 차이와 요청당 이산 분대 예산을 반영한다. 허용 후보 전체 사용/실시간 셀 분산 검사도 함께 유지하며 제품 정책을 테스트 통과를 위해 변경하지 않는다.
- Unity 프로세스의 실제 종료 코드와 NUnit XML의 total/passed/failed를 확인한다. 일반 PowerShell의 성공 종료나 XML이 없는 실행을 테스트 성공으로 기록하지 않는다. 부분 실행의 통과를 결합 실행 전체 통과로 합산해 표시하지 않는다.

11월 2일 회귀 기록: 확장 검사 91개 중84개 통과 후7개 실패를 보존했다. fixture의 대기·상태 조회·Scene 분리, 실제 Additive Player 소속을 수정한 뒤 실패 범위를 포함한10개 집중 검사가 모두 통과했다. 전체91개를 다시 실행해 통과했다고 표시하지 않는다. 실제 포털/다중 프로세스 인수는 별도다.
