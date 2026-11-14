// 공용 Match 시각에서 기존 Field가 보류된 구간만 빼서 투영합니다.
// 새 SDK/NGO 시계를 만들지 않으며 보스전 중 기존 적·터렛·투사체의 deadline도 멈추게 합니다.
// Player와 보스는 공용 시간을 사용하고 필드 Feature와 정규 생산만 이 시각을 읽습니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class FieldSimulationClock : IMatchTime
    {
        private readonly IMatchTime _source;
        private Guid _epoch;
        private double _offset, _heldAt;
        private bool _held;
        private long _revision;
        public bool IsAvailable => _source.IsAvailable;
        public bool IsHeld { get { RefreshEpoch(); return _held; } }
        public double ElapsedSeconds
        {
            get { RefreshEpoch(); return Math.Max(0, (_held ? _heldAt : _source.ElapsedSeconds) - _offset); }
        }
        public MatchTimeSnapshot Snapshot
        {
            get
            {
                RefreshEpoch(); var source = _source.Snapshot;
                // 공용 anchor는 마지막 정지/배속 변경 시각일 수 있습니다. 현재 원본 시각으로 다시 대응시킵니다.
                double sourceNow = source.SourceSecondsAtAnchor;
                if (!source.IsPaused && source.Rate > 0)
                    sourceNow += Math.Max(0, _source.ElapsedSeconds - source.GameSecondsAtAnchor) / source.Rate;
                return new MatchTimeSnapshot(source.MatchId, source.Epoch, checked(source.Revision + _revision),
                    ElapsedSeconds, sourceNow, source.Rate, source.IsPaused || _held, source.IsAvailable);
            }
        }
        public FieldSimulationClock(IMatchTime source)
        { _source = source ?? throw new ArgumentNullException(nameof(source)); _epoch = source.Snapshot.Epoch; }

        /// <summary>Match 단계 소유자만 호출합니다. 공용 Player/날씨 시각은 정지하지 않습니다.</summary>
        public void SetHeld(bool held)
        {
            RefreshEpoch();
            if (_held == held) return;
            double now = _source.ElapsedSeconds;
            if (held) _heldAt = now;
            else _offset += Math.Max(0, now - _heldAt);
            _held = held; _revision = checked(_revision + 1);
        }

        /// <summary>월드 복원 조립부가 공용 시계 복원 뒤 Field의 경과 시각·보류를 복원합니다.</summary>
        public void Restore(double fieldSeconds, bool held)
        {
            if (!double.IsFinite(fieldSeconds) || fieldSeconds < 0 || fieldSeconds > _source.ElapsedSeconds + .000001)
                throw new ArgumentOutOfRangeException(nameof(fieldSeconds));
            _epoch = _source.Snapshot.Epoch;
            _heldAt = _source.ElapsedSeconds; _offset = Math.Max(0, _heldAt - fieldSeconds);
            _held = held; _revision = checked(_revision + 1);
        }

        private void RefreshEpoch()
        {
            Guid epoch = _source.Snapshot.Epoch;
            if (_epoch == epoch) return;
            // Field writer가 뒤이어 저장 상태를 적용합니다. 이전 실행 세대의 offset은 전달하지 않습니다.
            _epoch = epoch; _offset = 0; _heldAt = 0; _held = false;
            _revision = checked(_revision + 1); // Client가 복원 전 anchor보다 오래된 revision으로 거부하지 않게 합니다.
        }
    }
}
