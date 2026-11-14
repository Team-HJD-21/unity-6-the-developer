using System;
using System.IO;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

public sealed class PlayerWeaponAmmoTests
{
    [TestCase(WeaponType.DefaultGun, 15)]
    [TestCase(WeaponType.CryoBlaster, 15)]
    [TestCase(WeaponType.Sniper, 5)]
    public void EmptyMagazinePreventsFurtherShots(WeaponType weapon, int capacity)
    {
        var ammo = new PlayerWeaponAmmoState();
        Assert.That(ammo.GetRounds(weapon), Is.EqualTo(capacity));
        for (int i = 0; i < capacity; i++) Assert.That(ammo.TryConsumeRound(weapon), Is.True);
        Assert.That(ammo.TryConsumeRound(weapon), Is.False);
        Assert.That(ammo.GetRounds(weapon), Is.Zero);
    }

    [Test]
    public void WeaponsKeepIndependentRoundsAndSpannerNeverRunsOut()
    {
        var ammo = new PlayerWeaponAmmoState();
        for (int i = 0; i < 3; i++) ammo.TryConsumeRound(WeaponType.DefaultGun);
        ammo.TryConsumeRound(WeaponType.CryoBlaster);
        ammo.TryConsumeRound(WeaponType.Sniper);
        for (int i = 0; i < 100; i++) Assert.That(ammo.TryConsumeRound(WeaponType.Spanner), Is.True);
        Assert.That(ammo.GetRounds(WeaponType.DefaultGun), Is.EqualTo(12));
        Assert.That(ammo.GetRounds(WeaponType.CryoBlaster), Is.EqualTo(14));
        Assert.That(ammo.GetRounds(WeaponType.Sniper), Is.EqualTo(4));
        Assert.That(ammo.TryStartReload(WeaponType.Spanner, 1.5), Is.False);
    }

    [Test]
    public void ReloadFillsOnlyItsMagazineAfterFullDurationAndBlocksShooting()
    {
        var ammo = new PlayerWeaponAmmoState();
        ammo.TryConsumeRound(WeaponType.DefaultGun);
        ammo.TryConsumeRound(WeaponType.Sniper);
        Assert.That(ammo.TryStartReload(WeaponType.DefaultGun, 1.5), Is.True);
        Assert.That(ammo.TryConsumeRound(WeaponType.DefaultGun), Is.False);
        Assert.That(ammo.TryStartReload(WeaponType.Sniper, 1.5), Is.False);
        ammo.AdvanceReload(.75);
        Assert.That(ammo.ReloadProgress, Is.EqualTo(.5f));
        Assert.That(ammo.GetRounds(WeaponType.DefaultGun), Is.EqualTo(14));
        ammo.AdvanceReload(.75);
        Assert.That(ammo.IsReloading, Is.False);
        Assert.That(ammo.GetRounds(WeaponType.DefaultGun), Is.EqualTo(15));
        Assert.That(ammo.GetRounds(WeaponType.Sniper), Is.EqualTo(4));
        Assert.That(ammo.TryStartReload(WeaponType.DefaultGun, 1.5), Is.False);
    }

    [Test]
    public void CancelledReloadDoesNotRefillOrResetOtherMagazines()
    {
        var ammo = new PlayerWeaponAmmoState();
        ammo.TryConsumeRound(WeaponType.Sniper);
        ammo.TryStartReload(WeaponType.Sniper, 1.5);
        ammo.AdvanceReload(.5);
        ammo.CancelReload();
        ammo.AdvanceReload(10);
        Assert.That(ammo.GetRounds(WeaponType.Sniper), Is.EqualTo(4));
        Assert.That(ammo.CanFire(WeaponType.Sniper), Is.True);
        Assert.That(ammo.TryStartReload(WeaponType.Sniper, 1.5), Is.True);
        ammo.AdvanceReload(.5);
        Assert.That(ammo.IsReloading, Is.True);
    }

