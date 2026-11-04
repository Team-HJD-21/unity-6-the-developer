using UnityEngine;

/// <summary>미리 배치한 설치 미리보기의 본체·방벽 연결선과 가능 색상을 갱신합니다.</summary>
public class PlayerSkillPlacementPreview : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _renderers;
    [SerializeField] private SpriteRenderer _anchor;
    [SerializeField] private LineRenderer _connection;
    [SerializeField] private LineRenderer _range;
    [SerializeField] private Color _validColor = new(.4f, 1f, .65f, .45f);
    [SerializeField] private Color _invalidColor = new(1f, .25f, .2f, .45f);

    public bool IsConfigured => _range != null && _range.loop && !_range.useWorldSpace && _range.positionCount >= 3
        && _renderers != null && _renderers.Length > 0
        && System.Array.TrueForAll(_renderers, item => item != null && item.sprite != null);

    public void Refresh(Vector3 position, bool valid, Vector3? anchor = null)
    {
        transform.position = position;
        Color color = valid ? _validColor : _invalidColor;
        foreach (var renderer in _renderers) { renderer.enabled = true; renderer.color = color; }
        if (_anchor != null)
        {
            _anchor.gameObject.SetActive(anchor.HasValue);
            if (anchor.HasValue) { _anchor.transform.position = anchor.Value; _anchor.color = _validColor; }
        }
        if (_connection != null)
        {
            _connection.enabled = anchor.HasValue;
            if (anchor.HasValue)
            {
                _connection.startColor = _connection.endColor = color;
                PlayerFieldLineFactory.SetLine(_connection, anchor.Value + Vector3.up * .3f, position + Vector3.up * .3f);
            }
        }
        SetVisible(true);
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    /// <summary>원은 프리팹의 단위원을 사용하고 중심과 반지름만 갱신합니다. 후보가 움직여도 플레이어를 따라갑니다.</summary>
    public void RefreshRange(Vector3 center, float radius)
    {
        _range.transform.position = center;
        _range.transform.rotation = Quaternion.identity;
        _range.transform.localScale = new Vector3(radius, radius, 1);
        SetVisible(true);
    }

    public void HideCandidate()
    {
        foreach (var renderer in _renderers) renderer.enabled = false;
        if (_anchor != null) _anchor.gameObject.SetActive(false);
        if (_connection != null) _connection.enabled = false;
    }
}
