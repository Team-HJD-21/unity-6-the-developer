using UnityEngine;

/// <summary>임시 포탑의 체력·적 충돌과 피격 틴트를 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class TemporaryTurretHealth : MonoBehaviour, IPlayerFieldTarget
{
    [SerializeField, Min(1)] private float _maxHealth = 100;
    [SerializeField, Min(.1f)] private float _blockRadius = .35f;
    private float _health, _hitRemaining;
    private bool _initialized;
    private CircleCollider2D _blocker;
    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;
    private readonly PlayerFieldEnemyScanner _scanner = new();
    public float Health => _health;
    public float MaxHealth => Mathf.Max(1, _maxHealth);
    public float HealthRatio => Mathf.Clamp01(_health / MaxHealth);
    public bool IsAlive => this != null && _initialized && isActiveAndEnabled && _health > 0;
    public PlayerFieldDeviceKind Kind => PlayerFieldDeviceKind.TemporaryTurret;
    public Vector3 Position => transform.position;
    public float Radius => _blockRadius;
    private void Awake()
    {
        _health = MaxHealth;
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _originalColors[i] = _renderers[i].color;
        int layer = LayerMask.NameToLayer("PlayerFieldDevice");
        if (layer >= 0) gameObject.layer = layer;
        _blocker = GetComponent<CircleCollider2D>();
        if (_blocker == null) _blocker = gameObject.AddComponent<CircleCollider2D>();
        _blocker.isTrigger = false;
        _blocker.radius = _blockRadius / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x));
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        _blocker.excludeLayers = enemyLayer >= 0 ? ~(1 << enemyLayer) : ~0;
        _blocker.layerOverridePriority = 10;
        _initialized = true;
    }
    private void OnEnable() { if (_initialized) PlayerFieldDeviceRegistry.Register(this); }
    private void Update()
    {
        _scanner.Tick(transform.position, _blockRadius + 2, Time.deltaTime);
        _hitRemaining = Mathf.Max(0, _hitRemaining - Time.deltaTime);
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].color = _hitRemaining > 0 ? new Color(1, .6f, .6f) : _originalColors[i];
    }
    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        _health = Mathf.Max(0, _health - damage);
        _hitRemaining = .12f;
        if (_health > 0) return;
        if (_blocker != null) _blocker.enabled = false;
        gameObject.SetActive(false);
        PlayerFieldDevice.Dispose(gameObject);
    }
    public Vector2 ApproachPoint(Vector2 origin) => _blocker != null ? _blocker.ClosestPoint(origin) : (Vector2)transform.position;
    public bool Intersects(Vector2 origin, Vector2 goal, out float fraction)
    {
        Vector2 delta = goal - origin;
        float squared = delta.sqrMagnitude;
        Vector2 offset = origin - (Vector2)transform.position;
        float radius = _blockRadius + .15f;
        float c = offset.sqrMagnitude - radius * radius;
        if (c <= 0) { fraction = 0; return true; }
        if (squared <= .000001f) { fraction = 0; return false; }
        float b = Vector2.Dot(offset, delta);
        float discriminant = b * b - squared * c;
        if (discriminant < 0) { fraction = 0; return false; }
        fraction = (-b - Mathf.Sqrt(discriminant)) / squared;
        return fraction >= 0 && fraction <= 1;
    }
    private void OnDisable() => PlayerFieldDeviceRegistry.Unregister(this);
    private void OnDestroy() => PlayerFieldDeviceRegistry.Unregister(this);
}
