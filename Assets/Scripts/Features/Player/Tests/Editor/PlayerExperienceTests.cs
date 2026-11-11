using System;
using NUnit.Framework;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
#endif

public sealed class PlayerExperienceTests
{
    [Test]
    public void TenKillsAtTenExperienceAdvanceOneLevel()
    {
        var state = new PlayerExperienceState();
        for (int i = 0; i < 9; i++) Assert.That(state.AddExperience(10), Is.True);
        Assert.That(state.Level, Is.EqualTo(1));
        Assert.That(state.ExperienceInLevel, Is.EqualTo(90));
        Assert.That(state.Progress, Is.EqualTo(.9f).Within(.0001f));
        state.AddExperience(10);
        Assert.That(state.Level, Is.EqualTo(2));
        Assert.That(state.ExperienceInLevel, Is.Zero);
        Assert.That(state.TotalExperience, Is.EqualTo(100));
    }

    [Test]
    public void LargeRewardPreservesRemainderAcrossSeveralLevels()
    {
        var state = new PlayerExperienceState();
        state.AddExperience(250);
        Assert.That(state.Level, Is.EqualTo(3));
        Assert.That(state.ExperienceInLevel, Is.EqualTo(50));
        Assert.That(state.RequiredExperience, Is.EqualTo(100));
        Assert.That(state.Progress, Is.EqualTo(.5f));
    }

    [Test]
    public void ConfiguredGrowthChangesEachLevelRequirement()
    {
        var state = new PlayerExperienceState(100, 50);
        state.AddExperience(249);
        Assert.That(state.Level, Is.EqualTo(2));
        Assert.That(state.ExperienceInLevel, Is.EqualTo(149));
        Assert.That(state.RequiredExperience, Is.EqualTo(150));
        state.AddExperience(1);
        Assert.That(state.Level, Is.EqualTo(3));
        Assert.That(state.ExperienceInLevel, Is.Zero);
        Assert.That(state.RequiredExperience, Is.EqualTo(200));
    }

    [Test]
    public void InvalidAwardsDoNotRemoveExperience()
    {
        var state = new PlayerExperienceState();
        state.AddExperience(20);
        Assert.That(state.AddExperience(0), Is.False);
        Assert.That(state.AddExperience(-100), Is.False);
        Assert.That(state.TotalExperience, Is.EqualTo(20));
    }

    [Test]
    public void MaximumLevelClampsHugeRewardWithoutOverflow()
    {
        var state = new PlayerExperienceState(int.MaxValue, int.MaxValue, 1000);
        state.AddExperience(long.MaxValue);
        Assert.That(state.Level, Is.EqualTo(1000));
        Assert.That(state.TotalExperience, Is.EqualTo(state.TotalRequiredForLevel(1000)));
        Assert.That(state.IsMaximumLevel, Is.True);
        Assert.That(state.Progress, Is.EqualTo(1f));
        Assert.That(state.RequiredExperience, Is.Zero);
        Assert.That(state.AddExperience(10), Is.False);
    }

    [Test]
    public void RestoringTotalRecomputesLevelAndRejectsInvalidStateAtomically()
    {
        var state = new PlayerExperienceState();
        state.RestoreTotalExperience(350);
        Assert.That(state.Level, Is.EqualTo(4));
        Assert.That(state.ExperienceInLevel, Is.EqualTo(50));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.RestoreTotalExperience(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.RestoreTotalExperience(long.MaxValue));
        Assert.That(state.TotalExperience, Is.EqualTo(350));
    }

    [TestCase(0, 0, 100)]
    [TestCase(100, -1, 100)]
    [TestCase(100, 0, 1)]
    [TestCase(100, 0, 1001)]
    public void InvalidCurveIsRejected(int first, int increase, int maximum)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerExperienceState(first, increase, maximum));

