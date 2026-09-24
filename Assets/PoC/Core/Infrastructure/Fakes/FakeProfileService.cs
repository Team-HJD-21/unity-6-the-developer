// 테스트와 로컬 개발을 위한 메모리 기반 프로필 구현입니다.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Infrastructure.Fakes
{
    public sealed class FakeProfileService : IProfileService
    {
        private readonly Dictionary<PlayerId, PlayerProfileSnapshot> _profiles = new Dictionary<PlayerId, PlayerProfileSnapshot>();

        public FakeProfileService(IEnumerable<PlayerProfileSnapshot> profiles = null)
        {
            if (profiles == null) return;
            foreach (var profile in profiles)
            {
                if (profile == null) throw new ArgumentException("Profile entries cannot be null.", nameof(profiles));
                if (_profiles.ContainsKey(profile.PlayerId))
                    throw new ArgumentException($"Duplicate fake profile for '{profile.PlayerId}'.", nameof(profiles));
                _profiles.Add(profile.PlayerId, profile);
            }
        }

        public Task<PlayerProfileSnapshot> LoadAsync(PlayerId playerId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _profiles.TryGetValue(playerId, out var profile);
            return Task.FromResult(profile);
        }

        public Task SaveAsync(PlayerProfileSnapshot profile, CancellationToken cancellationToken)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            cancellationToken.ThrowIfCancellationRequested();
            _profiles[profile.PlayerId] = profile;
            return Task.CompletedTask;
        }
    }
}