    [Test]
    public void PausedOrInvalidDeltaDoesNotCompleteReload()
    {
        var ammo = new PlayerWeaponAmmoState();
        ammo.TryConsumeRound(WeaponType.CryoBlaster);
        ammo.TryStartReload(WeaponType.CryoBlaster, 1.5);
        foreach (double delta in new[] { 0d, -1d, double.NaN, double.PositiveInfinity }) ammo.AdvanceReload(delta);
        Assert.That(ammo.ReloadProgress, Is.Zero);
        ammo.AdvanceReload(5);
        Assert.That(ammo.GetRounds(WeaponType.CryoBlaster), Is.EqualTo(15));
    }

    [Test]
    public void CheckpointRoundTripRestoresAmmoAndUnfinishedReload()
    {
        var original = new PlayerWeaponAmmoState();
        original.TryConsumeRound(WeaponType.DefaultGun);
        original.TryConsumeRound(WeaponType.Sniper);
        original.TryStartReload(WeaponType.Sniper, 1.5);
        original.AdvanceReload(.5);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) original.CaptureCheckpoint().Write(writer);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        var restored = new PlayerWeaponAmmoState();
        restored.RestoreCheckpoint(PlayerWeaponAmmoState.CheckpointState.Read(reader));
        Assert.That(restored.GetRounds(WeaponType.DefaultGun), Is.EqualTo(14));
        Assert.That(restored.GetRounds(WeaponType.Sniper), Is.EqualTo(4));
        restored.AdvanceReload(.99);
        Assert.That(restored.IsReloading, Is.True);
        restored.AdvanceReload(.02);
        Assert.That(restored.GetRounds(WeaponType.Sniper), Is.EqualTo(5));
    }

    [Test]
    public void InvalidCheckpointIsRejectedWithoutChangingRounds()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(1000); writer.Write(15); writer.Write(0); writer.Write(5);
            writer.Write((int)WeaponType.DefaultGun); writer.Write(0d); writer.Write(0d);
        }
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        var ammo = new PlayerWeaponAmmoState();
        ammo.TryConsumeRound(WeaponType.DefaultGun);
        Assert.Throws<InvalidDataException>(() => ammo.RestoreCheckpoint(PlayerWeaponAmmoState.CheckpointState.Read(reader)));
        Assert.That(ammo.GetRounds(WeaponType.DefaultGun), Is.EqualTo(14));
    }

