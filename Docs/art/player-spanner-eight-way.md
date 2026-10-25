# 스패너 8방향 자세 — 2026-10-23

기존 캐릭터의 흰색·회색 복장, 둥근 헬멧, 검은 바이저와 무채색 스패너를 기준으로 대각선 네 방향을 추가했다. 그림은 내장 `image_gen` 편집으로 제작했고, 코드는 프레임 분리·균일 배율·위치 정렬·공통 팔레트와 알파 정리에 사용했다. 오른쪽 위 공격 준비 자세의 팔과 이동 자세의 부츠가 불꽃처럼 보이던 부분은 별도로 수정했다.

- 새 프레임 56장: `Assets/Art/Sprites/Player/Motions/V2/SpannerEightWay/`
- 생성 원본: `V2/Source/SpannerEightWay_*_Source.png`
- 새 클립 24개: `Assets/Art/Animations/Player/WeaponPosesV2/`의 대각선 ToolIdle, ToolMove, Attack, Repair, HoverAttack, HoverRepair
- 연결 데이터: `Assets/Data/Player/PlayerMotions.asset`
- 기존 Controller에 새 상태를 추가해 몸체 상태가 80개에서 104개가 됐다. 기존 80개 상태와 기존 그림·클립 파일은 보존했다.

| 동작 | 재생 | 그림 순서 | 판정 시점 |
| --- | --- | --- | --- |
| ToolIdle / ToolMove | 0.8초 반복 | 고정 대기 / 부유 자세 | 없음 |
| Attack / HoverAttack | 0.4초 | 대기 → 준비 → 타격 → 복귀 → 대기 | 0.2초 |
| Repair / HoverRepair | 0.6초 | 대기 → 준비 → 작업 → 복귀 → 대기 | 0.3초 |

이동 중 공격·수리는 ToolMove 자세에서 시작하고 돌아온다. 부유 중 다리는 지면에 붙지 않으며 흰색 추진 표현은 제트팩 아래에 있다. 각 프레임은 160×160, 32 PPU, 중앙 피벗, Point 필터를 사용한다. 기존 대기·이동 그림의 발 기준점에 등록했고, 측정한 세로 차이는 최대 1픽셀이다. 발 아래 그림자는 새 클립에 제작한 키로 연결했다.

`PlayerMotionSet.HasEightWayTools`는 대기·이동·공격·수리·이동 공격·이동 수리의 여섯 모션이 모두 8방향으로 연결됐는지 검사한다. 구성 하나라도 빠지면 기존 4방향을 사용한다. 총기의 8방향 지원 여부와는 독립적이다. `PlayerWeaponRig`와 `PlayerSpriteMotion`이 무기에 맞춰 방향을 선택하며, 행동 중 방향이나 이동 상태가 변해도 진행률을 유지한다. 몸체 그림에 스패너가 포함돼 있으므로 별도 스패너 그림은 기존처럼 숨긴다. 구형 별도 무기 표시 경로의 손 앵커는 기존 4방향을 유지한다.

피해·수리량·탐색 범위·공격 간격·수리 API·멀티플레이 권한은 변경하지 않았다. Player 프리팹과 콜라이더도 이 작업 직전과 파일 해시가 동일하다. 몬스터 변경 후 공격·수리의 실제 동작 검증은 별도다.

[8방향 대기 비교](player-spanner-eight-way-preview.png) · [동작 미리보기](player-spanner-eight-way-preview.html) · [프롬프트·원본·프레임·검사 기록](player-spanner-eight-way.json)

56장 크기·알파·팔레트, 24개 클립의 키·타이밍·GUID 연결, 기존 상태와 에셋 보존을 정적으로 검사했다. 편집기 C# 빌드는 오류 0개, 기존 경고 4개로 통과했다. Unity Test Runner와 실제 플레이는 실행하지 않았다.
