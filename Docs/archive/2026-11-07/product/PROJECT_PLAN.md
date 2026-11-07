> 보관 사본 · [교체 전 원문](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/product/PROJECT_PLAN.md) · [현행 문서](../../../product/PROJECT_PLAN.md)
>
> 당시 결정과 검증 설명은 원문 시점에 해당한다. 상대 링크는 같은 커밋의 원본을 가리킨다.

# The Developer — Product Plan

버전: 0.4
기준일: 2026-11-07
상태: 현행 PL 기준 / M1·M2 경계에서 검증 지속 · 10월 17일 제품 방향 기록 · 정식 완료 판정 없음

이 문서는 `무엇을 왜 만드는가`를 정의한다. 구현 순서와 일정은 [milestones-and-core-design.md](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/milestones-and-core-design.md), 담당 경계는 [TEAM_ROLE_OWNERSHIP.md](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/team/TEAM_ROLE_OWNERSHIP.md)를 따른다.

## 1. 게임 정체성

> 선발대가 행성에 미리 설치한 터렛 거점을 제한된 전력으로 운용해 전선을 밀고 당기며, 플레이어가 위험한 전선을 찾아가 지원하는 2D 탑다운 액션 타워디펜스.

터렛은 플레이어가 건설하거나 이동하지 않는다. `설치`는 제작자가 Scene/Prefab에 미리 배치한다는 뜻이며, 플레이어 행동은 터렛 선택·가동·정지, 전력 재배치와 현장 지원이다. 플레이어는 혼자 적을 쓸어버리는 주 화력보다 수리·보조 공격·군중 제어로 무너지는 전선을 복구하는 기동 지원 유닛에 가깝다.

## 2. 플레이어 경험 목표

1. 모든 터렛을 켤 수 없다는 긴장감을 느낀다.
2. 적 조합과 공격 방향을 읽고 전력을 배분한다.
3. 전력이 없는 방어선으로 직접 이동해 빈틈을 메운다.
4. 새 Sector가 열리면서 새로운 선택지를 발견한다.
5. 확장과 함께 전선·이동 거리·위협도 커지는 부담을 감수한다.
6. 마지막에는 자신이 구성한 방어망 전체로 행성을 지켜냈다는 성취감을 얻는다.

## 3. Core Loop와 Meta Loop

Stage 1 PoC는 전제 1(동적 전선)과 전제 2(지역 해금)가 각각 재미있는 전력·전선 선택을 만드는지 시험한다. 두 안의 구현 순서와 병행 범위는 아직 정하지 않았고, 전제 3(기존 웨이브 진행)은 보류한다. Spaceship의 연구, 결과 성장, 다음 Mission 선택은 이번 재미 검증의 완료 조건이 아니라 M2 Product Vertical Slice의 목표다. 보관된 [진행 방식 비교](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/archive/2026-09-22-progression-options.md)와 [9월 26일 회의 근거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/archive/2026-09-26-stage1-planning.md)를 참고한다.

점유 계산은 전제 1과 전제 2를 구분해 설계한다. 한 전제에서 논의한 노드·연결 조건을 두 전제의 공통 규칙으로 적용하지 않는다.

| 전제 | 이번 회의에서 다룬 방향 | 확정하지 않은 부분 |
| --- | --- | --- |
| 전제 1 — 동적 전선/영역 | CU를 공간 위치 노드에 포함하고 시작 시 CU 주변 일부를 점유하도록 하는 안을 검토했다. 실제 시제품으로 확인한다. | 노드가 만드는 영역과 최종 점유 셀의 관계, 점유·상실·연결 판정의 최종 계약 |
| 전제 2 — 정적 지역 해금 | 우선 CU 거리 기반 Grid의 지역 해금을 비교 시험한다. 해금 권한·터렛 활성화·실제 점유를 구분한다. 송전탑은 별도 해금안이다. | 지역 해금과 실제 점유의 연결, 영역별 점유·상실 규칙 및 최종 해금 방식 |

회의에서 언급한 RegionGrid 셀·삼각형 입력·상하좌우 연결·먼 터렛의 독립 점유 문제는 전제별로 적용 범위를 확인해야 한다. 이를 ‘두 전제 모두 CU에서 상하좌우로 이어져야만 점유된다’는 공통 확정 규칙으로 기록하지 않는다. CU를 공간 노드에 포함하는 논의도 CU 수명·체력까지 터렛과 통합한다는 의미는 아니다. 전제별 판정표와 시제품 검증, 최종 전제 선택은 후속이다. [논의 근거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-11-06-planning-review.md)

