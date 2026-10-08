/// <summary>스킬 이름과 설명문을 모아 둔 표시용 카탈로그입니다.</summary>
public static class PlayerSkillDescriptions
{
    public static string Name(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => "시한폭탄",
        PlayerSkillId.TemporaryTurret => "보조 포탑",
        PlayerSkillId.EnergyBarrier => "방벽 투사기",
        PlayerSkillId.LureBeacon => "유도 비콘",
        _ => "미선택"
    };

    public static string Description(PlayerSkillId skill, PlayerSkillController owner)
    {
        return (skill switch
        {
            PlayerSkillId.Bomb => "발밑에 시한폭탄을 설치합니다.\n잠시 후 주변 적에게 범위 피해\n보유 수량: " + owner.BombCount,
            PlayerSkillId.TemporaryTurret => "보조 포탑을 설치합니다.\n체력 100 · 최대 3개 · 설치 간격 3초\n근처에서 F키로 회수\n손상 비율에 따라 최대 30초 재충전\n파괴 시 30초 후 보유 수량 복구",
            PlayerSkillId.EnergyBarrier => "발밑에 펄스 설치 → 같은 키로 연결\n연결 거리 1~18 · 지형 통과 불가\n몬스터 이동 차단 · 아군 탄환 통과\n보유 3개 · 18초마다 1개 충전\n우클릭으로 미완성 펄스 취소",
            PlayerSkillId.LureBeacon => "발밑에 주변 적을 유인하는 비콘 설치\n유인 반경 12 · 유지 8초\n체력 80 · 재사용 14초",
            _ => ""
        });
    }
}
