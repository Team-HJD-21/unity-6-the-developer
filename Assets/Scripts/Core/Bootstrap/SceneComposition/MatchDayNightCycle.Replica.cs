// Host의 현재 낮밤 구간 기준을 공용 게임 시계와 함께 Client에 표시합니다.
// 필드 전용 시계·deltaTime 누적·날씨 추첨을 사용하지 않으며 미준비/다른 epoch는 NaN입니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class MatchDayNightCycle
    {
        private IMatchTime _replicaClock;
        private DayNightCycle _replicaPhaseAnchor;
        private Guid _replicaEpoch;
        private long _replicaRevision;

        /// <summary>Scene 환경 projection이 현재 확정 구간을 읽습니다. Client의 다음 구간 예측에 쓰지 않습니다.</summary>
        internal DayNightCycle CapturePhaseAnchor()
        {
            if (!IsAuthority || _cycle == null) return null;
            // _transition은 마지막으로 Host가 확정한 구간입니다. 게시 직전 시각이 경계를 넘어도
            // 아직 확정하지 않은 다음 낮밤/날씨를 패킷에 섞지 않습니다.
            double start = _cycle.EpochSeconds + (_transition / 2) * (_cycle.DaySeconds + _cycle.NightSeconds);
            if ((_transition & 1) != 0)
                start += _cycle.InitialPhase == DayNightPhase.Day ? _cycle.DaySeconds : _cycle.NightSeconds;
            return new DayNightCycle(_cycle.DaySeconds, _cycle.NightSeconds, start, Phase);
        }

        /// <summary>환경 replica의 수명만 연결합니다. 실제 잔여시간은 같은 Match의 공용 clock 수신 뒤 제공됩니다.</summary>
        public IDisposable BindReplica(MatchId match)
        {
            Begin(match, false);
            return new Scope(this);
        }

        /// <summary>검증된 환경 replica가 호출합니다. 수신은 표시만 바꾸며 PhaseAdvanced를 발생시키지 않습니다.</summary>
        public void ApplyReplica(MatchEnvironmentSnapshot snapshot)
        {
            if (!_bound || _authority || snapshot == null || snapshot.MatchId != _match ||
                !snapshot.HasTimeOfDay || snapshot.Revision <= _replicaRevision) return;
            if (snapshot.TimeOfDayId.Value != "day" && snapshot.TimeOfDayId.Value != "night")
                throw new ArgumentException("Unknown day/night phase.", nameof(snapshot));
            Phase = snapshot.TimeOfDayId.Value == "day" ? DayNightPhase.Day : DayNightPhase.Night;
            _replicaPhaseAnchor = snapshot.PhaseAnchor;
            _replicaEpoch = snapshot.Epoch;
            _replicaRevision = snapshot.Revision;
            Changed?.Invoke();
        }

        /// <summary>Client Scene 조립부만 사용합니다. 필드 정지 시계가 아닌 권위 공용 게임 시계를 전달합니다.</summary>
        internal void SetReplicaClock(IMatchTime time)
        {
            if (time != null && (_authority || (_bound && time.Snapshot.MatchId != _match)))
                throw new InvalidOperationException("Bind only the current Client Match clock to day/night.");
            _replicaClock = time;
        }

        private double ReadReplicaRemainingSeconds()
        {
            if (!_bound || _replicaPhaseAnchor == null || _replicaClock == null || !_replicaClock.IsAvailable ||
                _replicaClock.Snapshot.MatchId != _match || _replicaClock.Snapshot.Epoch != _replicaEpoch)
                return double.NaN;
            double now = _replicaClock.ElapsedSeconds;
            if (!double.IsFinite(now) || now < _replicaPhaseAnchor.EpochSeconds) return double.NaN;
            // 통신 지연으로 경계를 먼저 지나면 0에서 Host 확정을 기다립니다. 날씨를 선행 전환하지 않습니다.
            return _replicaPhaseAnchor.GetTransitionIndex(now) == 0 ? _replicaPhaseAnchor.GetRemainingSeconds(now) : 0;
        }

        private void ClearReplicaState()
        {
            _replicaClock = null;
            _replicaPhaseAnchor = null;
            _replicaEpoch = Guid.Empty;
            _replicaRevision = 0;
        }
    }
}
