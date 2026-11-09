using UnityEngine;
using UnityEngine.UI;

/// <summary>제작된 세 Image에 현재 몸체·무기·앞손 Sprite를 복사합니다. 카메라/RenderTexture나 별도 Player는 생성하지 않습니다.</summary>
[DefaultExecutionOrder(500)]
public sealed class PlayerStatusCharacterPreview : MonoBehaviour
{
    [SerializeField] private Image _bodyImage;
    [SerializeField] private Image _weaponImage;
    [SerializeField] private Image _handsImage;
    [SerializeField, Min(10)] private float _canvasHeight = 270;
    private PlayerInfo _owner;
    private SpriteRenderer _body, _weapon, _hands;
    private Vector2 _frameCenter;
    public bool IsConfigured => _bodyImage != null && _weaponImage != null && _handsImage != null;
    public void Bind(PlayerInfo owner)
    {
        if (ReferenceEquals(_owner, owner)) return;
        _owner = owner;
        _body = owner != null ? owner.GetComponent<SpriteRenderer>() : null;
        var visual = owner != null ? owner.GetComponent<PlayerWeaponVisual>() : null;
        _weapon = visual != null ? visual.Renderer : null;
        _hands = visual != null ? visual.ForegroundHandsRenderer : null;
    }
    private void LateUpdate()
    {
        if (!IsConfigured) return;
        if (_owner == null || _body == null || _body.sprite == null)
        { _bodyImage.enabled = _weaponImage.enabled = _handsImage.enabled = false; return; }
        float scale = _canvasHeight / Mathf.Max(.01f, _body.sprite.bounds.size.y);
        _frameCenter = _body.sprite.bounds.center;
        if (_body.flipX) _frameCenter.x = -_frameCenter.x;
        if (_body.flipY) _frameCenter.y = -_frameCenter.y;
        Copy(_body, _bodyImage, scale); Copy(_weapon, _weaponImage, scale); Copy(_hands, _handsImage, scale);
        _bodyImage.transform.SetSiblingIndex(_weapon != null && _weapon.sortingOrder < _body.sortingOrder ? 1 : 0);
        _weaponImage.transform.SetSiblingIndex(_weapon != null && _weapon.sortingOrder < _body.sortingOrder ? 0 : 1);
        _handsImage.transform.SetAsLastSibling();
    }
    private void Copy(SpriteRenderer source, Image image, float scale)
    {
        image.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
        if (!image.enabled) return;
        Sprite sprite = source.sprite;
        image.sprite = sprite; image.color = source.color;
        RectTransform rect = image.rectTransform;
        rect.pivot = sprite.pivot / sprite.rect.size;
        rect.sizeDelta = (Vector2)sprite.bounds.size * scale;
        rect.anchoredPosition = ((Vector2)_owner.transform.InverseTransformPoint(source.transform.position) - _frameCenter) * scale;
        Vector3 ratio = source.transform.lossyScale;
        Vector3 root = _owner.transform.lossyScale;
        rect.localScale = new Vector3((source.flipX ? -1 : 1) * ratio.x / Mathf.Max(.001f, Mathf.Abs(root.x)),
            (source.flipY ? -1 : 1) * ratio.y / Mathf.Max(.001f, Mathf.Abs(root.y)), 1);
        rect.localRotation = Quaternion.Inverse(_owner.transform.rotation) * source.transform.rotation;
    }
    private void OnDisable() => Bind(null);
}
