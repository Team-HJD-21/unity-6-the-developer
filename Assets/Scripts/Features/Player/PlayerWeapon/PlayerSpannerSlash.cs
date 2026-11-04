using UnityEngine;

/// <summary>제작된 초승달 Sprite를 실제 스패너 타격 위치·방향·반경에 맞춰 표시합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Animator), typeof(PlayerSpriteEffect))]
public sealed class PlayerSpannerSlash : MonoBehaviour
{
    [Tooltip("Sprite 피벗에서 검기 바깥쪽 끝까지의 로컬 반경입니다. 그림이나 PPU를 바꾸면 함께 맞춥니다.")]
    [SerializeField, Min(.01f)] private float _authoredRadius = 1.390625f;
    [SerializeField] private int _sortingOrderOffset = 2;

    /// <summary>타격 순간의 월드 위치에 고정합니다. 이후 플레이어 이동이나 손 위치를 따라가지 않습니다.</summary>
    public bool Show(Transform owner, Vector2 direction, float range)
    {
        if (owner == null || !PlayerAimUtility.IsValidDirection(direction) ||
            !IsPositiveFinite(range) || !IsPositiveFinite(_authoredRadius)) return false;
        transform.position = owner.position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        float scale = range / _authoredRadius;
        transform.localScale = new Vector3(scale, scale, 1f);
        var ownerRenderer = owner.GetComponent<SpriteRenderer>();
        if (ownerRenderer != null)
        {
            var renderer = GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = ownerRenderer.sortingLayerID;
            renderer.sortingOrder = ownerRenderer.sortingOrder + _sortingOrderOffset;
        }
        return true;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