```text
Spaceship에서 Planet 전투 시작
→ Player 이동·전투, 미리 배치된 터렛 운용, 전력 제약 아래 전선·지역 변화
→ 점유 영역 확장 이후 Boss encounter로 이어지는 흐름 검토
→ Boss 처치 승리를 지향하되, Stage 1 PoC의 완료 조건과는 별도로 기록
```

현재 Stage 1 PoC는 전제 1·2의 재미를 확인하는 시험이다. 아래의 보스 승리·게임오버 방향을 PoC의 구현·검증 완료 기준으로 간주하지 않는다.

```text
전제 1의 동적 전선 또는 전제 2의 지역 해금 시험
→ 전력 제약·전선 변화와 조작의 재미·이해도 관찰
→ Player 사망은 디버그 부활 또는 무적으로 시험
```

### 승리·실패와 안전장치

점유 이후 보스 encounter를 거쳐 보스 처치로 승리하는 방향이다. 25%마다 보스/중간보스가 등장할 수 있도록 기반을 설계하되, 보스 수·컨셉·최종 등장 조건·공간·승리 확정은 후속 설계다. CU 파괴는 게임오버 방향이며 실제 구현·인수와 구분한다.

Player 사망 안전장치는 3회로 정리했다. 보스/중간보스전 시작 전과 클리어 후 체크포인트를 저장하며 첫 저장이 없으면 처음으로 돌아간다. 기본 복귀에서 경험치·재화는 현재 값을 유지하고 나머지는 저장 시점으로 복원하는 방향이다. 재화 롤백 선택 계약·세부 저장 필드·원자적 복구·정상 종료 후 재개·비정상 종료 자동 저장은 별도 기술 설계와 검증 대상이다. 실패 결과의 추가 보상은 없으며 진행 중 획득 보상과 구분한다.

협동에서는 한 명 사망 시 구조할 수 있고 전멸 또는 구조 제한시간 소진 시 안전장치로 복귀하는 방향이다. 공용 구조시간은 사용한 시간이 누적 소모되는 형태로 설명됐다. 최종 초 수·초기화 범위는 미정이며 예시 수치를 제품값으로 고정하지 않는다. SOS 지원자 스펙은 요청자와의 레벨차가 아닌 Stage 기준으로 하향 보정하는 방향이다. 적용 능력치·배율·인원 난이도 곡선은 미정이며 SOS의 기존 Milestone 범위를 앞당기지 않는다.

이 제품 정책은 기술 PoC 완료 조건·CI·실제 동작 및 PL 인수와 별개다. [11월 6일 후반 근거와 미정](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-11-06-planning-review.md)

## 4. 핵심 게임 규칙

### 4.1 Power Budget

- 최대 전력은 공간에 펼쳐진 Loadout Cost다.
- 전제 1에서는 개별 터렛 활성화가 동적 전선과 점유 변화에 어떤 선택을 만드는지 시험한다.
- 전제 2에서는 지정 지역 해금과 그 안의 터렛 운용이 어떤 선택을 만드는지 시험한다.
- **2026-10-30 후속:** 일반 OFF는 즉시 비활성화하고 기존 점진 전력 반환을 시작하며 냉각을 함께 진행한다. 즉시 전량 충전이 아니다. 자동 활성화는 사거리 내 적 수 우선·동률이면 전력 1당 공격력 설명에 따라 비교하고, 현재 ON 터렛을 동시에 OFF한 뒤 순차 ON한다. 다음 터렛의 전력이 부족하면 예약하고 회복을 기다리는 방향이다. 취소·파괴·잠금 변경과 재계산·실제 API/실행은 별도 확인한다. [전사 대조 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-30-planning-review.md)
- 기존 Wave 단위 운영 규칙은 전제 3의 재검토 전까지 Stage 1 PoC 규칙으로 취급하지 않는다.

초기 Paper Simulation 기준값은 최대 100, Cannon 20, Missile 30, Laser 50이다. 이는 확정 Balance가 아니다.

### 4.2 Map Expansion

확장마다 아래 두 항목을 모두 제공한다.

