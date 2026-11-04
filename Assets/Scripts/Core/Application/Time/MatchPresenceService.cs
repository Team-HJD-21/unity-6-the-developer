// 현재 참가자 전원의 우주선 입장을 확인해 필드 정지 여부를 결정합니다.
// 위치 사실은 Host의 실제 actor 바인딩이 제공하며 Client의 신고를 직접 신뢰하지 않습니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchPresenceService : IDisposable
    {
        private readonly Dictionary<PlayerId, MatchPlayerLocation> _locations = new();
        private readonly Dictionary<PlayerId, Registration> _actors = new();
        private readonly Func<bool> _mayWrite;
        private readonly Action<bool> _setPaused;
        private bool _disposed;
        public MatchId MatchId { get; }
        public IReadOnlyDictionary<PlayerId, MatchPlayerLocation> Locations { get; }
        public bool IsFieldPaused { get; private set; }
        public long Revision { get; private set; }

        public MatchPresenceService(MatchConfig config, Func<bool> mayWrite, Action<bool> setPaused)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            MatchId = config.MatchId;
            _mayWrite = mayWrite ?? throw new ArgumentNullException(nameof(mayWrite));
            _setPaused = setPaused ?? throw new ArgumentNullException(nameof(setPaused));
            foreach (var player in config.Players) _locations.Add(player.PlayerId, MatchPlayerLocation.Field);
            if (_locations.Count == 0) throw new ArgumentException("A presence scope requires participants.", nameof(config));
            Locations = new ReadOnlyDictionary<PlayerId, MatchPlayerLocation>(_locations);
        }

        /// <summary>Host의 Player 생성자가 현재 참가자의 실제 actor에 대한 위치 보고권을 등록합니다.</summary>
        /// <returns>actor 교체·제거 때 해제할 등록입니다. 미등록 참가자는 필드에 있는 것으로 처리합니다.</returns>
        public Registration RegisterActor(PlayerId playerId)
        {
            RequireAuthority();
            if (!_locations.ContainsKey(playerId)) throw new ArgumentException("The player is not a Match participant.", nameof(playerId));
            if (_actors.ContainsKey(playerId)) throw new InvalidOperationException("This participant already has a presence actor.");
            var registration = new Registration(this, playerId);
            _actors.Add(playerId, registration);
            return registration;
        }

        private void RequireAuthority()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchPresenceService));
            if (!_mayWrite()) throw new InvalidOperationException("Only the running Match authority may report presence.");
        }
        private void SetLocation(Registration registration, MatchPlayerLocation location)
        {
            RequireAuthority();
            if (!Enum.IsDefined(typeof(MatchPlayerLocation), location)) throw new ArgumentOutOfRangeException(nameof(location));
            if (!_actors.TryGetValue(registration.PlayerId, out var current) || current != registration)
                throw new InvalidOperationException("The presence actor is no longer registered.");
            if (_locations[registration.PlayerId] == location) return;
            Revision = checked(Revision + 1);
            _locations[registration.PlayerId] = location;
            Refresh();
        }
        private void Release(Registration registration)
        {
            if (_disposed || !_actors.TryGetValue(registration.PlayerId, out var current) || current != registration) return;
            _actors.Remove(registration.PlayerId);
            if (_locations[registration.PlayerId] != MatchPlayerLocation.Field) Revision = checked(Revision + 1);
            _locations[registration.PlayerId] = MatchPlayerLocation.Field;
            if (_mayWrite()) Refresh();
        }
        private void Refresh()
        {
            bool paused = true;
            foreach (var location in _locations.Values) paused &= location == MatchPlayerLocation.Spaceship;
            if (paused == IsFieldPaused) return;
            _setPaused(paused);
            IsFieldPaused = paused;
        }
        /// <summary>Scene이 Match 정리 시 호출합니다. 종료된 시계를 다시 재개하지 않습니다.</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _actors.Clear(); }

        public sealed class Registration : IDisposable
        {
            private MatchPresenceService _owner;
            public PlayerId PlayerId { get; }
            public bool CanReport => _owner != null && !_owner._disposed && _owner._mayWrite();
            internal Registration(MatchPresenceService owner, PlayerId id) { _owner = owner; PlayerId = id; }
            /// <summary>Host의 출입 어댑터가 이동 성공 후 확정된 위치를 기록합니다.</summary>
            public void Report(MatchPlayerLocation location)
            { if (_owner == null) throw new ObjectDisposedException(nameof(Registration)); _owner.SetLocation(this, location); }
            public void Dispose() { var owner = _owner; _owner = null; owner?.Release(this); }
        }
    }
}
