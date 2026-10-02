// Match가 소유하는 공간 분석 결과의 생성·조회·폐기 경계입니다.

using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class BattlefieldSpatialRuntime : IDisposable
    {
        private BattlefieldSpatialSnapshot _snapshot;
        private bool _isDisposed;

        public BattlefieldSpatialSnapshot Snapshot
        {
            get
            {
                ThrowIfDisposed();
                return _snapshot;
            }
        }

        public BattlefieldSpatialRuntime(BattlefieldSpatialInput input)
        {
            _snapshot = new BattlefieldTopologyBuilder().Build(input ?? throw new ArgumentNullException(nameof(input)));
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
    }
}