- 새 이점: 신규 터렛, 외곽 요격선, 지름길, Radar, 보조 시설 중 하나 이상
- 새 위협: 신규 Spawn 방향, 긴 이동 거리, 다중 전선, 특수 Enemy 중 하나 이상

면적과 배경만 늘어나는 확장은 Content로 인정하지 않는다. 이전 Sector의 터렛은 확장 후에도 내곽 방어선으로 유효해야 한다.

2026-09-19 Stage 1 PoC에서는 여러 짧은 맵보다 하나의 확장형 전장을 먼저 검증하기로 했다. 9월 22일 회의의 후보 비교 후 9월 26일에는 ① 동적 전선, ② 지역 해금을 우선 검토하고 ③ 기존 스테이지 방식은 뒤로 두기로 했다. ①·② 중 최종 진행 방식을 선택하거나 병행 범위·일정을 정한 것은 아니다. 거점 확보와 비선형 확장은 검증 후보이며 확정 규칙이 아니다.

### 4.3 Player와 Turret

Player 총기는 탄창 내 탄 수를 제한하고 재장전 시간을 두되 예비 탄창/총 탄약의 보급 제한은 두지 않는 방향이다. 무기별 탄 수·장전 시간은 미정이다. [후반 확인](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-11-06-planning-review.md)

- 터렛만 켜고 기다리는 플레이와 Player 화력만으로 전력 규칙을 무시하는 플레이를 모두 피한다.
- Player는 이동 가능한 지원 전력이며 터렛 사각, 긴급 Enemy, 전력이 없는 구역을 담당한다.
- 플레이어는 터렛을 직접 수리하는 기본 기능을 가진다. 2026-10-24 수기에는 Interaction range 내 시간 기반 수리·복구, 재화 비용 없음, 복구 취소 후 게이지 유지, 키 유지 수리 상태 진입 방향을 기록했다. 후반 전사에서는 별도 스패너 수리 무기 조작을 대체하되 수리 모션은 유지할 수 있고 근접 무기는 별도로 다루는 방향을 확인했다. Repair는 일반 HP 회복, Restore는 HP 0 복구이며 복구 중 터렛을 공격하지 않는다는 설명은 실행 검증과 구분한다. 수리 드론은 보류한다. 범위·속도·시간·입력·저장·네트워크 정책은 미정이다. [10월 10일 회의](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-10-regular-meeting.md) · [10월 24일 회의](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-24-regular-meeting.md)
- 캐릭터와 무기 스프라이트는 분리 제작하는 방향이다. 8방향 표현은 가능 여부를 시험하며, 방향 수·애니메이션 범위는 미정이다. Player의 직접 공격 비중과 무기·스킬·몬스터의 대응 관계도 후속 검토한다. [10월 3일 회의](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-03-poc-followup.md)
- 2026-10-03 회의에서는 개별 터렛 해금과 지역 단위 해금을 모두 지원하고, 해금 상태를 Control Unit과 메이저 스테이지에 귀속하는 방향을 확인했다. 지역 관리·Core 연결, UI와 저장 계약은 추가 조율한다. [후속 검토](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-03-poc-followup.md)
- 2026-10-10 회의에서는 잠긴 터렛은 파괴되지 않고, 해금 후 별도 수리 없이 사용할 수 있게 하는 방향을 정리했다. 전력 부족으로 플레이어가 끈 터렛은 잠금과 구분하며 적의 공격 대상이 될 수 있다. 현재 main은 CU 거리 기반 Grid 해금 프로토타입을 제공하지만, 제품의 고정/동적 지역, 해금 조건, 저장 자료 구조와 전선 토폴로지 연결은 미정이다. [회의 검토 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-10-regular-meeting.md)
- Turret Cost·Range·역할·현재 상태와 Enemy의 주요 특성은 전투 중 읽을 수 있어야 한다.

### 4.4 Spaceship Hub

Spaceship은 출격 전 준비·행성 이동·재도전·클리어를 연결하는 허브다. 전투 중 필드에서 내부로 자유롭게 출입하지 않으며, 필요한 정비·업그레이드는 필드의 우주선 외부 상호작용으로 제공하는 방향이다. 내부에서는 필드 기능과 추가 준비 기능을 제공할 수 있다. 보스 클리어 뒤 일시 출입과 안전장치 복귀 시 내부 스폰을 논의했으며, 정확한 Scene 전이·안전장치 시점의 필드/시간 복원·일반 사망과 최종 실패의 구분은 실행 계약으로 확인한다.