#if UNITY_EDITOR
    [Test]
    public void CryoHudShowsPercentageAndRefreshesWhenEqualCapacityWeaponsSwitch()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var player = new GameObject("Cryo percentage test player");
        GameObject hud = null;
        try
        {
            var ammo = player.AddComponent<PlayerWeaponAmmo>();
            var selection = player.GetComponent<PlayerWeaponSelection>();
            typeof(PlayerWeaponSelection).GetMethod("Awake", flags).Invoke(selection, null);
            typeof(PlayerWeaponAmmo).GetMethod("Awake", flags).Invoke(ammo, null);
            hud = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab"));
            var weaponHud = hud.GetComponentInChildren<PlayerWeaponHud>(true);
            typeof(PlayerWeaponHud).GetField("_owner", flags).SetValue(weaponHud, selection);
            var view = hud.GetComponentInChildren<PlayerWeaponAmmoView>(true);
            var label = (TMPro.TMP_Text)new SerializedObject(view).FindProperty("_ammoLabel").objectReferenceValue;
            var refresh = typeof(PlayerWeaponAmmoView).GetMethod("LateUpdate", flags);
            var consume = typeof(PlayerWeaponAmmo).GetMethod("TryConsumeRound", flags);
            var weapon = typeof(PlayerWeaponSelection).GetField("_currentWeapon", flags);
            refresh.Invoke(view, null); Assert.That(label.text, Is.EqualTo("15/15"));
            weapon.SetValue(selection, WeaponType.CryoBlaster);
            refresh.Invoke(view, null); Assert.That(label.text, Is.EqualTo("100%"));
            consume.Invoke(ammo, new object[] { WeaponType.CryoBlaster });
            refresh.Invoke(view, null); Assert.That(label.text, Is.EqualTo("94%"));
            weapon.SetValue(selection, WeaponType.DefaultGun);
            consume.Invoke(ammo, new object[] { WeaponType.DefaultGun });
            refresh.Invoke(view, null); Assert.That(label.text, Is.EqualTo("14/15"));
            weapon.SetValue(selection, WeaponType.CryoBlaster);
            refresh.Invoke(view, null); Assert.That(label.text, Is.EqualTo("94%"));
            for (int i = 0; i < 14; i++) consume.Invoke(ammo, new object[] { WeaponType.CryoBlaster });
            Assert.That(ammo.CurrentPercentage, Is.Zero);
        }
        finally
        {
            if (hud != null) UnityEngine.Object.DestroyImmediate(hud);
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    [TestCase(WeaponType.DefaultGun)] [TestCase(WeaponType.CryoBlaster)] [TestCase(WeaponType.Sniper)]
    public void AuthoredAndNewPlayerReloadInHalfThePreviousTime(WeaponType weapon)
    {
        var player = new GameObject("Reload duration test player");
        try
        {
            Assert.That(player.AddComponent<PlayerWeaponAmmo>().GetReloadDuration(weapon), Is.EqualTo(.75f));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Assert.That(prefab.GetComponent<PlayerWeaponAmmo>().GetReloadDuration(weapon), Is.EqualTo(.75f));
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [TestCase(WeaponType.DefaultGun, 15)]
    [TestCase(WeaponType.CryoBlaster, 15)]
    [TestCase(WeaponType.Sniper, 5)]
    public void LastSuccessfulShotStartsAutomaticReload(WeaponType weapon, int capacity)
    {
        float originalTimeScale = Time.timeScale;
        var player = new GameObject("Automatic reload test player");
        try
        {
            Time.timeScale = 1f;
            var ammo = player.AddComponent<PlayerWeaponAmmo>();
            var selection = player.GetComponent<PlayerWeaponSelection>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerWeaponSelection).GetField("_currentWeapon", flags).SetValue(selection, weapon);
            typeof(PlayerWeaponSelection).GetMethod("Awake", flags).Invoke(selection, null);
            typeof(PlayerWeaponAmmo).GetMethod("Awake", flags).Invoke(ammo, null);
            var consume = typeof(PlayerWeaponAmmo).GetMethod("TryConsumeRound", flags);
            for (int i = 0; i < capacity - 1; i++)
            {
                Assert.That(consume.Invoke(ammo, new object[] { weapon }), Is.True);
                Assert.That(ammo.IsReloading, Is.False);
            }
            Assert.That(consume.Invoke(ammo, new object[] { weapon }), Is.True);
            Assert.That(ammo.CurrentRounds, Is.Zero);
            Assert.That(ammo.IsReloading, Is.True);
            Assert.That(consume.Invoke(ammo, new object[] { weapon }), Is.False);
        }
        finally
        {
            try { UnityEngine.Object.DestroyImmediate(player); }
            finally { Time.timeScale = originalTimeScale; }
        }
    }

    [Test]
    public void PlayerAndHudHaveAuthoredAmmoComponents()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(player.GetComponent<PlayerWeaponAmmo>(), Is.Not.Null);
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        var view = hud.GetComponentInChildren<PlayerWeaponAmmoView>(true);
        Assert.That(view, Is.Not.Null);
        var serialized = new SerializedObject(view);
        Assert.That(serialized.FindProperty("_weaponHud").objectReferenceValue,
            Is.EqualTo(hud.GetComponentInChildren<PlayerWeaponHud>(true)));
        foreach (string property in new[] { "_weaponHud", "_ammoLabel", "_reloadIndicator", "_reloadArrows", "_reloadLabel" })
            Assert.That(serialized.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
    }
#endif
}
