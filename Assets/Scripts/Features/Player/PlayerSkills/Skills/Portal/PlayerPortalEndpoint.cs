using UnityEngine;

/// <summary>포털의 발밑 위치·설치 공간과 본체 표시를 제공합니다. 받침 충돌은 프리팹에 두며 중앙 도착 공간은 비웁니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerPortalEndpoint : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private Sprite _secondSprite;
    [SerializeField] private PlayerDeviceSortingOrder _depth;
    [SerializeField] private Vector2 _footprintSize = new(1.8f, 1.2f);
    [SerializeField] private Vector2 _footprintOffset;
    [SerializeField] private Color _firstColor = new(.3f, .8f, 1f);
    [SerializeField] private Color _secondColor = new(1f, .65f, .25f);

    public int SlotIndex { get; private set; } = -1;
    public bool IsConfigured => _body != null && _body.sprite != null
        && float.IsFinite(_footprintSize.x) && float.IsFinite(_footprintSize.y)
        && _footprintSize.x > 0 && _footprintSize.y > 0
        && PlayerSkillPlacementUtility.IsFinite(_footprintOffset)
        && transform.localRotation == Quaternion.identity
        && PlayerSkillPlacementUtility.IsFinite(transform.localScale)
        && transform.localScale.x > 0 && transform.localScale.y > 0;

    public Bounds FootprintAt(Vector3 position)
    {
        Vector3 scale = transform.lossyScale;
        return new Bounds(position + Vector3.Scale(_footprintOffset, scale),
            new Vector3(_footprintSize.x * Mathf.Abs(scale.x), _footprintSize.y * Mathf.Abs(scale.y), 0));
    }

    internal void Initialize(int slot, Transform owner = null)
    {
        SlotIndex = slot;
        // 전용 A/B 이미지는 외장색을 유지하고 에너지 색만 구분합니다.
        if (_secondSprite != null)
        {
            if (slot == 1) _body.sprite = _secondSprite;
            _body.color = Color.white;
        }
        else _body.color = slot == 0 ? _firstColor : _secondColor;
        _depth?.Bind(owner);
    }
}
