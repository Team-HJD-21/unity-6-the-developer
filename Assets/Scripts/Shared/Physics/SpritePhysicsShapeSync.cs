using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animator의 방향과 형태에 맞는 대표 피격 윤곽을 적용한다.
/// 애니메이션 프레임만 바뀌는 경우에는 콜라이더를 유지한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class SpritePhysicsShapeSync : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Animator animator;
    [SerializeField] private PolygonCollider2D targetCollider;

    [Header("방향 파라미터")]
    [SerializeField] private string directionXParameter = "MoveX";
    [SerializeField] private string directionYParameter = "MoveY";

    [Header("방향별 대표 윤곽")]
    [SerializeField] private Sprite downSprite;
    [SerializeField] private Sprite upSprite;
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite rightSprite;

    [Header("형태 변경")]
    [Tooltip("형태가 고정되는 몬스터만 연결한다.")]
    [SerializeField] private Sprite fixedShapeSprite;

    [Tooltip("고정 형태에 해당하는 Animator State의 Tag.")]
    [SerializeField] private string fixedShapeTag = "FixedShape";

    private readonly List<Vector2> _shapePoints = new();

    private int _directionXHash;
    private int _directionYHash;

    private Sprite _directionSprite;
    private Sprite _currentShapeSprite;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (targetCollider == null ||
            downSprite == null ||
            upSprite == null ||
            leftSprite == null ||
            rightSprite == null)
        {
            Debug.LogError(
                "SpritePhysicsShapeSync의 콜라이더와 방향별 스프라이트를 연결해주세요.",
                this);

            enabled = false;
            return;
        }

        _directionXHash = Animator.StringToHash(directionXParameter);
        _directionYHash = Animator.StringToHash(directionYParameter);
    }

    private void OnEnable()
    {
        if (animator == null || targetCollider == null)
            return;

        // 방향 값이 없는 최초 상태에서는 아래쪽 윤곽을 사용한다.
        _directionSprite = downSprite;
        _currentShapeSprite = null;

        RefreshShape();
    }

    private void LateUpdate()
    {
        RefreshShape();
    }

    /// <summary>
    /// 현재 방향과 형태에 맞는 대표 스프라이트가 바뀐 경우만 갱신한다.
    /// </summary>
    private void RefreshShape()
    {
        Sprite nextSprite = GetShapeSprite();

        if (nextSprite == _currentShapeSprite)
            return;

        ApplyShape(nextSprite);
    }

    /// <summary>
    /// 고정 형태이면 전용 윤곽을, 나머지는 현재 방향의 윤곽을 반환한다.
    /// </summary>
    private Sprite GetShapeSprite()
    {
        if (fixedShapeSprite != null &&
            animator.GetCurrentAnimatorStateInfo(0).IsTag(fixedShapeTag))
        {
            return fixedShapeSprite;
        }

        float directionX = animator.GetFloat(_directionXHash);
        float directionY = animator.GetFloat(_directionYHash);

        // 정지하면서 방향 값이 0이 되어도 마지막 방향을 유지한다.
        if (directionX == 0f && directionY == 0f)
            return _directionSprite;

        if (Mathf.Abs(directionX) > Mathf.Abs(directionY))
        {
            _directionSprite = directionX > 0f
                ? rightSprite
                : leftSprite;
        }
        else
        {
            _directionSprite = directionY > 0f
                ? upSprite
                : downSprite;
        }

        return _directionSprite;
    }

    /// <summary>대표 스프라이트의 Physics Shape를 콜라이더에 적용한다.</summary>
    private void ApplyShape(Sprite sprite)
    {
        // 잘못 설정된 같은 스프라이트의 경고가 매 프레임 반복되지 않게 한다.
        _currentShapeSprite = sprite;

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

        if (targetCollider.pathCount != shapeCount)
            targetCollider.pathCount = shapeCount;

        for (int i = 0; i < shapeCount; i++)
        {
            _shapePoints.Clear();
            sprite.GetPhysicsShape(i, _shapePoints);
            targetCollider.SetPath(i, _shapePoints);
        }
    }
}
