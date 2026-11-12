using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 스프라이트의 Physics Shape를 PolygonCollider2D에 적용한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpritePhysicsShapeSync : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PolygonCollider2D targetCollider;

    private readonly List<Vector2> _shapePoints = new();
    private Sprite _currentSprite;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (targetCollider == null)
        {
            Debug.LogError(
                "SpritePhysicsShapeSync에 PolygonCollider2D가 연결되지 않았습니다.",
                this);

            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (spriteRenderer != null && targetCollider != null)
            ApplyCurrentSpriteShape();
    }

    private void LateUpdate()
    {
        if (spriteRenderer.sprite == _currentSprite)
            return;

        ApplyCurrentSpriteShape();
    }

    private void ApplyCurrentSpriteShape()
    {
        Sprite sprite = spriteRenderer.sprite;
        _currentSprite = sprite;

        if (sprite == null)
            return;

        int shapeCount = sprite.GetPhysicsShapeCount();

        if (shapeCount == 0)
        {
            Debug.LogWarning(
                $"{sprite.name}에 Physics Shape가 없습니다.",
                sprite);

            return;
        }

        targetCollider.pathCount = shapeCount;

        for (int i = 0; i < shapeCount; i++)
        {
            _shapePoints.Clear();
            sprite.GetPhysicsShape(i, _shapePoints);
            targetCollider.SetPath(i, _shapePoints);
        }
    }
}