// 실제 Host 원본 port로 구조 유지·입력 취소·팀 예산·실패 차감 경계를 검사합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchRecoveryTests
    {
        private static readonly PlayerId A = new("a"), B = new("b"), C = new("c");
        private sealed class Clock : IMatchClock { public double Now; public double ElapsedSeconds => Now; }
        private sealed class Actor : IPlayerRescueActor
        {
            public bool Alive = true, Available = true, Interact = true;
            public double X, Ratio; public int Revived;
            public bool TryCaptureRescue(out PlayerInteractionSnapshot snapshot)
            { snapshot = new PlayerInteractionSnapshot(new BattlefieldPoint(X, 0), Alive, Interact); return Available; }
            public bool TryRevive(double ratio) { if (Alive) return false; Alive = true; Ratio = ratio; Revived++; return true; }
        }
        private static MatchRecovery Create(MissionRecoveryLedger ledger, Clock clock, params PlayerId[] roster)
            => new(ledger, clock, () => true, roster, new MissionRecoverySettings());

        [Test]
        public void ContinuousValidHoldRevivesOnceAtThirtyPercentWithoutRefundingTeamTime()
        {
            var ledger = new MissionRecoveryLedger(); var clock = new Clock(); var living = new Actor(); var down = new Actor { Alive = false };
            using var recovery = Create(ledger, clock, A, B); recovery.Register(A, living); recovery.Register(B, down);
            recovery.SetRescueHeld(A, B, true); recovery.Pump();
            for (int i = 1; i <= 30; i++)
            { clock.Now = i / 10d; recovery.SetRescueHeld(A, B, true); recovery.Pump(); }
            Assert.That(down.Revived, Is.EqualTo(1)); Assert.That(down.Ratio, Is.EqualTo(.3));
            Assert.That(ledger.RescueSeconds, Is.EqualTo(57).Within(.00001));
            clock.Now = 10; recovery.Pump(); Assert.That(down.Revived, Is.EqualTo(1));
            Assert.That(ledger.RescueSeconds, Is.EqualTo(57).Within(.00001));
        }

        [TestCase("release")]
        [TestCase("distance")]
        [TestCase("expired")]
        public void InvalidatedHoldDoesNotFinishAnOldChannel(string cause)
        {
            var ledger = new MissionRecoveryLedger(); var clock = new Clock(); var living = new Actor(); var down = new Actor { Alive = false };
            using var recovery = Create(ledger, clock, A, B); recovery.Register(A, living); recovery.Register(B, down);
            recovery.SetRescueHeld(A, B, true); recovery.Pump();
            clock.Now = 2.9;
            if (cause == "release") recovery.SetRescueHeld(A, B, false);
            else if (cause == "distance") living.X = 20;
            recovery.Pump();
            living.X = 0; clock.Now = 3.1; recovery.Pump();
            Assert.That(down.Revived, Is.Zero); Assert.That(recovery.HoldProgress(A), Is.Zero);
        }

        [Test]
        public void TwoDownPlayersSpendOneSharedSecondPerGameSecond()
        {
            var ledger = new MissionRecoveryLedger(); var clock = new Clock();
            using var recovery = Create(ledger, clock, A, B, C);
            recovery.Register(A, new Actor()); recovery.Register(B, new Actor { Alive = false }); recovery.Register(C, new Actor { Alive = false });
            recovery.Pump(); clock.Now = 10; recovery.Pump();
            Assert.That(ledger.RescueSeconds, Is.EqualTo(50));
        }

        [Test]
        public void AllDownEmitsOneFailureAndTechnicalRetriesCannotSpendAnotherUse()
        {
            var ledger = new MissionRecoveryLedger(); var clock = new Clock(); var down = new Actor { Alive = false };
            using var recovery = Create(ledger, clock, A); recovery.Register(A, down);
            int failures = 0; Guid pending = Guid.Empty;
            recovery.Failure += (id, allowed, reason) => { failures++; pending = id; Assert.That(allowed, Is.True); };
            recovery.Pump(); clock.Now = 100; recovery.Pump();
            Assert.That(failures, Is.EqualTo(1)); Assert.That(ledger.SafetyUses, Is.EqualTo(1));
            Assert.That(ledger.TryBeginFailure(pending), Is.True); Assert.That(ledger.SafetyUses, Is.EqualTo(1));
            down.Alive = true; recovery.CompleteRecovery(pending);
            Assert.That(ledger.SafetyUses, Is.EqualTo(1)); Assert.That(ledger.RescueSeconds, Is.EqualTo(60));
        }

        [Test]
        public void FourthFailureEndsTheRunAndOnlyExplicitNewMissionResetsIt()
        {
            var ledger = new MissionRecoveryLedger(); Guid run = ledger.RunId;
            for (int i = 0; i < 3; i++) { var id = Guid.NewGuid(); Assert.That(ledger.TryBeginFailure(id), Is.True); ledger.CompleteRecovery(id); }
            Assert.That(ledger.TryBeginFailure(Guid.NewGuid()), Is.False); Assert.That(ledger.IsEnded, Is.True);
            Assert.That(ledger.SafetyUses, Is.EqualTo(3));
            ledger.BeginNewMission(); Assert.That(ledger.IsEnded, Is.False); Assert.That(ledger.SafetyUses, Is.Zero); Assert.That(ledger.RunId, Is.Not.EqualTo(run));
        }

        [Test]
        public void MissingAuthorityActorCannotBeAssumedDeadOrSpendTheTeamBudget()
        {
            var ledger = new MissionRecoveryLedger(); var clock = new Clock();
            using var recovery = Create(ledger, clock, A, B); recovery.Register(A, new Actor { Alive = false });
            recovery.Pump(); clock.Now = 30; recovery.Pump();
            Assert.That(recovery.IsReady, Is.False); Assert.That(ledger.SafetyUses, Is.Zero); Assert.That(ledger.RescueSeconds, Is.EqualTo(60));
            Assert.Throws<InvalidOperationException>(() => recovery.Register(C, new Actor()));
        }

        [Test]
        public void ExhaustedTeamBudgetRequestsRecoveryAndAClockRewindNeverRefillsIt()
        {
            var ledger = new MissionRecoveryLedger(); var clock = new Clock();
            using var recovery = Create(ledger, clock, A, B); recovery.Register(A, new Actor()); recovery.Register(B, new Actor { Alive = false });
            recovery.Pump(); clock.Now = 40; recovery.Pump(); clock.Now = 10; recovery.Pump();
            Assert.That(ledger.RescueSeconds, Is.EqualTo(20));
            clock.Now = 30; recovery.Pump(); Assert.That(recovery.IsRecoveryPending, Is.True); Assert.That(ledger.SafetyUses, Is.EqualTo(1));
        }
    }
}
