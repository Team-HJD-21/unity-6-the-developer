// 정적 구역 하나의 전투 성과·확보 유지·열세·이탈 유예를 게임 절대 시각으로 판단합니다.
// Unity/Scene을 읽지 않고 관측값만 받으며, 여러 참가자가 있어도 시간은 한 번만 흐릅니다.
using System;

namespace TeamHJD.Game.Domain
{
    public enum StaticRegionCaptureReason { Locked, NoParticipant, NeedsCombat, EnemyPressure, Holding, Secured, Lost, Abandoned }

    public sealed class StaticRegionCapture
    {
        public int RegionId { get; }
        public double HoldSeconds { get; private set; }
        public double TeamDamage { get; private set; }
        public double AbsenceSeconds { get; private set; }
        public double SuperioritySeconds { get; private set; }
        public bool IsSecured { get; private set; }
        public int LivingParticipants { get; private set; }
        public int LivingEnemies { get; private set; }
        public StaticRegionCaptureReason Reason { get; private set; } = StaticRegionCaptureReason.Locked;
        public StaticRegionCaptureSnapshot Snapshot => new StaticRegionCaptureSnapshot(this);
        private double? _lastTime;
        private bool _wasHolding, _wasAbsent, _wasUnderPressure;

        public StaticRegionCapture(int regionId)
        {
            if (regionId <= 0) throw new ArgumentOutOfRangeException(nameof(regionId));
            RegionId = regionId;
        }

        /// <summary>실제 Player/Turret 피해만 Scene 관측자가 보고합니다. 제거/디버그 Kill은 전투 성과가 아닙니다.</summary>
        public void RecordDamage(double damage)
        {
            if (double.IsNaN(damage) || double.IsInfinity(damage) || damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));
            TeamDamage = Math.Min(double.MaxValue, TeamDamage + damage);
        }

        /// <summary>상태가 바뀌면 true입니다. 확보/상실 셀 확정과 다음 구역 해금은 소유자가 한 번만 수행합니다.</summary>
        public bool Tick(double gameTime, bool unlocked, int livingParticipants, int livingEnemies,
            StaticRegionCaptureSettings settings)
        {
            if (double.IsNaN(gameTime) || double.IsInfinity(gameTime) || gameTime < 0)
                throw new ArgumentOutOfRangeException(nameof(gameTime));
            if (livingParticipants < 0 || livingEnemies < 0) throw new ArgumentOutOfRangeException(nameof(livingParticipants));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            LivingParticipants = livingParticipants; LivingEnemies = livingEnemies;
            double delta = _lastTime.HasValue ? Math.Max(0, gameTime - _lastTime.Value) : 0;
            _lastTime = gameTime;
            // 새 표본에서 처음 만족한 조건으로 이전의 미확인 시간까지 소급해서 세지 않습니다.
            double holdDelta = _wasHolding ? delta : 0;
            double absenceDelta = _wasAbsent ? delta : 0;
            double pressureDelta = _wasUnderPressure ? delta : 0;
            bool pressure = livingEnemies > (long)settings.EnemiesPerParticipant * livingParticipants;
            _wasHolding = unlocked && livingParticipants > 0 && TeamDamage >= settings.MinimumTeamDamage && !pressure;
            _wasAbsent = livingParticipants == 0;
            _wasUnderPressure = pressure;
            if (!unlocked) { Reason = StaticRegionCaptureReason.Locked; return false; }
            bool previous = IsSecured;
            if (IsSecured)
            {
                SuperioritySeconds = pressure ? SuperioritySeconds + pressureDelta : 0;
                if (SuperioritySeconds >= settings.LossSeconds)
                {
                    IsSecured = false;
                    HoldSeconds = TeamDamage = AbsenceSeconds = SuperioritySeconds = 0;
                    Reason = StaticRegionCaptureReason.Lost;
                }
                else Reason = pressure ? StaticRegionCaptureReason.EnemyPressure : StaticRegionCaptureReason.Secured;
                return previous != IsSecured;
            }

            AbsenceSeconds = livingParticipants == 0 ? AbsenceSeconds + absenceDelta : 0;
            if (AbsenceSeconds >= settings.AbandonSeconds)
            {
                HoldSeconds = TeamDamage = 0;
                Reason = StaticRegionCaptureReason.Abandoned;
            }
            else if (livingParticipants == 0) Reason = StaticRegionCaptureReason.NoParticipant;
            else if (TeamDamage < settings.MinimumTeamDamage) Reason = StaticRegionCaptureReason.NeedsCombat;
            else if (pressure) Reason = StaticRegionCaptureReason.EnemyPressure;
            else
            {
                HoldSeconds = Math.Min(settings.HoldSeconds, HoldSeconds + holdDelta);
                IsSecured = HoldSeconds >= settings.HoldSeconds;
                Reason = IsSecured ? StaticRegionCaptureReason.Secured : StaticRegionCaptureReason.Holding;
            }
            return previous != IsSecured;
        }

        /// <summary>검증한 체크포인트 진행을 적용합니다. 복원 직후 첫 표본에서는 과거 시간차를 누적하지 않습니다.</summary>
        public void Restore(double hold, double damage, double absence, double superiority, bool secured)
        {
            foreach (double value in new[] { hold, damage, absence, superiority })
                if (value < 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(hold));
            HoldSeconds = hold; TeamDamage = damage; AbsenceSeconds = absence; SuperioritySeconds = superiority;
            IsSecured = secured; _lastTime = null;
            _wasHolding = _wasAbsent = _wasUnderPressure = false;
            Reason = secured ? StaticRegionCaptureReason.Secured : StaticRegionCaptureReason.NeedsCombat;
        }
    }
}
