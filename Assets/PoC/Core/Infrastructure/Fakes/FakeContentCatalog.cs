// Resolves preconfigured match configs for tests without loading Unity assets.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Infrastructure.Fakes
{
    public sealed class FakeContentCatalog : IContentCatalog
    {
        private readonly Dictionary<MatchId, MatchConfig> _configs = new Dictionary<MatchId, MatchConfig>();

        public FakeContentCatalog(IEnumerable<MatchConfig> configs = null)
        {
            if (configs == null) return;
            foreach (var config in configs) _configs[config.MatchId] = config;
        }

        public MatchConfig ResolveMatchConfig(MatchSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (!_configs.TryGetValue(selection.MatchId, out var config))
                throw new KeyNotFoundException($"No fake match config is registered for '{selection.MatchId}'.");
            return config;
        }
    }
}
