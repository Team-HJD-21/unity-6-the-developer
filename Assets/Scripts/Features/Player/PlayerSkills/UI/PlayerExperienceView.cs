using TMPro;
using UnityEngine;

/// <summary>기존 HP HUD가 바인딩한 로컬 Player의 레벨과 경험치를 미리 배치한 오브젝트로 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerExperienceView : MonoBehaviour
{
    [SerializeField] private StagePlayerVitalsView _vitals;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _experienceText;
    [SerializeField] private RectTransform _experienceClip;
    [SerializeField, Min(1)] private float _barWidth = 86f;
    private PlayerInfo _boundPlayer;
    private PlayerExperience _experience;

    private void LateUpdate()
    {
        var player = _vitals != null && _vitals.HasPlayer ? _vitals.Player : null;
        if (ReferenceEquals(_boundPlayer, player)) return;
        Unbind();
        _boundPlayer = player;
        _experience = player != null ? player.GetComponent<PlayerExperience>() : null;
        if (_experience != null) _experience.Changed += Refresh;
        Refresh();
    }
    private void OnEnable() => Refresh();
    private void Refresh()
    {
        if (_levelText != null) _levelText.text = _experience != null ? $"Lv.{_experience.Level}" : "Lv.—";
        if (_experienceText != null) _experienceText.text = _experience == null ? "XP —" : _experience.IsMaximumLevel
            ? "XP MAX" : $"XP {_experience.ExperienceInLevel}/{_experience.RequiredExperience}";
        if (_experienceClip != null) _experienceClip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            _barWidth * (_experience != null ? _experience.Progress : 0f));
    }
    private void Unbind()
    {
        if (_experience != null) _experience.Changed -= Refresh;
        _experience = null; _boundPlayer = null;
    }
    private void OnDisable() { Unbind(); Refresh(); }
}
