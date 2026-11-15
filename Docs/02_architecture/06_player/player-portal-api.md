# 플레이어 포털 스킬

2026-11-15 현재 `Player.prefab`에 본체·프리뷰와 함께 연결했다. 스킬 트리에서 해금하고 후보에 배치한 뒤 Q/E로 설치한다. 근처에서 **R 이동, F 회수**를 사용한다. 같은 R 누름은 놓을 때까지 기존 수리·재장전에 재사용하지 않는다. F는 자기 포털·터렛 중 가까운 한 개를 회수한다. [해금·강화·후보 UI](player-skill-tree-api.md)를 따른다.

`PlayerPortalSkill`은 `PlayerPlacementSkillBase`의 원형 설치 프리뷰·클릭 확정·취소를 재사용한다. `PlayerSkillId.Portal`(5)을 해금·후보·Q/E 선택 목록에 포함한다.

- 소유자마다 A/B 두 개만 유지한다. 한 번 확정할 때 한 개를 설치하고, 다음 설치 요청은 빈 슬롯을 사용한다. 한 개만 설치되어 있으면 이동할 수 없다.
- `NearestInteractable`은 같은 Scene의 소유 포털만 조회하며 거리와 사이의 장애물을 검사한다.
- `TryTravel()`은 반대쪽 포털 위치에 **현재 발밑 콜라이더 중심**을 맞춘다. 이동/대시를 정지하고 Z는 유지한다. 이동 직전에 육지 전체 포함과 캐릭터·포탑·장애물 점유를 다시 검사하며 실패하면 현재 위치를 유지한다.
- `TryRecoverNearest()`는 가까운 자기 포털 하나만 회수한다. 다른 포털은 유지하고 빈 슬롯에 재설치할 수 있다.
- 이동 요청을 명시적으로 받은 경우만 이동한다. Trigger 자동 왕복은 없으며 이동 후 공용 Match 절대 시각 기준 0.75초 대기를 적용한다. 설치 간격/재화 소비/적 피해/사운드는 추가하지 않았다.
- 사망, 메뉴 잠금, 필드 정지, Match 종료, 읽기 전용 Client는 설치·이동·회수를 실행하지 않는다. 원격 Client 요청/복제는 별도다.

초기 임시 설정은 상호작용 거리 2.5, 두 포털 사이 최소 거리 3, 설치 반경은 공통 기본값 12다. 수치는 Inspector에서 조정할 수 있다.

`PlayerPortalEndpoint`에는 제작된 SpriteRenderer와 발밑 설치 영역을 지정한다. 좌우 받침은 이동을 막으며 중앙 문 공간은 열어 둔다. A는 청색, B는 주황색으로 구분한다. `PlayerPortal.prefab`은 전용 세로형 기계식 문 `PortalGateA.png`/`PortalGateB.png`를 사용하며 `PlayerPortalPreview.prefab`과 트리·HUD 아이콘도 같은 그림을 참조한다. A/B 외장색은 동일하고 내부 에너지만 청록/주황으로 구분한다. 이미지 피벗과 배치 기준은 발밑이며 높이 3.5, 발밑 설치 깊이 0.65다. `PlayerDeviceSortingOrder`가 소유자의 발밑 기준 앞뒤 표시를 정렬한다. 좌우 발판에 0.34×0.5 비 Trigger `BoxCollider2D`를 두고 중앙 1.5폭은 비워 둔다. 그림 전체 높이에 물리 콜라이더를 씌우지 않는다. 받침은 기존 설치물과 같은 `PlayerFieldDevice` 레이어·우선순위 10을 사용하고 `PlayerFeetBox`/`EnemyFeetBox`/`EnemyProjectile`과 접촉한다. R/F 조회에서는 선택 대상 포털 자체의 받침만 시야 검사에서 제외하고, 다른 벽과 포털·도착 공간의 받침 충돌은 계속 검사한다. 설치 범위는 스킬 강화 1/2/3레벨에서 12/13.8/15.6이다.

