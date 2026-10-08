// 조회 시점 수치 표본입니다. 생존 수 차분을 생성량 또는 처치량으로 사용하지 않습니다.
using System;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugSample
    {
        public double GameSeconds, RealSeconds, TerritoryArea, FrontlineLength;
        public long Revision;
        public int AliveEnemies, UnattributedEnemies, TurretCount, ActivatedTurrets, OperationalTurrets;
        public int AvailablePower, MaximumPower;
        public float TimeScale;
    }
}
