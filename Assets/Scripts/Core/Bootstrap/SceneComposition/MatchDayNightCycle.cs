// Scene의 낮밤 source를 Host 공용 게임 시계와 예약에 연결합니다.
// Client는 확정 시간대만 읽고, Weather 선택·효과는 기존 Weather에 맡깁니다.
// Scene 조립부가 Weather 옆에 확보하고 Match 해제 시 예약을 취소합니다.
using System;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    [DisallowMultipleComponent]
    public sealed partial class MatchDayNightCycle : MonoBehaviour, IMatchDayNightCycle, IMatchScheduledEnvironmentFeature,
        IMatchDayNightDebugControl
    {
        [SerializeField, Min(.01f)]
        private double daySeconds = 300;
        [SerializeField, Min(.01f)]
        private double nightSeconds = 300;
        // Apply에서 참조용
        public double DaySeconds => daySeconds;
        public double NightSeconds => nightSeconds;
        private DayNightCycle _cycle;
        private IMatchClock _clock;
        private IMatchScheduler _scheduler;
        private Func<bool> _mayWrite;
        private CancellationToken _gameplay;
        private MatchId _match;
        private IDisposable _timer;
        private bool _bound, _authority;
        private long _transition;
        public event Action Changed;
        public event Action PhaseAdvanced;
        public DayNightPhase Phase { get; private set; }
        public DefinitionId TimeOfDayId => !_bound ? default : new DefinitionId(Phase == DayNightPhase.Day ? "day" : "night");
        public bool IsAuthority => _bound && _authority;
        public double RemainingSeconds => IsAuthority ? _cycle.GetRemainingSeconds(_clock.ElapsedSeconds) : double.NaN;
        public bool CanRestartCurrentPhase => MayAdvance;

        /// <summary>Host 디버그 HUD가 사용합니다. 현재 구간을 재시작하고 기존 경계 예약을 교체하며, 날씨 자체는 선택하지 않습니다.</summary>
        public bool TryRestartCurrentPhase()
        {
            if (!MayAdvance) return false;
            double now = _clock.ElapsedSeconds;
            _cycle = new DayNightCycle(_cycle.DaySeconds, _cycle.NightSeconds, now, _cycle.GetPhase(now));
            ApplyCurrentTime(false);
            ScheduleNext();
            Changed?.Invoke();
            return true;
        }
        // Host 디버그 HUD가 사용 -> 낮밤 전환
        public bool TryAdvancePhase()
        {
            if (!MayAdvance) return false;
            double now = _clock.ElapsedSeconds;
            DayNightPhase next = Phase == DayNightPhase.Day ? DayNightPhase.Night : DayNightPhase.Day;
            _cycle = new DayNightCycle(_cycle.DaySeconds, _cycle.NightSeconds, now, next);
            ApplyCurrentTime(false);
            ScheduleNext();
            try
            {
                PhaseAdvanced?.Invoke();
            }
            finally
            {
                Changed?.Invoke();
            }
            return true;
        }

        public void RequireMatchScope()
        {
        }

        public IDisposable BindAuthority(MatchId match, IMatchClock clock, int seed, Func<bool> canWrite, CancellationToken gameplay) => BindAuthority(match, clock, null, seed, canWrite, gameplay);
        public IDisposable BindAuthority(MatchId match, IMatchClock clock, IMatchScheduler scheduler, int seed, Func<bool> canWrite, CancellationToken gameplay)
        {
            if (clock == null || canWrite == null)
                throw new ArgumentNullException(clock == null ? nameof(clock) : nameof(canWrite));
            Begin(match, true);
            _clock = clock;
            _scheduler = scheduler;
            _mayWrite = canWrite;
            _gameplay = gameplay;
            try
            {
                _cycle = new DayNightCycle(daySeconds, nightSeconds);
                ApplyCurrentTime(false);
                ScheduleNext();
                return new Scope(this);
            }
            catch
            {
                Release();
                throw;
            }
        }

        public IDisposable BindReplica(MatchId match)
        {
            Begin(match, false);
            return new Scope(this);
        }

        public void ApplyReplica(MatchEnvironmentSnapshot snapshot)
        {
            if (!_bound || _authority || snapshot == null || snapshot.MatchId != _match || !snapshot.HasTimeOfDay)
                return;
            if (snapshot.TimeOfDayId.Value != "day" && snapshot.TimeOfDayId.Value != "night")
                throw new ArgumentException("Unknown day/night phase.", nameof(snapshot));
            Phase = snapshot.TimeOfDayId.Value == "day" ? DayNightPhase.Day : DayNightPhase.Night;
            Changed?.Invoke();
        }

        private void Begin(MatchId match, bool authority)
        {
            if (_bound || match.IsEmpty)
                throw new InvalidOperationException("Bind one current Match to day/night.");
            _match = match;
            _bound = true;
            _authority = authority;
        }

        private void OnEnable()
        {
            if (!MayAdvance)
                return;
            ApplyCurrentTime(true);
            ScheduleNext();
        }

        private void Update()
        {
            if (MayAdvance && _scheduler == null)
                ApplyCurrentTime(true);
        }

        private bool MayAdvance => IsAuthority && isActiveAndEnabled && !_gameplay.IsCancellationRequested && _mayWrite();

        private void ScheduleNext()
        {
            _timer?.Dispose();
            _timer = null;
            if (!IsAuthority || _scheduler == null || _gameplay.IsCancellationRequested)
                return;
            _timer = _scheduler.Schedule(_cycle.GetRemainingSeconds(_clock.ElapsedSeconds), _ =>
            {
                try
                {
                    if (MayAdvance)
                        ApplyCurrentTime(true);
                }
                finally
                {
                    ScheduleNext();
                }
            }, _gameplay);
        }

        private void ApplyCurrentTime(bool notifyAdvance)
        {
            long next = _cycle.GetTransitionIndex(_clock.ElapsedSeconds);
            bool advanced = next != _transition;
            _transition = next;
            Phase = _cycle.GetPhase(_clock.ElapsedSeconds);
            if (!notifyAdvance || !advanced)
                return;
            // Weather가 먼저 다음 값을 선택한 뒤 환경 projection이 두 축을 함께 읽습니다.
            try
            {
                PhaseAdvanced?.Invoke();
            }
            finally
            {
                Changed?.Invoke();
            }
        }

        private void Release()
        {
            _timer?.Dispose();
            _timer = null;
            _bound = false;
            _authority = false;
            _match = default;
            _clock = null;
            _scheduler = null;
            _mayWrite = null;
            _gameplay = default;
        }

        private void OnDestroy() => Release();
        private sealed class Scope : IDisposable
        {
            private MatchDayNightCycle _owner;
            public Scope(MatchDayNightCycle owner) => _owner = owner;
            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                if (owner != null)
                    owner.Release();
            }
        }
    }
}
