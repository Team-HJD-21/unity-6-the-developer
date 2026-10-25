// 지연 Scene 작업의 취소/commit·중복·실패와 소유자별 입력 잠금 수명을 검증합니다.
// 실제 Unity Scene 로드 검사는 별도 통합 사례가 담당합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class SceneFlowControllerTests
    {
        private sealed class Scenes : ISceneFlow
        {
            public readonly HashSet<string> Loaded = new HashSet<string>();
            public TaskCompletionSource<bool> Pending;
            public bool Fail;
            public float Progress => Pending == null ? 1f : .5f;
            public bool CanLoad(string key) => key == "stage" || key == "hub";
            public bool IsLoaded(string key) => Loaded.Contains(key);
            public async Task LoadAsync(string key, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); if (Pending != null) await Pending.Task;
                if (Fail) throw new InvalidOperationException("loader");
                Loaded.Add(key);
                if (token.IsCancellationRequested) { Loaded.Remove(key); token.ThrowIfCancellationRequested(); }
            }
            public async Task ReplaceAsync(string key, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); if (Pending != null) await Pending.Task;
                Loaded.Clear(); Loaded.Add(key);
            }
            public Task UnloadAsync(string key, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Loaded.Remove(key); return Task.CompletedTask; }
        }

        [Test]
        public async Task AdditiveCancellationKeepsInputBlockedUntilNativeCompletionAndRollback()
        {
            var scenes = new Scenes { Pending = new TaskCompletionSource<bool>() }; scenes.Loaded.Add("stage");
            using var input = new InputLockService(); using var flow = new SceneFlowController(scenes, input);
            Task pending = flow.LoadAsync("hub", CancellationToken.None);
            Assert.That(flow.IsTransitioning && input.IsBlocked, Is.True);
            Assert.That(flow.Progress, Is.EqualTo(.5f));
            Assert.That(flow.CancelCurrent(), Is.True);
            Assert.That(pending.IsCompleted, Is.False, "Native Scene operations cannot be aborted immediately.");
            scenes.Pending.SetResult(true);
            try { await pending; Assert.Fail("Cancellation was expected."); } catch (OperationCanceledException) { }
            Assert.That(scenes.Loaded.Contains("hub"), Is.False);
            Assert.That(scenes.Loaded.Contains("stage"), Is.True);
            Assert.That(flow.IsTransitioning || input.IsBlocked, Is.False);
        }

        [Test]
        public async Task DuplicateTransitionIsRejectedAndSingleCommitWinsOverLateCancellation()
        {
            var scenes = new Scenes { Pending = new TaskCompletionSource<bool>() };
            using var input = new InputLockService(); using var flow = new SceneFlowController(scenes, input);
            using var cancel = new CancellationTokenSource();
            Task pending = flow.ReplaceAsync("stage", cancel.Token);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.LoadAsync("hub", CancellationToken.None));
            Assert.That(flow.CancelCurrent(), Is.False);
            cancel.Cancel(); scenes.Pending.SetResult(true); await pending;
            Assert.That(scenes.Loaded.Contains("stage"), Is.True);
            Assert.That(input.Count, Is.Zero);
        }

        [Test]
        public async Task LoaderFailureReleasesOnlyItsOwnLockAndAllowsRetry()
        {
            var scenes = new Scenes { Fail = true };
            using var input = new InputLockService(); using var menu = input.Acquire("menu");
            using var flow = new SceneFlowController(scenes, input);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.LoadAsync("hub", CancellationToken.None));
            Assert.That(flow.LastFailure, Is.Not.Null); Assert.That(input.Count, Is.EqualTo(1));
            scenes.Fail = false; await flow.LoadAsync("hub", CancellationToken.None);
            Assert.That(flow.LastFailure, Is.Null); Assert.That(input.IsBlocked, Is.True);
        }

        [Test]
        public void PreflightAndDisposedOwnerCannotAcquireNewInputLocks()
        {
            using var input = new InputLockService(); var flow = new SceneFlowController(new Scenes(), input);
            Assert.ThrowsAsync<ArgumentException>(async () => await flow.LoadAsync("missing", CancellationToken.None));
            Assert.That(input.Count, Is.Zero);
            flow.Dispose();
            Assert.ThrowsAsync<ObjectDisposedException>(async () => await flow.LoadAsync("hub", CancellationToken.None));
            Assert.That(input.Count, Is.Zero);
        }

        [Test]
        public void InputOwnersCannotReleaseEachOthersLocksOrReopenDisposedApp()
        {
            var input = new InputLockService(); var menu = input.Acquire("menu"); var popup = input.Acquire("popup");
            menu.Dispose(); menu.Dispose(); Assert.That(input.Count, Is.EqualTo(1));
            Assert.That(input.IsBlocked, Is.True); popup.Dispose(); Assert.That(input.IsBlocked, Is.False);
            var late = input.Acquire("late"); input.Dispose(); late.Dispose();
            Assert.That(input.IsBlocked, Is.True);
            Assert.Throws<ObjectDisposedException>(() => input.Acquire("new"));
        }

        [Test]
        public async Task SceneCreatedNetworkDriverAppliesOnlyAfterCurrentOperationFinishes()
        {
            var initial = new Scenes { Pending = new TaskCompletionSource<bool>() }; var network = new Scenes();
            using var input = new InputLockService(); using var flow = new SceneFlowController(initial, input);
            Task pending = flow.ReplaceAsync("stage", CancellationToken.None);
            flow.SetSceneFlow(network);
            Assert.That(flow.Progress, Is.EqualTo(.5f));
            initial.Pending.SetResult(true); await pending;
            await flow.LoadAsync("hub", CancellationToken.None);
            Assert.That(network.Loaded.Contains("hub"), Is.True);
            Assert.That(initial.Loaded.Contains("hub"), Is.False);
        }
    }
}
