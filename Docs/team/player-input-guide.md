# Player V2 조작 및 연결 안내

수정일: 2026-10-09

| 입력 | 동작 |
| --- | --- |
| WASD / 방향키 / 게임패드 왼쪽 스틱 | 이동 |
| 마우스 | 조준 |
| Space 유지 | 기본 사격 |
| Tab | 무기 교체 |
| Q / E | 슬롯에 선택한 스킬 사용 |
| F | 가까운 본인 소유 임시 포탑 회수 |
| 우클릭 | 방벽의 미완성 첫 펄스 취소 |
| Escape | 열린 스킬 선택창 닫기 |

`GameInput`은 장치 입력을 읽고, `PlayerInputHandler`는 플레이어 조준 방향과 프레임별 입력을 수집한다. 실제 스킬 실행과 선택은 `PlayerSkillController`, 공통 사용 조건은 `PlayerSkillBase`가 담당한다. UI는 개별 View 클래스로 나뉜다.

조작 잠금은 `PlayerControlState.SetInputBlocked`로 전달한다. Match를 소유한 Scene은 `PlayerSceneAdapter.Bind`를 통해 `IPlayerMatchBinding` 구현체에 현재 Match를 연결하고, 종료할 때 `Unbind`한다. 연결 해제된 플레이어는 조작할 수 없다. 플레이어 사망은 `PlayerInfo.onDeath` 이벤트로 전달하며 기존 씬에서는 `InGameManager`가 구독한다. Player V2 코드에서 해당 매니저를 검색하지 않는다.

현재 PoC 설정은 준비 중 행동과 전투 중 스킬 선택을 허용한다. 정식 단계별 제한을 적용할 때 `PlayerControlState`의 `_allowActionsDuringPreparation`, `_allowSelectionDuringCombat` 설정을 끈다. 기존 Scene의 상태 공급 함수는 Match가 연결되지 않은 경우에만 사용한다.

임시 포탑은 처음 3개를 보유하고 설치 간격은 3초다. `Assets/Prefabs/Player/TemporaryTurret.prefab`의 `TemporaryTurretHealth`에서 체력 100과 충돌 반경을 조절한다. 회수 시 충전 시간은 `(1 - 현재 체력 / 최대 체력) × 최대 충전 시간`이다. 손상 없는 포탑은 즉시 반환되고, 절반 손상은 15초, 파괴는 30초 후 반환된다. 각 포탑은 독립적으로 충전된다. 최대 충전 시간은 Player 프리팹의 `PlayerTurretSkill._maximumRepairSeconds`에서 변경한다. 설치 쿨타임과 충전은 별도로 진행된다.

피격 표시는 색상 틴트만 사용한다. 추가 파편, 섬광, 흔들림과 체력바는 추가하지 않는다. 신규 네트워크 적은 스킬 측 중재 컴포넌트로 연결하며 몬스터 원본은 변경하지 않는다. 장비 자체의 네트워크 Spawn과 클라이언트 설치 요청은 후속 작업이다.
