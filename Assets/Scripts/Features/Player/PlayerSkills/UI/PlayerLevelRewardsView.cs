using UnityEngine;
using UnityEngine.UI;

/// <summary>ScrollRect의 위치에 맞춰 제작된 보상 행을 재사용합니다. 최대 레벨까지 UI를 런타임 생성하지 않습니다.</summary>
[DefaultExecutionOrder(600)]
public sealed class PlayerLevelRewardsView : MonoBehaviour
{
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private PlayerLevelRewardRow[] _rows;
    [SerializeField, Min(20)] private float _rowHeight = 40;
    private PlayerInfo _owner;
    private PlayerExperience _experience;
    private PlayerBaseStats _stats;
    public bool IsConfigured => _scroll != null && _scroll.content != null && _scroll.viewport != null &&
        _rows != null && _rows.Length >= 8 && System.Array.TrueForAll(_rows, row => row != null && row.IsConfigured);
    public void Bind(PlayerInfo owner)
    {
        if (ReferenceEquals(_owner, owner)) return;
        _owner = owner;
        _experience = owner != null ? owner.GetComponent<PlayerExperience>() : null;
        _stats = owner != null ? owner.GetComponent<PlayerBaseStats>() : null;
        if (IsConfigured) _scroll.verticalNormalizedPosition = 1;
    }
    private void LateUpdate()
    {
        if (!IsConfigured) return;
        int count = _experience != null ? _experience.MaximumLevel - 1 : 0;
        _scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(_scroll.viewport.rect.height, count * _rowHeight));
        int first = Mathf.Clamp(Mathf.FloorToInt(_scroll.content.anchoredPosition.y / _rowHeight), 0,
            Mathf.Max(0, count - _rows.Length));
        for (int i = 0; i < _rows.Length; i++)
        {
            int index = first + i;
            _rows[i].gameObject.SetActive(index < count);
            if (index >= count) continue;
            ((RectTransform)_rows[i].transform).anchoredPosition = new Vector2(0, -(index + .5f) * _rowHeight);
            _rows[i].Refresh(_stats, index + 2, _experience.Level);
        }
    }
    private void OnDisable() => Bind(null);
}
