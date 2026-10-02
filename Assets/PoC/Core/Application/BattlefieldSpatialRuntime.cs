// Match가 소유하는 공간 분석 결과의 생성·조회·폐기 경계입니다.

using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class BattlefieldSpatialRuntime : IDisposable
    {
        private readonly MatchId _matchId;
        private readonly BattlefieldTopologyBuilder _topologyBuilder = new BattlefieldTopologyBuilder();
        private readonly BattlefieldGridBuilder _gridBuilder = new BattlefieldGridBuilder();
        private BattlefieldSpatialInput _staticInput;
        private BattlefieldDynamicSpatialInput _dynamicInput;
        private BattlefieldGridConfiguration _gridConfiguration;
        private BattlefieldSpatialSnapshot _geometry;
        private BattlefieldSpatialSnapshot _snapshot;
        private long _revision;
        private bool _isDisposed;

        public BattlefieldSpatialSnapshot Snapshot
        {
            get
            {
                ThrowIfDisposed();
                return _snapshot;
            }
        }

        public BattlefieldSpatialRuntime(
            MatchId matchId,
            BattlefieldSpatialInput staticInput,
            BattlefieldDynamicSpatialInput dynamicInput,
            BattlefieldGridConfiguration gridConfiguration)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Battlefield runtime requires a valid match ID.", nameof(matchId));
            _matchId = matchId;
            _staticInput = staticInput ?? throw new ArgumentNullException(nameof(staticInput));
            _dynamicInput = dynamicInput ?? throw new ArgumentNullException(nameof(dynamicInput));
            _gridConfiguration = gridConfiguration ?? throw new ArgumentNullException(nameof(gridConfiguration));
            _geometry = _topologyBuilder.Build(_staticInput);
            PublishSnapshot(_geometry, _staticInput, _dynamicInput, _gridConfiguration);
        }

        public void UpdateDynamicInput(BattlefieldDynamicSpatialInput dynamicInput)
        {
            ThrowIfDisposed();
            if (dynamicInput == null) throw new ArgumentNullException(nameof(dynamicInput));
            PublishSnapshot(_geometry, _staticInput, dynamicInput, _gridConfiguration);
            _dynamicInput = dynamicInput;
        }

        public void ReconfigureGrid(BattlefieldGridConfiguration gridConfiguration)
        {
            ThrowIfDisposed();
            if (gridConfiguration == null) throw new ArgumentNullException(nameof(gridConfiguration));
            PublishSnapshot(_geometry, _staticInput, _dynamicInput, gridConfiguration);
            _gridConfiguration = gridConfiguration;
        }

        public void UpdateTurretLayout(BattlefieldSpatialInput staticInput)
        {
            ThrowIfDisposed();
            if (staticInput == null) throw new ArgumentNullException(nameof(staticInput));
            var geometry = _topologyBuilder.Build(staticInput);
            PublishSnapshot(geometry, staticInput, _dynamicInput, _gridConfiguration);
            _geometry = geometry;
            _staticInput = staticInput;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _snapshot = null;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(BattlefieldSpatialRuntime));
        }

        private void PublishSnapshot(
            BattlefieldSpatialSnapshot geometry,
            BattlefieldSpatialInput staticInput,
            BattlefieldDynamicSpatialInput dynamicInput,
            BattlefieldGridConfiguration gridConfiguration)
        {
            var nextRevision = checked(_revision + 1);
            var grid = _gridBuilder.Build(gridConfiguration, staticInput, dynamicInput);
            var nextSnapshot = BattlefieldSpatialSnapshot.CreateMatchSnapshot(geometry, _matchId, nextRevision, grid);
            _revision = nextRevision;
            _snapshot = nextSnapshot;
        }
    }
}
