// 한 Match의 서버 actor 제공자를 명시적으로 등록하고 검증 시점에 최신 상태를 읽습니다.
// 등록 lease가 끝나면 조회를 차단하며 옛 lease가 새 actor 등록을 제거할 수 없습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class PlayerInteractionRegistry : IPlayerInteractionSource, IDisposable
    {
        private readonly MatchId _matchId;
        private readonly HashSet<PlayerId> _participants;
        private readonly Dictionary<PlayerId, Registration> _sources = new Dictionary<PlayerId, Registration>();
        private bool _disposed;
        private sealed class Registration : IDisposable
        {
            public readonly PlayerId Player;
            public readonly Func<PlayerInteractionSnapshot?> Capture;
            private PlayerInteractionRegistry _owner;
            public Registration(PlayerInteractionRegistry owner, PlayerId player, Func<PlayerInteractionSnapshot?> capture)
            { _owner = owner; Player = player; Capture = capture; }
            public void Dispose()
            {
                var owner = _owner; _owner = null;
                if (owner != null && owner._sources.TryGetValue(Player, out var current) && ReferenceEquals(current, this))
                    owner._sources.Remove(Player);
            }
        }
        public PlayerInteractionRegistry(MatchId matchId, IEnumerable<PlayerId> participants)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match is required.", nameof(matchId));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            _matchId = matchId; _participants = new HashSet<PlayerId>();
            foreach (PlayerId player in participants)
                if (player.IsEmpty || !_participants.Add(player) || _participants.Count > 16)
                    throw new ArgumentException("Provide 1-16 unique Match participants.", nameof(participants));
            if (_participants.Count == 0) throw new ArgumentException("A participant is required.", nameof(participants));
        }
        public IDisposable Register(PlayerId playerId, Func<PlayerInteractionSnapshot?> capture)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PlayerInteractionRegistry));
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            if (!_participants.Contains(playerId)) throw new ArgumentException("The actor is not a participant.", nameof(playerId));
            if (_sources.ContainsKey(playerId)) throw new InvalidOperationException("Release the previous actor registration first.");
            var registration = new Registration(this, playerId, capture); _sources.Add(playerId, registration); return registration;
        }
        public bool TryCapture(MatchId matchId, PlayerId playerId, out PlayerInteractionSnapshot snapshot)
        {
            snapshot = default;
            if (_disposed || matchId != _matchId || !_sources.TryGetValue(playerId, out var registration)) return false;
            PlayerInteractionSnapshot? value = registration.Capture();
            if (_disposed || !value.HasValue || !_sources.TryGetValue(playerId, out var current) || !ReferenceEquals(current, registration)) return false;
            snapshot = value.Value; return true;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _sources.Clear(); _participants.Clear(); }
    }
}
