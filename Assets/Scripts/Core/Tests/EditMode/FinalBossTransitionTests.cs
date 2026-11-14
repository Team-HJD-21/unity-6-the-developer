// 최종 전환의 저장 선행·정지/입력 임대·취소/복원 무효화를 실제 Core 객체로 검사합니다.
// 저장 응답과 Field 생성 port만 제어하며 실제 Boss Scene·네트워크 도착/컷씬 인수를 대신하지 않습니다.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class FinalBossTransitionTests
    {
        private sealed class Timestamp : ITimestampSource
        {
            public double Now;
            public bool TryReadSeconds(out double seconds) { seconds = Now; return true; }
        }

        private sealed class Spawns : IFieldBossSpawnService
        {
            public bool TrySpawnFieldBoss(Guid id, int threshold, RegionGridSnapshot regions, out EntityId actor, out string reason)
            { actor = new EntityId("field-" + threshold); reason = null; return true; }
        }

        private sealed class Harness : IDisposable
        {
            public readonly MatchId Match = new("final-transition-test");
            public readonly Timestamp Source = new();
            public readonly InputLockService Input = new();
            public readonly MatchSession Session;
            public readonly MatchTimeService Time;
            public readonly StageProgression Stage;
            public bool Available = true;
            public Harness(int cells = 4)
            {
                var config = new MatchConfig(Match, MatchMode.Story, new StageId("stage-1"), new DefinitionId("normal"), "v1", 1,
                    new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
                Session = new MatchSessionFactory().Create(config, new MatchState(Match, MatchPhase.Preparing), new StoryModeRules());
                Session.Start();
                Time = new MatchTimeService(Session, Source, () => true);
                Time.Pump(1);
                Stage = new StageProgression(Match, Guid.NewGuid(), Time.FieldClock, () => Time.IsGameplayRunning, new Spawns());
                SetCapture(cells);
            }
            public void SetCapture(int count)
            {
                var definition = new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 20, 20, 2, 2));
                Stage.Pump(RegionGridSnapshot.FromCapturedCells(Match, Guid.NewGuid(), 1, RegionPolicyMode.Static, definition, Enumerable.Range(0, count)));
            }
            public FinalBossTransition Begin(CancellationToken cancellation = default) =>
                new(Stage, Time, Input, () => Available, default, cancellation);
            public void Dispose() { Time.Dispose(); Session.Dispose(); }
        }

        private static CheckpointOperationResult Saved(bool current = true) =>
            new(Guid.NewGuid(), CheckpointOperationStatus.Saved, "Saved", current);
        private static Task<CheckpointOperationResult> Save(CheckpointSlot slot, CancellationToken cancellation) => Task.FromResult(Saved());

        [Test]
        public async Task ActualSaveMustFinishAndPauseCoversScenePreparationUntilConfirmation()
        {
            using var f = new Harness();
            using var transition = f.Begin();
            var pending = new TaskCompletionSource<CheckpointOperationResult>();
            var prepare = transition.PrepareAsync(new CheckpointSlot("before-final"), (_, _) => pending.Task);
            Assert.That(transition.IsPrepared, Is.False);
            Assert.That(f.Input.IsBlocked, Is.True);
            f.Source.Now = 5; f.Time.Pump(1);
            Assert.That(f.Time.Clock.ElapsedSeconds, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => transition.ConfirmReady(new EntityId("final")));
            pending.SetResult(Saved());
            await prepare;
            Assert.That(transition.IsPrepared, Is.True);
            Assert.That(f.Time.Clock.Snapshot.IsPaused, Is.True);
            transition.ConfirmReady(new EntityId("final"));
            Assert.That(f.Stage.Snapshot.FinalBoss, Is.EqualTo(new EntityId("final")));
            Assert.That(f.Input.IsBlocked, Is.False);
            f.Source.Now = 10; f.Time.Pump(1);
            Assert.That(f.Time.Clock.ElapsedSeconds, Is.GreaterThan(0));
            Assert.That(f.Time.FieldClock.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public async Task RejectedOrOldScopeSaveReleasesResourcesWithoutCommittingBattle()
        {
            foreach (var result in new[] { new CheckpointOperationResult(Guid.NewGuid(), CheckpointOperationStatus.Rejected, "writer missing", true), Saved(false) })
            {
                using var f = new Harness();
                using var transition = f.Begin();
                try { await transition.PrepareAsync(new CheckpointSlot("before-final"), (_, _) => Task.FromResult(result)); Assert.Fail("The save must reject entry."); }
                catch (InvalidOperationException) { }
                Assert.That(transition.IsDisposed, Is.True);
                Assert.That(f.Input.IsBlocked, Is.False);
                Assert.That(f.Time.Clock.Snapshot.IsPaused, Is.False);
                Assert.That(f.Time.FieldClock.IsHeld, Is.False);
                Assert.That(f.Stage.Snapshot.FinalBoss.IsEmpty, Is.True);
            }
        }

        [Test]
        public void IncompleteCaptureCannotAcquirePauseOrInput()
        {
            using var f = new Harness(3);
            Assert.Throws<InvalidOperationException>(() => f.Begin());
            Assert.That(f.Input.IsBlocked, Is.False);
            Assert.That(f.Time.PauseOwners, Is.Empty);
        }

        [Test]
        public async Task CancellationAfterPreparedReturnStillInvalidatesTheScope()
        {
            using var f = new Harness();
            using var cancel = new CancellationTokenSource();
            using var transition = f.Begin(cancel.Token);
            await transition.PrepareAsync(new CheckpointSlot("before-final"), Save);
            cancel.Cancel();
            transition.InvalidateIfStale();
            Assert.That(transition.IsDisposed, Is.True);
            Assert.That(f.Input.IsBlocked, Is.False);
            Assert.Throws<OperationCanceledException>(() => transition.ConfirmReady(new EntityId("final")));
        }

        [Test]
        public async Task RestoreEpochOrPlayerFailureCannotUseThePreviousPreparedScope()
        {
            using var f = new Harness();
            using var transition = f.Begin();
            await transition.PrepareAsync(new CheckpointSlot("before-final"), Save);
            f.Time.Restore(0);
            transition.InvalidateIfStale();
            Assert.That(f.Input.IsBlocked, Is.False);
            Assert.Throws<OperationCanceledException>(() => transition.ConfirmReady(new EntityId("final")));
            using var next = f.Begin();
            await next.PrepareAsync(new CheckpointSlot("before-final"), Save);
            f.Available = false;
            next.InvalidateIfStale();
            Assert.That(f.Time.PauseOwners, Is.Empty);
        }

        [Test]
        public async Task LateSavedResultAfterDisposeDoesNotGrantEntryOrClearAnotherPause()
        {
            using var f = new Harness();
            using var other = f.Time.AcquirePause("OtherOwner");
            using var transition = f.Begin();
            var pending = new TaskCompletionSource<CheckpointOperationResult>();
            var prepare = transition.PrepareAsync(new CheckpointSlot("before-final"), (_, _) => pending.Task);
            transition.Dispose();
            pending.SetResult(Saved());
            try { await prepare; Assert.Fail("Old success must not grant entry."); } catch (OperationCanceledException) { }
            Assert.That(f.Input.IsBlocked, Is.False);
            Assert.That(f.Time.PauseOwners, Is.EqualTo(new[] { "OtherOwner" }));
            Assert.That(f.Stage.Snapshot.FinalBoss.IsEmpty, Is.True);
        }

        [Test]
        public async Task InvalidFinalIdentityDoesNotLeaveTheFieldHeldAndCanBeCorrected()
        {
            using var f = new Harness();
            using var transition = f.Begin();
            await transition.PrepareAsync(new CheckpointSlot("before-final"), Save);
            Assert.Throws<InvalidOperationException>(() => transition.ConfirmReady(f.Stage.Snapshot.FirstFieldBoss));
            Assert.That(f.Time.FieldClock.IsHeld, Is.False);
            Assert.That(f.Input.IsBlocked, Is.True);
            transition.ConfirmReady(new EntityId("final"));
            Assert.That(f.Time.FieldClock.IsHeld, Is.True);
        }
    }
}