외부 상호작용 방식과 내부 전용 시간제 연구는 후속 설계다. 출격 시 귀환 제한을 알리는 연출·호출/텔레포트는 자동으로 필수 기능에 포함하지 않는다. 작은 기능성 Interior를 우선하며 대형 함선·긴 빈 복도는 만들지 않는다.

과거의 전투 중 자유 출입 및 입장 시 필드 정지 정책은 이번 출입 흐름으로 대체한다. 11월 6일 중간에 논의한 ‘필드는 정지하고 낮밤만 진행’ 안도 후반에 폐기했다. 일반 Pause·설정 메뉴·앱 일시 중단까지 같은 정책으로 확대하지 않는다. [논의 순서와 폐기 근거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-11-06-planning-review.md)

CU는 자동 활성화·전술 지도·점유된 땅의 원격 터렛 작동을 제공하는 기본 역할이다. Player/무기 성장과 터렛/CU 전역 성장의 화면·사용 장소·편의 해금 순서는 성장 트리와 함께 구체화한다. 클리어 행성은 점령 상태 유지 재방문 방향이며 처치 이벤트·재보상을 반복하지 않는다. 별도 수급처·NPC·자동 생산은 확정하지 않는다.

### 4.5 Enemy와 전선

암살자는 기본적으로 터렛이 탐지·추적하지 못하지만, **플레이어는 드론 없이도 암살자를 볼 수 있다.** 플레이어 가시성과 아래 드론의 보조 역할은 2026-11-07 PL 보강으로 명시한다.

**드론은 터렛이 암살자를 탐지하도록 돕는 보조수단**이다. 위치를 지정해 보내는 조작형을 스킬과 별도로 시험하며, 자동 수리 드론과는 다르다. 드론의 탐지 결과를 터렛 타기팅에 연결하는 API·범위·사용 제한은 후속 계약이다. 기존 지원형/암살자 역할을 대체하지 않는다.

- Enemy의 1차 목표는 Player 추격이 아니라 터렛 거점과 핵심 시설 공격이다.
- 2026-10-02에는 기존 점수 기반 몬스터 타깃 선택을 먼저 시험하고 고정 경로안은 채택하지 않기로 했다. 2026-10-23 후속 회의에서는 복합 점수식을 바로 확장하기보다 **최단거리와 주변 밀집도만 우선 적용**하고, 목표가 새로 필요할 때 선정한 대상을 유지하는 방향으로 시험하기로 했다. 이는 우선 시험 방향이며 최종 AI 구조·규칙이나 구현 완료를 뜻하지 않는다. 단순 기준이 부족하면 평가 요소를 확장할 수 있다. [10월 2일 AI 구조 검토](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-02-ai-architecture-review.md) · [10월 23일 몬스터 회의](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-23-monster-meeting.md)
- 지역 내부의 터렛·Player 선택과 타깃 점수의 화력 비율·이벤트 조정은 실험 후 결정한다.
- Spawn 위치와 주기는 약한 전선과 Player 위치를 고려하되, 구체 규칙은 PoC 결과로 결정한다.
- 2026-10-03 회의에서는 몬스터 **종류별** 목표 특성과 탱커·원거리 등의 분대 프리셋을 함께 검토했다. 2026-10-23에는 감지 범위 안의 플레이어를 추적하고 이탈하면 터렛 행동으로 돌아오는 필수 동작을 먼저 시험하기로 했다. 감지 반경, 복귀 시 목표 유지·재선정, 분대별 예외는 미정이며 실제 실행 결과도 확인되지 않았다. [10월 3일 후속 검토](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-03-poc-followup.md) · [10월 23일 몬스터 회의](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-23-monster-meeting.md)

