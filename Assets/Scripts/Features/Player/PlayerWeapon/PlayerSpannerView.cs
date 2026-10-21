using UnityEngine;

/// <summary>Keeps the authored wrench sprite beside or behind its owner.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerSpannerView : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private SpriteRenderer _playerRenderer;

    private void Awake() => _renderer = GetComponent<SpriteRenderer>();

    public void SetBehindPlayer(bool behind)
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (_playerRenderer == null && transform.parent != null)
            _playerRenderer = transform.parent.GetComponentInParent<SpriteRenderer>();
        if (_playerRenderer == null) return;
        _renderer.sortingLayerID = _playerRenderer.sortingLayerID;
        _renderer.sortingOrder = _playerRenderer.sortingOrder + (behind ? -1 : 1);
    }

    public void SetVisible(bool visible)
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        _renderer.enabled = visible;
    }
}
