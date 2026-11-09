# Player 레벨·경험치 API

갱신: 2026-11-10. Player 담당: 이영빈.

구현 상태: 코드와 프리팹 연결 추가, C# 컴파일 확인. Unity 실제 처치·HUD·Host/Client 실행 확인은 남아 있다.

## 현재 규칙

- 레벨 1, 경험치 0에서 시작한다. 실제 적 사망당 **10 XP**를 같은 전장의 활성 Player 객체 모두에게 지급한다. 공용 포탑·플레이어 무기·설치물의 처치를 구분하지 않는다.
- 기본 요구량은 매 레벨 **100 XP**, 임시 최대 레벨은 100이다. 큰 보상은 여러 레벨 상승과 남은 XP를 함께 계산한다. 최대 레벨에서는 바를 채우고 `XP MAX`를 표시한다.
- `Player.prefab`의 `PlayerExperience`에서 첫 요구량·레벨당 증가량·최대 레벨을 조절한다. 현재 증가량은 0이다. 레벨업 자동 스탯 지급은 하지 않는다. `PlayerBaseStats`가 달성 레벨을 읽고 **보상받기 클릭 시** 2레벨부터 1회씩 스탯 포인트를 지급한다. 체력 증가·스킬 토큰 지급은 아직 없다. [기본 스탯·레벨 보상 API](player-base-stats-api.md).
- 지급 대상은 적과 같은 Unity Scene, 같은 Match ID, 필드에 있는 Player다. 우주선 내부·종료된 Match·비활성 Player에는 지급하지 않는다. 생존 여부로 제외하지 않는다. 아직 Match에 연결하지 않은 독립 Feature 시험은 빈 Match ID끼리 동작한다.
- 거리 분배나 마지막 타격자 판정은 없다. 일반 제거·Despawn만으로 보상하지 않으며 한 활성 수명에서 실제 사망 한 번만 지급한다.

## 호출 흐름과 소유 범위

`EnemyHealth.Died` → `PlayerEnemyExperienceReward` → `PlayerExperience.ShareEnemyExperience` → 각 Player의 `PlayerExperienceState` → `Changed` / `LevelChanged` → `PlayerExperienceView`.

Enemy와 Core의 C#은 변경하지 않았다. Player 영역의 보상 어댑터를 `EnemyRoot.prefab`에 하나 배치했으며 Moving/Melee/Ranged 프리팹은 이를 상속한다. 현재 보상 원본은 어댑터의 `_experienceReward = 10`이다. 몬스터 종류별 EXP 데이터가 준비되면 이 어댑터에서 그 값을 읽도록 연결하면 된다. Player 계산·HUD는 그대로 쓸 수 있다. 별도 적 프리팹을 만들면 어댑터 상속/배치도 확인한다.

네트워크에서는 서버의 실제 사망만 보상한다. 어댑터는 기존 사망 후 Despawn 구독보다 앞선 `OnEnable`에서 연결한다. 서버가 보상값과 Match ID를 `SendTo.NotServer` RPC로 보내며 서버 자신의 Player 원본도 직접 갱신한다. Host는 RPC를 중복 수신하지 않는다. Client가 XP 지급을 요청하는 RPC는 없다.

이 경로는 현재 활성 actor에 처치 보상을 전달하는 기능이다. **늦은 참가·재접속·actor 재생성 시 누적 XP를 서버에서 다시 받는 스냅샷과 계정/프로필 저장은 아직 없다.** 완전한 네트워크 성장 상태 동기화로 취급하지 않는다. 서버·Client가 같은 네트워크 적 프리팹과 컴포넌트 구성을 사용해야 한다.

## 공개 값과 이벤트

