using System;
using TeamHJD.Game.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>터렛 선택창의 배치와 표시만 담당합니다.</summary>
internal sealed class StageTurretControlView
{
    private readonly GameObject _panel;
    private readonly TMP_Text _title, _level, _details, _status, _feedback, _toggleText;
    private readonly Button _toggle, _up, _down, _close;
    public event Action ToggleRequested, LevelUpRequested, LevelDownRequested, CloseRequested;

    public StageTurretControlView(RectTransform parent, TMP_FontAsset font, Sprite menu, Sprite button)
    {
        var rect = Rect("Turret Control Panel", parent, new Vector2(740, 300), new Vector2(0, 35));
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        rect.pivot = new Vector2(.5f, 0);
        _panel = rect.gameObject;
        var background = rect.gameObject.AddComponent<Image>();
        background.sprite = menu;
        background.type = menu != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = menu != null ? Color.white : new Color(.025f, .09f, .12f, .97f);
        background.raycastTarget = true;
        _title = Label(rect, font, "", new Vector2(0, 110), new Vector2(580, 35), 27);
        _level = Label(rect, font, "", new Vector2(-265, 0), new Vector2(125, 35), 23);
        _details = Label(rect, font, "", new Vector2(0, -65), new Vector2(700, 35), 21);
        _status = Label(rect, font, "", new Vector2(0, -100), new Vector2(700, 28), 18);
        _feedback = Label(rect, font, "", new Vector2(0, -128), new Vector2(700, 28), 17);
        _toggle = Button(rect, font, button, "Activate", Vector2.zero, () => ToggleRequested?.Invoke());
        ((RectTransform)_toggle.transform).sizeDelta = new Vector2(240, 60);
        _toggleText = _toggle.GetComponentInChildren<TMP_Text>();
        _up = ArrowButton(rect, font, button, new Vector2(-175, 21), true, () => LevelUpRequested?.Invoke());
        _down = ArrowButton(rect, font, button, new Vector2(-175, -21), false, () => LevelDownRequested?.Invoke());
        _close = Button(rect, font, button, "Close", new Vector2(320, 110), () => CloseRequested?.Invoke());
        ((RectTransform)_close.transform).sizeDelta = new Vector2(70, 30);
        var closeLabel = _close.GetComponentInChildren<TMP_Text>();
        closeLabel.rectTransform.sizeDelta = new Vector2(65, 28);
        closeLabel.fontSize = 16;
        _panel.SetActive(false);
    }

    public void Show(TurretBase turret, bool canUp, bool canDown)
    {
        _panel.SetActive(turret != null);
        if (turret == null) return;
        _title.text = turret.DisplayName;
        _level.text = $"Level {turret.Definition?.Level ?? 0}";
        _details.text = $"HP {turret.CurrentHealth}/{turret.MaxHealth}   Power {turret.EffectivePower}   Damage {turret.EffectiveDamage}   Range {turret.EffectiveRange:0.#}";
        _status.text = turret.IsDestroyed ? "Destroyed" : turret.IsLocked ? "Locked" :
            turret.IsOperational ? "Operational" : turret.IsActivated ? "Activated - Suspended" :
            turret.IsCoolingDown ? $"Cooling down: {turret.CoolingDownRemaining:0.0}s" : "Inactive";
        bool editable = Time.timeScale > 0 && !turret.IsDestroyed && !turret.IsLocked && turret.IsInitialized;
        _toggle.interactable = editable && !turret.IsManualActivationBlocked && (turret.IsActivated || !turret.IsCoolingDown);
        _toggleText.text = turret.IsDestroyed ? "Destroyed" : turret.IsLocked ? "Locked" : turret.IsActivated ? "Deactivate" : "Activate";
        _toggleText.color = turret.IsDestroyed || turret.IsLocked ? Color.gray : turret.IsActivated ? Color.red : Color.green;
        _up.interactable = editable && canUp;
        _down.interactable = editable && canDown;
    }

    public void SetFeedback(string text) => _feedback.text = text;

    private static Button ArrowButton(Transform parent, TMP_FontAsset font, Sprite sprite, Vector2 position, bool up, UnityEngine.Events.UnityAction action)
    {
        var button = Button(parent, font, sprite, up ? "Level Up" : "Level Down", position, action);
        ((RectTransform)button.transform).sizeDelta = new Vector2(44, 36);
        button.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
        var rect = Rect("Arrow", button.transform, new Vector2(20, 13), Vector2.zero);
        var arrow = rect.gameObject.AddComponent<StageLevelArrowGraphic>();
        arrow.PointsUp = up;
        return button;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    internal static TMP_Text Label(Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Rect("Label", parent, size, position);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return label;
    }

    private static Button Button(Transform parent, TMP_FontAsset font, Sprite sprite, string title, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, parent, new Vector2(210, 45), position);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = sprite != null ? Color.white : new Color(.08f, .25f, .28f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        Label(rect, font, title, Vector2.zero, new Vector2(205, 40), 21);
        return button;
    }
}
