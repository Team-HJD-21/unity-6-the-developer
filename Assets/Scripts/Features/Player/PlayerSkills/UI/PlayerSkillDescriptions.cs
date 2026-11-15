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
        PlayerSkillId.Portal => "순간이동 포털",
        PlayerSkillId.DualWield => "양손 무기",
        _ => "미선택"
    };

    /// <summary>스킬 트리의 짧은 표시 설명입니다. 실시간 수량·재사용 시간은 기존 선택 UI에서 표시합니다.</summary>
    public static string Summary(PlayerSkillId skill, PlayerSkillController owner = null) => skill switch
    {
        PlayerSkillId.Bomb => "설치 후 잠시 뒤 폭발하여\n주변 적에게 범위 피해를 줍니다.",
        PlayerSkillId.PlayerTurret => "플레이어 주변 8방향에 설치해\n접근하는 적을 자동 공격합니다.",
        PlayerSkillId.EnergyBarrier => "두 본체를 연결해 장벽을 만듭니다.\n적의 이동을 막고 아군 탄환은 통과합니다.",
        PlayerSkillId.LureBeacon => "주변 적을 끌어들이는\n유도 비콘을 설치합니다.",
        PlayerSkillId.Portal => "한 쌍의 포털을 설치합니다.\nR 이동 · F 회수",
        PlayerSkillId.DualWield => $"{(owner != null ? owner.UpgradeDurationValue(skill) : 8):0.#}초 동안 총을 두 개 듭니다.\n탄약 소비 유지 · 대기 20초",
        _ => ""
    };

    public static string UpgradeStats(PlayerSkillId skill, PlayerSkillController owner)
    {
        var research = owner.Research;
        int level = research != null ? research.Level(skill) : 0;
        if (level == 0) return "해금 후 강화할 수 있습니다.";
        bool maximum = level >= PlayerSkillResearch.MaximumLevel;
        float ratio = maximum ? 1 : PlayerSkillResearch.MultiplierAtLevel(level + 1) / research.Multiplier(skill);
        string Row(string label, float value, string suffix = "") => maximum
            ? $"{label}  {value:0.#}{suffix}"
            : $"{label}  {value:0.#}{suffix}  >  {value * ratio:0.#}{suffix}";
        string text = Row(owner.UpgradeStatName(skill), owner.UpgradeStatValue(skill), skill == PlayerSkillId.DualWield ? "%" : "");
        if (maximum) text += "  ·  최고 레벨";
        float health = owner.UpgradeHealthValue(skill), duration = owner.UpgradeDurationValue(skill);
        if (health > 0) text += "\n" + Row("체력", health);
        if (skill == PlayerSkillId.Portal) text += "\n" + Row("보호량", owner.PortalShieldCapacity, " HP");
        if (duration > 0) text += "\n" + Row(skill == PlayerSkillId.Portal ? "보호 시간" : "지속시간", duration, "초");
        return text;
    }

    public static string Description(PlayerSkillId skill, PlayerSkillController owner)
    {
        return (skill switch
        {
            PlayerSkillId.Bomb => "플레이어 주변 원 안의 커서 위치에 시한폭탄 설치\n왼쪽 클릭 확정 · 우클릭 / Esc / 같은 키로 취소\n캐릭터·장애물과 겹치면 설치 불가\n잠시 후 주변 적에게 범위 피해\n보유 수량: " + owner.BombCount,
            PlayerSkillId.PlayerTurret => $"스킬 사용 후 1·2·3번 포탑 상태에서 선택\n플레이어 주변 8칸 중 마우스 방향에 설치\n왼쪽 클릭 확정 · 우클릭 / Esc / 같은 키로 취소\n설치 간격 3초 · 근처에서 F키로 회수\n체력 {owner.UpgradeHealthValue(skill):0.#} · R키 유지 수리 · 전력 사용 없음\n회수·재설치 시 해당 슬롯의 체력 비율 유지\n파괴된 슬롯은 30초 후 최대 체력으로 복구",
            PlayerSkillId.EnergyBarrier => $"플레이어 주변 반경 12 안에 첫 본체 설치\n두 번째는 첫 본체 중심 반경 {owner.UpgradeStatValue(skill):0.#} 안에서 클릭\n두 번째 설치 실패 시 첫 위치 유지 · 재시도 가능\n캐릭터·장애물 겹침 불가 · 아군 탄환 통과\n체력 {owner.UpgradeHealthValue(skill):0.#} · 유지 {owner.UpgradeDurationValue(skill):0.#}초\n완성에만 1개 소모 · 18초마다 1개 충전\n우클릭 / Esc / 같은 키로 전체 취소",
            PlayerSkillId.LureBeacon => $"플레이어 주변 원 안의 커서 위치에 비콘 설치\n왼쪽 클릭 확정 · 우클릭 / Esc / 같은 키로 취소\n유인 반경 {owner.UpgradeStatValue(skill):0.#} · 유지 {owner.UpgradeDurationValue(skill):0.#}초\n체력 {owner.UpgradeHealthValue(skill):0.#} · 재사용 14초",
            PlayerSkillId.DualWield => $"{owner.UpgradeDurationValue(skill):0.#}초 동안 같은 총을 양손에 장착\n기본총 · 냉각총 · 스나이퍼에 적용\n두 총 모두 마우스 커서를 조준\n탄약 소비와 공격 간격 유지 · 재사용 20초\n스패너는 적용하지 않음",
            PlayerSkillId.Portal => $"한 쌍의 포털 A/B 설치\n가까이서 R: 반대편 포털로 이동\nF: 가까운 설치물 회수 · 재설치 가능\n이동 후 보호막: {owner.PortalShieldCapacity:0.#} HP · {owner.UpgradeDurationValue(skill):0.#}초\n보호막 생성 대기 {owner.PortalShieldCooldown:0.#}초 · 중첩 없음",
            _ => ""
        });
    }
}
