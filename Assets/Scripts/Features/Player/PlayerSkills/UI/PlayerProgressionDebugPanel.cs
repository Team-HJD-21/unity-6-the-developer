using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>에디터/개발 빌드 전용 XP·투자 회수 도구입니다. 지급/권한 검사를 우회하지 않습니다.</summary>
public sealed class PlayerProgressionDebugPanel : MonoBehaviour
{
    [SerializeField] private Button _addTenButton;
    [SerializeField] private Button _addHundredButton;
    [SerializeField] private Button _nextLevelButton;
    [SerializeField] private Button _refundAllButton;
    [SerializeField] private TMP_Text _feedback;
    private PlayerExperience _experience;
    private PlayerBaseStats _stats;
    private PlayerInfo _owner;
    public static bool IsAvailable
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }
    }
    public void Bind(PlayerInfo player)
    {
        if (ReferenceEquals(_owner, player)) return;
        _owner = player;
        _experience = player != null ? player.GetComponent<PlayerExperience>() : null;
        _stats = player != null ? player.GetComponent<PlayerBaseStats>() : null;
        if (_feedback != null) _feedback.text = string.Empty;
    }
    private void OnEnable()
    {
        if (!IsAvailable) { gameObject.SetActive(false); return; }
        _addTenButton.onClick.AddListener(AddTen);
        _addHundredButton.onClick.AddListener(AddHundred);
        _nextLevelButton.onClick.AddListener(NextLevel);
        _refundAllButton.onClick.AddListener(RefundAll);
    }
    private void OnDisable()
    {
        _addTenButton.onClick.RemoveListener(AddTen);
        _addHundredButton.onClick.RemoveListener(AddHundred);
        _nextLevelButton.onClick.RemoveListener(NextLevel);
        _refundAllButton.onClick.RemoveListener(RefundAll);
        Bind(null);
    }
    private void LateUpdate()
    {
        bool canAdd = IsAvailable && _experience != null && _experience.CanGrantExperience && !_experience.IsMaximumLevel;
        _addTenButton.interactable = _addHundredButton.interactable = _nextLevelButton.interactable = canAdd;
        _refundAllButton.interactable = IsAvailable && _stats != null && _stats.CanModify &&
            (_stats.Invested(PlayerBaseStat.Attack) + _stats.Invested(PlayerBaseStat.Movement) + _stats.Invested(PlayerBaseStat.CriticalChance) > 0);
    }
    private void AddTen() => AddExperience(10);
    private void AddHundred() => AddExperience(100);
    private void NextLevel() => AddExperience(_experience != null
        ? (int)System.Math.Min(int.MaxValue, _experience.RemainingToNextLevel) : 0);
    private void AddExperience(int amount)
    {
        GameInput.TryConsumePrimaryPointerPress();
        bool applied = IsAvailable && _experience != null && _experience.AddExperience(amount);
        _feedback.text = applied ? $"XP +{amount}" : "XP 지급 불가";
    }
    private void RefundAll()
    {
        GameInput.TryConsumePrimaryPointerPress();
        bool applied = IsAvailable && _stats != null && _stats.TryResetInvestments();
        _feedback.text = applied ? "투자 포인트 회수" : "회수할 투자 없음";
    }
}