2026-10-24에는 분대 단위로 스폰·목표 터렛 파괴 때 목표를 배정하고 이후 수용량 변화만으로 계속 재선정하지 않는 기본안을 확인했다. 필드 전체 개체 수에 별도 상한을 두지 않고 스폰량으로 조절하되, 터렛별 수용량과 구분한다. 대량 개체 성능은 미검증이고 스포너 식물·투하·함정은 제안이다. [전사 대조 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-24-regular-meeting.md#5-몬스터-목표스폰성능)

2026-10-31 후속: 스폰 압력은 Player가 어느 구역에서 멀어졌는지보다 전선·구역·이벤트·날씨로 다룬다. 개별 Enemy의 Player 수리 상태 참조는 별개다. 점령 지역의 스폰 영향 감소/제거와 상실 시 재활성을 검토하지만, 전체 점유율 하나로 다른 방향 스포너까지 끄는 반례가 있어 알고리즘은 미정이다. 조수빈의 스포너 그룹화와 지역별 시험을 먼저 진행한다. 보스 상세는 기본 루프 통합 뒤로 미룬다. [회의 기록](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-31-regular-meeting.md)

### 4.6 낮과 밤 실험

- 낮 전진과 밤의 강한 적·위험 증가를 기본 방향으로 검토한다. 과거의 밤중 Spaceship 내부 피신/정비 구상은 §4.4의 전투 중 내부 진입 제한에 맞춰 재설계한다. 잔류 전투의 보상 증가, 암살자·태양열 전력 감소 등의 세부는 후보이며 배율·주기·방어선 처리와 실제 밸런스는 미정이다.
- 날씨별 효과 전체를 확정하지 않는다. “스폰 주기 증가”의 간격/빈도 의미, 가중치·행성별 종류·최종 개수는 미정이다. 기존 낮밤·강수·번개 표현에 더위 표현 정도를 보강하고 시험하면서 덜어내자는 후반 범위를 따른다. 무제한 전력 이벤트를 채택한다면 종료 후 자동 활성화 fallback 방향이며 이벤트 자체 구현 완료는 아니다.
- 밤·날씨 후보가 Stage 1 필수 범위나 Gate를 자동 확대하지 않는다. [기획 보강 근거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-30-planning-review.md) · [정지 재확인](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-31-regular-meeting.md)

### 4.7 성장·재화 검토 (2026-10-24)

후반 전사는 경험치 → 레벨 업 → 스킬트리 토큰의 성장 흐름과 스테이지 종료 후 경험치 유지를 확인했다. 무기용·터렛용 Blueprint와 재화를 함께 사용하고, 터렛 레벨에 따라 Minor Upgrade 상한을 두며 CU 성장 시 HP·EP를 함께 올리는 방향이다. 토큰 지급 간격·개수, Blueprint/재화 소비표와 저장 구현은 미정·미검증이다. 레벨별 상한 2/3/5는 예시다. Player 기본 스탯·장비와 스킬트리 경계, 재화1·유료 재화의 명칭·정책과 BFS/DFS 구조는 미정이다. 몬스터·보스·중간 보스·레벨 업 보상 경로를 검토했고 수기의 확률은 최종 밸런스 승인 여부가 확인되지 않았다. 취소선 처리된 맵 상호작용·메이저 스테이지 클리어 보상은 채택하지 않는다. 이 기록이 Stage 1 PoC, M2 또는 Deferred 범위를 자동 확대하지 않는다. [후보·미결정 항목](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-24-regular-meeting.md#4-성장재화-검토)

2026-10-30 성장 후속: 터렛 현장 승급과 전역 세부 성장을 분리하고 고효율/고화력 두 갈래를 스킬트리 형태로 재설계한다. Player와 같은 UI/성장 로직을 쓰되 하나의 트리로 합치지 않는다. CU 성장 관문에 재화·특정 Stage 클리어 조건을 두고 터렛 승급 상한을 연다. 터렛 등급별 전력 소모는 직접 강화 항목과 구분하며 CU 총 전력량/전력 효율은 교차 강화 방향이다. 트리·가격·클리어 번호·저장 계약·필드/우주선 해금 순서는 미정이다. 황재동의 트리 구체화와 10월 31일 파트별 대응 자료 정리는 후속 작업이며 구현 완료나 PoC 완료 조건이 아니다. [성장 근거](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-30-planning-review.md) · [자료 정리 후속](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-31-regular-meeting.md)

## 5. M2 Product Vertical Slice

M1 New Core Foundation이 통과된 뒤 M2에서 제작한다. 2026-10-02 Stage 1 PoC는 핵심 재미와 구현 가능성을 빠르게 확인하는 time-box 실험이며, 그 자체로 M1 또는 M2 Gate 통과를 뜻하지 않는다.

### 플레이 범위

아래는 M2 목표 계획이다. 진행 방식과 확장 단계의 구체 판정은 Stage 1의 전제 1·2 실험 결과로 다시 정하며, 기존 웨이브 진행을 확정한 것은 아니다.

- 목표 플레이 시간: 15~20분
- Planet 1 Full Map 1개
- 검증 상태: 50% → 75% → 100%
- 완성형 확장 계획: 50% → 65% → 75% → 90% → 100%
- Player Weapon 1종, Control Unit 1개
- Cannon, Missile, Laser 터렛
- Default, Tanker, Assassin Enemy와 Boss 1종
- Power HUD와 개별 터렛 ON/OFF
- 소형 Spaceship Interior와 필수 Console
- Spaceship → Planet → Result → Spaceship 왕복
- Upgrade Choice 1개, Clear·Fail·Retry
- 최소 Tutorial과 Context 설명

### M2 제외 범위

- 실제 Online Co-op, Random Match, SOS
- 두 번째 Planet과 다수 Player Weapon
- 플레이어의 터렛 건설·이동
- Sector 단위 일괄 전원 제어
- 대형 Research Tree와 장기 Economy
- Random/Procedural Map
- Multi-deck Spaceship, Ship Combat, Crew/Maintenance Simulation
- 전체 Story와 장편 Cutscene

## 6. 출시 방향

- M2까지는 Single-player Core와 Product Vertical Slice를 검증한다.
- M3에서 Steam 친구 초대 기반 2-player Co-op MVP를 검증한다.
- M4에서 Random Match, SOS, Reconnect, Chat/Ping과 Content Alpha를 범위별로 검증한다.
- 실제 Host Migration은 Deferred다. M4에서 Snapshot/Authority 이전 PoC만 수행하고 출시 포함 여부는 별도 결정한다.
- PvP와 `RivalPlayerAgent`는 Release 이후 별도 Product Track이다.
- Profile은 Single·Co-op에서 공유하며 Backend authoritative를 목표로 한다. Local versioned binary는 캐시다.

## 7. Product Vertical Slice 통과 지표

- 외부 Tester 5명 중 4명이 설명 없이 터렛을 켜고 Power Limit을 이해한다.
- 5명 중 4명이 Expansion과 Power Allocation을 게임의 특징으로 기억한다.
- 5명 중 4명이 Spaceship에서 다음 행동과 Launch 방법을 찾는다.
- 서로 다른 두 Turret Build로 Clear할 수 있다.
- Player의 직접 전투가 최소 한 번 이상 승패에 의미 있게 영향을 준다.
- Expansion 뒤 Attack Direction과 Power Allocation이 실제로 달라진다.
- 이전 Sector가 최종 방어에서도 사용된다.
- Progression Blocker, Crash, Save Corruption이 없다.

## 8. 미결정 항목

다음은 정해진 기능이 아니라 검증할 질문이다.

- 우주선은 §4.4의 전투 중 진입 제한을 따르며 외부 상호작용·클리어/안전장치 전이·저장·멀티플레이 및 명시적 Pause 계약은 추가 설계
- 이전 Mission의 터렛 상태·피해를 다음 Mission에 유지할지
- 전투 중 전력 전환의 지연과 횟수 제한
- 터렛 파괴·수리 규칙
- 미확장 구역 접근 방식
- 최종 Planet 수와 전체 플레이 시간
- Sector 일괄 전원 제어의 필요성
- Single-player Offline 정책과 Backend 장애 시 동작
- Host Migration의 출시 포함 여부
- Player의 주 지원 행동과 공격 조작 방식
- 동적 전선·지역 해금 중 최종 진행 방식, 기존 스테이지 방식 재검토 여부와 점유율 판정 데이터
- 전제 1·2를 순차 또는 병행 검토할지와 구현 일정
- 터렛 단위와 region 단위 전선 계산 중 채택할 방식
- 터렛 파괴·수리·재활성화 비용과 Player 사망/Control Unit 파괴 뒤 유지·초기화 범위
- 터렛 파괴 시 예약 전력의 실제 반환 여부·손실률. 0% 손실이면 전량 반환, 50% 손실이면 절반 반환하도록 API 입력값은 열어두되 제품 수치는 정하지 않음
- 지역 해금의 단위·조건과 지역/그리드 데이터의 저장·전선 토폴로지 연결 방식
- 몬스터 목표 배정의 거리 기준·밀집도 계산과 상한, 재선정 시점, 플레이어 감지 반경·이탈 후 목표 복귀 규칙, 분대별 예외와 복합 점수식 확장 필요 여부
- 터렛 설치 지형·겹침 판정의 기술 방식
- 기본 수리의 속도·수치와 수리 드론/로봇의 지급·판매·가격·밸런스
- 자동 활성화의 우선순위·전력 대기는 10월 30일 방향을 따른다. 공격력 비교와 실제 DPS의 관계, 취소·파괴·잠금 변경·재계산과 재사용 간격은 미정이며 1분을 확정하지 않음
- 2인 SOS의 late join, 매칭 실패 시 Ally AI, 난이도·보상·호스트 권한 규칙. 10월 2일 PoC 범위 밖
- 낮/밤 주기와 위험 효과
- 한 행성의 목표 플레이 시간. 20~25분 의견은 나왔으나 확정하지 않음

미결정 항목은 현재 Milestone의 P0/P1을 대체하지 않는다. 실험 결과와 함께 Decision Log에 승인된 뒤 Backlog로 이동한다.

## 9. 결정 근거

과거 결정 이력은 [교체 전 Product Plan](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/archive/2026-11-05/product/PROJECT_PLAN.md#9-decision-log)에 보존한다. 이 문서 앞쪽의 현행 제품 방향을 우선한다.


## 2026-10-27 진행 공유와 미결정 후보

조작 요청은 Core/Match 검증을 거쳐 각 기능 API로 전달하는 방향을 우선 설계한다. 이는 제품 규칙의 새 확정이나 네트워크 권한 계약 완성을 뜻하지 않는다. World의 영역 방어 야생동물·이동 영향 식물과 날씨 셰이더 표현은 구체화·조사 대상이며 채택·효과·수치·Weather 통합 여부는 미정이다. 기존 게임 범위와 승패·성장 합의를 대체하지 않는다. [근거와 담당 후속](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-27-weekly-progress.md)

## 2026-10-30·31 기획 보강과 후속

[10월 30일 전사·수기 대조](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-30-planning-review.md)와 [10월 31일 후속](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-10-31-regular-meeting.md)을 함께 반영했다. 위 핵심 방향과 별도로 성장 트리/저장, 스포너 지역별 판정, 재방문 수급, 날씨 효과 수치·주기, 새 우주선 출입 흐름의 실제 구현/멀티플레이 검증은 미완료다. **전원 크런치 모드 도입은 확정된 팀 공통 운영 결정**으로 정정한다(2026-11-01 황재동 확인). 17일까지 집중 작업·18–23일 휴식 발언과 연결하며 상세 시간·개인 일정·복귀일을 조율한다. 11월 내 전제 선택 목표나 크런치 도입은 검증 완료·새 Sprint 번호·기능별 확정 납기를 뜻하지 않는다. Drive 목록과 인게임 도감 UI, 회의 보고와 실제 실행 증거도 구분한다.

## 2026-11-03 지원형 몬스터와 비탐지 방향

- 지원형은 **소환형과 회복형 두 종으로 분리**한다. 소환형은 안전한 위치를 찾아 뿌리내린 뒤 이동·자체 공격 없이 소환을 지속한다. 안전 위치 판정과 회복형의 구체적인 치료 방식은 후속 설계다.
- 터렛이 추적하지 못하는 우선 대상은 **암살자**다. **플레이어는 드론 없이도 암살자를 볼 수 있고, 드론은 터렛의 암살자 탐지를 돕는 보조수단이다**(2026-11-07 PL 보강). 반투명 표현 정도와 탐지 업그레이드의 도입·적용 범위는 미정이다. 모든 몬스터·터렛에 공통 은신 정책을 새로 확정하지 않는다.
- Player/터렛 탄환이 통과하면 분열하는 방벽은 시험 제작·피드백 후보다. 정식 채택·배수·효과와 터렛 탄환 API 추가는 확정하지 않았으며 채택 시 계약을 협의한다.

기존 제품 범위·승패·성장 규칙과 Sprint를 바꾸지 않는다. 현재 수치 Sheet는 정리본이며 최종 밸런스가 아니다. [회의 근거와 미정](https://github.com/Team-HJD-21/unity-6-the-developer/blob/86db66681056df294bd95fa47cd48a121d2d9e34/Docs/planning/2026-11-03-weekly-progress.md)
