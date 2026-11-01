// 실제 PlayMode 물리 프레임에서 정지 중 복원한 위치·속도가 재개 후 유지되는지 검사합니다.
// 빈 Scene을 사용하며 종료 시 원래 Editor Scene 구성을 복원합니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CheckpointPhysicsPlayTests
    {
        private sealed class Timestamp : ITimestampSource
        { public bool TryReadSeconds(out double seconds) { seconds = Time.unscaledTimeAsDouble; return true; } }
        private SceneSetup[] _setup;
        private MatchClock _clock;
        [SetUp] public void RememberScene()
        { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
        [UnityTearDown]
        public IEnumerator RestoreScene()
        {
            _clock?.Dispose(); _clock = null;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
            if (_setup != null && _setup.Any(scene => scene.isLoaded) && _setup.Count(scene => scene.isActive) == 1)
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }

        [UnityTest]
        public IEnumerator RestoredBodyStaysHeldThenContinuesWithSavedVelocity()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _clock = new MatchClock(new MatchId("physics-restore"), new Timestamp());
            var actor = new GameObject("checkpoint play probe");
            var body = actor.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
            body.linearVelocity = new Vector2(8, 0);
            var field = FieldSimulationActor.Attach(actor, _clock);
            _clock.SetPaused(true); field.Synchronize();
            var target = new CheckpointRigidbody2DState(new Vector2(20, 10), 15, new Vector2(2, 0), 0, true, false);
            field.RestoreBody(body, target);
            double end = Time.realtimeSinceStartupAsDouble + 0.08;
            while (Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(body.position, Is.EqualTo(new Vector2(20, 10)));
            Assert.That(body.simulated, Is.False);
            _clock.SetPaused(false); field.Synchronize();
            double nextPhysics = Time.fixedTimeAsDouble + 0.04;
            double timeout = Time.realtimeSinceStartupAsDouble + 3;
            while (Time.fixedTimeAsDouble < nextPhysics && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            Assert.That(Time.fixedTimeAsDouble, Is.GreaterThanOrEqualTo(nextPhysics));
            Assert.That(body.position.x, Is.GreaterThan(20));
            Assert.That(body.position.y, Is.EqualTo(10).Within(0.001));
            Assert.That(body.linearVelocity.x, Is.EqualTo(2).Within(0.001));
            Assert.That(body.simulated, Is.True);
        }
    }
}
