// Host Match의 논리 source와 등록 세대·취소 수명을 관리합니다. 실제 생성은 수행하지 않습니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class EncounterSpawnSourceRegistry : IEncounterSpawnSourceRegistry, IDisposable
    {
        public const int MaximumSources = 1024;
        private readonly MatchId _match;
        private readonly Func<bool> _mayWrite;
        private readonly CancellationToken _gameplay;
        private readonly Dictionary<DefinitionId, Lease> _sources = new Dictionary<DefinitionId, Lease>();
        private bool _disposed;
        public EncounterSpawnSourceRegistry(MatchId match, Func<bool> mayWrite, CancellationToken gameplay)
        {
            if (match.IsEmpty) throw new ArgumentException("A Match is required.");
            _match = match; _mayWrite = mayWrite ?? throw new ArgumentNullException(nameof(mayWrite)); _gameplay = gameplay;
        }
        private bool Active => !_disposed && !_gameplay.IsCancellationRequested && _mayWrite();
        public IEncounterSpawnSourceLease Register(MatchId match, DefinitionId id, SpawnArea area)
        {
            if (!Active || match != _match) throw new InvalidOperationException("Only the active Host Match can register a source.");
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (_sources.Count >= MaximumSources || _sources.ContainsKey(id)) throw new InvalidOperationException("Source capacity or duplicate ID.");
            var lease = new Lease(this, new SpawnSourceReference(match, id, Guid.NewGuid()), area, _gameplay);
            _sources.Add(id, lease); return lease;
        }
        public EncounterSpawnPlacement Resolve(EncounterSpawnOrigin origin)
        {
            if (!Active || origin == null) return null;
            if (origin.Kind == SpawnOriginKind.AuthoredPoint) return new EncounterSpawnPlacement(origin);
            if (origin.Kind == SpawnOriginKind.WorldPoint || origin.Kind == SpawnOriginKind.WorldArea)
                return new EncounterSpawnPlacement(origin, origin.Area);
            if (origin.Kind != SpawnOriginKind.DynamicSource || origin.Source.MatchId != _match ||
                !_sources.TryGetValue(origin.Source.SourceId, out var lease) ||
                lease.Reference.Generation != origin.Source.Generation || lease.Cancellation.IsCancellationRequested) return null;
            return new EncounterSpawnPlacement(origin, lease.Area);
        }
        public CancellationToken GetSourceCancellation(SpawnSourceReference source)
        {
            if (!Active || source == null || source.MatchId != _match ||
                !_sources.TryGetValue(source.SourceId, out var lease) || lease.Reference.Generation != source.Generation)
                return new CancellationToken(true);
            return lease.Cancellation;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            foreach (var lease in new List<Lease>(_sources.Values))
                try { lease.Dispose(); } catch (Exception error) { errors.Add(error); }
            _sources.Clear();
            if (errors.Count > 0) throw new AggregateException("Spawn sources released; cancellation observers failed.", errors);
        }
        private sealed class Lease : IEncounterSpawnSourceLease
        {
            private EncounterSpawnSourceRegistry _owner;
            private readonly CancellationTokenSource _cancellation;
            private readonly CancellationToken _token;
            public SpawnSourceReference Reference { get; }
            public CancellationToken Cancellation => _token;
            public SpawnArea Area { get; private set; }
            public Lease(EncounterSpawnSourceRegistry owner, SpawnSourceReference reference, SpawnArea area, CancellationToken gameplay)
            {
                _owner = owner; Reference = reference; Area = area;
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(gameplay); _token = _cancellation.Token;
            }
            public bool TryUpdate(SpawnArea area)
            {
                if (_owner == null || !_owner.Active || area == null) return false;
                Area = area; return true;
            }
            public void Dispose()
            {
                var owner = _owner; if (owner == null) return; _owner = null;
                owner._sources.Remove(Reference.SourceId);
                try { _cancellation.Cancel(); } finally { _cancellation.Dispose(); }
            }
        }
    }
}
