// Feature 재생성 순서와 무관하게 최종 actor에 참조를 연결하고 실패 시 백업의 참조까지 복구하는지 검사합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CheckpointReferenceRestoreTests
    {
        private sealed class Actor
        { public readonly int Value; public Actor Target; public Actor(int value) { Value = value; } }
        private sealed class Participant : ICheckpointParticipant
        {
            public string Key { get; }
            public int Version => 1;
            public CheckpointSectionKind Kind => CheckpointSectionKind.World;
            public int RestoreOrder { get; }
            private readonly Dictionary<string, Actor> _world;
            private readonly string _target;
            public int Applies, Rollbacks, Resolves, RollbackResolves;
            public Action OnResolve, OnRollbackResolve;
            public Participant(Dictionary<string, Actor> world, string key, int order, string target = null)
            { _world = world; Key = key; RestoreOrder = order; _target = target; }
            public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec) =>
                codec.Write(Key, Version, Kind, writer => writer.Write(_world[Key].Value));
            public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
            {
                int saved = codec.Read(section, Version, reader => reader.ReadInt32());
                int backup = _world[Key].Value;
                return new CheckpointRestoreStep(
                    () => { Applies++; _world[Key] = new Actor(saved); },
                    () => { Rollbacks++; _world[Key] = new Actor(backup); },
                    resolveReferences: () => { Resolves++; Resolve(); OnResolve?.Invoke(); },
                    resolveRollbackReferences: () => { RollbackResolves++; Resolve(); OnRollbackResolve?.Invoke(); });
            }
            private void Resolve() { if (_target != null) _world[Key].Target = _world[_target]; }
        }

        [Test]
        public void TargetCreatedAfterProjectileIsResolvedBeforePublishing()
        {
            using var h = new CheckpointServiceTests.Harness();
            var world = new Dictionary<string, Actor> { ["projectile"] = new(1), ["enemy"] = new(2) };
            var projectile = new Participant(world, "projectile", 0, "enemy");
            var enemy = new Participant(world, "enemy", 1);
            h.Service.Register(projectile); h.Service.Register(enemy); var oldEnemy = world["enemy"];
            var result = h.Service.Restore(h.Target(("projectile", CheckpointSectionKind.World, 10), ("enemy", CheckpointSectionKind.World, 20)));
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Restored));
            Assert.That(world["enemy"], Is.Not.SameAs(oldEnemy));
            Assert.That(world["projectile"].Target, Is.SameAs(world["enemy"]));
            Assert.That(world["projectile"].Target.Value, Is.EqualTo(20));
            Assert.That(h.Runtime.Finishes, Is.EqualTo(1));
        }

        [Test]
        public void ReferenceFailureRebuildsAllBackupActorsBeforeRebindingReverseOrder()
        {
            using var h = new CheckpointServiceTests.Harness();
            var world = new Dictionary<string, Actor> { ["projectile"] = new(1), ["enemy"] = new(2) };
            var enemy = new Participant(world, "enemy", 0);
            var projectile = new Participant(world, "projectile", 1, "enemy") { OnResolve = () => throw new InvalidOperationException("missing saved target") };
            h.Service.Register(enemy); h.Service.Register(projectile); h.Runtime.Advance(30);
            var result = h.Service.Restore(h.Target(("enemy", CheckpointSectionKind.World, 20), ("projectile", CheckpointSectionKind.World, 10)));
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Failed));
            Assert.That(result.Reason, Is.EqualTo("RestoreRolledBack"));
            Assert.That(world["projectile"].Value, Is.EqualTo(1)); Assert.That(world["enemy"].Value, Is.EqualTo(2));
            Assert.That(world["projectile"].Target, Is.SameAs(world["enemy"]), "The enemy was replaced after the projectile's Rollback.");
            Assert.That(projectile.RollbackResolves, Is.EqualTo(1));
            Assert.That(h.Runtime.Time.ElapsedSeconds, Is.EqualTo(30)); Assert.That(h.Runtime.Finishes, Is.EqualTo(1));
            Assert.That(h.Runtime.Faults, Is.Zero);
        }

        [Test]
        public void FailedBackupReferenceIsFaultedWithoutPublishingAPartialWorld()
        {
            using var h = new CheckpointServiceTests.Harness();
            var world = new Dictionary<string, Actor> { ["actor"] = new(1) };
            var actor = new Participant(world, "actor", 0)
            {
                OnResolve = () => throw new InvalidOperationException("bad saved reference"),
                OnRollbackResolve = () => throw new InvalidOperationException("bad backup reference")
            };
            h.Service.Register(actor);
            var result = h.Service.Restore(h.Target(("actor", CheckpointSectionKind.World, 10)));
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Faulted));
            Assert.That(h.Runtime.Finishes, Is.Zero); Assert.That(h.Runtime.Faults, Is.EqualTo(1));
            Assert.That(h.Runtime.Time.Snapshot.IsPaused, Is.True);
        }

        [Test]
        public void CancellationDuringReferencesStillRebuildsAndResolvesEveryBackup()
        {
            using var h = new CheckpointServiceTests.Harness(); using var cancel = new CancellationTokenSource();
            var world = new Dictionary<string, Actor> { ["projectile"] = new(1), ["enemy"] = new(2) };
            var projectile = new Participant(world, "projectile", 0, "enemy") { OnResolve = cancel.Cancel };
            var enemy = new Participant(world, "enemy", 1);
            h.Service.Register(projectile); h.Service.Register(enemy);
            var result = h.Service.Restore(h.Target(("projectile", CheckpointSectionKind.World, 10), ("enemy", CheckpointSectionKind.World, 20)), cancellation: cancel.Token);
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Cancelled));
            Assert.That(enemy.Resolves, Is.Zero); Assert.That(enemy.RollbackResolves, Is.EqualTo(1));
            Assert.That(world["projectile"].Target, Is.SameAs(world["enemy"]));
            Assert.That(world["enemy"].Value, Is.EqualTo(2)); Assert.That(h.Runtime.Finishes, Is.EqualTo(1));
        }

        [Test]
        public void ResolverPairAndDisposedLifetimeAreEnforced()
        {
            Assert.Throws<ArgumentException>(() => new CheckpointRestoreStep(() => { }, () => { }, resolveReferences: () => { }));
            var step = new CheckpointRestoreStep(() => { }, () => { }, resolveReferences: () => { }, resolveRollbackReferences: () => { });
            step.Dispose();
            Assert.Throws<ObjectDisposedException>(step.ResolveReferences);
            Assert.Throws<ObjectDisposedException>(step.ResolveRollbackReferences);
        }
    }
}
