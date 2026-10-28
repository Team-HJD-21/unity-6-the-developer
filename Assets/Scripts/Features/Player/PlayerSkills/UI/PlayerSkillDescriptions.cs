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
        _ => "이곳에는 포탑을 설치할 수 없습니다."
    };

    public static string Name(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => "시한폭탄",
        PlayerSkillId.PlayerTurret => "플레이어 터렛",
        PlayerSkillId.EnergyBarrier => "방벽 투사기",
        PlayerSkillId.LureBeacon => "유도 비콘",
        _ => "미선택"
    };

    public static string Description(PlayerSkillId skill, PlayerSkillController owner)
    {
        return (skill switch
        {
            PlayerSkillId.Bomb => "발밑에 시한폭탄을 설치합니다.\n몬스터·장애물과 겹치면 설치 불가\n잠시 후 주변 적에게 범위 피해\n보유 수량: " + owner.BombCount,
            PlayerSkillId.PlayerTurret => "플레이어 주변 8칸 중 마우스 방향에 설치\n왼쪽 클릭 확정 · 플레이어 이동 시 후보도 이동\n우클릭 / Esc / 같은 스킬 키로 취소\n플레이어·몬스터와 겹치면 설치 불가\n본체 통과 불가 · 체력 100 · 최대 3개\n설치 간격 3초 · 근처에서 F키로 회수\n손상 비율에 따라 최대 30초 재충전",
            PlayerSkillId.EnergyBarrier => "발밑에 펄스 설치 → 같은 키로 연결\n연결 거리 1~18 · 지형 통과 불가\n몬스터 이동 차단 · 아군 탄환 통과\n보유 3개 · 18초마다 1개 충전\n우클릭으로 미완성 펄스 취소",
            PlayerSkillId.LureBeacon => "발밑에 주변 적을 유인하는 비콘 설치\n유인 반경 12 · 유지 8초\n체력 80 · 재사용 14초",
            _ => ""
        });
    }
}
