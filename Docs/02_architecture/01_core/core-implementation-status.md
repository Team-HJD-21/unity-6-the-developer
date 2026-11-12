# Core API 구현 현황

**중요 · Feature를 Core에 연결하기 전에 읽습니다.** 기준은 2026-11-12 게임 main `2cbc4542b`입니다. 이 페이지는 호출 가능한 기술 계약과 아직 연결되지 않은 범위를 정리합니다. 개별 맵의 미술 인수나 전체 게임 완료표는 아닙니다.

## Camera와 Audio부터 구분하기

**CameraManager는 구현돼 있습니다.** 기존 Player 추적 카메라에 자동 연결되며 임시 시점·블렌드·복귀를 요청할 수 있습니다. **새 AudioManager는 아직 없습니다.** CameraManager가 AudioListener 하나를 선택하는 기능은 BGM/SFX를 재생하는 관리 기능과 다릅니다.

| 기능 | 지금 사용할 수 있는 것 | 아직 별도로 필요한 것 |
| --- | --- | --- |
| 로컬 카메라 | `CameraManager.Current`, `PushView`, 반환 lease의 `Dispose`, 출력/시점 알림 | 제품 컷씬·자막 콘텐츠, 실제 조준/Weather 표현 인수 |
| 오디오 | 출력 Camera의 활성 AudioListener 선택 | 공용 BGM/SFX 서비스·볼륨/믹서·Scene 음악 연결. 기존 AudioManager 호출 재활성화 금지 |
| App/Scene 흐름 | Startup·우주선 허브·Mission 출격/귀환, 소유 Scene 정리와 전환 실패 전달 | 추가 방/전장·제품별 도착점과 전체 Scene 인수 |
| Match 관리 | Scene 조립·Player/터렛·CU 바인딩, 완료/해제 수명과 모드 규칙 경계 | 최종 전투 진행·컷씬·결과 UI 등 실제 콘텐츠 연결 |
| 공용 시간·낮밤 | Host 게임 시간/정지/배속, 낮밤 기준과 Client 잔여시간 읽기 | 실제 Weather writer·표현 소유자와의 최종 인수 |
| Random | Host Match 난수, 범용/가중 선택과 소유권 헬퍼 | 각 Feature의 기존 난수 호출을 사용할 수명에 맞게 연결 |
| Battlefield | 터렛 topology·전선·Player/Enemy 밀도 Grid snapshot | 밀도·법선을 자동으로 실제 전투 우세 판정으로 쓰는 기능 |
| 지역 Grid | 모드 Reset·확정 셀 읽기·Client 복제, CU 시작 셀·4방향 동적 점유 | 해금과 실제 확보의 추가 정책·지형 제작 마스크·Enemy 제한 소비 |
| Encounter 요청 | 전용 요청 서비스, 실제 기존 계획/생성 경계·결과 관측 | 모든 특수 생성의 공통 수명·저장, 별도 Enemy Pool |
| HUD/입력 | 기존 화면/입력 잠금·상태 Port와 일부 표시 | 제품의 모든 HUD·컷씬/메뉴/허브 예외까지 최종 통합 |
| Checkpoint | 명시 Save/Restore 실행, 참여자 등록, 검증·실패 rollback | 모든 실제 Feature writer. 누락된 필수 원본이 있으면 전체 저장 차단 |
| Network/Steam | 연결·Match 준비/시작 계약, 기존 지역/터렛/환경 전달, Steam 어댑터 | 원격 Player 본체·전체 협동 플레이·실게임 transport의 최종 인수 |
| 대사/컷씬 | CameraManager·InputLock·SceneFlow 기반을 조합할 수 있음 | 실제 대사/자막·Letterbox·연출 재생/종료·다음 전장 자동 연결 |

## 구현됐다는 말의 범위

Camera 기반의 원본 검증 기록은 Editor 11개와 실제 Scene Play 2개, 합계 13/13입니다. 별도 Windows Host/Client의 자기 Player/출력 확인 기록도 있습니다. 마우스 조준 감각과 실제 컷씬 미술·날씨 효과를 모두 인수했다는 뜻은 아닙니다.

CU 시작 영역은 도메인 14/14, 바인딩/설정 저장 6/6, 실제 Stage·Editor UI 1/1의 기록이 있습니다. 기존 통합 검사 한 개는 CU가 없다는 이전 가정을 수정한 뒤 1/1로 재통과했습니다. 새 정책의 실제 두 Client 인수와 전체 전장 Save는 별도입니다. 이 Docs 동기화에서는 게임 실행 검사를 다시 수행하지 않았습니다.

“클래스가 있음”, “실제 Scene에서 호출됨”, “테스트한 조건”, “제품 전체 연결 완료”를 구분해 사용하세요. 전체 게임의 완료율을 API 개수나 병합 수로 계산하지 않습니다.

## Audio 구현 상태

공용 AudioManager는 Legacy 제거 후 새로 구현되지 않았다. FieldTurret의 옛 PlaySfx·StopSfx 호출은 주석이다. CameraManager의 AudioListener 선택은 재생 관리가 아니므로, 이 주석을 풀거나 가상의 AppServices.Audio를 호출하면 안 된다. 다음 구현에서는 실제 클립/설정·재생/정지 수명·Scene 음악을 공용 계약으로 정한 뒤 각 Feature 호출을 연결한다. 이 단락은 현재 사용 가능한 새 재생 API를 정의하지 않는다.

## 다음에 읽기

- [CameraManager 사용법](../03_scene-flow/camera-manager-api.md)
- [Audio 현재 상태와 연결 주의](#audio-구현-상태)
- [지역·Encounter API](core-sprint-region-and-request-api.md)
- [공용 API](core-api-usage.md) · [모듈 통합 체크리스트](core-feature-integration-guide.md)
