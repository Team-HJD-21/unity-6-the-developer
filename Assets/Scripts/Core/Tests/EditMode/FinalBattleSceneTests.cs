// 도착 명단의 불변성·혼합 Scene 저장 차단·실제 Native Scene/부모/위치 원복을 확인합니다.
// 임시 Scene은 이 검사에서 만든 것만 닫으며 현재 사용자의 Scene·미저장 상태는 보존합니다.
using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class FinalBattleSceneTests
    {
        [Test]
        public void ReadyArrivalRosterIsCopiedAndRejectsEmptyOrDuplicateIdentities()
        {
            var players = new List<PlayerId> { new("host") };
            var ready = new FinalBattleSceneReady(new MatchId("match"), new EntityId("boss"), players);
            players.Clear();
            Assert.That(ready.ArrivedPlayers.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new FinalBattleSceneReady(default, new EntityId("boss"), ready.ArrivedPlayers));
            Assert.Throws<ArgumentException>(() => new FinalBattleSceneReady(new MatchId("match"), default, ready.ArrivedPlayers));
            Assert.Throws<ArgumentException>(() => new FinalBattleSceneReady(new MatchId("match"), new EntityId("boss"), new[] { new PlayerId("host"), new PlayerId("host") }));
        }

        private sealed class Timestamp : ITimestampSource
        {
            public bool TryReadSeconds(out double seconds) { seconds = 0; return true; }
        }

        [Test]
        public void SceneTransactionMayBlockRestoreWithoutDisablingTheWholeMatch()
        {
            var config = new MatchConfig(new MatchId("match"), MatchMode.Story, new StageId("stage-1"), new DefinitionId("normal"), "v1", 1,
                new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
            using var session = new MatchSessionFactory().Create(config, new MatchState(config.MatchId, MatchPhase.Preparing), new StoryModeRules());
            session.Start();
            using var time = new MatchTimeService(session, new Timestamp(), () => true);
            bool transactionOpen = true;
            var runtime = new MatchCheckpointRuntime(session, time, () => true, () => true, default, () => { }, () => { }, _ => { },
                () => !transactionOpen, () => !transactionOpen);
            Assert.That(runtime.CanCapture, Is.False);
            Assert.That(runtime.CanRestore, Is.False);
            Assert.That(session.IsGameplayRunning, Is.True);
            transactionOpen = false;
            Assert.That(runtime.CanCapture, Is.True);
            Assert.That(runtime.CanRestore, Is.True);
        }

        [UnityTest]
        public IEnumerator ActorRollbackRestoresOriginalSceneParentAndPoseWithoutReloadingTheUserScene()
        {
            yield return new EnterPlayMode();
            var original = SceneManager.GetActiveScene();
            var target = SceneManager.CreateScene("Owned final rollback target " + Guid.NewGuid());
            GameObject parent = null;
            try
            {
                parent = new GameObject("Owned QA parent");
                SceneManager.MoveGameObjectToScene(parent, original);
                var actor = new GameObject("Owned QA actor");
                SceneManager.MoveGameObjectToScene(actor, original);
                actor.transform.SetParent(parent.transform, false);
                actor.transform.position = new Vector3(3, 4, 0);
                actor.transform.localScale = new Vector3(2, 2, 1);
                var type = typeof(InitialStageCompositionRoot).Assembly.GetType("TeamHJD.Game.Bootstrap.SceneComposition.FinalBattleActorSnapshot");
                var backup = Activator.CreateInstance(type, new object[] { actor });
                actor.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(actor, target);
                actor.transform.position = new Vector3(100, 200, 0);
                type.GetMethod("Restore").Invoke(backup, null);
                Assert.That(actor.scene, Is.EqualTo(original));
                Assert.That(actor.transform.parent, Is.SameAs(parent.transform));
                Assert.That(Vector3.Distance(actor.transform.position, new Vector3(3, 4, 0)), Is.LessThan(.0001f));
                Assert.That(actor.transform.localScale, Is.EqualTo(new Vector3(2, 2, 1)));
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                if (parent != null) UnityEngine.Object.Destroy(parent);
            }
            yield return SceneManager.UnloadSceneAsync(target);
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator StopOwnedPlayAfterFailure()
        {
            if (UnityEditor.EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
