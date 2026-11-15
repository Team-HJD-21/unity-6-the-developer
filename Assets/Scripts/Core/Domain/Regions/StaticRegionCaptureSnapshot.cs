// HUD/디버거가 읽는 정적 구역의 불변 진행 표본입니다.
// 실제 판정기를 노출하지 않으므로 외부에서 시간을 진행하거나 성과를 변경할 수 없습니다.
namespace TeamHJD.Game.Domain
{
    public sealed class StaticRegionCaptureSnapshot
    {
        public int RegionId { get; }
        public double HoldSeconds { get; }
        public double TeamDamage { get; }
        public double AbsenceSeconds { get; }
        public double SuperioritySeconds { get; }
        public bool IsSecured { get; }
        public int LivingParticipants { get; }
        public int LivingEnemies { get; }
        public StaticRegionCaptureReason Reason { get; }

        internal StaticRegionCaptureSnapshot(StaticRegionCapture state)
        {
            RegionId = state.RegionId; HoldSeconds = state.HoldSeconds; TeamDamage = state.TeamDamage;
            AbsenceSeconds = state.AbsenceSeconds; SuperioritySeconds = state.SuperioritySeconds;
            IsSecured = state.IsSecured; LivingParticipants = state.LivingParticipants; LivingEnemies = state.LivingEnemies;
            Reason = state.Reason;
        }
    }
}
