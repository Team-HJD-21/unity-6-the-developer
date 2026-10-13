using System;
using TeamHJD.Game.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Stage 1 외형에 실제 PlayerInfo, CU 체력과 터렛 전력 값을 표시합니다.</summary>
internal sealed class StagePlayerVitalsView : IDisposable
{
    private readonly Image[] _cells;
    private readonly Image _cuFill, _powerFill;
    private readonly TMP_Text _playerText, _cuText, _powerText, _powerDetails;
    private PlayerInfo _player;
    private ControlUnitHealth _cu;
    private TurretController _controller;
    public bool HasPlayer => _player != null;
    public bool HasControlUnit => _cu != null;

    public StagePlayerVitalsView(RectTransform player, RectTransform cu, RectTransform power, TMP_FontAsset font)
    {
        _cells = Array.FindAll(player.GetComponentsInChildren<Image>(true), image => image.name.StartsWith("Cell") || image.name == "MSCell");
        ArrangeHealthCells();
        _cuFill = Find<Image>(cu, "FillHp");
        _powerFill = Find<Image>(power, "FillPower");
        _cuText = Find<TMP_Text>(cu, "CUHpInfo");
        _powerText = Find<TMP_Text>(power, "CUPowerInfo");
        _playerText = StageTurretControlView.Label(player, font, "Player HP", new Vector2(0, -62), new Vector2(360, 28), 20);
        _powerDetails = StageTurretControlView.Label(power, font, "Waiting for power source", new Vector2(-180, -115), new Vector2(400, 30), 16);
        ShowPlayer(0, 0);
        if (_cuText != null) _cuText.text = "—";
        if (_powerText != null) _powerText.text = "—";
    }

    private static T Find<T>(Transform root, string name) where T : Component =>
        Array.Find(root.GetComponentsInChildren<T>(true), component => component.name == name);

    private void ArrangeHealthCells()
    {
        if (_cells.Length == 0) return;
        var row = (RectTransform)_cells[0].transform.parent;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
        row.anchoredPosition = new Vector2(2, -6);
        row.sizeDelta = new Vector2(240, 20);
        float width = (240f - (_cells.Length - 1) * 1.8f) / _cells.Length;
        for (int i = 0; i < _cells.Length; i++)
        {
            var image = _cells[i];
            var aspect = image.GetComponent<AspectRatioFitter>();
            if (aspect != null) aspect.enabled = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(i * (width + 1.8f), 0);
            rect.sizeDelta = new Vector2(width, 20);
            image.enabled = true;
        }
    }

    public void BindPlayer(PlayerInfo player)
    {
        if (ReferenceEquals(_player, player)) return;
        if (_player != null) _player.onHpChange.RemoveListener(ShowPlayer);
        _player = player;
        if (_player == null) { ShowPlayer(0, 0); return; }
        _player.onHpChange.AddListener(ShowPlayer);
        ShowPlayer(_player.curHp, _player.maxHp);
    }

    public void BindControlUnit(ControlUnitHealth cu)
    {
        if (ReferenceEquals(_cu, cu)) return;
        if (_cu != null) _cu.HealthChanged -= ShowControlUnit;
        _cu = cu;
        if (_cu == null) return;
        _cu.HealthChanged += ShowControlUnit;
        ShowControlUnit(_cu.CurrentHealth, _cu.MaxHealth);
    }

    public void BindPower(TurretController controller)
    {
        if (ReferenceEquals(_controller, controller)) { ShowPower(0, 0); return; }
        if (_controller != null) _controller.PowerChanged -= ShowPower;
        _controller = controller;
        if (_controller != null) _controller.PowerChanged += ShowPower;
        ShowPower(0, 0);
    }

    private void ShowPlayer(int current, int maximum)
    {
        float ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0;
        for (int i = 0; i < _cells.Length; i++)
            _cells[i].color = Color.Lerp(new Color(.25f, .4f, .45f), new Color(.4f, 1, 1), Mathf.Clamp01(ratio * _cells.Length - i));
        _playerText.text = maximum > 0 ? $"Player HP {Mathf.Clamp(current, 0, maximum)} / {maximum}" : "Waiting for player";
    }

    private void ShowControlUnit(int current, int maximum)
    {
        float ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0;
        if (_cuFill != null) _cuFill.fillAmount = ratio;
        if (_cuText != null) _cuText.text = $"{Mathf.RoundToInt(ratio * 100)}%";
    }

    private void ShowPower(int current, int maximum)
    {
        if (_controller == null) return;
        var state = _controller.GetSnapshot();
        float ratio = state.MaximumPower > 0 ? Mathf.Clamp01((float)state.AvailablePower / state.MaximumPower) : 0;
        if (_powerFill != null) _powerFill.fillAmount = ratio;
        if (_powerText != null) _powerText.text = $"{Mathf.RoundToInt(ratio * 100)}%";
        _powerDetails.text = $"Power {state.AvailablePower}/{state.MaximumPower} | Recovering {state.PendingRecoveryPower}";
    }

    public void Dispose()
    {
        if (_player != null) _player.onHpChange.RemoveListener(ShowPlayer);
        if (_cu != null) _cu.HealthChanged -= ShowControlUnit;
        if (_controller != null) _controller.PowerChanged -= ShowPower;
    }
}
