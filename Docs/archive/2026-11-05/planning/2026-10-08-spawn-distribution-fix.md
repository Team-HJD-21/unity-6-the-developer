> 보관 문서 · 2026-11-05 교체 전 스냅샷
> 기준 커밋: `1a03e9a9164e5db94cbc8775162467d08cf63a3b`. 당시 설명·검증 근거이며 현행 실행 지시가 아니다.

# 2026-10-08 스폰 공간 분산 수정 결과

## 원인과 수정

사용자 런타임 관측 후 Unity CLI로 기록 `0b18aeb47aa946aca40f70a36731c11f`를 조회했다. 요청 122회/실제 생성 1,220마리 중 `Encounter Spawn Right (26)`에 420마리(34.4%), Right에 205마리, Left에 160마리가 몰렸다. 사용 지점은 10개뿐이었다. 상세가 남은 요청 91~122에서 42개 지점을 모두 후보로 관측했고 `LaneA_02` 하나만 spawn disabled/captured로 제외됐다. 요청 122에는 적 0인 미배정 적합 후보 31개가 있었다. 이벤트 누락은 0이며 오래된 공간 상세 90개는 정상 보존 상한으로 제거됐다. 따라서 전체 이벤트 합계는 확인됐지만 오래된 후보 탈락 사유까지 추정하지 않는다.

원인은 요청마다 새로 계산한 지점별 예정량, 전선 최단 거리 우선, 이동 후 0이 되는 셀 점유였다. 같은 셀의 복제 지점들도 예정량을 공유하지 않았다.

수정 설계는 [Spatial Spawn Distribution](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/architecture/spatial-spawn-distribution.md)이다.

- **Runtime:** 셀을 먼저 선택하고 셀 내부 지점을 고른다. Match별 service와 셀 공유 예정량을 도입하고 실제 Executor 생성만 commit한다.
- **Contracts / Editor:** service/pending/final score를 불변 판단 DTO → 독립 기록 → 후보 표/선택 상세에 전달한다. 이전 JSON은 N/A, 새 값은 round-trip 및 음수 검증을 제공한다.
- **Docs:** Core README, Game Debugger 사용법 및 별도 문서 사이트의 촬영 전 초안을 최신 정책에 맞춘다. 사이트 push/배포는 하지 않는다.

## CLI 검증

| 검사 | 결과 / 증거 |
| --- | --- |
| 공간 정책 회귀 + 판단 JSON/UI + 등록 테스트 | **24/24 성공**, `Logs/spawn-distribution-editmode.xml` |
| Scene 통합 및 기존 자동/UI/Scene/Debug/반복 경로 | **2/2 성공**, `Logs/spawn-distribution-integration.xml` |
| 최신 동적 Grid 입력을 추가한 공간 통합 | **1/1 성공**, `Logs/spawn-distribution-live.xml`. 앞 통합과 동일 테스트의 보강 재실행이므로 합산해 별도 테스트처럼 계산하지 않는다. |
| 최종 null 보호 보완 후 정책/기록 재검사 | **20/20 성공**, `Logs/spawn-distribution-final.xml`. 집중 회귀와 중복되는 재검사다. |
| Unity .meta 짝 검사 | 오류 0개. 신규 History 스크립트의 GUID 중복 없음. |
| 문서 사이트 로컬 검사 | API/에디터 도구 링크·목차·사진 14개 번호 검사 통과. 배포하지 않음. |

순수 정책은 요청 예산보다 많은 후보의 반복 분산, 같은 셀 복제의 비중 불변/지점 교대, 밀도가 높은 셀의 무한 starvation 방지, 미실행/실패/부분 성공, Match 격리·stale commit 거부, 신규 후보 기준값, XY Grid reset/Z 유지, 입력 순서 불변을 검사했다.

실제 `Initial_Stage`에는 **삼각형 6개/전선 변 8개**, 적합 스폰 셀 30개가 있었다. 동일 snapshot을 사용해 이동 후 빈 셀 조건을 고정한 48회/480마리 NGO 생성에서 **30/30셀 사용**, 셀별 최소 15/최대 20마리였다. 실제 생성/이력 commit을 우회하지 않았다.

이후 적 이동과 살아 있는 적의 **최신 Grid snapshot**을 사용하는 0.1초 간격 24회/240마리 NGO 요청에서 **27/30셀**을 사용했다. 현재 적 밀도가 있으므로 이 결과를 정확한 균등 수량이라고 주장하지 않는다. 기존 일부 가까운 지점의 반복 독점 대신 공간적으로 분산됐음을 검증했다. 장시간 최종 밸런스, 영향력/우세, 모든 네트워크 Client 및 Windows Build까지 검증한 것은 아니다.

첫 집중 검사 22/24에서는 NUnit이 fixture 인스턴스를 재사용해 새 상태형 정책의 이력이 테스트 간 공유된 두 실패가 있었다. 테스트별 `[SetUp]`에서 Policy를 생성하도록 수정했고 24/24 재실행에 통과했다. 결과 파일은 Editor 종료 때 정리되는 Temp 대신 ignored Logs에 보존한다.

## 사용자 확인

1. 최신 코드로 컴파일 후 `Initial_Stage`에서 새 Play를 시작한다. 이전 실행을 그대로 이어서 새 정책의 결과라고 비교하지 않는다.
2. Game Debugger → Encounter에서 Normal/10 일회 요청 후 Repeat를 시작한다. 비교 실험은 시작 상태·clock·간격을 맞춘다.
3. Statistics → Spawn decisions / distribution에서 후보별 Cell service / Cell pending / Final cell score / Point service와 planned/actual을 본다.
4. 같은 셀의 지점들은 셀 생성 비중을 나눠 갖는다. 지점별 수량이 모두 같은지를 목표로 삼지 않는다. 무조건 모든 셀에 한 요청마다 생성하는 것도 아니다.
5. 이전 기록의 service는 N/A다. 사진 #11은 수정 이후 열을 포함해 촬영한다.

## 보존과 범위

커밋은 Runtime/Contracts/정책·통합 회귀(`ea31faf7`, `fix(spawning): distribute match spawns using shared grid cell service`)와 Editor 진단/UI·사용법(`feat(debugger): inspect spatial spawn service and document distribution`) 두 단위로 분리한다. 별도 문서 사이트 변경은 사진 전 로컬 초안으로 유지한다. 이번 작업은 push/PR/배포를 수행하지 않는다.

이번 작업 전부터 있던 `Initial_Stage.unity` 사용자 수정(추가 1,715/삭제 23줄)은 유지했고 이번 수정/커밋 대상에서 제외한다. 기존 `Validation/`도 건드리지 않는다. Scene authoring·터렛·스포너 개수/좌표/활성 플래그를 임의 변경하지 않았다. 수정은 요청 경로의 생성 선택/진단에 한정한다.
