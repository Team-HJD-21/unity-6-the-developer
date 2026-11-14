// 이동 거부·취소가 기존 Mission의 복귀 식별자를 먼저 초기화하지 않는지 검사합니다.
// 허용된 이동만 NewMission/Retry/Resume 의도를 App에 전달합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MissionEntryTests
    {
        private sealed class Host : IGameFlowHost
        {
            public string ActiveSceneKey { get; private set; } = "ship";
            public string TravelRestriction { get; set; }
            public bool Loadable = true;
            public readonly List<MissionEntryKind> Entries = new();
            public void PrepareMission(MissionEntryKind entry) => Entries.Add(entry);
            public bool CanLoad(string scene) => Loadable;
            public bool IsLoaded(string scene) => scene == ActiveSceneKey;
            public Task TravelAsync(string scene, CancellationToken cancellation) { ActiveSceneKey = scene; return Task.CompletedTask; }
            public Task WaitForMissionReadyAsync(string scene, CancellationToken cancellation) => Task.CompletedTask;
        }
        [TestCase("blocked")]
        [TestCase("unloadable")]
        [TestCase("cancelled")]
        public async Task RejectedTravelDoesNotResetTheExistingMissionIntent(string failure)
        {
            var host = new Host(); using var flow = new GameFlowController(host, new GameFlowRoutes("menu", "ship"));
            flow.AdoptSynchronizedScene("ship", false);
            using var cancellation = new CancellationTokenSource();
            if (failure == "blocked") host.TravelRestriction = "Blocked";
            if (failure == "unloadable") host.Loadable = false;
            if (failure == "cancelled") cancellation.Cancel();
            try { await flow.LaunchMissionAsync("stage", cancellation.Token); Assert.Fail("The rejected travel succeeded."); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OperationCanceledException) { }
            Assert.That(host.Entries, Is.Empty); Assert.That(flow.Phase, Is.EqualTo(GameFlowPhase.Spaceship));
        }
        [Test]
        public async Task ValidEntryDistinguishesNewMissionRetryAndResume()
        {
            var host = new Host(); using var flow = new GameFlowController(host, new GameFlowRoutes("menu", "ship"));
            flow.AdoptSynchronizedScene("ship", false);
            await flow.LaunchMissionAsync("stage"); await flow.RetryMissionAsync("stage"); await flow.ResumeMissionAsync("stage");
            await flow.RestartMissionAsync();
            Assert.That(host.Entries, Is.EqualTo(new[] { MissionEntryKind.NewMission, MissionEntryKind.Retry, MissionEntryKind.Resume, MissionEntryKind.NewMission }));
        }
    }
}
