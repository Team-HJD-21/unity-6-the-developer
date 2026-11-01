using UnityEngine;

/// <summary>임시 포탑의 체력·적 충돌과 피격 틴트를 담당합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TemporaryTurretHealth : MonoBehaviour, IPlayerFieldTarget
{
    [SerializeField, Min(1)] private float _maxHealth = 100;
    [SerializeField] private BoxCollider2D _blocker;
    [Tooltip("설치된 본체가 막을 레이어: Player / Enemy")]
    [SerializeField] private LayerMask _blockingLayers = (1 << 18) | (1 << 7);
    private float _health, _hitRemaining;
    private bool _initialized;
    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;
    private readonly PlayerFieldEnemyScanner _scanner = new();
    public float Health => _health;
    public float MaxHealth => Mathf.Max(1, _maxHealth);
    public float HealthRatio => Mathf.Clamp01(_health / MaxHealth);
    public bool IsAlive => this != null && _initialized && isActiveAndEnabled && _health > 0;
    public PlayerFieldDeviceKind Kind => PlayerFieldDeviceKind.TemporaryTurret;
    public Vector3 Position => transform.position;
    public float Radius => _blocker != null ? ((Vector2)_blocker.bounds.extents).magnitude : 0f;
    private void Awake()
    {
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
        _initialized = true;
    }
    private void OnEnable() { if (_initialized) PlayerFieldDeviceRegistry.Register(this); }
    private void Update()
    {
        _scanner.Tick(transform.position, Radius + 2, Time.deltaTime);
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
        fraction = 0f;
        if (_blocker == null || !_blocker.enabled) return false;
        // 정렬용 Z는 무시하고 실제 사각형 본체의 XY 경계에 접근하도록 합니다.
        Bounds bounds = _blocker.bounds;
        bounds.center = new Vector3(bounds.center.x, bounds.center.y, 0f);
        bounds.size = new Vector3(bounds.size.x + .3f, bounds.size.y + .3f, 1f);
        Vector3 start = new(origin.x, origin.y, 0f);
        if (bounds.Contains(start)) return true;
        Vector2 delta = goal - origin;
        float distance = delta.magnitude;
        if (distance <= .000001f || !bounds.IntersectRay(new Ray(start, delta / distance), out float hitDistance)) return false;
        fraction = hitDistance / distance;
        return fraction <= 1f;
    }
    private void OnDisable() => PlayerFieldDeviceRegistry.Unregister(this);
    private void OnDestroy() => PlayerFieldDeviceRegistry.Unregister(this);
}
