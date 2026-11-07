// 서버 탄환의 피해·절대 수명과 Client 표현을 관리합니다.
// 생성자에게서 Match 시계를 상속받고 정지 중 충돌/수명 진행을 막습니다.
using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using TeamHJD.Game.FieldTurrets.Contracts;
using Unity.Netcode;
using UnityEngine.Tilemaps;

public partial class EnemyProjectile : NetworkBehaviour
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
    private double _expiresAt;
    private readonly NetworkVariable<ulong> _ownerId = new(ulong.MaxValue);
    private FieldSimulationActor _simulation;
    
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
        BindReplicatedOwner();
        if (_simulation != null && _simulation.GameplayCancellation.IsCancellationRequested)
        {
            if (!IsSpawned || IsServer) Remove();
            return;
        }
        if (!FieldSimulationTime.CanAdvance(this)) return;
        if ((!IsSpawned || IsServer) && FieldSimulationTime.Seconds(this) >= _expiresAt)
        {
            Remove();
            return;
        }
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
        BindReplicatedOwner();
        _simulation?.Synchronize();
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
        _expiresAt = FieldSimulationTime.Seconds(this) + lifetime;
    }
    
    /// <summary>Spawner가 Initialize 전에 호출합니다. NetworkObject ID는 현재 실행의 소유자 연결에만 사용합니다.</summary>
    public void BindOwner(Component owner, bool destroyOnCompletion = false)
    {
        if (owner == null) return;
        var enemy = owner.GetComponentInParent<EnemyNetworkController>();
        if (enemy != null) { _checkpointExecutorId = enemy.SimulationOwnerId; _checkpointEnemyType = enemy.CheckpointEnemyType; }
        FieldSimulationTime.BindSpawned(gameObject, owner, destroyOnCompletion);
        _simulation = GetComponent<FieldSimulationActor>();
        var source = owner.GetComponentInParent<NetworkObject>();
        if (source != null && source.IsSpawned) _ownerId.Value = source.NetworkObjectId;
    }

    private void BindReplicatedOwner()
    {
        if (_simulation?.TimeSource != null || !IsSpawned || NetworkManager?.SpawnManager == null ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(_ownerId.Value, out var owner)) return;
        if (owner.TryGetComponent<EnemySpawnExecutor>(out var executor))
        {
            _simulation = executor.BindCheckpointProjectile(gameObject);
            return;
        }
        if (owner.GetComponent<FieldSimulationActor>()?.TimeSource == null) return;
        FieldSimulationTime.BindSpawned(gameObject, owner, false);
        _simulation = GetComponent<FieldSimulationActor>();
    }

    public override void OnNetworkDespawn()
    {
        if (_simulation?.TimeSource != null) _simulation.Unbind(_simulation.TimeSource);
        _simulation = null;
        base.OnNetworkDespawn();
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

    protected virtual void PlayImpactEffect(Vector3 position)
    {
    }
    
    protected void ApplyDamage(ITargetable target)
    {
        if (target == null || !target.CanBeTargeted)
            return;

        Transform targetTransform = target.TargetTransform;

        if (targetTransform == null)
            return;

        // 피해 수신자의 원본 계약만 사용합니다. Player 전용 분기를 두지 않습니다.
        IDamageableTarget damageable = targetTransform.GetComponentInParent<IDamageableTarget>();
        damageable?.ApplyDamage(_projectileDamage);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit || (IsSpawned && !IsServer) || !FieldSimulationTime.CanAdvance(this))
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

            NetworkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
