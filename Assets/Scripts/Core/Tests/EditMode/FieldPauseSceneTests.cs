// 실제 Initial_Stage의 위치 보고가 시계·터렛 쿨다운·필드 물리와 연결되는지 검사합니다.
// 우주선 이동 연출과 실제 원격 Player spawn은 별도 게임 사이클/2인 인수에서 확인합니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class FieldPauseSceneTests
    {
        private SceneSetup[] _setup;
        [SetUp] public void RememberScenes()
        { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var app = Object.FindAnyObjectByType<AppRoot>(); if (app != null) app.EndCurrentMatch();
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
            if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }
        [UnityTest]
        public IEnumerator PresencePausesFieldClockCooldownAndBodiesButNotPlayerOrConnection()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
            yield return new EnterPlayMode(); yield return null;
            var root = Object.FindAnyObjectByType<InitialStageCompositionRoot>();
            float deadline = Time.realtimeSinceStartup + 15;
            while (root.MatchPresence == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(root.MatchPresence, Is.Not.Null);
            var player = root.LocalPlayerActors.Values.Single();
            var presence = player.GetComponent<PlayerMatchPresence>();
            Assert.That(presence.CanReport, Is.True);
            var controller = Object.FindAnyObjectByType<FieldTurretController>();
            Assert.That(controller.FieldTimeSource, Is.SameAs(root.MatchTime));
            controller.TryStartAutoActivation();
            var probe = new GameObject("Field physics probe");
            var body = probe.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.linearVelocity = Vector2.right * 2;
            FieldSimulationActor.Attach(probe, root.MatchTime, root.MatchCoordinator.GameplayCancellation, destroyOnCompletion: true);
            yield return new WaitForSecondsRealtime(.15f);
            presence.Report(MatchPlayerLocation.Spaceship);
            double pausedAt = root.MatchTime.ElapsedSeconds;
            float cooldown = controller.AutoActivationCooldownRemaining;
            yield return new WaitForSecondsRealtime(.15f);
            Vector2 position = body.position;
            Assert.That(body.simulated, Is.False);
            Assert.That(player.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(Object.FindAnyObjectByType<AppRoot>().NetworkSession.IsServer, Is.True);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(root.MatchTime.ElapsedSeconds, Is.EqualTo(pausedAt));
            var anchor = Object.FindAnyObjectByType<NetworkMatchAnchor>();
            Assert.That(anchor.TimeState.TryRead(out _, out var publishedTime), Is.True);
            Assert.That(publishedTime.IsPaused, Is.True);
            Assert.That(publishedTime.GameSecondsAtAnchor, Is.EqualTo(pausedAt));
            Assert.That(controller.AutoActivationCooldownRemaining, Is.EqualTo(cooldown).Within(.001f));
            Assert.That(body.position, Is.EqualTo(position));
            presence.Report(MatchPlayerLocation.Field);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(root.MatchTime.ElapsedSeconds, Is.GreaterThan(pausedAt));
            Assert.That(body.simulated, Is.True);
            Assert.That(body.position.x, Is.GreaterThan(position.x));
            Assert.That(controller.AutoActivationCooldownRemaining, Is.LessThan(cooldown));
            Object.FindAnyObjectByType<AppRoot>().CompleteCurrentMatch(MatchOutcome.Defeat, 1000);
            yield return null; yield return null;
            Assert.That(probe == null, Is.True, "Field projectiles must not survive completed gameplay.");
        }
    }
}
