// Match 준비 세대와 단조 revision으로 묶은 전체 HUD 투영입니다.
// 지역 원본/터렛 원본을 대체하지 않으며 Client는 읽기 모델로만 보관합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class TurretPresentationFrame
    {
        public const int MaximumTurrets = 1024;
        public MatchId MatchId { get; }
        public Guid DescriptorId { get; }
        public long Revision { get; }
        public long ConfirmedCommandSequence { get; }
        public TurretControllerPresentationSnapshot Controller { get; }
        public IReadOnlyList<TurretPresentationSnapshot> Turrets { get; }
        private readonly Dictionary<EntityId, TurretPresentationSnapshot> _byId;
        public TurretPresentationFrame(MatchId matchId, Guid descriptorId, long revision,
            TurretControllerPresentationSnapshot controller, IEnumerable<TurretPresentationSnapshot> turrets, long confirmedCommandSequence = 0)
        {
            if (matchId.IsEmpty || descriptorId == Guid.Empty || revision < 1) throw new ArgumentException("Provide a running Match frame identity.");
            if (confirmedCommandSequence < 0) throw new ArgumentOutOfRangeException(nameof(confirmedCommandSequence));
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            if (turrets == null) throw new ArgumentNullException(nameof(turrets));
            var rows = new List<TurretPresentationSnapshot>(); _byId = new Dictionary<EntityId, TurretPresentationSnapshot>();
            foreach (var row in turrets)
            {
                if (row == null || rows.Count >= MaximumTurrets || !_byId.TryAdd(row.EntityId, row))
                    throw new ArgumentException("Provide a bounded unique turret set.", nameof(turrets));
                rows.Add(row);
            }
            MatchId = matchId; DescriptorId = descriptorId; Revision = revision;
            ConfirmedCommandSequence = confirmedCommandSequence;
            Turrets = Array.AsReadOnly(rows.OrderBy(row => row.EntityId.Value, StringComparer.Ordinal).ToArray());
        }
        public bool TryGet(EntityId entityId, out TurretPresentationSnapshot snapshot) => _byId.TryGetValue(entityId, out snapshot);
        public bool HasSameContent(TurretPresentationFrame other)
        {
            if (other == null || MatchId != other.MatchId || DescriptorId != other.DescriptorId ||
                ConfirmedCommandSequence != other.ConfirmedCommandSequence || !Controller.Equals(other.Controller) || Turrets.Count != other.Turrets.Count) return false;
            for (int index = 0; index < Turrets.Count; index++) if (!Turrets[index].Equals(other.Turrets[index])) return false;
            return true;
        }
    }
}
