using UnityEngine;

/// <summary>미리보기 프리팹의 위치와 설치 가능 색상만 갱신합니다.</summary>
public sealed class PlayerTurretPlacementPreview : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _renderers;
    [SerializeField] private Color _validColor = new(.4f, 1f, .65f, .45f);
    [SerializeField] private Color _invalidColor = new(1f, .25f, .2f, .45f);

    public bool IsConfigured
    {
        get
        {
            if (_renderers == null || _renderers.Length == 0) return false;
            foreach (var renderer in _renderers)
                if (renderer == null || renderer.sprite == null) return false;
            return true;
        }
    }

    public void Refresh(Vector3 position, bool canPlace)
    {
        transform.position = position;
        Color color = canPlace ? _validColor : _invalidColor;
        foreach (var renderer in _renderers)
            if (renderer != null) renderer.color = color;
        SetVisible(true);
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);
}
