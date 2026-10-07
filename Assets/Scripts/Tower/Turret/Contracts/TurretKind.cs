namespace TeamHJD.Game.Turrets.Contracts
{
    /// <summary>레벨이나 Stage와 무관한 터렛 계열. 미이식 종류는 Unknown으로 분류한다.</summary>
    public enum TurretKind
    {
        Unknown = 0,
        Canon = 1,
        Missile = 2,
        Laser = 3,
        Tesla = 4
    }
}