`PlayerSkillController.Update`가 포털 근처 R 눌림을 처리하고 `PortalConsumesR`을 노출한다. `PlayerTurretRepair`와 `PlayerWeaponAmmo`는 같은 누름을 건너뛴다. HUD는 R 이동/F 회수와 반대편 포털 미설치 상태를 안내한다. F 안내와 실행은 동일한 후보를 사용하며, R/F 동시 입력은 이동만 처리한다. Shift+R도 이동으로만 처리한다.

체크포인트는 공통 스킬 writer의 `/skill/5`, v2를 사용한다. A/B 슬롯별 존재·위치, 설치 프리뷰 여부, 남은 이동 대기 시간과 도착 보호막의 현재/초기 보호량·남은 시간을 저장한다. v1은 보호막 없이 읽는다. 복원은 플레이어 이동이나 입력을 재실행하지 않으며 rollback은 이전 포털 상태와 보호막을 복원한다. 현재 Player의 실제 section 집합에 포함되며 해금·강화는 `/loadout` v3에 저장한다.

이동에 성공하면 최대 체력의 10%를 2초간 보호한다. 스킬 레벨 2/3에서는 11.5%·2.3초 / 13%·2.6초이며 보호량은 정수 올림이다. 재이동은 현재 강화 수치로 갱신하고 중첩하지 않는다. `PlayerInfo.ApplyDamage`의 권한·생존·필드 시간 검사 이후 보호량을 먼저 소모하며 초과분만 HP에 적용한다. 만료·사망·컴포넌트 비활성화·Match 해제 시 종료한다. 제작된 `Portal Arrival Shield` LineRenderer가 청록 테두리를 표시한다. 피해 흡수는 추가 콜라이더 없이 처리한다.

`PlayerPortalSkillTests`는 별도 Unity 실행 환경에서 한 쌍 제한·취소·발밑 정렬·명시적 왕복·지면/장애물 재검사·회수/재설치·권한/생존/정지·체크포인트 복원과 rollback을 확인한다. 실제 게임 Scene의 조작/UI·협동 전송은 후속 검증이다.

검증 결과: Unity 컴파일 오류 0개, `PlayerPortalSkillTests` 7/7, 기존 `PlayerSkillSelectionTests` 12/12 통과. 새 `.meta` 쌍과 GUID 검사도 통과했다. 실제 게임 Scene 수동 플레이와 빌드는 실행하지 않았다.

2026-11-15 세로형 포털/F 회수 후속 검증: `PlayerSkillTreeRuntimeTests` 12/12, `PlayerPortalSkillTests` 7/7 통과. R/F 동시 입력은 이동만 처리하며 Shift+R로 회수되지 않고, 포털·터렛 회수 범위가 겹쳐도 안내된 가까운 한 개만 회수됨을 실제 프리팹 fixture로 검사했다. Unity 카메라에서 두 포털과 플레이어 크기를 함께 렌더하고 실제 알파·이미지/설치 영역·아이콘 참조를 확인했다. 컴파일 및 `.meta` 검사 오류 0개. 실제 Field Scene 수동 플레이·멀티플레이·빌드는 별도다.

전용 아트는 내장 `image_gen`으로 생성했다. 생성 지시: 기존 장비에 맞춘 올리브색 기계식 세로문, 검은 윤곽·금속 볼트·발판, 청록색 에너지 소용돌이, 투명 배경. B는 같은 외장의 에너지만 주황색으로 변경했다. 원본 PNG는 `Assets/Art/Sprites/Player/Skills/PortalGateA.png`와 `PortalGateB.png`이며 Unity에서 최대 256/Point/압축 없음으로 가져온다.

2026-11-15 발판 충돌 후속: 제작 프리팹과 플레이어 Rigidbody를 사용하는 통합 fixture에서 좌우 받침의 이동 차단·중앙 통로 통과·측면 R/F 접근·R 이동/F 회수를 확인했다. `PlayerSkillTreeRuntimeTests` 12/12와 `PlayerPortalSkillTests` 7/7 통과. Scene/물리 설정 변경은 없으며 실제 Field Scene 수동 인수와 멀티플레이는 별도다.
