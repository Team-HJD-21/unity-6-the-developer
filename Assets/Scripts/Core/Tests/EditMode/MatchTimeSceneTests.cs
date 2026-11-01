// 실제 Initial_Stage의 Host Match에 공용 시계·예약이 주입되고 종료 시 차단되는지 확인합니다.
// Scene 외형·2인 필드 정지·Weather 전체 이관은 해당 기능의 별도 인수입니다.
using System;
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchTimeSceneTests
    {
        private sealed class TimerProbe
        {
            public bool Fired;
            public void OnTick(MatchTimerTick tick) => Fired = true;
        }
        private SceneSetup[] _setup;
        [SetUp] public void RememberScenes()
        { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var app = Object.FindAnyObjectByType<AppRoot>();
            if (app != null) app.EndCurrentMatch();
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
            if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }

        [UnityTest]
        public IEnumerator AuthoritySceneUsesSharedTimeAndCompletionRejectsFutureReservations()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
            yield return new EnterPlayMode(); yield return null;
            var scene = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
            var app = Object.FindAnyObjectByType<AppRoot>();
            float deadline = Time.realtimeSinceStartup + 10;
            while (scene.MatchTime == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(scene.MatchTime, Is.Not.Null);
            Assert.That(scene.MatchTime.IsAvailable, Is.True);
            Assert.That(scene.PendingMatchTimers, Is.GreaterThanOrEqualTo(1), "The authored Weather interval must use the shared scheduler.");
            Assert.That(app.NetworkSession.IsServer, Is.True);
            double before = scene.MatchTime.ElapsedSeconds;
            var probe = new TimerProbe();
            scene.MatchTimers.Schedule(0.03, probe.OnTick);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(probe.Fired, Is.True);
            Assert.That(scene.MatchTime.ElapsedSeconds, Is.GreaterThan(before));
            Assert.That(scene.MatchCoordinator.ElapsedSeconds, Is.EqualTo(scene.MatchTime.ElapsedSeconds));
            var timers = scene.MatchTimers;
            timers.Schedule(10, _ => Assert.Fail("Completed gameplay must not run an old reservation."));
            app.CompleteCurrentMatch(MatchOutcome.Defeat, 1000);
            Assert.That(scene.MatchTime.Snapshot.IsPaused, Is.True);
            Assert.That(scene.PendingMatchTimers, Is.Zero);
            AssertClosed(timers);
        }

        // EnterPlayMode를 넘는 iterator 안에서 지역 변수를 람다로 캡처하지 않습니다.
        private static void AssertClosed(IMatchScheduler timers)
            => Assert.Throws<OperationCanceledException>(() => timers.Schedule(0, _ => { }));
    }
}
