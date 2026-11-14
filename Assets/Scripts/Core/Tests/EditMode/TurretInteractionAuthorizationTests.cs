// 권위 actor 제공자의 수명·최신 조회와 공용 명령의 거리/생존/조작 검증을 확인합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class TurretInteractionAuthorizationTests
    {
        private static readonly MatchId Match = new MatchId("match");
        private static readonly PlayerId Host = new PlayerId("host"), Client = new PlayerId("client");
        private static readonly EntityId Turret = new EntityId("turret/key");
        private static TurretCommandRequest Request(TurretCommandKind kind = TurretCommandKind.SetActivation)
            => new TurretCommandRequest(new MatchRequestContext(Match, Guid.NewGuid(), Client), kind,
                kind == TurretCommandKind.StartAutoActivation ? default : Turret);
        [Test]
        public void SharedCommandsRequireLatestAuthorityActorAndInclusiveRange()
        {
            using var actors = new PlayerInteractionRegistry(Match, new[] { Host, Client });
            var position = new BattlefieldPoint(6, 8); bool alive = true, canInteract = true;
            var policy = new TurretInteractionAuthorization(Host, actors, id => id == Turret ? new BattlefieldPoint(0, 0) : (BattlefieldPoint?)null,
                () => new BattlefieldPoint(0, 0), 10, new[] { TurretCommandKind.SetActivation, TurretCommandKind.StartAutoActivation });
            Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("AuthorityActorUnavailable"));
            using var actor = actors.Register(Client, () => new PlayerInteractionSnapshot(position, alive, canInteract));
            Assert.That(policy.Authorize(Request(), Client).IsAllowed, Is.True);
            Assert.That(policy.Authorize(Request(TurretCommandKind.StartAutoActivation), Client).IsAllowed, Is.True);
            Assert.That(policy.Authorize(Request(TurretCommandKind.UpgradeLevel), Client).Code, Is.EqualTo("SharedCommandNotAllowed"));
            position = new BattlefieldPoint(6, 8.01); Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("OutOfInteractionRange"));
            position = default; alive = false; Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("PlayerDead"));
            alive = true; canInteract = false; Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("InteractionBlocked"));
            actor.Dispose(); Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("AuthorityActorUnavailable"));
        }
        [Test]
        public void OccupiedRemoteCommandsStillRequireLiveActorAndNeverGrantRemoteCuAuto()
        {
            using var actors = new PlayerInteractionRegistry(Match, new[] { Host, Client });
            bool captured = true, alive = true, interactive = true, targetExists = true;
            using var actor = actors.Register(Client, () => new PlayerInteractionSnapshot(new BattlefieldPoint(100, 100), alive, interactive));
            var policy = new TurretInteractionAuthorization(Host, actors,
                id => targetExists && id == Turret ? default(BattlefieldPoint) : (BattlefieldPoint?)null,
                () => default(BattlefieldPoint), 10,
                new[] { TurretCommandKind.SetActivation, TurretCommandKind.StartAutoActivation }, id => id == Turret && captured);
            Assert.That(policy.Authorize(Request(), Client).IsAllowed, Is.True);
            Assert.That(policy.Authorize(Request(TurretCommandKind.StartAutoActivation), Client).Code, Is.EqualTo("OutOfInteractionRange"));
            captured = false; Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("OutOfInteractionRange"));
            captured = true; interactive = false; Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("InteractionBlocked"));
            interactive = true; alive = false; Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("PlayerDead"));
            alive = true; targetExists = false; Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("InteractionTargetUnavailable"));
        }
        [Test]
        public void RegistryRejectsForeignMatchDuplicateActorAndStaleLease()
        {
            using var actors = new PlayerInteractionRegistry(Match, new[] { Host, Client });
            var old = actors.Register(Client, () => new PlayerInteractionSnapshot(default, true, true));
            Assert.Throws<InvalidOperationException>(() => actors.Register(Client, () => null));
            Assert.Throws<ArgumentException>(() => actors.Register(new PlayerId("unknown"), () => null));
            Assert.That(actors.TryCapture(new MatchId("other"), Client, out _), Is.False);
            old.Dispose(); using var next = actors.Register(Client, () => new PlayerInteractionSnapshot(new BattlefieldPoint(2, 3), true, true));
            old.Dispose(); Assert.That(actors.TryCapture(Match, Client, out var snapshot), Is.True);
            Assert.That(snapshot.Position.X, Is.EqualTo(2)); actors.Dispose(); Assert.That(actors.TryCapture(Match, Client, out _), Is.False);
        }
        [Test]
        public void RegistryRechecksReentrantRemovalAndHugeCoordinatesNeverPassDistance()
        {
            using var actors = new PlayerInteractionRegistry(Match, new[] { Host, Client });
            IDisposable registration = null;
            registration = actors.Register(Client, () => { registration.Dispose(); return new PlayerInteractionSnapshot(default, true, true); });
            Assert.That(actors.TryCapture(Match, Client, out _), Is.False);
            using var next = actors.Register(Client, () => new PlayerInteractionSnapshot(new BattlefieldPoint(double.MaxValue, double.MaxValue), true, true));
            var policy = new TurretInteractionAuthorization(Host, actors, id => default(BattlefieldPoint), () => default(BattlefieldPoint),
                10, new[] { TurretCommandKind.SetActivation });
            Assert.That(policy.Authorize(Request(), Client).Code, Is.EqualTo("OutOfInteractionRange"));
        }
    }
}
