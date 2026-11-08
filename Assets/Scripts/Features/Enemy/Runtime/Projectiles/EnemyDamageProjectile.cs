using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using TeamHJD.Game.FieldTurrets.Contracts;
using Unity.Netcode;

/// <summary>직선 이동과 충돌 피해를 처리하는 공격 투사체다.</summary>
public partial class EnemyDamageProjectile : EnemyProjectile
{
    private int _projectileDamage;
    [SerializeField] private float projectileSpeed = 1f;
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField] private Collider2D collider2D;
    [SerializeField] private Animator animator;
    
    private bool _hasHit;
    
    private Vector2 _direction;
    
    private static readonly int XHash = Animator.StringToHash("DirectionX");
    private static readonly int YHash = Animator.StringToHash("DirectionY");

    private readonly NetworkVariable<Vector2> _directionState = new();

    protected override void Awake()
    {
        base.Awake();
        if(rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
        if(collider2D == null)
            collider2D = GetComponent<Collider2D>();
        if(animator == null)
            animator = GetComponent<Animator>();
        
        rigidbody2D.simulated = false;
    }

    /// <summary>
    /// 서버에서 물리를 실행하고 동기화된 발사 방향을 애니메이션에 적용한다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        rigidbody2D.simulated = IsServer;
        
        if (IsServer)
            _directionState.Value = _direction;

        ApplyAnimationDirection(_directionState.Value);
        _simulation?.Synchronize();
    }
    
    /// <summary>
    /// 피해량과 발사 방향을 설정하고 직선 이동 및 수명 계산을 시작한다.
    /// </summary>
    public void Initialize(int projectileDamage, Vector2 direction)
    {
        _projectileDamage = projectileDamage;
        _direction = direction.normalized;
        _hasHit = false;

        rigidbody2D.simulated = true;
        rigidbody2D.linearVelocity = _direction * projectileSpeed;

        ApplyAnimationDirection(_direction);
        BeginLifetime();
    }
    
    /// <summary>
    /// 연속적인 발사 방향에서 더 큰 축을 골라 상하좌우 애니메이션 방향을 적용한다.
    /// </summary>
    private void ApplyAnimationDirection(Vector2 direction)
    {
        Vector2 animationDirection;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            animationDirection = new Vector2(Mathf.Sign(direction.x), 0f);
        else
            animationDirection = new Vector2(0f, Mathf.Sign(direction.y));

        animator.SetFloat(XHash, animationDirection.x);
        animator.SetFloat(YHash, animationDirection.y);
    }

    /// <summary>
    /// 직격 피해를 적용한다. 범위 피해나 추가 효과는 파생 투사체에서 확장한다.
    /// </summary>
    protected virtual void OnHit(ITargetable target)
    {
        ApplyDamage(target);
    }

    /// <summary>
    /// 명중 위치에 필요한 효과를 파생 투사체에서 재생한다.
    /// </summary>
    protected virtual void PlayImpactEffect(Vector3 position)
    {
    }
    
    /// <summary>
    /// 타깃의 공통 피해 수신자에 투사체 피해량을 전달한다.
    /// </summary>
    protected void ApplyDamage(ITargetable target)
    {
        if (target == null || !target.CanBeTargeted)
            return;

        Transform targetTransform = target.TargetTransform;

        if (targetTransform == null)
            return;

        IDamageableTarget damageable = targetTransform.GetComponentInParent<IDamageableTarget>();
        damageable?.ApplyDamage(_projectileDamage);
    }

    /// <summary>
    /// 서버에서 유효한 Player·Turret 명중을 한 번만 처리한 뒤 효과를 재생하고 제거한다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit || !CanSimulate)
            return;

        ITargetable target = other.GetComponentInParent<ITargetable>();

        if (target == null || !target.CanBeTargeted)
            return;

        if (target.TargetTransform  == null || _projectileDamage <= 0)
            return;

        if (target.TargetType != TargetType.Player &&
            target.TargetType != TargetType.Turret)
            return;
        
        _hasHit = true;
        // 물리 충돌은 LateUpdate보다 먼저 발생하므로 명중 FX의 깊이도 갱신한다.
        UpdateDepth();
        OnHit(target);
        PlayImpactEffect(transform.position);
        Remove();
    }

}
