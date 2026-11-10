# 우주선 카메라·페이드·첫 출격 위치 수정

기준일: 2026-11-05. 기존 출입 연결 뒤 실제 플레이에서 확인한 세 가지 차이를 수정한 기록입니다.

## 현재 동작

| 구간 | 화면 전환 | Player/Match |
| --- | --- | --- |
| Startup → 시작 우주선 | 기존 검은 임시 로딩 안내 + 하단 진행 바 | 허브 이동용 Player/Camera 생성 |
| 시작 우주선 → Initial_Stage | 0.3초 암전 → 최소 0.3초 이동 → 0.3초 복귀 | 정식 Match·Player 생성. 필드 문 밖 PlayerStart에서 시작 |
| Initial_Stage → 우주선 → Initial_Stage | 같은 페이드, 문구·진행 바 없음 | 기존 Match와 동일 actor 유지. 위치 보고 후 필드 정지/재개 |

기본 목표는 약 0.9초입니다. 실제 Scene 로드·Match 준비가 0.3초보다 길면 **가운데 완전 암전 구간만 연장**합니다. 준비되지 않은 화면이나 카메라 이동을 노출하면서 시간만 맞추지는 않습니다. 페이드는 실시간을 사용하므로 게임 정지/배속에 영향을 받지 않습니다.

## 수정한 부분

- 시작 우주선의 orthographic size를 **12 → 18**로 변경했습니다. culling mask **4190711**, SolidColor/투명 검정, depth **-1**, near/far **0.3/1000**, Camera Z **-10**도 Initial_Stage 기준으로 맞췄습니다. 배율·표시 레이어는 기존 `SpaceshipHubSettings`에서 조절합니다.
- `SceneFadePresenter`를 추가했습니다. 검은 Image 하나와 CanvasGroup만 사용하며, 이동 함수는 alpha=1에 도달한 뒤 실행합니다. Camera 정렬과 실패 시 원위치 복구도 암전 안에서 끝냅니다.
- `AppScreenPresenter`는 우주선 페이드 중 일반 Loading/진행 바와 Escape 메뉴 처리를 보류합니다. 입력 잠금은 `SpaceshipTravelService`의 전환 수명으로 잡고 끝날 때 자기 잠금만 해제합니다.
- 문/입구는 공용 이동 API의 `IsTransitioning`을 보고 이동 중 발생하는 다른 접촉을 중복 요청하지 않습니다.
- `Initial_Stage`의 `PlayerStart_Local`은 **(-18.97, -83.36, 0)**, `PlayerStart_Peer`는 **(-15.97, -83.36, 0)**으로 옮겼습니다. 실제 필드 출입구 Collider 범위와 기존 Player 몸체를 기준으로 정한 문 밖 위치입니다. Peer는 3만큼 옆으로 띄워 서로 겹치지 않습니다.

## 첫 진입에서 Scene 두 개를 상시 로드하는가?

이번에는 사용자가 제안한 **PlayerStart 이동안**을 적용했습니다. Startup에서 우주선에 들어올 때 필드를 미리 열어 Match까지 시작하는 구조로 바꾸지 않았습니다. 첫 출격은 기존 GameFlow로 필드를 열지만, 도착 위치는 이후 우주선 방문에서 필드로 복귀하는 지점과 일치합니다. 진행 중 방문은 기존대로 필드 + 우주선 Additive 구성을 사용합니다.

## 팀원 전달

두 by-owner 폴더의 진태님 문서를 갱신했습니다. 이번 추가 변경은 Core 페이드 UI/이동 서비스, 허브 Camera 설정, 문 이동 중복 방지, Scene의 PlayerStart 두 개입니다. Weather 추첨·큐·Weather 저장 writer는 수정하지 않았습니다. 원격 Client 출입 권위 RPC의 기존 경계도 그대로입니다.

[현재 이동 API와 Scene 설정](../../../../02_architecture/03_scene-flow/spaceship-travel-api.md)을 참고합니다. Play를 종료한 뒤 Startup부터 다시 시작하면 됩니다. 이미 로드한 Scene을 그대로 둔 채 스크립트만 재컴파일하면 옮긴 PlayerStart의 제작 위치가 반영되지 않을 수 있으므로 새 진입으로 확인합니다.

## 검증 결과

실행별 결과는 공유 제외 `Docs/Local/Work/DayNightSteam-20261105`에 보관합니다. 아래 최종 실행 결과를 기준으로 판정합니다.


- 초기 `fade-final.xml` 4/4는 1.5초였던 시점의 기록입니다. **0.3초씩 총 0.9초로 줄인 뒤 `weather-hud-final.xml`의 11/11에서 다시 통과**했습니다. [함께 검사한 HUD와 결과](../../../../02_architecture/09_world/weather-debug-hud.md)를 참고합니다.
- 첫 출격/왕복 2회/도착 표식 실패 복구: 동일 Match·actor 수명, Camera 배율·레이어·배경·depth·clip 값 일치, 첫 출격 위치와 문 복귀 위치 일치를 확인했습니다.
- 실제 자식 Collider와 기존 문의 물리 접촉: 출격·방문·복귀, 필드 시간 정지/재개, 자동 재입장 방지까지 확인했습니다.
- 페이드 중 일반 Loading 객체/문구/진행 바가 없고, 암전/완전 검정/복귀 단계가 실제 프레임에서 진행되는 것을 확인했습니다. 완전 검정 유지 시간은 최소 0.3초입니다.
- Time.timeScale=0에서도 실시간 전환이 끝나고 이동 콜백은 alpha=1에서만 실행됩니다. 실패한 이동 뒤에도 화면이 복구됩니다.
- Startup 버튼·허브 이동·메뉴 및 독립 사망 잠금·일반 임시 로딩의 기존 GPU 검사도 통과했습니다.
- 첫 검사에서 물리 fixture가 페이드 종료 전 강제로 다음 위치로 옮겨 중복 이동 예외가 난 기록은 `fade-first.xml`에 보존했습니다. 실제 전환 종료를 기다리도록 fixture를 수정하고, 문에서도 전환 중 다른 접촉을 무시하도록 보강한 뒤 위 최종 검사를 통과했습니다.

- `native-build-fade.log` / `.exit`: **Windows Standalone Build Success / 종료 0**.
