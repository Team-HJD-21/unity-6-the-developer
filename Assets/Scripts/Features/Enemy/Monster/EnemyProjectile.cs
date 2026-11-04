using System;
using UnityEngine;
using TeamHJD.Game.FieldTurrets.Contracts;
using Unity.Netcode;
using UnityEngine.Tilemaps;

public class EnemyProjectile : NetworkBehaviour
{
    private int _projectileDamage;
    [SerializeField] private float projectileSpeed = 1f;
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField] private Collider2D collider2D;
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.01f)] private float lifetime = 5f;

    [Header("깊이 범위")]
    [SerializeField] private float minDepth = 65f;
    [SerializeField] private float maxDepth = 70f;

    private TilemapRenderer _groundRenderer;
    
    private bool _hasHit;
    
    private Vector2 _direction;
    
    private static readonly int XHash = Animator.StringToHash("DirectionX");
    private static readonly int YHash = Animator.StringToHash("DirectionY");

    private readonly NetworkVariable<Vector2> _directionState = new();

    void Awake()
    {
        if(rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
        if(collider2D == null)
            collider2D = GetComponent<Collider2D>();
        if(animator == null)
            animator = GetComponent<Animator>();
        
        rigidbody2D.simulated = false;
        _groundRenderer = FindGroundRenderer();
    }

    private void LateUpdate()
    {
        if (IsSpawned && !IsServer)
            return;

        UpdateDepth();
    }

    public override void OnNetworkSpawn()
    {
        rigidbody2D.simulated = IsServer;
        
        if (IsServer)
            _directionState.Value = _direction;

        ApplyAnimationDirection(_directionState.Value);
    }
    
    public void Initialize(int projectileDamage, Vector2 direction)
    {
        _projectileDamage = projectileDamage;
        _direction = direction.normalized;
        _hasHit = false;

        UpdateDepth();

        rigidbody2D.simulated = true;
        rigidbody2D.linearVelocity = _direction * projectileSpeed;

        ApplyAnimationDirection(_direction);
        Invoke(nameof(Remove), lifetime);
    }
    
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

    protected virtual void OnHit(ITargetable target)
    {
        ApplyDamage(target);
    }

    protected void ApplyDamage(ITargetable target)
    {
        if (target == null || !target.CanBeTargeted)
            return;

        Transform targetTransform = target.TargetTransform;

        if (targetTransform == null)
            return;

        if (target.TargetType == TargetType.Player)
        {
            PlayerInfo player =
                targetTransform.GetComponentInParent<PlayerInfo>();

            if (player != null)
                player.TakeDamage(_projectileDamage);
        }
        else if (target.TargetType == TargetType.Turret)
        {
            IDamageableTarget damageable =
                targetTransform.GetComponentInParent<IDamageableTarget>();

            if (damageable != null)
                damageable.ApplyDamage(_projectileDamage);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit || (IsSpawned && !IsServer))
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
        Remove();
    }

    private void UpdateDepth()
    {
        if (_groundRenderer == null)
            return;

        Bounds groundBounds = _groundRenderer.bounds;
        Vector3 position = transform.position;
        float normalizedY = Mathf.InverseLerp(
            groundBounds.min.y, groundBounds.max.y, position.y);
        position.z = Mathf.Lerp(minDepth, maxDepth, normalizedY);
        transform.position = position;
    }

    private TilemapRenderer FindGroundRenderer()
    {
        TilemapRenderer groundRenderer = null;
        long largestArea = 0;

        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tilemap.gameObject.scene != gameObject.scene ||
                !tilemap.TryGetComponent(out TilemapCollider2D groundCollider) ||
                !groundCollider.enabled || groundCollider.isTrigger ||
                !tilemap.TryGetComponent(out TilemapRenderer renderer))
                continue;

            long area = (long)tilemap.size.x * tilemap.size.y;
            if (area <= largestArea)
                continue;

            largestArea = area;
            groundRenderer = renderer;
        }

        return groundRenderer;
    }
    
    private void Remove()
    {
        if (IsSpawned)
        {
            if (!IsServer)
                return;

            CancelInvoke(nameof(Remove));
            NetworkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
