// Host의 실제 Player actor와 Match의 위치 등록을 연결합니다.
// 출입 Feature는 이동 성공 후 Report를 호출하며 Client 전용 actor는 보고권을 갖지 않습니다.
using System;
using System.Threading;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    [DisallowMultipleComponent]
    public sealed class PlayerMatchPresence : MonoBehaviour
    {
        private MatchPresenceService.Registration _registration;
        public bool CanReport => _registration?.CanReport == true;
        public IMatchTime TimeSource { get; private set; }
        public CancellationToken GameplayCancellation { get; private set; }
        public MatchPlayerLocation Location { get; private set; }
        public bool CanSimulateField => !GameplayCancellation.IsCancellationRequested && (TimeSource == null || (Location == MatchPlayerLocation.Field &&
            TimeSource.IsAvailable && !TimeSource.Snapshot.IsPaused && TimeSource.Snapshot.Rate > 0));
        public double FieldSeconds => TimeSource?.ElapsedSeconds ?? Time.timeAsDouble;
        public void Bind(MatchPresenceService.Registration registration, IMatchTime time, CancellationToken gameplay)
        {
            if (_registration != null) throw new InvalidOperationException("Presence is already bound.");
            Location = MatchPlayerLocation.Field;
            GameplayCancellation = gameplay;
            TimeSource = time ?? throw new ArgumentNullException(nameof(time));
            _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        }
        /// <summary>Host의 출입 어댑터가 실제 이동 성공 후 호출합니다. 네트워크 요청 API가 아닙니다.</summary>
        public void Report(MatchPlayerLocation location)
        {
            if (!CanReport) throw new InvalidOperationException("This actor has no current presence authority.");
            _registration.Report(location);
            Location = location;
        }
        public void Unbind(MatchPresenceService.Registration registration) { if (ReferenceEquals(_registration, registration)) Unbind(); }
        public void Unbind() { var registration = _registration; _registration = null; TimeSource = null; GameplayCancellation = default; registration?.Dispose(); }
        private void OnDestroy() => Unbind();
    }
}
