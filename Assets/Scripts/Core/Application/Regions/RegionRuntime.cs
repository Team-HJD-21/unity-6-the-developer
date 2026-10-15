// Match 범위의 지역 Grid 상태·세대와 변경 통지를 소유합니다.
// 정책은 셀 집합만 전달하며 종료 또는 다른 세대의 결과는 적용되지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class RegionRuntime : IRegionGridSource, IDisposable
    {
        private RegionGridState _state;
        private bool _isDisposed;
        private bool _isNotifying;
        public event Action<RegionGridSnapshot> Changed;

        public RegionGridSnapshot Snapshot
        {
            get { ThrowIfDisposed(); return _state.Snapshot; }
        }

        public RegionRuntime(MatchId matchId, RegionGridDefinition definition,
            RegionPolicyMode mode = RegionPolicyMode.Static)
        {
            _state = new RegionGridState(matchId, Guid.NewGuid(), definition, mode);
        }

        public RegionUpdateStatus TryApply(RegionGridSnapshot basis, IEnumerable<int> cells,
            RegionCaptureUpdateMode updateMode)
        {
            ThrowIfDisposed();
            if (_isNotifying) throw new InvalidOperationException("Do not mutate regions inside a change notification.");
            if (basis == null) throw new ArgumentNullException(nameof(basis));
            RegionGridSnapshot current = _state.Snapshot;
            if (basis.MatchId != current.MatchId) return RegionUpdateStatus.DifferentMatch;
            if (basis.GridId != current.GridId) return RegionUpdateStatus.DifferentGrid;
            if (basis.Revision != current.Revision) return RegionUpdateStatus.StaleRevision;
            if (!_state.Apply(cells, updateMode)) return RegionUpdateStatus.Unchanged;
            Notify();
            return RegionUpdateStatus.Applied;
        }

        /// <summary>명시적 초기화입니다. 기존 점령을 비우고 새 Grid 세대를 발급합니다.</summary>
        public void Reset(RegionGridDefinition definition, RegionPolicyMode mode, IEnumerable<int> initialCells = null)
        {
            ThrowIfDisposed();
            if (_isNotifying) throw new InvalidOperationException("Do not reset regions inside a change notification.");
            var next = new RegionGridState(_state.Snapshot.MatchId, Guid.NewGuid(), definition, mode);
            if (initialCells != null) next.Apply(initialCells, RegionCaptureUpdateMode.Replace);
            _state = next;
            Notify();
        }

        private void Notify()
        {
            Action<RegionGridSnapshot> observers = Changed;
            if (observers == null) return;
            RegionGridSnapshot snapshot = _state.Snapshot;
            var errors = new List<Exception>();
            _isNotifying = true;
            try
            {
                foreach (Action<RegionGridSnapshot> observer in observers.GetInvocationList())
                {
                    try { observer(snapshot); }
                    catch (Exception exception) { errors.Add(exception); }
                }
            }
            finally { _isNotifying = false; }
            // 통지는 이미 확정된 사실입니다. 오류가 나도 상태를 되돌리거나 다른 관측자를 건너뛰지 않습니다.
            if (errors.Count > 0) throw new AggregateException("Region state committed; notification failed.", errors);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Changed = null;
            _state = null;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(RegionRuntime));
        }
    }
}
