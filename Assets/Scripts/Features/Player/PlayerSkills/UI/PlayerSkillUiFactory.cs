using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스킬 UI 부품 생성과 공통 스타일을 담당합니다.</summary>
public sealed class PlayerSkillUiFactory
{
    public static readonly Color Cyan = new Color(.35f, .95f, 1f);
    public static readonly Color Dark = new Color(.035f, .09f, .12f, .97f);
    private readonly TMP_FontAsset _font;
    public PlayerSkillUiFactory(TMP_FontAsset font) { _font = font; }
    public static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }
    public static Image Box(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    public TMP_Text Label(string text, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color)
    {
        var rect = Rect("Label", parent, size);
        rect.anchoredPosition = position;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }
    public Button TextButton(string title, Transform parent, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, parent, new Vector2(290, 44));
        rect.anchoredPosition = position;
        Box(rect, new Color(.08f, .24f, .28f)).raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        Label(title, rect, new Vector2(280, 40), Vector2.zero, 20, Color.white);
        return button;
    }
    public static PlayerFieldSkillIcon CreateDeviceIcon(RectTransform parent, PlayerSkillId skill)
    {
        var rect = Rect("Device Icon", parent, parent.sizeDelta);
        var icon = rect.gameObject.AddComponent<PlayerFieldSkillIcon>();
        icon.raycastTarget = false;
        icon.SetSkill(skill);
        icon.enabled = skill == PlayerSkillId.EnergyBarrier || skill == PlayerSkillId.LureBeacon;
        return icon;
    }
}