| API | 의미 |
| --- | --- |
| `Level`, `TotalExperience` | 현재 레벨, 누적 XP |
| `ExperienceInLevel`, `RequiredExperience`, `Progress` | 현재 레벨 내 XP, 다음 레벨 요구량, 0~1 진행도 |
| `IsMaximumLevel` | 최대 레벨 여부 |
| `MaximumLevel`, `RemainingToNextLevel` | 설정된 최대 레벨, 다음 레벨까지 남은 XP |
| `CanGrantExperience` | 현재 로컬/Host·필드 시간 조건에서 XP 지급 가능한지. 디버그 버튼도 같은 조건을 사용한다. |
| `AddExperience(int)` | 독립 로컬 또는 현재 권위 Player에 보상 적용. 음수·0·최대 레벨은 거부하며 Client 임의 호출은 거부한다. |
| `Changed` | XP 적용 또는 저장 복원 후 표시 갱신 |
| `LevelChanged(int previous, int current)` | 보상으로 레벨이 실제 바뀌었을 때 한 번 통지. 복원 시 재발행하지 않는다. |

## HUD와 체크포인트

HP 아래 사다리꼴은 기존 HP 프레임 스프라이트의 빈 장식 영역이었다. `InitialStagePlayerHUD.prefab` 안에 XP 채움 Image·RectMask2D·텍스트를 미리 배치했다. `PlayerExperienceView`는 기존 `StagePlayerVitalsView`에 연결된 로컬 actor를 그대로 따라간다. 임의 Player 검색이나 런타임 UI 오브젝트 생성은 하지 않는다. XP 숫자는 하단 바 오른쪽에 표시한다.

레벨은 초상화 바로 아래 `PImg/Player Level Badge`의 76×28 배지에 표시한다. 청록색 얇은 테두리·어두운 배경·`LV` 표제·흰색 숫자를 Image/TMP 오브젝트로 미리 배치했다. 기존 `_levelText` 참조는 유지하고 숫자만 `01`, `02`, … 형식으로 갱신한다. 세 자리 이상은 그대로 표시하며 작은 배지 안에 맞도록 숫자 Text의 자동 크기 조절을 사용한다. HP·XP 바와 캐릭터, XP 숫자의 위치는 바꾸지 않는다. 추가 Graphic은 모두 Raycast Target을 꺼 입력을 가로채지 않는다.

`PlayerExperience.Checkpoint.cs`는 기존 actor 체크포인트 참여자 수집 경로에 연결된다. 저장 내용은 Player ID, 성장 곡선 설정, 누적 XP다. 섹션은 **Progression**이며 Core 기본 복원 정책은 이 범주를 되감지 않아 현재 성장을 유지한다. Progression 복원을 명시적으로 허용하면 저장값을 복원하고 화면만 갱신한다. 파일 IO·저장 순서는 Core가 소유한다. 새로운 성장 곡선과 기존 저장 설정이 다르면 복원을 거부한다.

## 확인 범위

C# 런타임·Editor 컴파일 오류 0개, 프리팹 fileID/GUID·부모/자식·참조 연결 정적 검사를 확인했다. XP 계산 경계값과 실제 사망/제거/공유 지급을 확인할 Editor 테스트도 추가했다. 별도 .NET 테스트 실행기는 Windows 애플리케이션 제어 정책에 차단되어 실행 결과를 얻지 못했으며 재실행하지 않는다. Unity 테스트 및 NGO 후처리·실제 네트워크 실행 통과를 의미하지 않는다.

Unity에서 확인할 항목:

1. 처치마다 XP가 10씩 오르고 10마리 처치 후 배지에 `LV 02`, 경험치에 `XP 0/100`이 표시되는지.
2. 공용 포탑 처치도 지급되고, 살아 있는 적 제거에는 지급되지 않는지.
3. Host/Client 각각 같은 전장 Player에 한 번씩 지급되며 다른 Match·우주선 Player는 제외되는지.
4. 로컬 actor 교체·HUD 재활성화 시 이전 구독이 해제되고 현재 actor의 값이 보이는지.
5. 체크포인트 기본 되감기에서는 현재 XP를 유지하고, 명시적인 Progression 복원에서는 저장값을 읽는지.
