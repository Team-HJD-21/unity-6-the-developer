using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using static PlayerSkillUiFactory;

/// <summary>스킬 슬롯의 빈 표시·아이콘·잔여 수량·쿨타임을 표시합니다.</summary>
public sealed class PlayerSkillSlotView
{
    private PlayerSkillController _owner;
    private Func<PlayerSkillId, Sprite> _icon;
    public Button Button;
    public Image Border, Icon, Cooldown;
    public PlayerFieldSkillIcon DeviceIcon;
    public GameObject Empty;
    public TMP_Text Status;
    public static PlayerSkillSlotView Create(int index, Transform parent, Transform canvas, PlayerSkillController owner, PlayerSkillUiFactory ui, Func<PlayerSkillId, Sprite> icon, Sprite mask, PlayerSkillTooltipView tooltip, UnityAction open)
    {
        bool preview = parent != null;
        var root = Rect(index == 0 ? "Q Skill Slot" : "E Skill Slot", parent != null ? parent : canvas, new Vector2(88, 88));
        if (preview)
        {
            root.anchoredPosition = new Vector2(index == 0 ? -148 : 148, -205);
            root.localScale = Vector3.one * 1.5f;
        }
        else
        {
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0, 1);
            root.anchoredPosition = new Vector2(40, -180 - index * 104);
        }
        var slot = new PlayerSkillSlotView { _owner = owner, _icon = icon, Border = Box(root, new Color(.22f, .39f, .43f)) };
        slot.Button = root.gameObject.AddComponent<Button>();
        if (!preview) slot.Button.onClick.AddListener(open);
        tooltip.AddHover(root, () => index == 0 ? owner.Loadout.Q : owner.Loadout.E);
        var inner = Rect("Background", root, new Vector2(80, 80));
        Box(inner, Dark);
        var empty = Rect("Empty Grid", inner, new Vector2(72, 72));
        slot.Empty = empty.gameObject;
        for (int i = -1; i <= 1; i++)
        {
            var vertical = Rect("Grid Column", empty, new Vector2(1, 70));
            vertical.anchoredPosition = new Vector2(i * 22, 0);
            Box(vertical, new Color(.22f, .4f, .43f, .5f));
            var horizontal = Rect("Grid Row", empty, new Vector2(70, 1));
            horizontal.anchoredPosition = new Vector2(0, i * 22);
            Box(horizontal, new Color(.22f, .4f, .43f, .5f));
        }
        ui.Label("+", empty, new Vector2(65, 65), Vector2.zero, 48, new Color(.65f, .78f, .8f));
        var iconRoot = Rect("Skill Icon", inner, new Vector2(66, 66));
        slot.Icon = Box(iconRoot, Color.white);
        slot.Icon.preserveAspect = true;
        slot.DeviceIcon = CreateDeviceIcon(iconRoot, PlayerSkillId.None);
        slot.Cooldown = Box(Rect("Cooldown Sweep", inner, new Vector2(66, 66)), new Color(.28f, .3f, .32f, .85f));
        slot.Cooldown.sprite = mask;
        slot.Cooldown.type = Image.Type.Filled;
        slot.Cooldown.fillMethod = Image.FillMethod.Radial360;
        slot.Cooldown.fillOrigin = (int)Image.Origin360.Top;
        // 남은 회색 영역은 반시계방향으로 채워, 사라지는 영역이 12시부터 시계방향으로 진행합니다.
        slot.Cooldown.fillClockwise = false;
        ui.Label(index == 0 ? "Q" : "E", root, new Vector2(25, 25), new Vector2(30, -30), 22, Color.white);
        slot.Status = ui.Label("", root, new Vector2(62, 26), new Vector2(-6, -28), 19, Color.white);
        return slot;
    }
    public void Refresh(PlayerSkillId skill)
    {

        bool assigned = skill != PlayerSkillId.None;
        Empty.SetActive(!assigned);
        Icon.sprite = _icon(skill);
        Icon.enabled = assigned && Icon.sprite != null;
        bool device = skill == PlayerSkillId.EnergyBarrier || skill == PlayerSkillId.LureBeacon;
        DeviceIcon.SetSkill(skill);
        DeviceIcon.enabled = device && Icon.sprite == null;
        float remaining = _owner.RemainingCooldown(skill);
        int count = _owner.SkillCount(skill);
        bool ready = assigned && _owner.IsSkillReady(skill);
        Border.color = assigned ? (ready ? Cyan : new Color(.2f, .5f, .55f)) : new Color(.22f, .39f, .43f);
        Icon.color = count > 0 ? Color.white : new Color(.4f, .5f, .55f);
        DeviceIcon.color = Icon.color;
        Cooldown.enabled = assigned && remaining > 0;
        Cooldown.color = new Color(.28f, .3f, .32f, skill == PlayerSkillId.EnergyBarrier && count > 0 ? .35f : .85f);
        Cooldown.fillAmount = _owner.CooldownProgress(skill);
        Status.text = assigned ? count.ToString() : "";
        // 전투 중에도 툴팁을 표시하되 대화/일시정지 UI는 가로채지 않습니다.
        Border.raycastTarget = _owner.CanPrepare || _owner.CanUse;
    }
}
