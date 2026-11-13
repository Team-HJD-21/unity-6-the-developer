using System.Collections;
using System.Linq;
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
        var slots = hud.GetComponentsInChildren<PlayerWeaponSlotView>(true).OrderBy(slot => slot.SlotNumber).ToArray();
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
            // TMP는 새 atlas의 문자가 처음 보일 때 자체 SubMeshUI를 만듭니다. 제작 UI 계층과 구분합니다.
            var authoredObjects = hudObject.GetComponentsInChildren<Transform>(true)
                .Where(t => t.GetComponent<TMPro.TMP_SubMeshUI>() == null).ToArray();
            // 소유자는 LateUpdate에서 연결됩니다. 테스트 코루틴의 재개 시점과 프레임 순서에 의존하지 않습니다.
            float ownerDeadline = Time.realtimeSinceStartup + 1;
            while (weaponHud.Owner != selection && Time.realtimeSinceStartup < ownerDeadline) yield return null;
            Assert.That(weaponHud.Owner, Is.EqualTo(selection));

            // 선택 카드가 앞에 그려지도록 sibling을 옮겨도 슬롯 번호/버튼 계약은 유지합니다.
            var slots = weaponHud.GetComponentsInChildren<PlayerWeaponSlotView>(true).OrderBy(slot => slot.SlotNumber).ToArray();
            slots[0].GetComponent<Button>().onClick.Invoke();
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            var marker = (GameObject)new SerializedObject(slots[0]).FindProperty("_selectedMarker").objectReferenceValue;
            Assert.That(marker.activeSelf, Is.True);

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
            Assert.That(System.Convert.ToDouble(cooldown.GetValue(attack)), Is.EqualTo(nextShot));

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
            // 닫히는 반원의 표시/입력 차단이 끝난 뒤 다음 장비 선택을 허용합니다.
            float closeDeadline = Time.realtimeSinceStartup + 1;
            while (skillHud.IsOpen && Time.realtimeSinceStartup < closeDeadline) yield return null;
            Assert.That(skillHud.IsOpen, Is.False);

            // Missing equipment is skipped rather than leaving an unusable slot equipped.
            var data = new SerializedObject(selection);
            data.FindProperty("_sniperBulletPrefab").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(selection.TrySelectWeaponSlot(3), Is.True);
            Assert.That(selection.SelectAdjacentWeapon(1), Is.True);
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            yield return null;
            Assert.That(slots[3].GetComponent<Button>().interactable, Is.False);
            var currentObjects = hudObject.GetComponentsInChildren<Transform>(true)
                .Where(t => t.GetComponent<TMPro.TMP_SubMeshUI>() == null).ToArray();
            CollectionAssert.AreEquivalent(authoredObjects, currentObjects,
                "Weapon changes must preserve the authored UI objects; only TMP's own font submeshes are excluded.");

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
