using System;
using UnityEngine;
using static PlayerSkillUiFactory;

/// <summary>회수 가능한 포탑 아래에 F 키 안내를 표시합니다.</summary>
public sealed class PlayerTurretRecoveryPromptView
{
    private readonly RectTransform _canvas;
    private readonly PlayerSkillController _owner;
    private readonly PlayerSkillUiFactory _ui;
    private readonly Func<bool> _isOpen;
    private RectTransform _recoveryPrompt;
    private Camera _worldCamera;
    public PlayerTurretRecoveryPromptView(RectTransform canvas, PlayerSkillController owner, PlayerSkillUiFactory ui, Func<bool> isOpen, Sprite key)
    { _canvas = canvas; _owner = owner; _ui = ui; _isOpen = isOpen; CreateRecoveryPrompt(key); }
    private void CreateRecoveryPrompt(Sprite keySprite)
    {
        _recoveryPrompt = Rect("Turret Recovery Prompt", _canvas, new Vector2(230, 60));
        Box(_recoveryPrompt, Cyan);
        Box(Rect("Recovery Background", _recoveryPrompt, new Vector2(226, 56)), Dark);
        var keyRect = Rect("F Key Icon", _recoveryPrompt, new Vector2(48, 48));
        keyRect.anchoredPosition = new Vector2(-82, 0);
        if (keySprite != null)
        {
            var image = Box(keyRect, Color.white);
            image.sprite = keySprite;
            image.preserveAspect = true;
        }
        else _ui.Label("F", keyRect, new Vector2(48, 48), Vector2.zero, 32, Color.white);
        _ui.Label("포탑 회수", _recoveryPrompt, new Vector2(150, 44), new Vector2(27, 0), 26, Color.white);
        _recoveryPrompt.gameObject.SetActive(false);
    }
    public void Refresh()
    {
        Transform turret = _owner.RecoverableTurret;
        if (_worldCamera == null) _worldCamera = Camera.main;
        bool visible = turret != null && _worldCamera != null && !_isOpen();
        _recoveryPrompt.gameObject.SetActive(visible);
        if (!visible) return;
        var baseRenderer = turret.GetComponent<SpriteRenderer>();
        Vector3 bottom = turret.position;
        if (baseRenderer != null) bottom.y = baseRenderer.bounds.min.y;
        Vector3 screen = _worldCamera.WorldToScreenPoint(bottom);
        if (screen.z <= 0) { _recoveryPrompt.gameObject.SetActive(false); return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas,
            screen, null, out Vector2 point);
        _recoveryPrompt.anchoredPosition = point + Vector2.down * 38;
    }
}
