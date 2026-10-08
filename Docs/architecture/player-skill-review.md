# Player skill responsibilities and review validation

수정일: 2026-10-09

PR #498 리뷰에 따라 스킬의 독립적인 책임을 분리했다. 기존 Player 소스와 몬스터 원본은 변경하지 않는다. 기존 MonoBehaviour의 GUID와 직렬화 필드는 유지하므로 현재 프리팹 연결을 그대로 사용한다. 공통 스킬 베이스와 역할별 보조 클래스로 분리했다. 임시 포탑 체력 컴포넌트는 프리팹에 연결했다. 피격 표시는 기존 색상 틴트만 유지한다.

## 역할

| 진입점 | 분리한 책임 |
| --- | --- |
| `PlayerFieldDevice` | 체력·수명 및 장비 활성 상태 |
| `PlayerFieldDeviceVisual` | 펄스·방벽·비콘 외형과 피격 표시 |
| `PlayerFieldDeviceRegistry` | 활성 장비 등록과 방벽 우선 목표 선택 |
| `PlayerBarrierGeometry` | 방벽과 이동 경로의 교차 판정 |
| `PlayerFieldEnemyScanner` | 주변 서버 네트워크 적에 중재 컴포넌트 연결 |
| `PlayerFieldEnemyAdapter` | 신규 네트워크 적과 장비 행동 연결 |
| `PlayerFieldEnemyControlLease` | 서버 권한 확인 및 기존 AI 중지·복구 |
| `PlayerFieldEnemyBehaviour` | 장비 접근과 공격 간격 |
| `PlayerFieldEnemyAnimation` | 이동 표현 및 기존 네트워크 공격 이벤트 호출 |
| `PlayerBarrierSkill` | 두 펄스 설치 흐름과 설치된 방벽 소유 |
| `PlayerSkillCharges` | 보유 수량과 순차 충전 |
| `PlayerBarrierPlacementRules` | 설치 구간의 지형 검사 |
| `PlayerBarrierPlacementPreview` | 설치 미리보기 |
| `PlayerSkillHud` | UI 조립 및 갱신 순서 |
| `PlayerSkillSlotView` | 개별 슬롯과 방사형 쿨타임 |
| `PlayerSkillSelectionPanelView` | 선택 카드와 Q/E 미리보기 |
| `PlayerSkillTooltipView` | 툴팁과 hover 처리 |
| `PlayerTurretRecoveryPromptView` | 포탑 아래 F 안내 |
| `PlayerSkillDescriptions` | 스킬 표시 이름과 설명문 |
| `PlayerSkillUiFactory`, `PlayerSkillHudFactory` | UI 부품 및 HUD 생성 |
| `PlayerSupportTurretTargeting`, `PlayerSupportTurretWeapon` | 임시 포탑 적 검색과 탄환 생성 |
| `PlayerBombAreaDamage` | 범위 피해와 대상별 중복 피해 방지 |

## 공통 스킬과 Scene 연결

네 스킬은 PlayerSkillBase를 상속하며 공통 사용 조건과 상태 조회를 제공한다. PlayerSkillController는 공통 베이스로 실행·UI 상태를 조회한다. PlayerControlState는 IPlayerMatchBinding을 통해 Scene 소유 Match의 단계를 전달받는다. Player V2의 GeneralManager·InGameManager 참조를 제거했다. 기존 씬의 GameOver 연결은 InGameManager가 PlayerInfo.onDeath를 구독하여 유지한다.

임시 포탑은 체력 100이며 손상 비율에 비례하여 최대 30초 충전한다. 독립적인 PlayerTurretInventory가 설치 수량과 각 포탑 충전을 담당한다. 설치 간격 3초와 충전은 별도다. 입력과 수치 변경 안내는 [Player Input Guide](../team/player-input-guide.md)를 따른다.

## 신규 몬스터 연동

장비 행동 중재는 `EnemyNetworkController`, `EnemyHealth`, `EnemyNetworkAnimator`를 사용한다. 레거시 `Monster`의 이동·공격 수치와 행동을 참조하지 않는다. 스폰된 서버 적만 기존 AI 실행을 잠시 중지하며, 클라이언트와 미스폰 적은 제어하지 않는다. 기존 타깃 및 분대 정보는 변경하지 않는다. 장비 소멸, 적 사망·Despawn, 중재 컴포넌트 비활성화 시 기존 실행 상태를 복구한다.

