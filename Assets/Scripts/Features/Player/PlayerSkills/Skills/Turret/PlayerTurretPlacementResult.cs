/// <summary>설치 판정 결과입니다. 화면에 표시할 문구는 UI에서 결정합니다.</summary>
public enum PlayerTurretPlacementResult
{
    Allowed,
    GroundUnavailable,
    InvalidFootprint,
    OutsideGround,
    OverlapsTurret,
    BlockedByObstacle
}
