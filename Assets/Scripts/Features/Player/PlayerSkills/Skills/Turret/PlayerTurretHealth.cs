// Player 소형 터렛의 실제 체력과 제거 상태를 소유합니다.
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>플레이어 터렛의 체력·적 충돌과 피격 틴트를 담당합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed partial class PlayerTurretHealth : MonoBehaviour, IPlayerFieldTarget, TeamHJD.Game.Contracts.IEntityIdentity
{
    [SerializeField, Min(1)] private float _maxHealth = 100;
    [SerializeField] private BoxCollider2D _blocker;
    [Tooltip("설치된 본체가 막을 레이어: PlayerFeetBox / EnemyFeetBox")]
    [SerializeField] private LayerMask _blockingLayers = (1 << 18) | (1 << 24);
    private float _health, _hitRemaining;
    private bool _initialized;
    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;
    private PlayerDeviceContactQuery _collision;
    private readonly PlayerFieldEnemyScanner _scanner = new();
    public float Health => _health;
    public float MaxHealth => Mathf.Max(1, _maxHealth);
    public float HealthRatio => Mathf.Clamp01(_health / MaxHealth);
    public bool IsAlive => this != null && _initialized && isActiveAndEnabled && _health > 0;
    public PlayerFieldDeviceKind Kind => PlayerFieldDeviceKind.PlayerTurret;
    public Vector3 Position => transform.position;
    public float Radius => _blocker != null ? ((Vector2)_blocker.bounds.extents).magnitude : 0f;
    private void Awake()
    {
        TeamHJD.Game.Presentation.Scene.CheckpointActorFactory.EnsureIdentity(gameObject);
        _health = MaxHealth;
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _originalColors[i] = _renderers[i].color;
        int layer = LayerMask.NameToLayer("PlayerFieldDevice");
        if (layer >= 0) gameObject.layer = layer;
        if (_blocker == null) _blocker = GetComponent<BoxCollider2D>();
        _blocker.isTrigger = false;
        // 충돌체의 크기와 오프셋은 프리팹에서 편집하며 기존 전역 충돌 제외 설정보다 우선합니다.
        _blocker.includeLayers = _blockingLayers;
        _blocker.excludeLayers = ~_blockingLayers.value;
        _blocker.layerOverridePriority = 10;
        _collision = new PlayerDeviceContactQuery(_blocker);
        _initialized = true;
    }
    private void OnEnable() { if (_initialized) PlayerFieldDeviceRegistry.Register(this); }
    private void Update()
    {
        if (!FieldSimulationTime.CanAdvance(this)) return;
        _scanner.Tick(transform.position, Radius + 2, FieldSimulationTime.DeltaTime(this));
        _hitRemaining = Mathf.Max(0, _hitRemaining - FieldSimulationTime.DeltaTime(this));
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].color = _hitRemaining > 0 ? new Color(1, .6f, .6f) : _originalColors[i];
    }
    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        _health = Mathf.Max(0, _health - damage);
        _hitRemaining = .12f;
        if (_health > 0) return;
        GetComponent<PlayerTurret>()?.ReleaseDestroyedOwner();
        if (_blocker != null) _blocker.enabled = false;
        gameObject.SetActive(false);
        PlayerFieldDevice.Dispose(gameObject);
    }
    public Vector2 ApproachPoint(Vector2 origin) => _blocker != null ? _blocker.ClosestPoint(origin) : (Vector2)transform.position;
    public bool TryGetContact(Collider2D body, out ColliderDistance2D contact)
    {
        contact = default;
        return _collision != null && _collision.TryGetContact(body, out contact);
    }
    public bool Intersects(Vector2 origin, Vector2 goal, out float fraction, Collider2D body = null)
    {
        fraction = 0;
        return _collision != null && _collision.Intersects(origin, goal, out fraction, body);
    }
    private void OnDisable() => PlayerFieldDeviceRegistry.Unregister(this);
    private void OnDestroy() => PlayerFieldDeviceRegistry.Unregister(this);
}
