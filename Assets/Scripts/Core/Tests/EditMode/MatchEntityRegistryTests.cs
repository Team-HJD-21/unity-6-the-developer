// 논리 ID/로컬 핸들 충돌과 등록 세대·Match 격리를 순수 C# 모델에서 검사합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchEntityRegistryTests
    {
        [Test]
        public void RegistrationIsBidirectionalAndIdempotent()
        {
            using var registry = new MatchEntityRegistry<int>(new MatchId("match"));
            var first = registry.Register(new EntityId("turret/a"), 17);
            Assert.That(registry.Register(first.EntityId, 17), Is.SameAs(first));
            Assert.That(registry.TryGetByHandle(17, out var byHandle), Is.True);
            Assert.That(registry.TryGetByEntityId(first.EntityId, out var byEntity), Is.True);
            Assert.That(byHandle, Is.SameAs(byEntity)); Assert.That(registry.Count, Is.EqualTo(1));
        }
        [Test]
        public void ConflictingEntityOrHandleNeverOverwritesTheOwner()
        {
            using var registry = new MatchEntityRegistry<int>(new MatchId("match"));
            var first = registry.Register(new EntityId("turret/a"), 1);
            Assert.Throws<InvalidOperationException>(() => registry.Register(first.EntityId, 2));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new EntityId("turret/b"), 1));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGetByHandle(1, out var current), Is.True); Assert.That(current, Is.SameAs(first));
        }
        [Test]
        public void LateAndForeignUnregisterCannotRemoveANewRegistration()
        {
            var matchId = new MatchId("match"); using var registry = new MatchEntityRegistry<int>(matchId);
            var old = registry.Register(new EntityId("turret/a"), 1);
            Assert.That(registry.Unregister(old), Is.True);
            var current = registry.Register(old.EntityId, 1);
            Assert.That(current.Generation, Is.GreaterThan(old.Generation));
            Assert.That(registry.Unregister(old), Is.False);
            using var another = new MatchEntityRegistry<int>(matchId);
            Assert.That(registry.Unregister(another.Register(old.EntityId, 1)), Is.False);
            Assert.That(registry.Unregister(current), Is.True);
        }
        [Test]
        public void DifferentProcessRegistrationOrderDoesNotDefineLogicalIdentity()
        {
            using var first = new MatchEntityRegistry<int>(new MatchId("shared"));
            using var second = new MatchEntityRegistry<int>(new MatchId("shared"));
            var canon = new EntityId("turret/canon"); var missile = new EntityId("turret/missile");
            first.Register(canon, 1); first.Register(missile, 2);
            second.Register(missile, 31); second.Register(canon, 32);
            first.TryGetByEntityId(canon, out var localA); second.TryGetByEntityId(canon, out var localB);
            Assert.That(localA.EntityId, Is.EqualTo(localB.EntityId)); Assert.That(localA.Handle, Is.Not.EqualTo(localB.Handle));
        }
        [Test]
        public void DisposeClearsLookupsAndRejectsNewRegistrations()
        {
            var registry = new MatchEntityRegistry<int>(new MatchId("match"));
            var old = registry.Register(new EntityId("turret/a"), 1); registry.Dispose(); registry.Dispose();
            Assert.That(registry.Count, Is.Zero); Assert.That(registry.TryGetByHandle(1, out _), Is.False);
            Assert.That(registry.Unregister(old), Is.False);
            Assert.Throws<ObjectDisposedException>(() => registry.Register(old.EntityId, 2));
        }
        [Test]
        public void EmptyIdentifiersAndNullHandlesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new MatchEntityRegistry<int>(default));
            using var registry = new MatchEntityRegistry<string>(new MatchId("match"));
            Assert.Throws<ArgumentException>(() => registry.Register(default, "a"));
            Assert.Throws<ArgumentNullException>(() => registry.Register(new EntityId("a"), null));
        }
    }
}
