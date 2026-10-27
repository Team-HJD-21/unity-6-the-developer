using UnityEngine;
using UnityEngine.Rendering;

/// <summary>미리 배치한 몸체·무기 Renderer에 한 장면을 보관하고 월드 위치에 고정한 채 지웁니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(450)]
public sealed class PlayerDashAfterimage : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private SpriteRenderer _weapon;
    [SerializeField] private SortingGroup _sorting;
    [SerializeField] private Color _tint = new(.65f, .88f, 1f, .22f);
    [SerializeField, Min(.01f)] private float _lifetime = .14f;
    private Vector3 _worldPosition;
    private float _elapsed;
    public bool IsPlaying { get; private set; }
    public bool IsConfigured => _body != null && _weapon != null && _sorting != null &&
        _body.transform.parent == transform && _weapon.transform.parent == transform && _sorting.transform == transform;

    public void Capture(SpriteRenderer body, SpriteRenderer weapon)
    {
        if (!IsConfigured || body == null || !body.enabled || body.sprite == null) { Clear(); return; }
        _worldPosition = body.transform.position;
        LockWorldTransform();
        CopyPose(body, _body);
        CopyPose(weapon, _weapon);
        _sorting.sortingLayerID = body.sortingLayerID;
        _sorting.sortingOrder = body.sortingOrder - 1;
        _elapsed = 0;
        IsPlaying = true;
        ApplyFade(1);
    }

    private static void CopyPose(SpriteRenderer source, SpriteRenderer target)
    {
        target.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
        if (!target.enabled) return;
        target.sprite = source.sprite;
        target.sharedMaterial = source.sharedMaterial;
        target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        target.transform.localScale = source.transform.lossyScale; // 복사본 루트의 월드 스케일은 1입니다.
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
    }

    private void LateUpdate()
    {
        if (!IsPlaying) return;
        _elapsed += Time.deltaTime;
        if (_elapsed >= _lifetime) { Clear(); return; }
        LockWorldTransform();
        ApplyFade(1 - _elapsed / Mathf.Max(.01f, _lifetime));
    }

    private void LockWorldTransform()
    {
        transform.SetPositionAndRotation(_worldPosition, Quaternion.identity);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(InverseScale(parentScale.x), InverseScale(parentScale.y), InverseScale(parentScale.z));
    }

    private static float InverseScale(float value) => Mathf.Abs(value) > .0001f ? 1 / value : 1;
    private void ApplyFade(float progress)
    {
        var color = _tint;
        color.a *= progress;
        _body.color = color;
        _weapon.color = color;
    }

    public void Clear()
    {
        IsPlaying = false;
        if (_body != null) _body.enabled = false;
        if (_weapon != null) _weapon.enabled = false;
    }

    private void OnEnable() => Clear();
    private void OnDisable() => Clear();
}
