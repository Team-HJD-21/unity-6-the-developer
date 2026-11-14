using System;
using UnityEngine;

/// <summary>
/// Animator의 방향과 형태에 맞춰 미리 설정한 캡슐 피격 범위를 적용한다.
/// 애니메이션 프레임만 바뀌는 경우에는 콜라이더를 유지한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class DirectionalCapsuleCollider2D : MonoBehaviour
{
    /// <summary>캡슐 콜라이더의 로컬 좌표 기준 피격 범위 설정.</summary>
    [Serializable]
    private sealed class CapsuleShape
    {
        public Vector2 offset;
        public Vector2 size = new(0.5f, 1f);
        public CapsuleDirection2D direction = CapsuleDirection2D.Vertical;
    }

    [Header("참조")]
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider2D targetCollider;

    [Header("방향 파라미터")]
    [SerializeField] private string directionXParameter = "MoveX";
    [SerializeField] private string directionYParameter = "MoveY";

    [Header("방향별 캡슐 설정")]
    [SerializeField] private CapsuleShape downShape = new();
    [SerializeField] private CapsuleShape upShape = new();
    [SerializeField] private CapsuleShape leftShape = new();
    [SerializeField] private CapsuleShape rightShape = new();

    [Header("형태 변경")]
    [Tooltip("변태 후 고정 형태를 사용하는 경우에만 켠다.")]
    [SerializeField] private bool useFixedShape;

    [Tooltip("고정 형태에 해당하는 Animator State의 Tag.")]
    [SerializeField] private string fixedShapeTag = "FixedShape";
    [SerializeField] private CapsuleShape fixedShape = new();

    private int _directionXHash;
    private int _directionYHash;

    private CapsuleShape _directionShape;
    private CapsuleShape _currentShape;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (targetCollider == null)
            targetCollider = GetComponent<CapsuleCollider2D>();

        if (animator == null || targetCollider == null)
        {
            Debug.LogError(
                "DirectionalCapsuleCollider2D의 Animator와 CapsuleCollider2D를 연결해주세요.",
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

        // 최초 방향 값이 없으면 아래쪽 설정을 적용한다.
        _directionShape = downShape;
        _currentShape = null;

        RefreshShape();
    }

    private void LateUpdate()
    {
        RefreshShape();
    }

    private void OnValidate()
    {
        // 실행 중 Inspector에서 값을 조절하면 다음 갱신 때 다시 적용한다.
        _currentShape = null;
    }

    /// <summary>방향이나 형태가 바뀌었을 때만 캡슐 설정을 적용한다.</summary>
    private void RefreshShape()
    {
        CapsuleShape nextShape = GetShape();

        if (ReferenceEquals(nextShape, _currentShape))
            return;

        ApplyShape(nextShape);
        _currentShape = nextShape;
    }

    /// <summary>고정 형태 또는 현재 방향에 해당하는 설정을 반환한다.</summary>
    private CapsuleShape GetShape()
    {
        if (useFixedShape &&
            animator.GetCurrentAnimatorStateInfo(0).IsTag(fixedShapeTag))
        {
            return fixedShape;
        }

        float directionX = animator.GetFloat(_directionXHash);
        float directionY = animator.GetFloat(_directionYHash);

        // 정지하면서 방향 값이 0이 되어도 마지막 방향을 유지한다.
        if (directionX == 0f && directionY == 0f)
            return _directionShape;

        if (Mathf.Abs(directionX) > Mathf.Abs(directionY))
        {
            _directionShape = directionX > 0f
                ? rightShape
                : leftShape;
        }
        else
        {
            _directionShape = directionY > 0f
                ? upShape
                : downShape;
        }

        return _directionShape;
    }

    /// <summary>값이 달라진 속성만 변경해 불필요한 물리 모양 갱신을 줄인다.</summary>
    private void ApplyShape(CapsuleShape shape)
    {
        Vector2 size = new(
            Mathf.Max(0.001f, shape.size.x),
            Mathf.Max(0.001f, shape.size.y));

        if (targetCollider.direction != shape.direction)
            targetCollider.direction = shape.direction;

        if (targetCollider.size != size)
            targetCollider.size = size;

        if (targetCollider.offset != shape.offset)
            targetCollider.offset = shape.offset;
    }
}
