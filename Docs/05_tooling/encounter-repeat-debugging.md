# Encounter 연속 요청

Game Debugger의 Encounter 화면에서 `연속 Encounter 요청 / 시뮬레이션`을 펼칩니다. 일회 요청과 같은 Encounter 계약을 사용하며 별도로 Monster를 Instantiate하지 않습니다.

## 사용

1. Initial_Stage를 Play Mode로 시작하고 활성 Match와 Encounter 대상이 준비되었는지 확인합니다.
2. 기존 Squad Preset Order와 Max Enemy Count를 선택합니다.
3. 간격(기본 2초)과 최대 요청 횟수(기본 10회)를 정합니다. 최소 간격은 0.1초, 유한 횟수 상한은 10,000회입니다. `0`은 명시적인 무제한입니다.
4. Start repeat를 누릅니다. 첫 요청은 한 간격 이후 발생하며 성공/시도 횟수, 다음 요청까지의 시간, 마지막 결과를 표시합니다.
5. Stop repeat로 종료합니다. 다른 pane에서도 같은 실행 상태를 조회하고 중지할 수 있습니다.

`Max Enemy Count`는 요청당 생성 마릿수 상한입니다. 비용 예산이나 현재 살아 있는 적 전체의 상한과 다릅니다. 무제한 요청은 생존 적을 계속 누적할 수 있습니다. #540의 현재 구현에는 전체 생존 적 상한이 없으며, 요청별 비용 예산은 지역 점유율로 산출합니다. [Encounter 편성·예산](../02_architecture/08_encounter/encounter-planning-guide.md)에서 요청 상한·출현 셀 수용량·공격인원 포화도를 구분합니다. 이 반복 버튼은 지정한 Order를 요청하며 상황별 자동 편성 선택 버튼이 아닙니다.

## 시간과 중지 정책

- 기본 clock은 Unity 게임시간입니다. Game Debugger의 timeScale 배속과 같은 속도로 흐릅니다.
- 실시간 clock을 선택하면 배속과 관계없는 초 단위를 사용합니다. 두 clock 모두 Editor Pause 및 timeScale 0에서는 대기합니다.
- Editor frame이 지연되어 여러 간격이 지나도 한 번만 실행합니다. 놓친 요청을 몰아서 생성하지 않고 다음 간격부터 다시 기다립니다.
- 시작 시 Squad와 요청당 예산을 고정합니다. 실행 중 설정 변경은 Stop 후 다시 Start해야 반영됩니다.
- Match 변경/종료, Play Mode 전환, 대상 제거/비활성화, 권한·준비 상태 상실, 요청 실패·예외, script reload, Editor 종료 시 자동 중지합니다. 자동 재시작하지 않습니다.
- 실행을 시작한 pane이 dispose되는 창 닫기/분할 재구성 시 중지합니다. 다른 pane을 닫는 것은 실행을 중지하지 않습니다. 기능 선택만 바꾸어 소유 pane이 유지되는 경우에는 실행이 계속됩니다.
- Capture mode는 진단 표시만 바꿉니다. 이미 시작한 반복 실행은 계속되며 Capture mode에서 새로운 Start UI는 표시하지 않습니다. 중지하려면 Live 모드의 Encounter 화면으로 돌아가 Stop repeat를 누르거나 소유 창을 닫습니다.
- 마지막 실행 결과와 시간·중지 정책의 긴 설명은 접힌 상세 영역에서 확인합니다. 상태·횟수·남은 시간은 상단에 표시하며 실패 중지 이유는 즉시 Warning으로 보여 줍니다.
- 전체 Editor에서 공유 실행기는 하나뿐입니다. 여러 pane에서 동시에 Start할 수 없으며 중첩 요청을 막습니다.

## 구조 / 통합 API

`EncounterRepeatScheduler`는 시간 delta와 Match/준비 상태를 받아 실행 횟수·다음 요청 시점을 관리하는 순수 상태 머신입니다. `EncounterRepeatDebugControls`는 Editor update, lifecycle, GUI와 연결합니다.

`BattlefieldDebugPanel.DrawEncounterControls()`는 아래 API를 호출합니다.

```csharp
EncounterRepeatDebugControls.Draw(this, squadOrder, maxEnemyCount, target);
```

같은 panel의 Dispose는 `EncounterRepeatDebugControls.Release(this)`를 호출합니다. 반복 요청은 `IEncounterDebugCommandTarget.TryRequestCurrentEncounter(..., EncounterRequestSource.DebugWindow, ...)`로 전달합니다. 매번 Encounter 대상이 바인딩한 최신 snapshot을 조회하며, 현재 구현의 `IGameDiagnosticsSource.BoundMatchId`도 비교해 다른 Match로의 요청을 차단합니다.

반복 경로는 기존 진단 이벤트를 그대로 발생시키므로 Statistics의 요청·생성·제거 그래프와 Spawn decisions 목록에 함께 나타납니다. 반복 요청 자체는 실제 게임 Wave 진행/난이도 조절/승패 조건을 추가하지 않습니다.

## 확인 범위

`EncounterRepeatSchedulerTests`는 기본 비활성화, 정확한 간격, 유한/무제한 횟수, pause, 지연 burst 방지, Match/Play 변경, 준비 실패, 실행 실패/예외, 재진입/중복 Start, Stop/재시작, 잘못된 입력을 검증합니다. 실제 Scene에서 연속 생성과 그래프 증가, pause/배속, 창 닫기 중지는 Play Mode에서 별도로 확인합니다.