#if UNITY_EDITOR
    [Test]
    public void ConfirmedEnemyDeathRewardsAllFieldPlayersOnce()
    {
        WithLocalPlayers((first, second, enemy, health) =>
        {
            health.TakeDamage(health.MaxHealth);
            health.TakeDamage(health.MaxHealth);
            Assert.That(first.TotalExperience, Is.EqualTo(10));
            Assert.That(second.TotalExperience, Is.EqualTo(10));
        });
    }

    [Test]
    public void RemovalOfLivingEnemyDoesNotRewardExperience()
    {
        WithLocalPlayers((first, second, enemy, health) =>
        {
            enemy.SetActive(false);
            UnityEngine.Object.DestroyImmediate(enemy);
            Assert.That(first.TotalExperience, Is.Zero);
            Assert.That(second.TotalExperience, Is.Zero);
        });
    }

    [Test]
    public void PerEnemyRewardCanBeReplacedWithoutChangingPlayerLogic()
    {
        WithLocalPlayers((first, second, enemy, health) =>
        {
            typeof(PlayerEnemyExperienceReward).GetField("_experienceReward", Private).SetValue(enemy.GetComponent<PlayerEnemyExperienceReward>(), 250);
            health.TakeDamage(health.MaxHealth);
            Assert.That(first.Level, Is.EqualTo(3));
            Assert.That(first.ExperienceInLevel, Is.EqualTo(50));
            Assert.That(second.TotalExperience, Is.EqualTo(250));
        });
    }

    [Test]
    public void DisabledPlayerDoesNotReceiveSharedExperience()
    {
        WithLocalPlayers((first, second, enemy, health) =>
        {
            second.gameObject.SetActive(false);
            Invoke(second, "OnDisable");
            health.TakeDamage(health.MaxHealth);
            Assert.That(first.TotalExperience, Is.EqualTo(10));
            Assert.That(second.TotalExperience, Is.Zero);
        });
    }

    [Test]
    public void ExperienceUsesProgressionCheckpointPolicy()
    {
        WithLocalPlayers((first, second, enemy, health) =>
        {
            var participant = (ICheckpointParticipant)first;
            Assert.That(participant.Kind, Is.EqualTo(CheckpointSectionKind.Progression));
            Assert.That(CheckpointRestorePolicy.Default.ShouldRestore(participant.Kind), Is.False);
        });
    }

    [Test]
    public void PlayerEnemyVariantsAndHudHaveAuthoredExperienceBindings()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(player.GetComponent<PlayerExperience>(), Is.Not.Null);
        foreach (string name in new[] { "EnemyRoot", "MovingEnemy", "MeleeEnemy", "RangedEnemy" })
        {
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Base/{name}.prefab");
            Assert.That(enemy.GetComponents<PlayerEnemyExperienceReward>().Length, Is.EqualTo(1), name);
        }
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab");
        var view = hud.GetComponent<PlayerExperienceView>();
        Assert.That(view, Is.Not.Null);
        var serialized = new SerializedObject(view);
        foreach (string field in new[] { "_vitals", "_levelText", "_experienceText", "_experienceClip" })
            Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
    }

    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

    private static void WithLocalPlayers(Action<PlayerExperience, PlayerExperience, GameObject, EnemyHealth> test)
    {
        float originalTimeScale = Time.timeScale;
        var registry = (HashSet<PlayerExperience>)typeof(PlayerExperience).GetField("ActivePlayers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var previousPlayers = registry.ToArray();
        var objects = new List<GameObject>();
        try
        {
            Time.timeScale = 1f;
            registry.Clear(); // Never award XP to the user's open-scene objects in EditMode tests.
            var firstObject = new GameObject("Experience test player1"); objects.Add(firstObject);
            var secondObject = new GameObject("Experience test player2"); objects.Add(secondObject);
            var first = firstObject.AddComponent<PlayerExperience>();
            var second = secondObject.AddComponent<PlayerExperience>();
            Invoke(first, "OnEnable"); Invoke(second, "OnEnable");
            var enemy = new GameObject("Experience test enemy"); objects.Add(enemy);
            enemy.AddComponent<NetworkObject>();
            var health = enemy.AddComponent<EnemyHealth>();
            var reward = enemy.AddComponent<PlayerEnemyExperienceReward>();
            Invoke(reward, "OnEnable");
            health.Initialize();
            test(first, second, enemy, health);
        }
        finally
        {
            try
            {
                foreach (var item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
            finally
            {
                registry.Clear();
                foreach (var player in previousPlayers) if (player != null) registry.Add(player);
                Time.timeScale = originalTimeScale;
            }
        }
    }
#endif
}