신규 몬스터는 공격력·사거리·쿨타임을 공개하는 API가 없어 장비 공격에는 Adapter의 임시 설정을 사용한다. 담당자 API 제공 시 이 설정을 교체한다. 이동 속도는 실제 이동량으로 추정하며 측정 전에는 임시 기본값을 사용한다. 기존 네트워크 Animator가 이동 상태를 게시하기 전에 표현을 갱신한다.

이 변경은 적 행동의 서버 권한을 보장한다. 스킬 장비 자체의 네트워크 Spawn·동기화 및 클라이언트의 설치 요청 처리는 별도의 후속 작업이다.

## 테스트

- 테스트는 `Test/Editor` 아래에 배치한다. 기존 Assembly-CSharp 런타임을 참조하므로 Editor 전용 컴파일을 유지한다. `AssetDatabase.MoveAsset`을 사용하는 `PlayerSkillFolderMigration`이 다음 에디터 컴파일 후 스킬을 Controllers·UI·Common·Devices·Enemy·Skills 폴더로 옮기고 테스트 폴더도 이동한다. 현재 에디터 연결이 응답하지 않아 실제 이동 완료는 아직 확인하지 못했다. 메뉴 `Tools > Player > Organize Skill Scripts`로도 실행할 수 있다. GUID와 meta는 Editor가 보존한다.
- Editor 폴더의 `AssetDatabase`는 기존 프리팹을 로드하는 검증용이다. 런타임 장비 생성은 직렬화된 프리팹 참조와 `Instantiate`를 사용한다.
- 테스트를 외형, 충돌, 목표 선택, 충전, 입력·선택 흐름, 폭발 및 무기 연결로 나눈다.
- 좌표·스케일·피해량·시간은 거리 또는 허용 오차로 비교한다.
- 테스트에서 `Awake`, `OnEnable`, `Start`, `LateUpdate`를 `SendMessage`로 호출하지 않는다. 생명주기 검증은 `EnterPlayMode` 후 실제 활성화와 프레임 진행을 사용한다.
- `PlayerFieldNetworkIntegrationTests`는 명시적 테스트다. `Initial_Stage` Host에서 스폰된 적이 비콘을 공격하고 기존 AI로 복구하는지 확인한다. 완료 후 이전 Scene 구성을 복구한다.

Unity Test Runner에서 `PlayerFieldSkillTests`, `PlayerFieldTargetSelectionTests`, `PlayerSkillChargeTests`, `PlayerFieldEnemyTests`, `PlayerWeaponMigrationTests`, `PlayerSkillSelectionTests`, `PlayerSkillPrefabTests`와 Runtime 테스트를 실행한다. 네트워크 통합 테스트는 명시적으로 선택하여 실행한다. 코드 컴파일 성공은 Host·Client 실행 검증을 대신하지 않는다.

## 이번 작업의 확인 결과

- 신규 스크립트의 `.meta`, 기존 직렬화 필드와 스크립트 GUID 유지, 원본 Player·Enemy 코드 및 몬스터 프리팹 미변경을 확인했다.
- 게임 코드와 전체 프로젝트 의존성을 포함한 컴파일에서 오류 0개였다. 기존 런타임 경고 12개는 유지된다.
- 충전 규칙은 초기 수량, 빈 수량 거부, 순차 충전, 충전 진행 유지, 최대 수량 및 잘못된 경과 시간 거부를 포함한 독립 실행 검증 8개가 통과했다.
- `git diff --check`가 통과했다.
- 전체 빌드를 막던 Core Editor 두 파일의 Application 이름 충돌을 UnityEngine.Application으로 명시하여 수정했다.
- Unity 테스트는 동일 프로젝트를 열고 있는 다른 Unity 인스턴스로 인해 실행되지 않았다. 열려 있는 에디터를 종료하지 않았다. NUnit·PlayMode·Host/Client 실행 통과를 주장하지 않는다.

- 공개 팀 문서 사이트는 Cloudflare Access 로그인 화면으로 연결되어 본문을 확인할 수 없었다. 로컬 Docs와 실제 Core/Enemy 계약을 기준으로 반영했다.

- 임시 포탑 재충전 규칙은 독립 실행 검증 17개가 통과했다. 초기 3개, 초과 설치 거부, 정상 회수 즉시 반환, 반피 15초, 파괴 30초, 병렬 충전, 중복 반환 거부, 잘못된 시간·체력 입력을 확인했다. 실행 환경의 구형 C# 컴파일러에 맞추어 검증용 복사본의 프로퍼티·초기화 문법만 변환했다.
