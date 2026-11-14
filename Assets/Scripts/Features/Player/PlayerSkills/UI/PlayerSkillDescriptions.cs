/// <summary>스킬 이름과 설명문을 모아 둔 표시용 카탈로그입니다.</summary>
public static class PlayerSkillDescriptions
{
    public static string BombPlacementFailure(PlayerBombPlacementResult result) => result switch
    {
        PlayerBombPlacementResult.Allowed => "",
        PlayerBombPlacementResult.OverlapsEnemy => "몬스터와 겹쳐 시한폭탄을 설치할 수 없습니다.",
        PlayerBombPlacementResult.BlockedByObstacle => "장애물과 겹쳐 시한폭탄을 설치할 수 없습니다.",
        _ => "시한폭탄 본체 설정을 확인해 주세요."
    };

    public static string TurretPlacementFailure(PlayerTurretPlacementResult result) => result switch
    {
        PlayerTurretPlacementResult.Allowed => "",
        PlayerTurretPlacementResult.OutsideGround => "지면이 부족합니다. 안쪽으로 이동해 주세요.",
        PlayerTurretPlacementResult.OverlapsTurret => "다른 포탑과 겹쳐 설치할 수 없습니다.",
        PlayerTurretPlacementResult.BlockedByObstacle => "장애물과 겹쳐 설치할 수 없습니다.",
        PlayerTurretPlacementResult.OverlapsPlayer => "플레이어와 겹쳐 설치할 수 없습니다.",
        PlayerTurretPlacementResult.OverlapsEnemy => "몬스터와 겹쳐 설치할 수 없습니다.",
        _ => "이곳에는 스킬을 설치할 수 없습니다."
    };

    public static string Name(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => "시한폭탄",
        PlayerSkillId.PlayerTurret => "플레이어 터렛",
        PlayerSkillId.EnergyBarrier => "방벽 투사기",
        PlayerSkillId.LureBeacon => "유도 비콘",
        _ => "미선택"
    };

    /// <summary>스킬 트리의 짧은 표시 설명입니다. 실시간 수량·재사용 시간은 기존 선택 UI에서 표시합니다.</summary>
    public static string Summary(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => "설치 후 잠시 뒤 폭발하여\n주변 적에게 범위 피해를 줍니다.",
        PlayerSkillId.PlayerTurret => "플레이어 주변 8방향에 설치해\n접근하는 적을 자동 공격합니다.",
        PlayerSkillId.EnergyBarrier => "두 본체를 연결해 장벽을 만듭니다.\n적의 이동을 막고 아군 탄환은 통과합니다.",
        PlayerSkillId.LureBeacon => "주변 적을 끌어들이는\n유도 비콘을 설치합니다.",
        _ => ""
    };

    public static string Description(PlayerSkillId skill, PlayerSkillController owner)
    {
        return (skill switch
        {
            PlayerSkillId.Bomb => "플레이어 주변 원 안의 커서 위치에 시한폭탄 설치\n왼쪽 클릭 확정 · 우클릭 / Esc / 같은 키로 취소\n캐릭터·장애물과 겹치면 설치 불가\n잠시 후 주변 적에게 범위 피해\n보유 수량: " + owner.BombCount,
            PlayerSkillId.PlayerTurret => "스킬 사용 후 1·2·3번 포탑 상태에서 선택\n플레이어 주변 8칸 중 마우스 방향에 설치\n왼쪽 클릭 확정 · 우클릭 / Esc / 같은 키로 취소\n설치 간격 3초 · 근처에서 F키로 회수\n체력 100 · R키 유지 수리 · 전력 사용 없음\n회수·재설치 시 해당 슬롯의 체력 유지\n파괴된 슬롯은 30초 후 최대 체력으로 복구",
            PlayerSkillId.EnergyBarrier => "플레이어 주변 반경 12 안에 첫 본체 설치\n두 번째는 첫 본체 중심 반경 13.5 안에서 클릭\n두 번째 설치 실패 시 첫 위치 유지 · 재시도 가능\n연결 거리 1~13.5 · 캐릭터·장애물 겹침 불가\n몬스터 이동 차단 · 아군 탄환 통과\n완성에만 1개 소모 · 18초마다 1개 충전\n우클릭 / Esc / 같은 키로 전체 취소",
            PlayerSkillId.LureBeacon => "플레이어 주변 원 안의 커서 위치에 비콘 설치\n왼쪽 클릭 확정 · 우클릭 / Esc / 같은 키로 취소\n유인 반경 12 · 유지 8초\n체력 80 · 재사용 14초",
            _ => ""
        });
    }
}
