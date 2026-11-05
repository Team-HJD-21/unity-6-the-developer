# Core 분리 PR 검토 순서와 PL 인수

기준일: 2026-11-05. PL이 Startup/Initial_Stage 수동 확인7단계를 모두 확인했다고 승인했습니다. 사용자 영상은 게임 진입/우주선 왕복과 타이머/날씨 변경의 수동 근거이며 자동 검사 결과와 구분합니다.

## 검토 순서

1. 공용 runtime/체크포인트 계약·보조 객체
2. FieldTurret/CU 실제 시간·상태 연결
3. Player 시간·피해/감속·실제 writer
4. Enemy/Encounter 요청·환경·실제 상태
5. Core 저장/복원 조립·네트워크 Scene/Match 수명
6. 낮밤/Weather 주기·디버그 타이머/Reset
7. Steam SDK·사용자·2인 로비
8. 시작 우주선/왕복·카메라·0.9초 전환과 전체 사용 문서

각 PR의 base는 바로 앞 PR의 브랜치입니다. diff는 자기 묶음만 표시하며, main 반영은 선행 순서와 재대상 지정 후 검증/승인을 따릅니다. 모든 PR을 main 대상으로 한꺼번에 겹쳐 올리지 않습니다. PR을 자동 병합하지 않습니다.

## 후속 Issue

- #619: Steam 로비→실제 NGO 접속 및 참가자 신원 매핑
- #620: Client 우주선 출입 요청의 Host 검증·개인 Scene 전환
- #621: Client 낮밤 남은 시간/재시작 기준 동기화
- #622: Steam ticket 기반 Backend 인증/프로필·온라인 진입

네 항목은 PL 담당/P2/착수 시점 미정으로 별도 보관합니다. 지금 인수한 Core 범위를 소급 미완료로 바꾸는 조건이 아닙니다. 실제 Weather writer/원격 Player 본체/미션 콘솔/팀 Gate는 각 기존 담당/Issue에 남습니다.

## 사용자 영상

[전체 게임 Flow·타이머·날씨 전환의 PL 수동 인수 영상](../videos/core/2026-11-05-game-flow-day-night.mp4)

원본: 사용자가 제공한 2026-11-05_12-44-52-010.mp4, 약14.1MB. 브라우저 자동 첨부는 로컬 파일 접근 제한 뒤 URL 확인 불가로 중단했습니다. 확장 권한/보안 설정을 변경하지 않고 이 문서 PR의 검증 영상 파일로 첨부합니다. 관련 PR 본문에서도 같은 파일로 연결합니다.

## 검증 구분

- 목적별 누적 브랜치마다 별도 격리 Unity에서 컴파일·실제 Stage 초기화·단일 App/Match·참가자 기반 시계 정지/재개를 확인합니다.
- 최종 수동 인수 소스의 HUD/왕복/페이드 집중 검사는11/11, Windows Standalone Build Success입니다. 기존 Core/2프로세스/Steam 결과는 각 실행의 범위를 함께 명시합니다.
- 실제 운영 Weather writer 부재로 전체 Save는 차단합니다. 대역으로 검사한 저장 성공을 제품 전체 저장 인수로 쓰지 않습니다.
- CI는 PR 생성 뒤의 상태입니다. 현재 workflow는 main 대상 PR만 자동 실행하므로 선행 브랜치를 base로 한 PR은 main 재대상 뒤 필수 검사 실행을 확인해야 합니다.
- Runtime 원본과 최종 누적 branch의 코드/Scene/Package 내용은 일치합니다. 원본 작업 브랜치와 개인 미커밋/stash는 보존합니다.

[수동 확인과 전체 경로](2026-11-05-core-local-review.md) · [날씨 HUD 사용법](../architecture/weather-debug-hud.md) · [낮밤 API](../architecture/day-night-api.md) · [Steam API](../architecture/steam-api.md)
