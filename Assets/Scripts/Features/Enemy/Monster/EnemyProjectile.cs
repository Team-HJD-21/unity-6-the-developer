using System;
using UnityEngine;
using TeamHJD.Game.FieldTurrets.Contracts;
using Unity.Netcode;

public class EnemyProjectile : NetworkBehaviour
{
    private int _projectileDamage;
    [SerializeField] private float projectileSpeed = 1f;
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField] private Collider2D collider2D;
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.01f)] private float lifetime = 5f;

    
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
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit || (IsSpawned && !IsServer))
            return;
        
        ITargetable target = other.GetComponentInParent<ITargetable>();

        if (target == null || !target.CanBeTargeted)
            return;
        
        Transform targetTransform = target.TargetTransform;
        
        if (targetTransform == null || _projectileDamage <= 0)
            return;

        if (target.TargetType == TargetType.Player)
        {
            PlayerInfo player =
                targetTransform.GetComponentInParent<PlayerInfo>();

            if (player == null)
                return;

            _hasHit = true;
            player.TakeDamage(_projectileDamage);
        }
        else if (target.TargetType == TargetType.Turret)
        {
            IDamageableTarget damageable =
                targetTransform.GetComponentInParent<IDamageableTarget>();

            if (damageable == null)
                return;

            _hasHit = true;
            damageable.ApplyDamage(_projectileDamage);
        }
        else
        {
            return;
        }
        
        Remove();
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
