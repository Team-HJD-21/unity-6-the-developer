using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>미리 배치한 보상 한 줄입니다. 지급·중복 검증은 PlayerBaseStats 원본에 요청합니다.</summary>
public sealed class PlayerLevelRewardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _rewardText;
    [SerializeField] private TMP_Text _buttonText;
    [SerializeField] private Button _claimButton;
    private PlayerBaseStats _stats;
    private int _level;
    public bool IsConfigured => _levelText != null && _rewardText != null && _buttonText != null && _claimButton != null;
    private void OnEnable() { if (_claimButton != null) _claimButton.onClick.AddListener(Claim); }
    private void OnDisable()
    {
        if (_claimButton != null) _claimButton.onClick.RemoveListener(Claim);
        _stats = null;
    }
    public void Refresh(PlayerBaseStats stats, int level, int achievedLevel)
    {
        _stats = stats; _level = level;
        _levelText.text = $"Lv. {level:00}";
        _rewardText.text = $"스탯 포인트 +{(stats != null ? stats.PointsPerLevelReward : 1)}";
        bool claimed = stats != null && stats.IsRewardClaimed(level);
        _buttonText.text = claimed ? "수령 완료" : achievedLevel < level ? "미달성" : "보상받기";
        _claimButton.interactable = stats != null && stats.CanClaimReward(level);
    }
    private void Claim()
    {
        GameInput.TryConsumePrimaryPointerPress();
        if (_stats != null) _stats.TryClaimReward(_level);
    }
}
