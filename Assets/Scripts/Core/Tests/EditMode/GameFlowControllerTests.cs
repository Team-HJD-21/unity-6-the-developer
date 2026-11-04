// 파일 로드 성공과 실제 Mission 준비를 구분하고 권한·중복·취소·실패 뒤 귀환을 검증합니다.
// 실제 제작 Scene/Player/터렛 인수와 분리된 Core 흐름 검사입니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GameFlowControllerTests
    {
        private sealed class Host : IGameFlowHost
        {
            public string ActiveSceneKey { get; private set; } = "menu";
            public string TravelRestriction { get; set; }
            public bool MissionReady = true, BlockTravel;
            public int Travels;
            public readonly TaskCompletionSource<bool> Gate = new();
            public bool CanLoad(string key) => !string.IsNullOrWhiteSpace(key);
            public bool IsLoaded(string key) => key == ActiveSceneKey;
            public async Task TravelAsync(string key, CancellationToken token)
            {
                Travels++;
                if (BlockTravel)
                {
                    using var cancel = token.Register(() => Gate.TrySetCanceled());
                    await Gate.Task;
                }
                token.ThrowIfCancellationRequested(); ActiveSceneKey = key;
            }
            public Task WaitForMissionReadyAsync(string key, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (!MissionReady) throw new InvalidOperationException("CORE-FEATURE-HOLD: required Feature binding unavailable.");
                return Task.CompletedTask;
            }
        }
        private static GameFlowController Start(Host host)
        {
            var flow = new GameFlowController(host, new GameFlowRoutes("menu", "hub")); flow.AdoptMainMenu(); return flow;
        }
        [Test] public async Task MainMenuSpaceshipMissionAndExplicitReturnUseOneFlow()
        {
            var host = new Host(); using var flow = Start(host);
            await flow.EnterSpaceshipAsync(); Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Spaceship));
            await flow.LaunchMissionAsync("stage"); Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Mission));
            await flow.RestartMissionAsync(); Assert.That(host.ActiveSceneKey, Is.EqualTo("stage"));
            await flow.ReturnToSpaceshipAsync(); Assert.That(host.ActiveSceneKey, Is.EqualTo("hub"));
            await flow.OpenMainMenuAsync(); Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.MainMenu));
        }
        [Test] public async Task LoadedSceneWithMissingFeatureIsNotAReadyMissionAndCanReturn()
        {
            var host = new Host { MissionReady = false }; using var flow = Start(host);
            await flow.EnterSpaceshipAsync();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.LaunchMissionAsync("stage"));
            Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Faulted)); Assert.That(flow.CurrentSceneKey, Is.EqualTo("stage"));
            Assert.That(flow.LastFailure.Message, Does.Contain("CORE-FEATURE-HOLD"));
            await flow.ReturnToSpaceshipAsync(); Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Spaceship));
        }
        [Test] public void UnauthorizedOrUnavailableTravelDoesNotEndTheCurrentContext()
        {
            var host = new Host { TravelRestriction = "Host only" }; using var flow = Start(host);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.EnterSpaceshipAsync());
            Assert.That(host.Travels, Is.Zero); Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.MainMenu));
            Assert.Throws<InvalidOperationException>(() => flow.LaunchMissionAsync("stage"));
        }
        [Test] public async Task DuplicateAndCancelledTravelCannotPublishASuccessfulArrival()
        {
            var host = new Host { BlockTravel = true }; using var flow = Start(host); using var cancel = new CancellationTokenSource();
            var first = flow.EnterSpaceshipAsync(cancel.Token);
            Assert.That(flow.IsTransitioning, Is.True);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.EnterSpaceshipAsync());
            cancel.Cancel();
            try { await first; Assert.Fail("Travel should be cancelled."); } catch (OperationCanceledException) { }
            Assert.That(flow.IsTransitioning, Is.False); Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Faulted));
            Assert.That(host.ActiveSceneKey, Is.EqualTo("menu"));
        }
        [Test] public async Task FailingViewObserverDoesNotUndoACommittedScene()
        {
            var host = new Host(); using var flow = Start(host); flow.Changed += () => throw new Exception("view failure");
            await flow.EnterSpaceshipAsync();
            Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Spaceship)); Assert.That(flow.LastObserverError, Is.Not.Null);
        }
    }
}
