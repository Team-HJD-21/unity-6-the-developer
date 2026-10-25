// 표시 복사본의 불변성·세대·revision·확정 명령 순번과 폐기 후 차단을 검사합니다.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class TurretPresentationReplicaTests
    {
        private static readonly MatchId Match = new MatchId("match");
        private static TurretPresentationSnapshot Row(string id = "turret/1", int hp = 100)
            => new TurretPresentationSnapshot(new EntityId(id), new DefinitionId("canon"), "Canon", default, 1, hp, 100, 5, 10,
                12, 0, default, 0, TurretPresentationFlags.Initialized);
        private static TurretControllerPresentationSnapshot Controller() => new TurretControllerPresentationSnapshot(100, 90, 10, 0, 1000, 1000, default, false, 0, RegionPolicyMode.Static);
        private static TurretPresentationFrame Frame(Guid descriptor, long revision, long confirmed = 0, int hp = 100)
            => new TurretPresentationFrame(Match, descriptor, revision, Controller(), new[] { Row(hp: hp) }, confirmed);
        [Test]
        public void ReplicaRejectsWrongGenerationRevisionConflictAndCommandRollback()
        {
            Guid descriptor = Guid.NewGuid(); using var replica = new TurretPresentationReplica(Match, descriptor); int changes = 0;
            replica.Changed += frame => changes++;
            Assert.That(replica.Snapshot, Is.Null); Assert.That(replica.TryApply(Frame(Guid.NewGuid(), 1)), Is.False);
            Assert.That(replica.TryApply(Frame(descriptor, 2, 4)), Is.True);
            Assert.That(replica.TryApply(Frame(descriptor, 2, 4)), Is.True); Assert.That(changes, Is.EqualTo(1));
            Assert.That(replica.TryApply(Frame(descriptor, 2, 4, 90)), Is.False);
            Assert.That(replica.TryApply(Frame(descriptor, 1, 4)), Is.False);
            Assert.That(replica.TryApply(Frame(descriptor, 3, 3)), Is.False);
            Assert.That(replica.TryApply(Frame(descriptor, 3, 5, 90)), Is.True);
            Assert.That(replica.Snapshot.ConfirmedCommandSequence, Is.EqualTo(5));
            replica.Dispose(); Assert.That(replica.Snapshot, Is.Null); Assert.That(changes, Is.EqualTo(3));
            Assert.That(replica.TryApply(Frame(descriptor, 4, 6)), Is.False);
        }
        [Test]
        public void FrameCopiesCollectionAndRejectsDuplicateAndInvalidPresentation()
        {
            var rows = new List<TurretPresentationSnapshot> { Row() }; var frame = new TurretPresentationFrame(Match, Guid.NewGuid(), 1, Controller(), rows);
            rows.Clear(); Assert.That(frame.Turrets.Count, Is.EqualTo(1));
            Assert.That(frame.TryGet(new EntityId("turret/1"), out var row), Is.True); Assert.That(row.CurrentHealth, Is.EqualTo(100));
            Assert.Throws<ArgumentException>(() => new TurretPresentationFrame(Match, Guid.NewGuid(), 1, Controller(), new[] { Row(), Row() }));
            Assert.Throws<ArgumentException>(() => new TurretControllerPresentationSnapshot(100, 90, 50, 0, 100, 100, default, false, 0, RegionPolicyMode.Static));
            Assert.Throws<ArgumentException>(() => new TurretPresentationSnapshot(new EntityId("id"), new DefinitionId("def"), "name", default,
                1, 100, 100, 5, 10, double.NaN, 0, default, 0, TurretPresentationFlags.Initialized));
        }
        [Test]
        public void UIEligibilityUsesHostFlagsNotLocalWriteAuthority()
        {
            var row = Row(); Assert.That(row.CanModifyUpgrades, Is.True);
            var active = new TurretPresentationSnapshot(row.EntityId, row.DefinitionId, row.DisplayName, row.Position, row.Level,
                row.CurrentHealth, row.MaxHealth, row.Damage, row.Power, row.Range, 0, default, 0, row.Flags | TurretPresentationFlags.Activated);
            Assert.That(active.CanModifyUpgrades, Is.False);
        }
    }
}
