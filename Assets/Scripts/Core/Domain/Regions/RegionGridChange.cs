// 확정된 지역 변경의 사유와 식별자를 UI·복제 소비자에게 전달합니다.
// 점유를 계산하거나 연출 진행을 소유하지 않으며, 해금 이후에도 마지막 점유 사실을 식별합니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public enum RegionChangeKind { Baseline, StateChanged, StaticSecured, StaticLost, DynamicCapture, Unlock }

    public sealed class RegionGridChange
    {
        public RegionChangeKind Kind { get; }
        public long PreviousRevision { get; }
        /// <summary>이 사실이 확정된 revision입니다. 뒤따른 해금의 Snapshot.Revision보다 작을 수 있습니다.</summary>
        public long Revision { get; }
        public IReadOnlyList<int> SecuredRegionIds { get; }
        public bool IsCaptureChange => Kind == RegionChangeKind.StaticSecured ||
            Kind == RegionChangeKind.StaticLost || Kind == RegionChangeKind.DynamicCapture;

        public RegionGridChange(RegionChangeKind kind, long previousRevision, long revision,
            IEnumerable<int> securedRegionIds = null)
        {
            if (!Enum.IsDefined(typeof(RegionChangeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (revision < 0 || (kind == RegionChangeKind.Baseline ? previousRevision != -1 :
                previousRevision < 0 || previousRevision >= revision)) throw new ArgumentOutOfRangeException(nameof(previousRevision));
            var ids = new SortedSet<int>();
            if (securedRegionIds != null)
                foreach (int id in securedRegionIds)
                    if (id <= 0 || !ids.Add(id)) throw new ArgumentException("Secured Region IDs must be positive and unique.", nameof(securedRegionIds));
            if ((kind == RegionChangeKind.StaticSecured) != (ids.Count > 0))
                throw new ArgumentException("Only a static completion carries secured Region IDs.", nameof(securedRegionIds));
            Kind = kind; PreviousRevision = previousRevision; Revision = revision;
            SecuredRegionIds = new ReadOnlyCollection<int>(new List<int>(ids));
        }

        public static RegionGridChange Baseline(long revision) => new RegionGridChange(RegionChangeKind.Baseline, -1, revision);
    }
}
