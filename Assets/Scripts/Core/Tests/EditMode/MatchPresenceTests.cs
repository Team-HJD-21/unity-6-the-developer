// 참가자 위치 등록·Host 권위·1/2인 정지·미등록/해제의 보수적 재개를 검증합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchPresenceTests
    {
        private static MatchConfig Config(params string[] players) => new MatchConfig(new MatchId("presence"),
            MatchMode.Story, new StageId("stage"), new DefinitionId("difficulty"), "content", 1,
            players.Select(id => new PlayerLoadoutSnapshot(new PlayerId(id), null)));

        [Test]
        public void SoloPausesOnEntryAndResumesOnExitWithoutDuplicateTransitions()
        {
            int changes = 0; bool paused = false;
            using var presence = new MatchPresenceService(Config("host"), () => true, value => { paused = value; changes++; });
            using var actor = presence.RegisterActor(new PlayerId("host"));
            actor.Report(MatchPlayerLocation.Spaceship); actor.Report(MatchPlayerLocation.Spaceship);
            Assert.That(paused, Is.True); Assert.That(changes, Is.EqualTo(1));
            actor.Report(MatchPlayerLocation.Field);
            Assert.That(paused, Is.False); Assert.That(changes, Is.EqualTo(2));
        }
        [Test]
        public void TwoPlayersRequireBothInsideAndUnregisteredParticipantPreventsPause()
        {
            using var presence = new MatchPresenceService(Config("host", "guest"), () => true, _ => { });
            using var host = presence.RegisterActor(new PlayerId("host"));
            host.Report(MatchPlayerLocation.Spaceship); Assert.That(presence.IsFieldPaused, Is.False);
            using var guest = presence.RegisterActor(new PlayerId("guest"));
            guest.Report(MatchPlayerLocation.Spaceship); Assert.That(presence.IsFieldPaused, Is.True);
            host.Report(MatchPlayerLocation.Field); Assert.That(presence.IsFieldPaused, Is.False);
            host.Report(MatchPlayerLocation.Spaceship); Assert.That(presence.IsFieldPaused, Is.True);
            guest.Dispose(); Assert.That(presence.IsFieldPaused, Is.False);
        }
        [Test]
        public void UnknownDuplicateAndStaleActorsCannotReport()
        {
            using var presence = new MatchPresenceService(Config("host"), () => true, _ => { });
            Assert.Throws<ArgumentException>(() => presence.RegisterActor(new PlayerId("stranger")));
            var old = presence.RegisterActor(new PlayerId("host"));
            Assert.Throws<InvalidOperationException>(() => presence.RegisterActor(new PlayerId("host")));
            old.Dispose(); using var current = presence.RegisterActor(new PlayerId("host"));
            Assert.Throws<ObjectDisposedException>(() => old.Report(MatchPlayerLocation.Spaceship));
            current.Report(MatchPlayerLocation.Spaceship); Assert.That(presence.IsFieldPaused, Is.True);
        }
        [Test]
        public void LostAuthorityAndDisposalRejectReportsWithoutResumingEndedClock()
        {
            bool authority = true; int changes = 0;
            var presence = new MatchPresenceService(Config("host"), () => authority, _ => changes++);
            using var actor = presence.RegisterActor(new PlayerId("host"));
            actor.Report(MatchPlayerLocation.Spaceship); authority = false;
            Assert.Throws<InvalidOperationException>(() => actor.Report(MatchPlayerLocation.Field));
            presence.Dispose(); actor.Dispose(); Assert.That(changes, Is.EqualTo(1));
            Assert.That(actor.CanReport, Is.False);
        }
    }
}
