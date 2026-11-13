// 승인 시안의 계층을 기존 Scene 소유 HUD 프리팹에 제작합니다. 기존 GUID/기능 참조와 로컬 actor binding을 보존합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HudV6PrefabSetup
{
    private static CombatHudStyle S;
    [MenuItem("Tools/TeamHJD/The Developer/HUD v6/Configure authored HUD")]
    public static void Configure()
    {
        S = AssetDatabase.LoadAssetAtPath<CombatHudStyle>(HudV6AssetImport.StylePath);
        if (S == null) throw new InvalidOperationException("Import the approved HUD assets first.");
        ConfigureSkill(); ConfigureVitals(); AssetDatabase.SaveAssets();
        Debug.Log("HUD_V6_PREFABS_CONFIGURED");
    }
    private static void ConfigureSkill()
    {
        const string path = "Assets/Prefabs/UI/PlayerSkillHUD.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            UseIndependentBodyFont(root);
            var canvas = (RectTransform)root.transform;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var hud = root.GetComponent<PlayerSkillHud>();
            RectTransform compactDock = Rect("V6 Compact Dock", canvas, 480, 160, new(.5f, 0), new(.5f, 0)); compactDock.anchoredPosition = Vector2.zero;
            var previousCompact = canvas.Find("V6 Compact"); if (previousCompact != null) previousCompact.SetParent(compactDock, false);
            RectTransform compact = Rect("V6 Compact", compactDock, 480, 160, new(.5f, 0), new(.5f, 0)); compact.anchoredPosition = Vector2.zero;
            var compactGroup = Get<CanvasGroup>(compact);
            var q = Ref<PlayerSkillSlotView>(hud, "_qSlot"); var e = Ref<PlayerSkillSlotView>(hud, "_eSlot");
            SkillSlot(q, compact, -154, "Q"); SkillSlot(e, compact, 9, "E");
            var dash = root.GetComponentInChildren<PlayerDashHud>(true);
            RectTransform dr = (RectTransform)dash.transform; dr.SetParent(compact, false);
            Place(dr, 132, 137, new(.5f, 0), new(.5f, .5f)); dr.anchoredPosition = new Vector2(172, 90.5f);
            HideGraphics(dr);
            var dashGroup = Get<CanvasGroup>(dr); Panel(dr, "skill");
            Picture("Dash", dr, "action_dash", 58, 58, 0, 20);
            var dashFill = Picture("V6 Cooldown", dr, "fill_white", 106, 80, 0, 16); Fill(dashFill);
            var dashKey = Text("V6 Key", dr, "SPACE", 120, 32, 0, -46, 22, S.LabelFont);
            Set(dash, "_group", dashGroup); Set(dash, "_cooldownFill", dashFill); Set(dash, "_status", dashKey);
            SetColor(dash, "_readyColor", S.Text); SetColor(dash, "_blockedColor", new Color(.65f, .68f, .64f));

            var weapon = root.GetComponentInChildren<PlayerWeaponHud>(true);
            var wr = (RectTransform)weapon.transform; Place(wr, 450, 270, Vector2.zero, Vector2.zero); wr.anchoredPosition = Vector2.zero;
            HideGraphics(wr);
            var slots = weapon.GetComponentsInChildren<PlayerWeaponSlotView>(true).OrderBy(s => s.SlotNumber).ToArray();
            var groups = new CanvasGroup[4];
            string[] ids = { "weapon_spanner", "weapon_default", "weapon_cryo", "weapon_sniper" };
            for (int i = 0; i < slots.Length; i++)
            {
                var sr = (RectTransform)slots[i].transform; HideGraphics(sr);
                Place(sr, 292, 84, Vector2.zero, Vector2.zero); sr.anchoredPosition = new Vector2(57 + i * 27, 68 + i * 36);
                groups[i] = Get<CanvasGroup>(sr);
                Panel(sr, "weapon"); var border = Picture("V6 Edge", sr, "weapon_edge_idle", 292, 84, 0, 0); Stretch(border.rectTransform);
                var icon = Picture("V6 Weapon", sr, ids[i], 204, 66, 26, 0); icon.preserveAspect = true;
                Text("V6 Key", sr, (i + 1).ToString(), 47, 55, -119, 0, 29, S.LabelFont);
                var marker = Picture("V6 Selection", sr, "fill_white", 19, 2, -124, -35); marker.color = S.Accent;
                var b = slots[i].GetComponent<Button>(); b.targetGraphic = border; border.raycastTarget = true; ButtonColors(b);
                Set(slots[i], "_button", b); Set(slots[i], "_border", border); Set(slots[i], "_icon", icon); Set(slots[i], "_selectedMarker", marker.gameObject);
                SetColor(slots[i], "_selectedColor", S.Accent); SetColor(slots[i], "_idleColor", Color.white);
            }
            var stack = Get<CombatWeaponStack>(wr); stack.Style = S; stack.Hud = weapon; stack.Slots = slots; stack.Groups = groups;
            var slide = root.GetComponentInChildren<PlayerCombatHudSlideController>(true);
            if (slide != null) { Set(slide, "_weapons", wr); Set(slide, "_dash", compactDock); }

            var picker = Get<CombatSkillPicker>(Rect("V6 Presentation", canvas, 1920, 1080, new(.5f, .5f), new(.5f, .5f)));
            Stretch((RectTransform)picker.transform);
            picker.Style = S; picker.CanvasRect = canvas; picker.CompactRect = compact; picker.CompactGroup = compactGroup;
            picker.PickerRect = Rect("Selector", picker.transform, 720, 360, new(.5f, 0), new(.5f, 0));
            picker.PickerGroup = Get<CanvasGroup>(picker.PickerRect);
            var inner = Picture("Inner Recess", picker.PickerRect, "picker_inner_surface", 720, 360, 0, 0); Bottom(inner.rectTransform, 720, 360);
            picker.Wedges = new CombatSkillWedge[4];
            for (int i = 0; i < 4; i++)
            {
                var r = Rect("Wedge " + i, picker.PickerRect, 720, 360, new(.5f, 0), new(.5f, 0));
                var wedge = Get<CombatSkillWedge>(r); wedge.Picker = picker; wedge.Style = S; wedge.Skill = (PlayerSkillId)(i + 1); wedge.Index = i;
                wedge.Surface = Picture("Surface", r, $"picker_wedge_{i}_surface", 720, 360, 0, 0); Bottom(wedge.Surface.rectTransform, 720, 360); wedge.Surface.raycastTarget = true;
                wedge.Edge = Picture("Edge", r, $"picker_wedge_{i}_edge_idle", 720, 360, 0, 0); Bottom(wedge.Edge.rectTransform, 720, 360);
                Vector2 p = CombatHudGeometry.Radial(262, CombatHudGeometry.SkillAngle(wedge.Skill));
                var art = Art("Art", r, 100, 90); art.transform.SetParent(r, false);
                art.GetComponent<RectTransform>().anchorMin = art.GetComponent<RectTransform>().anchorMax = new Vector2(.5f, 0);
                art.GetComponent<RectTransform>().anchoredPosition = p + new Vector2(0, 10); art.Show(wedge.Skill);
                var label = Text("Name", r, PlayerSkillDescriptions.Name(wedge.Skill), 165, 30, 0, 0, 21, S.BodyFont);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0); label.rectTransform.anchoredPosition = p + new Vector2(0, -52);
                picker.Wedges[i] = wedge;
            }
            var rail = Picture("Mounting Rail", picker.PickerRect, "picker_rail", 720, 360, 0, 0); Bottom(rail.rectTransform, 720, 360);
            picker.QNeedle = Needle("Q Needle", picker.PickerRect); picker.ENeedle = Needle("E Needle", picker.PickerRect);
            picker.QNeedleLabel = Text("Q Marker", picker.PickerRect, "Q", 45, 35, 0, 0, 23, S.LabelFont);
            picker.ENeedleLabel = Text("E Marker", picker.PickerRect, "E", 45, 35, 0, 0, 23, S.LabelFont);
            foreach (var label in new[] { picker.QNeedleLabel, picker.ENeedleLabel }) label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0);

            RectTransform band = Rect("Drop Band", picker.transform, 0, 0, new(.5f, .5f), new(.5f, .5f));
            band.anchorMin = new Vector2(0, 432f / 1080); band.anchorMax = new Vector2(1, 822f / 1080); band.offsetMin = band.offsetMax = Vector2.zero;
            picker.DropBand = band.gameObject;
            picker.LeftGradient = Gradient(band, "Q Field", false); picker.RightGradient = Gradient(band, "E Field", true);
            var qTarget = Text("Q Target", band, "Q", 140, 80, 0, 0, 49, S.LabelFont); qTarget.rectTransform.anchorMin = qTarget.rectTransform.anchorMax = new Vector2(.25f, .5f);
            var eTarget = Text("E Target", band, "E", 140, 80, 0, 0, 49, S.LabelFont); eTarget.rectTransform.anchorMin = eTarget.rectTransform.anchorMax = new Vector2(.7f, .5f);
            picker.QDropLabel = qTarget; picker.EDropLabel = eTarget;
            picker.DropHint = Text("Drop Hint", picker.transform, "", 320, 40, 0, 0, 23, S.BodyFont);
            picker.HoverUnderline = Picture("Hover Underline", picker.transform, "fill_white", 160, 1.4f, 0, 0); picker.HoverUnderline.color = S.Accent; picker.HoverUnderline.enabled = false;
            picker.DiskRect = Rect("Drag Disk", picker.transform, 200, 200, new(.5f, .5f), new(.5f, .5f));
            picker.DiskGroup = Get<CanvasGroup>(picker.DiskRect);
            picker.DiskBase = Picture("Disk Base", picker.DiskRect, "drag_disk_base", 220, 220, 0, 0);
            picker.DiskArt = Art("Disk Skill", picker.DiskRect, 93, 71); picker.DiskArt.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -34);
            var badge = Rect("Destination", picker.DiskRect, 45, 45, new(.5f, .5f), new(.5f, .5f)); badge.anchoredPosition = new Vector2(86, 87); Panel(badge, "effect");
            picker.DestinationBadge = badge.gameObject; picker.DestinationText = Text("Key", badge, "E", 40, 40, 0, 0, 24, S.LabelFont);
            picker.CancelBadge = Picture("Cancel Preview", picker.DiskRect, "drag_cancel", 44, 44, 86, 87).gameObject;
            picker.TooltipRect = Rect("Description", picker.transform, 310, 123, new(.5f, .5f), new(.5f, .5f)); Panel(picker.TooltipRect, "panel");
            picker.TooltipGroup = Get<CanvasGroup>(picker.TooltipRect);
            picker.TooltipTitle = Text("Title", picker.TooltipRect, "", 274, 32, 0, 35, 24, S.BodyFont);
            picker.TooltipBody = Text("Body", picker.TooltipRect, "", 274, 64, 0, -25, 20, S.BodyFont);
            picker.TooltipTitle.alignment = picker.TooltipBody.alignment = TextAlignmentOptions.Left;
            picker.PickerGroup.alpha = picker.DiskGroup.alpha = picker.TooltipGroup.alpha = 0;
            picker.PickerGroup.blocksRaycasts = picker.DiskGroup.blocksRaycasts = picker.TooltipGroup.blocksRaycasts = false;
            band.gameObject.SetActive(false);
            Set(hud, "_combatPicker", picker);
            ConfigureIndicators(root, hud, weapon, canvas);
            ConfigureHeader(hud, canvas);
            var scale = Get<CombatHudScaleBinding>(root.transform);
            scale.Elements = new[] {
                Scale(wr), Scale(compactDock), Scale(picker.PickerRect), Scale(picker.DiskRect), Scale(picker.TooltipRect, minimum:.9f),
                Scale(picker.DropHint.rectTransform), Scale(picker.QDropLabel.rectTransform), Scale(picker.EDropLabel.rectTransform), Scale(picker.HoverUnderline.rectTransform),
                Scale((RectTransform)canvas.Find("V6 Header"), minimum:.85f), Scale((RectTransform)canvas.Find("Slow Effect"), true, .85f),
                Scale((RectTransform)canvas.Find("V6 Boss"), true), Scale(root.GetComponentInChildren<CombatPlayerIndicators>(true).AmmoRoot) };
            HudUiReviewSetup.ApplySkill(root, S);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void SkillSlot(PlayerSkillSlotView slot, RectTransform compact, float x, string key)
    {
        var r = (RectTransform)slot.transform; r.SetParent(compact, false); HideGraphics(r);
        Place(r, 132, 137, new(.5f, 0), new(.5f, .5f)); r.anchoredPosition = new Vector2(x, 90.5f); Panel(r, "skill");
        var border = Picture("V6 Edge", r, "skill_edge_idle", 132, 137, 0, 0); Stretch(border.rectTransform);
        var art = Art("V6 Art", r, 94, 82); art.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 16);
        var fill = Picture("V6 Cooldown", r, "fill_white", 106, 80, 0, 16); Fill(fill);
        var count = Text("V6 Count", r, "", 45, 30, 43, -11, 21, S.LabelFont);
        var empty = Text("V6 Empty", r, "-", 60, 60, 0, 16, 32, S.BodyFont);
        Text("V6 Key", r, key, 100, 30, 0, -46, 25, S.LabelFont);
        var button = slot.GetComponent<Button>(); button.targetGraphic = border; ButtonColors(button);
        Set(slot, "_border", border); Set(slot, "_icon", art.Main); Set(slot, "_combatArt", art); Set(slot, "_cooldown", fill);
        Set(slot, "_status", count); Set(slot, "_empty", empty.gameObject);
        SetColor(slot, "_readyColor", Color.white); SetColor(slot, "_unreadyColor", new Color(.65f, .65f, .65f)); SetColor(slot, "_emptyColor", Color.white);
    }
    private static void ConfigureIndicators(GameObject root, PlayerSkillHud hud, PlayerWeaponHud weapon, RectTransform canvas)
    {
        var legacy = root.GetComponentInChildren<PlayerWeaponAmmoView>(true);
        if (legacy != null)
        {
            legacy.enabled = false;
            var label = Ref<TMP_Text>(legacy, "_ammoLabel"); if (label != null) label.gameObject.SetActive(false);
            var reload = Ref<GameObject>(legacy, "_reloadIndicator"); if (reload != null) reload.SetActive(false);
        }
        var r = Rect("V6 Player Indicators", canvas, 0, 0, new(.5f, .5f), new(.5f, .5f)); Stretch(r);
        var view = Get<CombatPlayerIndicators>(r); view.Style = S; view.WeaponHud = weapon; view.SkillHud = hud; view.CanvasRect = canvas;
        // The magazine may already be docked. Reuse its authored objects before the base layout is reapplied.
        if (view.AmmoRoot != null)
        {
            view.AmmoRoot.SetParent(r, false);
            if (view.ReloadRoot != null) view.ReloadRoot.transform.SetParent(view.AmmoRoot, false);
        }
        view.AmmoRoot = Rect("Ammo", r, 128, 128, new(.5f, .5f), new(.5f, .5f));
        view.AmmoGroup = Get<CanvasGroup>(view.AmmoRoot); view.AmmoGroup.alpha = 0; view.AmmoGroup.blocksRaycasts = false;
        var reloadRoot = Rect("Reload", view.AmmoRoot, 60, 60, new(.5f, .5f), new(.5f, .5f));
        view.ReloadRoot = reloadRoot.gameObject; view.ReloadArrows = Picture("Arrow", reloadRoot, "reload_arrow", 55, 55, 0, 0).rectTransform;
        const string popupPath = "Assets/Prefabs/UI/CombatExperiencePopup.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(popupPath);
        if (existing == null)
        {
            var popup = new GameObject("Combat XP Popup", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(CombatExperiencePopup));
            try
            {
                var rect = (RectTransform)popup.transform; rect.sizeDelta = new Vector2(200, 60); popup.layer = 5;
                var popupCanvas = popup.GetComponent<Canvas>(); popupCanvas.renderMode = RenderMode.WorldSpace; popupCanvas.sortingOrder = 150;
                var script = popup.GetComponent<CombatExperiencePopup>(); script.Group = popup.GetComponent<CanvasGroup>();
                script.Group.blocksRaycasts = false; script.Label = Text("XP", rect, "+10 XP", 200, 60, 0, 0, 24, S.LabelFont);
                existing = PrefabUtility.SaveAsPrefabAsset(popup, popupPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(popup); }
        }
        view.PopupPrefab = existing.GetComponent<CombatExperiencePopup>();
    }
    private static void ConfigureHeader(PlayerSkillHud hud, RectTransform canvas)
    {
        var r = Rect("V6 Header", canvas, 550, 70, new(.5f, 1), new(.5f, 1));
        var view = Get<CombatHudHeader>(r); view.Style = S; view.PlayerHud = hud;
        view.ClockLabel = Text("Clock", r, "-- --:--", 280, 50, -111, -36, 32, S.HeaderFont);
        var separator = Picture("Separator", r, "fill_white", 1, 28, 42, -36);
        Picture("Territory Lettering", r, "label_territory_ko", 64, 37, 99, -36);
        view.OccupancyLabel = Text("Occupancy", r, "--%", 118, 50, 201, -36, 32, S.HeaderFont);
        var rule = Picture("Rule", r, "fill_white", 506, 1, 7, -65); rule.color = new Color(.65f, .67f, .64f);
        // Header children are positioned from its top edge to keep baseline invariant as the screen changes.
        foreach (Transform child in r) { var cr = (RectTransform)child; cr.anchorMin = cr.anchorMax = new Vector2(.5f, 1); }
        var slow = Rect("Slow Effect", canvas, 70, 70, new(1, 1), new(1, 1)); slow.anchoredPosition = new Vector2(-95, -95);
        Panel(slow, "effect"); Picture("Slow", slow, "effect_slow", 52, 52, 0, 0);
        view.SlowGroup = Get<CanvasGroup>(slow); view.SlowGroup.alpha = 0; view.SlowGroup.blocksRaycasts = false;
        view.SlowCount = Text("Active Effect Count", slow, "", 30, 30, 20, -20, 19, S.LabelFont);
        view.SlowDescription = Text("Actual Multiplier", slow, "", 180, 30, -55, -54, 17, S.BodyFont);
        view.SlowDescription.enabled = false; Get<CombatHoverLabel>(slow).Label = view.SlowDescription;
        slow.Find("V6 Surface").GetComponent<Image>().raycastTarget = true;
        var br = Rect("V6 Boss", canvas, 740, 104, new(.5f, 1), new(.5f, 1)); br.anchoredPosition = new Vector2(0, -109);
        Panel(br, "panel"); var boss = Get<CombatBossView>(br); boss.Style = S; boss.Group = Get<CanvasGroup>(br); boss.Group.alpha = 0; boss.Group.blocksRaycasts = false;
        boss.Name = Text("Name", br, "", 680, 35, 0, 26, 24, S.BodyFont);
        Picture("Health Track", br, "boss_track", 680, 24, 0, -14);
        boss.Health = Picture("Health", br, "fill_white", 666, 9, 0, -14); boss.Health.type = Image.Type.Filled; boss.Health.fillMethod = Image.FillMethod.Horizontal; boss.Health.color = S.Health;
    }
    private static void ConfigureVitals()
    {
        const string path = "Assets/Prefabs/UI/InitialStagePlayerHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            UseIndependentBodyFont(root);
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var view = root.GetComponentInChildren<StagePlayerVitalsView>(true);
            foreach (string legacy in new[] { "UIPlayerHp", "UICUHp", "UICUPower", "Control Unit Controls" })
            { var old = root.transform.Find(legacy); if (old != null) old.gameObject.SetActive(false); }
            // Hide only the old vitals graphics; their existing source binding and status-panel references remain intact.
            var serialized = new SerializedObject(view);
            foreach (string field in new[] { "playerHealthText", "controlUnitFill", "controlUnitHealthText", "powerFill", "powerPercentText", "powerDetailsText" })
            {
                var g = serialized.FindProperty(field).objectReferenceValue as Graphic;
                if (g != null) { g.enabled = false; g.raycastTarget = false; }
            }
            var cells = serialized.FindProperty("healthCells");
            for (int i = 0; i < cells.arraySize; i++) if (cells.GetArrayElementAtIndex(i).objectReferenceValue is Graphic g) { g.enabled = false; g.raycastTarget = false; }
            var left = Vitals(root.transform, "V6 Player Vitals", false); var right = Vitals(root.transform, "V6 CU Vitals", true);
            Set(view, "combatPlayer", left); Set(view, "combatControlUnit", right);
            ConfigureDebug(root);
            Get<CombatHudScaleBinding>(root.transform).Elements = new[] { Scale((RectTransform)left.transform, true), Scale((RectTransform)right.transform, true) };
            HudUiReviewSetup.ApplyVitals(root, S);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void ConfigureDebug(GameObject root)
    {
        var overlay = Rect("V6 Debug Overlay", root.transform, 0, 0, new(.5f, .5f), new(.5f, .5f)); Stretch(overlay);
        var canvas = Get<Canvas>(overlay); canvas.overrideSorting = true; canvas.sortingOrder = 10010;
        Get<GraphicRaycaster>(overlay);
        var panel = Rect("Debug Panel", overlay, 352, 344, new(.5f, .5f), new(.5f, .5f)); panel.anchoredPosition = new Vector2(492, 0); Panel(panel, "panel");
        Text("Title", panel, "DEBUG", 310, 40, 0, 132, 27, S.HeaderFont);
        var controller = Get<CombatHudDebugPanel>(overlay); controller.Style = S; controller.Stage = root.GetComponent<InitialStagePlayerHUD>();
        controller.Panel = Get<CanvasGroup>(panel); controller.PanelRect = panel;
        // Transform.Find는 '/'를 경로로 해석하므로 이전 제작본의 중복을 한 번 정리합니다.
        foreach (Transform child in panel.Cast<Transform>().Where(t => t.name == "Day / Night").ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        controller.Reset = DebugButton(panel, "Reset", 80); controller.Advance = DebugButton(panel, "Day–Night", 4); controller.Auto = DebugButton(panel, "Auto Turret", -72);
        var oldHint = panel.Find("Auto Hint"); if (oldHint != null) oldHint.gameObject.SetActive(false);
        var toast = Rect("Command Feedback", overlay, 640, 74, new(.5f, 1), new(.5f, 1)); toast.anchoredPosition = new Vector2(0, -225); Panel(toast, "panel");
        controller.Toast = Get<CanvasGroup>(toast); controller.ToastLabel = Text("Message", toast, "", 600, 60, 0, 0, 21, S.BodyFont);
        controller.Panel.alpha = controller.Toast.alpha = 0; controller.Panel.blocksRaycasts = controller.Toast.blocksRaycasts = false;
    }
    private static Button DebugButton(Transform parent, string caption, float y)
    {
        var r = Rect(caption, parent, 302, 59, new(.5f, .5f), new(.5f, .5f)); r.anchoredPosition = new Vector2(0, y); Panel(r, "button");
        var image = r.Find("V6 Surface").GetComponent<Image>(); image.raycastTarget = true;
        var button = Get<Button>(r); button.targetGraphic = image; ButtonColors(button);
        Text("Label", r, caption, 280, 40, 0, 0, 22, S.LabelFont); return button;
    }
    private static CombatVitalsGroup Vitals(Transform parent, string name, bool right)
    {
        var r = Rect(name, parent, 110, 500, new(right ? 1 : 0, .5f), new(right ? 1 : 0, 1)); r.anchoredPosition = new Vector2(0, 338);
        var group = Get<CombatVitalsGroup>(r); group.Style = S; group.RightEdge = right;
        group.Slide = Rect("Slide", r, 110, 500, new(right ? 1 : 0, 1), new(right ? 1 : 0, 1));
        group.Group = Get<CanvasGroup>(group.Slide);
        var icon = Picture("Owner", group.Slide, right ? "owner_cu" : "owner_player", 52, 52, right ? -54 : 49, -26); icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(right ? 1 : 0, 1);
        group.Primary = Bar(group.Slide, "Health", right ? -69.5f : 49.5f, S.Health, group, right);
        if (right) group.Secondary = Bar(group.Slide, "Power", -37.5f, S.Power, group, true);
        group.Details = Text("Hover Value", group.Slide, "", 250, 46, right ? -135 : 140, 34, 22, S.LabelFont);
        group.Details.rectTransform.anchorMin = group.Details.rectTransform.anchorMax = new Vector2(right ? 1 : 0, 1);
        group.Pin = Picture("Pinned", group.Slide, "pin", 20, 20, right ? -84 : 75, -50).gameObject;
        ((RectTransform)group.Pin.transform).anchorMin = ((RectTransform)group.Pin.transform).anchorMax = new Vector2(right ? 1 : 0, 1);
        group.Group.alpha = 0; group.Group.blocksRaycasts = false; group.Pin.SetActive(false);
        return group;
    }
    private static CombatVitalsBar Bar(RectTransform parent, string name, float x, Color color, CombatVitalsGroup owner, bool right)
    {
        var r = Rect(name, parent, 27, 372, new(right ? 1 : 0, 1), new(.5f, 1)); r.anchoredPosition = new Vector2(x, -62);
        var bg = Picture("Track", r, "meter_track", 27, 372, 0, 0); Stretch(bg.rectTransform); bg.type = Image.Type.Sliced; bg.raycastTarget = true;
        var fill = Picture("Fill", r, "fill_white", 15, 360, 0, 0); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Vertical; fill.fillOrigin = 0; fill.color = color;
        var view = Get<CombatVitalsBar>(r); view.Owner = owner; view.Fill = fill;
        return view;
    }
    private static Image Gradient(RectTransform parent, string name, bool right)
    {
        var clip = Rect(name, parent, 0, 0, new(.5f, .5f), new(.5f, .5f));
        clip.anchorMin = new Vector2(right ? .5f : 0, 0); clip.anchorMax = new Vector2(right ? 1 : .5f, 1); clip.offsetMin = clip.offsetMax = Vector2.zero;
        Get<RectMask2D>(clip);
        var image = Picture("Global X Gradient", clip, "drop_global_horizontal_idle", 0, 0, 0, 0);
        image.rectTransform.anchorMin = new Vector2(right ? -1 : 0, 0); image.rectTransform.anchorMax = new Vector2(right ? 1 : 2, 1);
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero; image.raycastTarget = true;
        return image;
    }
    private static RectTransform Needle(string name, RectTransform parent)
    {
        var image = Picture(name, parent, "equipped_needle_long", 24, 176, 0, 0);
        var r = image.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, 0); r.pivot = new Vector2(.5f, 5f / 176); return r;
    }
    private static CombatHudArt Art(string name, Transform parent, float w, float h)
    {
        var r = Rect(name, parent, w, h, new(.5f, .5f), new(.5f, .5f)); var art = Get<CombatHudArt>(r); art.Style = S;
        art.Base = Picture("Base", r, "skill_turret_base", 0, 0, 0, 0); Stretch(art.Base.rectTransform); art.Base.rectTransform.anchorMin = new Vector2(.14f, .04f); art.Base.rectTransform.anchorMax = new Vector2(.78f, .7f); art.Base.preserveAspect = true;
        art.Main = Picture("Main", r, "skill_bomb", 0, 0, 0, 0); Stretch(art.Main.rectTransform); art.Main.preserveAspect = true; art.Base.enabled = false; return art;
    }
    private static void Panel(RectTransform r, string kind)
    {
        var bg = Picture("V6 Surface", r, kind + "_surface", 0, 0, 0, 0); Stretch(bg.rectTransform); bg.type = Image.Type.Sliced; bg.transform.SetAsFirstSibling();
        var edge = Picture("V6 Panel Edge", r, kind + "_edge_idle", 0, 0, 0, 0); Stretch(edge.rectTransform); edge.type = Image.Type.Sliced;
    }
    private static void Fill(Image image) { image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Vertical; image.fillOrigin = 0; image.color = new Color(.09f, .12f, .13f, .72f); }
    private static void UseIndependentBodyFont(GameObject root)
    {
        var original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/NEXON Lv1 Gothic OTF SDF.asset");
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text.font == original) text.font = S.BodyFont;
    }
    private static void ButtonColors(Button b)
    {
        var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .96f, .8f); colors.pressedColor = new Color(.65f, .65f, .6f); colors.selectedColor = Color.white; colors.fadeDuration = .09f; b.colors = colors;
    }
    private static CombatHudScaleBinding.Element Scale(RectTransform rect, bool offset = false, float minimum = 0)
        => new() { Target = rect, ScaleOffset = offset, BaseOffset = rect.anchoredPosition, MinimumScale = minimum };
    private static void HideGraphics(Transform root) { foreach (var g in root.GetComponentsInChildren<Graphic>(true)) { g.enabled = false; g.raycastTarget = false; } }
    private static T Get<T>(Component c) where T : Component
    {
        T component = c.GetComponent<T>();
        return component != null ? component : c.gameObject.AddComponent<T>();
    }
    private static T Ref<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object => (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value) { var s = new SerializedObject(owner); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetColor(UnityEngine.Object owner, string name, Color value) { var s = new SerializedObject(owner); s.FindProperty(name).colorValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static RectTransform Rect(string name, Transform parent, float w, float h, Vector2 anchor, Vector2 pivot)
    {
        var r = parent.Cast<Transform>().FirstOrDefault(t => t.name == name) as RectTransform;
        if (r == null) { r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); }
        Place(r, w, h, anchor, pivot); return r;
    }
    private static void Place(RectTransform r, float w, float h, Vector2 anchor, Vector2 pivot) { r.gameObject.layer = 5; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.sizeDelta = new Vector2(w, h); r.localScale = Vector3.one; r.localRotation = Quaternion.identity; }
    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    private static void Bottom(RectTransform r, float w, float h) { Place(r, w, h, new(.5f, 0), new(.5f, 0)); r.anchoredPosition = Vector2.zero; }
    private static Image Picture(string name, Transform parent, string sprite, float w, float h, float x, float y)
    {
        var r = Rect(name, parent, w, h, new(.5f, .5f), new(.5f, .5f)); r.anchoredPosition = new Vector2(x, y);
        var image = Get<Image>(r); image.sprite = S.Get(sprite); image.color = Color.white; image.enabled = true; image.raycastTarget = false;
        image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; return image;
    }
    private static TMP_Text Text(string name, Transform parent, string value, float w, float h, float x, float y, float size, TMP_FontAsset font)
    {
        var r = Rect(name, parent, w, h, new(.5f, .5f), new(.5f, .5f)); r.anchoredPosition = new Vector2(x, y);
        var text = Get<TextMeshProUGUI>(r); text.font = font; text.fontSize = size; text.color = S.Text; text.text = value;
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false; text.enabled = true; return text;
    }
}
