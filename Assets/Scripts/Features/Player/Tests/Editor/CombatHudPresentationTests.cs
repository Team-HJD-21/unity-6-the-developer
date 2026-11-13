// 실제 제작 프리팹의 EventSystem 판정과 Player 장착 경계를 검사합니다. 사람의 수동 입력 검수와 별개입니다.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TeamHJD.Game.Presentation;

public sealed class CombatHudPresentationTests
{
    private GameObject _player, _hud, _events, _camera, _appCanvas;
    private Mouse _mouse;
    private readonly List<InputDevice> _mutedPointers = new();

    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        foreach (var device in _mutedPointers) if (device.added && !device.enabled) InputSystem.EnableDevice(device);
        _mutedPointers.Clear();
        if (_hud != null) Object.DestroyImmediate(_hud);
        if (_appCanvas != null) Object.DestroyImmediate(_appCanvas);
        if (_player != null) Object.DestroyImmediate(_player);
        if (_events != null) Object.DestroyImmediate(_events);
        if (_camera != null) Object.DestroyImmediate(_camera);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void RebuildingMagazinePreservesOneNumericTray()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var builder = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("HudV6PrefabSetup")).First(t => t != null);
        var dock = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("HudAmmoDockSetup")).First(t => t != null);
        var field = builder.GetField("S", flags); var previous = field.GetValue(null);
        try
        {
            var view = root.GetComponentInChildren<CombatPlayerIndicators>(true);
            var tray = view.AmmoRoot; var count = view.RoundCount;
            field.SetValue(null, view.Style);
            builder.GetMethod("HideGraphics", flags).Invoke(null, new object[] { root.GetComponentInChildren<PlayerWeaponHud>(true).transform });
            builder.GetMethod("ConfigureIndicators", flags).Invoke(null, new object[] { root, root.GetComponent<PlayerSkillHud>(), root.GetComponentInChildren<PlayerWeaponHud>(true), (RectTransform)root.transform });
            dock.GetMethod("Apply", flags).Invoke(null, new object[] { root, view.Style });
            dock.GetMethod("Apply", flags).Invoke(null, new object[] { root, view.Style });
            Assert.That(view.AmmoRoot, Is.SameAs(tray));
            Assert.That(view.RoundCount, Is.SameAs(count));
            Assert.That(tray.parent.Cast<Transform>().Count(t => t.name == "Ammo"), Is.EqualTo(1));
            Assert.That(tray.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Bullet ") || t.name.StartsWith("Spent Round ")), Is.False);
            Assert.That(view.RoundCount.enabled && view.InfiniteLabel.enabled, Is.True);
            Assert.That(view.ReloadArrows.GetComponentInChildren<CombatHudSymbol>(true).enabled, Is.True);
            var stack = root.GetComponentInChildren<CombatWeaponStack>(true);
            Assert.That(stack.NumberLabels.All(t => t.enabled) && stack.NumberButtons.All(b => b.targetGraphic.enabled), Is.True);
            Assert.That(stack.NameLabel.enabled, Is.True);
        }
        finally { field.SetValue(null, previous); PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void AuthoredWeaponButtonsHaveRaycastSurfacesAndNoLegacyBackdrop()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        var weapons = root.GetComponentInChildren<PlayerWeaponHud>(true);
        foreach (var symbol in root.GetComponentsInChildren<CombatHudSymbol>(true))
            Assert.That(symbol.GetComponent<CanvasRenderer>(), Is.Not.Null, "Vector symbols require an authored renderer.");
        Assert.That(root.GetComponent<CombatHudIntro>().MaterialTemplate.shader.isSupported, Is.True);
        var strip = root.GetComponentInChildren<CombatWeaponStack>(true);
        var magazine = root.GetComponentInChildren<CombatPlayerIndicators>(true);
        Assert.That(magazine.AmmoRoot.parent, Is.SameAs(strip.PanelRect), "Magazine must inherit the weapon panel's fold and scale.");
        Assert.That(root.GetComponent<CombatHudScaleBinding>().Elements.Any(e => e.Target == magazine.AmmoRoot), Is.False,
            "An already parented magazine must not receive HUD scaling twice.");
        Assert.That(strip.PanelRect.Find("Surface").GetComponent<Image>().raycastTarget, Is.True, "The magazine background must block shooting through UI.");
        Assert.That(magazine.AmmoRoot.anchoredPosition.x - magazine.AmmoRoot.rect.width * .5f,
            Is.GreaterThan(((RectTransform)strip.Slots[0].transform).rect.width));
        Assert.That(magazine.RoundCount.raycastTarget, Is.False);
        Assert.That(magazine.ReloadArrows.GetComponentsInChildren<CombatHudSymbol>(true).Length, Is.EqualTo(1));
        Assert.That(strip.NameGroup.blocksRaycasts, Is.False);
        Assert.That(string.Concat(strip.WeaponNames).All(c => strip.NameLabel.font.HasCharacter(c)), Is.True);
        var cursor = root.GetComponent<CombatFieldCursor>().ViewPrefab;
        Assert.That(cursor.GetComponent<GraphicRaycaster>(), Is.Null, "The cursor cannot intercept field clicks.");
        Assert.That(cursor.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
        Assert.That(strip.NumberButtons.Length, Is.EqualTo(4));
        Assert.That(strip.NumberButtons.All(b => b.navigation.mode == Navigation.Mode.None), Is.True,
            "World movement keys must not navigate the weapon number strip.");
        foreach (var graphic in weapons.GetComponents<Graphic>()) Assert.That(graphic.enabled, Is.False);
        foreach (var slot in weapons.GetComponentsInChildren<PlayerWeaponSlotView>(true))
        {
            var target = slot.GetComponent<Button>().targetGraphic;
            Assert.That(target.enabled && target.raycastTarget, Is.True, slot.name);
            Assert.That(slot.GetComponent<CanvasGroup>(), Is.Not.Null);
        }
        var picker = root.GetComponentInChildren<CombatSkillPicker>(true);
        Assert.That(picker.PickerRect.pivot, Is.EqualTo(new Vector2(.5f, 0)));
        Assert.That(picker.QNeedle.pivot.y, Is.Zero);
        Assert.That(picker.ENeedle.parent, Is.SameAs(picker.QNeedle.parent));
        Assert.That(picker.QNeedleLabel.transform.parent, Is.SameAs(picker.QKeyBadge));
        Assert.That(picker.ENeedleLabel.transform.parent, Is.SameAs(picker.EKeyBadge));
        Assert.That(picker.QKeyBadge.GetSiblingIndex(), Is.GreaterThan(picker.ENeedle.GetSiblingIndex()),
            "Neither crossing stem may draw through the Q/E key faces.");
        foreach (var slot in root.GetComponentsInChildren<CombatSkillSlotFeedback>(true))
        {
            var mask = slot.transform.Find("Whole Slot Cooldown").GetComponent<Mask>();
            var fill = mask.GetComponentsInChildren<Image>(true).Single(i => i.transform != mask.transform);
            Assert.That(mask.showMaskGraphic, Is.False);
            Assert.That(mask.graphic, Is.TypeOf<CombatHudShape>(), "Soft sprite shadows must not expand the cooldown stencil into a rectangle.");
            Assert.That(((RectTransform)mask.transform).anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(((RectTransform)mask.transform).anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(slot.Overlay.raycastTarget, Is.False);
            Assert.That(fill.sprite, Is.Not.Null);
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(fill.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(fill.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero), "Cooldown must span the entire card, not its artwork inset.");
        }
    }

    [UnityTest]
    public IEnumerator AuthoredSectorsHoverPreviewSwapCancelAndControlLossPreserveTheRealLoadout()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        // 도메인 재로드 뒤 새 iterator에서 캡처를 만듭니다. 재로드 전 display-class를 이어 쓰지 않습니다.
        yield return ExercisePresentation();
    }

    private IEnumerator ExercisePresentation()
    {
        _mouse = InputSystem.AddDevice<Mouse>();
        // This automated fixture owns one pointer. Desktop activity must not replace it mid-hover.
        foreach (var pointer in InputSystem.devices.OfType<Mouse>().Where(m => m != _mouse && m.enabled).ToArray())
        { _mutedPointers.Add(pointer); InputSystem.DisableDevice(pointer); }
        _camera = new GameObject("HUD test camera", typeof(Camera));
        _camera.tag = "MainCamera";
        _camera.transform.position = new Vector3(10000, 10000, -10);
        _camera.GetComponent<Camera>().orthographic = true;
        _events = new GameObject("HUD test EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),
            new Vector3(10000, 10000), Quaternion.identity);
        _player.GetComponent<PlayerInputHandler>().enabled = false;
        _hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab"));
        _hud.GetComponent<PlayerSkillHudSceneBinding>().enabled = false;
        var hud = _hud.GetComponent<PlayerSkillHud>();
        _appCanvas = Object.Instantiate(Resources.Load<AppScreenStyle>("Core/AppScreenStyle").CanvasPrefab).gameObject;
        var alerts = _hud.GetComponent<CombatHudAlertLayout>();
        alerts.View = _appCanvas.GetComponent<AppScreenCanvasView>().Alerts;
        var owner = _player.GetComponent<PlayerSkillController>();
        Assert.That(owner.BindHud(hud), Is.True);
        Assert.That(owner.TryEquip(0, PlayerSkillId.Bomb), Is.True);
        Assert.That(owner.TryEquip(1, PlayerSkillId.EnergyBarrier), Is.True);
        var picker = hud.CombatPicker;
        var selection = _player.GetComponent<PlayerWeaponSelection>();
        var weapons = _hud.GetComponentInChildren<PlayerWeaponHud>();
        yield return Until(() => weapons.Owner == selection);
        yield return Until(() => _hud.GetComponent<CombatHudIntro>().Completed);
        var magazine = _hud.GetComponentInChildren<CombatPlayerIndicators>(true);
        yield return Until(() => magazine.DisplayedRounds == 15);
        Vector2 magazineHome = magazine.AmmoRoot.anchoredPosition;
        var attack = _player.GetComponent<PlayerAttack>();
        Assert.That(attack.TryFire(), Is.True);
        Assert.That(magazine.ShotPulse, Is.GreaterThan(.99f), "Feedback comes from the accepted shot, not a weapon's lower capacity.");
        Assert.That(attack.TryFire(), Is.False, "A second request during shot cooldown is not another shot.");
        yield return Until(() => magazine.DisplayedRounds == 14);
        Assert.That(magazine.RoundCount.text, Does.StartWith("14<size="));
        Assert.That(selection.TrySelectWeapon(WeaponType.Sniper), Is.True);
        Assert.That(magazine.ShotPulse, Is.Zero, "Switching 14→5 rounds must not simulate firing.");
        yield return Until(() => magazine.DisplayedRounds == 5);
        Assert.That(magazine.RoundCount.text, Does.StartWith("5<size="));
        Assert.That(selection.TrySelectWeapon(WeaponType.Spanner), Is.True);
        yield return Until(() => magazine.InfiniteLabel.gameObject.activeSelf);
        Assert.That(magazine.ReloadVisible, Is.False);
        Assert.That(magazine.RoundCount.gameObject.activeSelf, Is.False);
        Assert.That(selection.TrySelectWeapon(WeaponType.DefaultGun), Is.True);
        yield return Until(() => magazine.DisplayedRounds == 14);
        _player.transform.position += Vector3.right * 8;
        _camera.transform.position += Vector3.right * 8;
        yield return null;
        Assert.That(magazine.AmmoRoot.anchoredPosition, Is.EqualTo(magazineHome), "The magazine must not follow the Player or camera.");
        var stack = _hud.GetComponentInChildren<CombatWeaponStack>(true);
        foreach (var button in stack.NumberButtons)
        {
            var hits = new List<RaycastResult>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "The selected gun must not cover number clicks.");
        }
        var binding = _hud.GetComponent<CombatHudScaleBinding>(); float originalScale = binding.CurrentScale;
        var settings = new UiDisplaySettings(false); settings.SetHudScale(1.1f); binding.BindUiDisplaySettings(settings);
        Canvas.ForceUpdateCanvases();
        Assert.That(ScreenRect(stack.PanelRect).xMax, Is.LessThan(ScreenRect(picker.PickerRect).xMin), "At 110%, the weapon panel must stay left of the selector.");
        settings.SetHudScale(originalScale);
        owner.ClearSelection();
        int attempts = 0;
        System.Action<int, PlayerSkillId, bool> attempted = (slot, id, accepted) =>
        { Assert.That(slot, Is.Zero); Assert.That(id, Is.EqualTo(PlayerSkillId.None)); Assert.That(accepted, Is.False); attempts++; };
        owner.SlotUseAttempted += attempted;
        Assert.That(owner.TryUseSlot(-1), Is.False);
        _player.GetComponent<PlayerControlState>().SetInputBlocked(true);
        Assert.That(owner.TryUseSlot(0), Is.False);
        Assert.That(attempts, Is.Zero, "Blocked input must neither flash a slot nor produce an unassigned alert.");
        _player.GetComponent<PlayerControlState>().SetInputBlocked(false);
        Assert.That(owner.TryUseSlot(0), Is.False);
        Assert.That(attempts, Is.EqualTo(1));
        var qView = (PlayerSkillSlotView)new SerializedObject(hud).FindProperty("_qSlot").objectReferenceValue;
        var qFeedback = qView.GetComponent<CombatSkillSlotFeedback>();
        Assert.That(qFeedback.Overlay.color.a, Is.GreaterThan(0));
        yield return null;
        yield return Until(() => qFeedback.Overlay.color.a < .001f);
        Assert.That(qFeedback.transform.localScale.x, Is.EqualTo(1).Within(.001f));
        alerts.RefreshLayout();
        Assert.That(ScreenRect((RectTransform)alerts.View.transform).yMin,
            Is.GreaterThanOrEqualTo(ScreenRect(picker.CompactRect).yMax), "Alert must remain just above the compact skills.");
        owner.SlotUseAttempted -= attempted;
        owner.TryEquip(0, PlayerSkillId.Bomb); owner.TryEquip(1, PlayerSkillId.EnergyBarrier);
        yield return null; // indicators follows the weapon owner in its later presentation pass.
        var popups = Object.FindObjectsByType<CombatExperiencePopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(popups.Length, Is.EqualTo(4));
        Assert.That(popups.Any(p => p.gameObject.activeSelf), Is.False);
        Assert.That(_player.GetComponent<PlayerExperience>().AddExperience(10), Is.True);
        Assert.That(popups.Count(p => p.gameObject.activeSelf), Is.EqualTo(1));
        var gained = popups.Single(p => p.gameObject.activeSelf);
        Assert.That(gained.Label.text, Is.EqualTo("+10 XP"));
        Assert.That(gained.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
        yield return Until(() => popups.All(p => !p.gameObject.activeSelf));

        MovePointer(new Vector2(Screen.width * .5f, 8));
        yield return Until(() => picker.Reveal > .99f);
        Canvas.ForceUpdateCanvases();
        // 승인 시안의 네 이미지 중심. 구현의 각도/sector 계산을 기대값으로 재사용하지 않습니다.
        var points = new[] { new Vector2(-227, 131), new Vector2(-100, 242), new Vector2(90, 246), new Vector2(242, 100) };
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, picker.PickerRect.TransformPoint(points[i]));
            Assert.That(TopSector(screen), Is.SameAs(picker.Wedges[i]), "Visual sector must accept its own skill image.");
        }
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, picker.PickerRect.TransformPoint(new Vector2(0, 80)));
        Assert.That(TopSector(center), Is.Null, "The empty recess must not select a skill.");

        var turret = picker.Wedges.Single(w => w.Skill == PlayerSkillId.PlayerTurret);
        MovePointer(RectTransformUtility.WorldToScreenPoint(null, picker.PickerRect.TransformPoint(points[1])));
        // A shared tooltip can already be visible from a different sector before this artwork has lifted.
        yield return Until(() => picker.TooltipGroup.alpha > .99f && turret.Art.localScale.x > 1.04f);
        Assert.That(picker.TooltipTitle.text, Is.EqualTo(PlayerSkillDescriptions.Name(PlayerSkillId.PlayerTurret)));
        Assert.That(turret.Art.localScale.x, Is.GreaterThan(1));
        Assert.That(turret.Grip.alpha, Is.GreaterThan(0));
        alerts.RefreshLayout();
        Assert.That(ScreenRect((RectTransform)alerts.View.transform).Overlaps(ScreenRect(picker.TooltipRect)), Is.False,
            "The open selector description must not cover a text alert.");
        Assert.That(_hud.GetComponentInChildren<PlayerSkillTooltipView>(true).gameObject.activeSelf, Is.False);

        int changes = 0;
        System.Action changed = () => changes++;
        owner.Loadout.Changed += changed;
        Vector2 e = new(Screen.width * .75f, Screen.height * .55f);
        MovePointer(e);
        Assert.That(picker.BeginDrag(PlayerSkillId.PlayerTurret, e), Is.True);
        yield return null;
        Assert.That(turret.Grip.alpha, Is.Zero, "The live drag disk replaces the pickup affordance.");
        alerts.RefreshLayout();
        Assert.That(ScreenRect((RectTransform)alerts.View.transform).Overlaps(ScreenRect(picker.DiskBase.rectTransform)), Is.False);
        Assert.That(ScreenRect((RectTransform)alerts.View.transform).Overlaps(ScreenRect(picker.TooltipRect)), Is.False);
        Assert.That(picker.Destination, Is.EqualTo(1));
        Assert.That(picker.DestinationBadge.activeSelf && !picker.CancelBadge.activeSelf, Is.True);
        Assert.That(owner.Loadout.E, Is.EqualTo(PlayerSkillId.EnergyBarrier), "Preview is not a commit.");
        Assert.That(changes, Is.Zero);
        Assert.That(picker.Release(e), Is.True);
        Assert.That(owner.Loadout.Q, Is.EqualTo(PlayerSkillId.Bomb));
        Assert.That(owner.Loadout.E, Is.EqualTo(PlayerSkillId.PlayerTurret));
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(picker.DiskGroup.alpha, Is.GreaterThan(0), "The successful disk remains for its confirmation fade.");
        Assert.That(_player.GetComponent<PlayerInputHandler>().ResolvePrimaryAttack(true), Is.False);
        yield return Until(() => !hud.IsOpen);
        Assert.That(picker.CompactGroup.alpha, Is.EqualTo(1).Within(.001f));

        hud.Open();
        yield return Until(() => picker.Reveal > .99f);
        Vector2 q = new(Screen.width * .25f, Screen.height * .55f);
        MovePointer(q);
        Assert.That(picker.BeginDrag(PlayerSkillId.PlayerTurret, q), Is.True);
        Assert.That(picker.Release(q), Is.True);
        Assert.That(owner.Loadout.Q, Is.EqualTo(PlayerSkillId.PlayerTurret));
        Assert.That(owner.Loadout.E, Is.EqualTo(PlayerSkillId.Bomb));
        Assert.That(changes, Is.EqualTo(2));
        yield return Until(() => !hud.IsOpen);

        hud.Open();
        yield return Until(() => picker.Reveal > .99f);
        Vector2 outside = new(Screen.width * .5f, Screen.height * .95f);
        MovePointer(outside);
        Assert.That(picker.BeginDrag(PlayerSkillId.LureBeacon, outside), Is.True);
        Assert.That(picker.CancelBadge.activeSelf, Is.True);
        Assert.That(picker.Release(outside), Is.False);
        Assert.That(changes, Is.EqualTo(2));
        Assert.That(picker.DropBand.activeSelf, Is.False);
        Assert.That(picker.DiskGroup.alpha, Is.Zero);

        hud.Open(); MovePointer(e);
        Assert.That(picker.BeginDrag(PlayerSkillId.EnergyBarrier, e), Is.True);
        _player.GetComponent<PlayerControlState>().SetInputBlocked(true);
        yield return Until(() => !hud.IsOpen);
        Assert.That(picker.IsDragging, Is.False);
        Assert.That(changes, Is.EqualTo(2));
        Assert.That(owner.Loadout.Q, Is.EqualTo(PlayerSkillId.PlayerTurret));
        Assert.That(owner.Loadout.E, Is.EqualTo(PlayerSkillId.Bomb));
        owner.Loadout.Changed -= changed;
        hud.Unbind();
        alerts.RefreshLayout();
        Assert.That(((RectTransform)alerts.View.transform).anchoredPosition.y, Is.EqualTo(alerts.View.DefaultBottom).Within(.001f));
        Assert.That(picker.DiskGroup.alpha + picker.TooltipGroup.alpha, Is.Zero);
    }

    private void MovePointer(Vector2 point)
    {
        _mouse.MakeCurrent();
        InputSystem.QueueStateEvent(_mouse, new MouseState { position = point });
        InputSystem.Update();
    }
    private static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }
    private static CombatSkillWedge TopSector(Vector2 point)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        return hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<CombatSkillWedge>() : null;
    }
    private static IEnumerator Until(System.Func<bool> condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        float deadline = Time.realtimeSinceStartup + 2;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, $"HUD transition/source at line {line} did not settle within two seconds.");
    }
}
