# 우주선 첫 출격·왕복 패치 결과

**최신 연출 수정:** [동일 카메라·1.5초 페이드·첫 출격 위치](2026-11-05-spaceship-fade-results.md). 아래는 이전 검은 로딩 방식 시점의 기록입니다. 현재 우주선↔필드는 문구/진행 바 없이 페이드로 전환합니다.

기준일: 2026-11-05. 현재 브랜치의 낮밤·Steam 구현 이후 요청한 후속 패치입니다. PR/push/merge는 하지 않습니다.

## 고친 것

1. 우주선 문이 `Initial_Stage`의 사전 로드를 요구하던 조건을 제거했습니다. 시작 우주선에서 나가면 Core GameFlow가 정식 Mission을 열고 Match 준비까지 기다립니다.
2. 진행 중 Match의 우주선 방문은 Additive로 유지했습니다. 기존 Player와 Match를 유지하고, 실제 이동 완료 뒤 Spaceship/Field 위치를 보고합니다. 첫 출격과 방문 복귀를 같은 Scene 교체로 처리하지 않습니다.
3. 실제 Player의 몸체 Collider는 자식 `PlayerBody` 레이어에 있습니다. 출입문은 PlayerActorIdentity를 찾아 판단하도록 바꿨습니다. 여러 자식 Collider가 가까이 있을 때 문 애니메이션의 접근 상태도 한 Collider의 Exit만으로 끄지 않습니다.
4. 필드 입구의 존재하지 않는 도착 이름 `SpacshipEnter`를 내부 표식 `Capsule`로 수정했습니다. 돌아올 위치는 실제 필드 입구 Collider 밖으로 계산합니다.
5. 검은 화면에 기존 폰트로 **임시 로딩 화면**과 하단 바를 표시합니다. 실제 프레임이 지나 화면이 덮인 뒤 이동하며, Camera를 도착 위치에 즉시 맞춘 뒤 다시 보여줍니다. 평소 Camera의 부드러운 추적은 유지합니다.
6. 포털 이동 중 다른 Scene 교체 요청과 중복 출입을 막았습니다. 잘못된 도착 표식으로 방문에 실패하면 Player 위치·입력·위치 보고를 복구하고, 해당 요청이 새로 로드한 Scene만 정리합니다.

## 이번에 직접 확인한 범위

격리한 동일 Unity 프로젝트에서 실제 Startup/SpaceShipGrayBox/Initial_Stage와 기존 Player prefab을 사용했습니다. 사용 중인 Editor를 닫거나 Scene 작업을 덮지 않았습니다. 가짜 Player나 이동 성공 Mock만으로 판정하지 않았습니다.

| 검사 | 결과 |
| --- | --- |
| 실제 문 API로 첫 출격, 왕복 2회, 잘못된 도착 표식 실패 복구 | 통과 |
| 기존 문 BoxCollider/필드 트리거의 실제 물리 접촉으로 출격·방문·복귀 | 통과 |
| 기존 Startup 버튼·허브 이동·메뉴/사망 독립 입력 잠금 및 새 로딩 UI GPU 렌더 | 통과 |
| Match ID/동일 actor 유지, 시작 허브 Player·Camera 정리, 활성 Camera 1개 | 통과 |
| 우주선 도착 후 필드 시간 정지, 복귀 후 재개, 새 Match 중복 생성 없음 | 통과 |
| 복귀 후 입구 Collider 비접촉, 자동 재입장 없음, 로딩·입력 잠금 해제 | 통과 |
| 도착 시 Camera XY와 actor XY 일치, 검은 로딩 배경·정확한 문구 | 통과 |

중간 검수 XML은 `Docs/Local/Work/DayNightSteam-20261105/portal-physics.xml`의 3/3입니다. Player 자식 Collider에 맞추기 전의 검수 코드 실패도 앞선 실행 파일에 보존했습니다. 최종 실행·빌드 결과는 아래에 추가합니다.

[실제 로딩 화면 렌더](https://github.com/Team-HJD-21/unity-6-the-developer/blob/6ed4f15650965830dda1139b0a1b4b3baa0e1f78/Docs/images/core/2026-11-05-loading.png)

## 문서와 팀원 변경 안내

[왕복 API와 Scene 설정](../../../../02_architecture/03_scene-flow/spaceship-travel-api.md)에 책임·메서드·현재 네트워크 경계를 적었습니다. 두 by-owner 폴더의 진태님 문서에도 `SpaceshipEnter.cs`, `SpaceshipExit.cs`, Initial_Stage 도착 이름 변경 이유를 추가했습니다. 날씨 추첨·예고 큐·Weather 저장 writer는 수정하지 않았습니다.

## 사용자에게 남기는 확인

Play를 종료하고 Startup부터 다시 시작한 뒤 문으로 나갔다가 돌아오는 **연출과 조작감**만 확인하면 됩니다. 자동 검사는 실제 물리 접촉까지 포함하지만 사람이 문에 접근할 때 애니메이션/카메라 느낌을 대신 승인하지는 않습니다. Editor 재시작은 필요하지 않습니다.

실제 2인 Client 출입 RPC/원격 actor 권위 연결은 추가하지 않았습니다. 보고권 없는 Client actor는 이동 전에 거부하므로 잘못된 위치 보고로 공용 시간만 틀어지게 하지 않습니다. 로컬/Host 왕복 통과를 원격 Client 출입 완료라고 적지 않습니다. 기존 2인 NGO 전체 Scene 이동과 이번 개인 Additive 방문은 다른 경로입니다.


### 최종 재검사

- `portal-final.xml`: **3/3 통과, Unity 종료 0**. 마지막으로 추가한 포털 이동 중 별도 Scene 교체 거부까지 포함했습니다.
- `native-build-portal.log` / `.exit`: **Windows Standalone Build Success, 종료 0**. Editor 전용 코드로만 돌아가는 상태가 아님을 확인했습니다.
- 검수용 테스트/빌드 드라이버는 격리 프로젝트와 공유 제외 Docs/Local 안에 두었습니다. 제품 Runtime에 검수 드라이버를 넣지 않았습니다.
- 기존 폰트·DefinitionId·ProjectSettings의 개인/Editor 변경과 stash는 이 패치 커밋에서 제외했습니다. PR/push/merge는 하지 않았습니다.
