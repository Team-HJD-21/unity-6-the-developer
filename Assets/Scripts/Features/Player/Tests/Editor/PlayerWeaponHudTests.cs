using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class PlayerWeaponHudTests
{
    private const string HudPath = "Assets/Prefabs/UI/PlayerSkillHUD.prefab";
    private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";

    [Test]
    public void HudPrefabHasFourAuthoredSlotsInsideTheExistingCanvas()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        var hud = prefab.GetComponentInChildren<PlayerWeaponHud>(true);
        Assert.That(hud, Is.Not.Null);
        Assert.That(hud.IsConfigured, Is.True);
        Assert.That(prefab.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
        var slots = hud.GetComponentsInChildren<PlayerWeaponSlotView>(true);
        Assert.That(slots.Length, Is.EqualTo(4));
        Assert.That(slots[0].Weapon, Is.EqualTo(WeaponType.Spanner));
        Assert.That(slots[1].Weapon, Is.EqualTo(WeaponType.DefaultGun));
        Assert.That(slots[2].Weapon, Is.EqualTo(WeaponType.CryoBlaster));
        Assert.That(slots[3].Weapon, Is.EqualTo(WeaponType.Sniper));
        foreach (var slot in slots)
        {
            Assert.That(slot.GetComponent<Button>(), Is.Not.Null);
            Assert.That(slot.GetComponentInChildren<TMPro.TMP_Text>().font, Is.Not.Null);
        }
    }

    [UnityTest]
    public IEnumerator AuthoredButtonsFollowTheBoundPlayerAndRespectControlLocks()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        GameObject hudObject = null;
        float previousTimeScale = Time.timeScale;
        try
        {
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath),
                new Vector3(12000, 12000), Quaternion.identity);
            player.GetComponent<PlayerInputHandler>().enabled = false;
            hudObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
            hudObject.GetComponent<PlayerSkillHudSceneBinding>().enabled = false;
            var skills = player.GetComponent<PlayerSkillController>();
            var skillHud = hudObject.GetComponent<PlayerSkillHud>();
            var weaponHud = hudObject.GetComponentInChildren<PlayerWeaponHud>();
            var attack = player.GetComponent<PlayerAttack>();
            var selection = player.GetComponent<PlayerWeaponSelection>();
            Assert.That(skills.BindHud(skillHud), Is.True);
            int authoredObjectCount = hudObject.GetComponentsInChildren<Transform>(true).Length;
            yield return null;
            Assert.That(weaponHud.Owner, Is.EqualTo(selection));

            var slots = weaponHud.GetComponentsInChildren<PlayerWeaponSlotView>(true);
            slots[0].GetComponent<Button>().onClick.Invoke();
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            Assert.That(slots[0].transform.Find("Selected Marker").gameObject.activeSelf, Is.True);

            Assert.That(selection.SelectAdjacentWeapon(-1), Is.True);
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Sniper));
            Assert.That(selection.SelectAdjacentWeapon(1), Is.True);
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            Assert.That(selection.TrySelectWeaponSlot(0), Is.False);

            float nextShot = Time.time + 10;
            var cooldown = typeof(PlayerAttack).GetField("_nextShotTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            cooldown.SetValue(attack, nextShot);
            Assert.That(selection.TrySelectWeaponSlot(2), Is.True);
            Assert.That((float)cooldown.GetValue(attack), Is.EqualTo(nextShot));

            var control = player.GetComponent<PlayerControlState>();
            control.SetInputBlocked(true);
            slots[0].GetComponent<Button>().onClick.Invoke();
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.DefaultGun));
            Assert.That(selection.SelectAdjacentWeapon(1), Is.False);
            control.SetInputBlocked(false);

            skillHud.Open();
            Assert.That(selection.TrySelectWeaponSlot(1), Is.False);
            skillHud.Close();
            Time.timeScale = 0;
            Assert.That(selection.TrySelectWeaponSlot(1), Is.False);
            Time.timeScale = previousTimeScale;

            // Missing equipment is skipped rather than leaving an unusable slot equipped.
            var data = new SerializedObject(selection);
            data.FindProperty("_sniperBulletPrefab").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(selection.TrySelectWeaponSlot(3), Is.True);
            Assert.That(selection.SelectAdjacentWeapon(1), Is.True);
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            yield return null;
            Assert.That(slots[3].GetComponent<Button>().interactable, Is.False);
            Assert.That(hudObject.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(authoredObjectCount));

            skillHud.Unbind();
            yield return null;
            Assert.That(weaponHud.Owner, Is.Null);
            Assert.That(weaponHud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            if (hudObject != null) Object.DestroyImmediate(hudObject);
            if (player != null) Object.DestroyImmediate(player);
        }
        yield return new ExitPlayMode();
    }
}
