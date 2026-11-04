using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class PlayerWeaponSelectionTests
{
    private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";

    [Test]
    public void PlayerPrefab_AuthorsSelectionAndPreservesStartingWeaponAndEquipment()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var selection = player.GetComponent<PlayerWeaponSelection>();
        Assert.That(player.GetComponents<PlayerWeaponSelection>().Length, Is.EqualTo(1));
        Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.DefaultGun));
        var data = new SerializedObject(selection);
        foreach (string field in new[] { "_bulletPrefab", "_sniperBulletPrefab", "_spannerPrefab", "_input", "_weaponRig" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        var attack = new SerializedObject(player.GetComponent<PlayerAttack>());
        Assert.That(attack.FindProperty("_selection").objectReferenceValue, Is.EqualTo(selection));
        Assert.That(attack.FindProperty("_currentWeapon"), Is.Null, "Selection state must have a single owner.");
        var visual = new SerializedObject(player.GetComponent<PlayerWeaponVisual>());
        Assert.That(visual.FindProperty("_selection").objectReferenceValue, Is.EqualTo(selection));
    }

    [UnityTest]
    public IEnumerator SelectionRemainsIndependentOfAttackAndNotifiesOnlyActualChanges()
    {
        yield return new EnterPlayMode();
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath),
            new Vector3(16000, 16000), Quaternion.identity);
        try
        {
            player.GetComponent<PlayerInputHandler>().enabled = false;
            var selection = player.GetComponent<PlayerWeaponSelection>();
            var attack = player.GetComponent<PlayerAttack>();
            attack.enabled = false;
            int changes = 0;
            selection.WeaponChanged += _ => changes++;
            Assert.That(selection.TrySelectWeaponSlot(2), Is.True);
            Assert.That(changes, Is.Zero);
            Assert.That(selection.TrySelectWeaponSlot(1), Is.True);
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            Assert.That(selection.Spanner.gameObject.activeSelf, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(selection.TrySelectWeaponSlot(1), Is.True);
            Assert.That(selection.TrySelectWeaponSlot(0), Is.False);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<PlayerSpannerWeapon>(true).Length, Is.EqualTo(1));
            attack.enabled = true;
            selection.enabled = false;
            Assert.That(selection.TrySelectWeaponSlot(2), Is.False);
            Assert.That(attack.TryFire(), Is.False, "Attack cannot use a disabled selection component.");
            Assert.That(changes, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NumberAndTabInputsSwitchThroughSelectionWithoutAttackUpdate()
    {
        yield return new EnterPlayMode();
        Keyboard keyboard = null;
        GameObject player = null;
        try
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath),
                new Vector3(16000, 16000), Quaternion.identity);
            player.GetComponent<PlayerAttack>().enabled = false;
            var selection = player.GetComponent<PlayerWeaponSelection>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            InputSystem.Update();
            yield return null;
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            InputSystem.Update();
            yield return null;
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.DefaultGun));
        }
        finally
        {
            if (player != null) Object.DestroyImmediate(player);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        }
        yield return new ExitPlayMode();
    }
}
