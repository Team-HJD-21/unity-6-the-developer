using UnityEngine;

public enum PlayerFieldDeviceKind { Barrier, Beacon, TemporaryTurret }

/// <summary>장비의 체력과 수명을 관리하고 표현·검색·충돌 판정을 연결합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerFieldDevice : MonoBehaviour, IPlayerFieldTarget
{
    [SerializeField] private PlayerFieldDeviceKind _kind;
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer _firstEmitter;
    [SerializeField] private SpriteRenderer _secondEmitter;
    [SerializeField] private LineRenderer _membrane;
    [SerializeField, Min(.1f)] private float _visualScale = 1;
    private Vector2 _start, _end;
    private float _width, _maxHealth, _health, _remaining, _radius, _hitFlash;
    private bool _initialized;
    private BoxCollider2D _blocker;
    private PlayerFieldDeviceVisual _visual;
    private readonly PlayerFieldEnemyScanner _scanner = new();
    public PlayerFieldDeviceKind Kind => _kind;
    public float Health => _health;
    public float Radius => _radius;
    public Vector3 Position => transform.position;
    public bool IsAlive => this != null && _initialized && isActiveAndEnabled && _health > 0 && _remaining > 0;
    public static bool HasActiveDevices => PlayerFieldDeviceRegistry.HasActiveDevices;
    public Sprite EmitterSprite => _firstEmitter != null ? _firstEmitter.sprite : null;
    public Vector3 EmitterScale => _firstEmitter != null ? _firstEmitter.transform.localScale : Vector3.one;
    public float VisualScale => Mathf.Max(.1f, _visualScale);
    public void Initialize(PlayerFieldDeviceKind kind, Vector3 start, Vector3 end,
        float width, float health, float duration, float radius = 0)
    {
        if (_initialized) return;
        _kind = kind; _start = start; _end = end;
        _width = Mathf.Max(.1f, width);
        _health = _maxHealth = Mathf.Max(1, health);
        _remaining = Mathf.Max(.1f, duration); _radius = Mathf.Max(0, radius);
        transform.position = kind == PlayerFieldDeviceKind.Barrier ? (start + end) * .5f : start;
        if (kind == PlayerFieldDeviceKind.Barrier)
        {
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
            int layer = LayerMask.NameToLayer("PlayerFieldDevice");
            if (layer >= 0) gameObject.layer = layer;
            _blocker = gameObject.AddComponent<BoxCollider2D>();
            _blocker.size = new Vector2(Vector2.Distance(start, end), _width);
            // 아군 탄환과 플레이어는 통과합니다. 이동을 직접 처리하는 적은 경로 교차 검사도 사용합니다.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            _blocker.excludeLayers = enemyLayer >= 0 ? ~(1 << enemyLayer) : ~0;
            _blocker.layerOverridePriority = 10;
        }
        _visual = new PlayerFieldDeviceVisual(transform, _firstEmitter, _secondEmitter, _membrane, VisualScale);
        _visual.Initialize(kind, start, end, _width, _radius);
        _initialized = true;
        PlayerFieldDeviceRegistry.Register(this);
    }
    private void Update()
    {
        if (!PlayerFieldSimulationUtility.CanAdvance(this)) return;
        if (!_initialized) return;
        _remaining -= Time.deltaTime;
        _hitFlash = Mathf.Max(0, _hitFlash - Time.deltaTime);
        if (_remaining <= 0) { Retire(); return; }
        float scanRadius = _kind == PlayerFieldDeviceKind.Beacon ? _radius + 1 : Vector2.Distance(_start, _end) * .5f + 2;
        _scanner.Tick(transform.position, scanRadius, Time.deltaTime);
        _visual.Refresh(_kind, _health / _maxHealth, _hitFlash > 0, _radius);
    }
    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        _health = Mathf.Max(0, _health - damage);
        _hitFlash = .12f;
        if (_health <= 0) Retire();
    }
    private void Retire()
    {
        PlayerFieldDeviceRegistry.Unregister(this);
        if (_blocker != null) _blocker.enabled = false;
        gameObject.SetActive(false);
        Dispose(gameObject);
    }
    public Vector2 ApproachPoint(Vector2 origin) => _kind == PlayerFieldDeviceKind.Barrier && _blocker != null ?
        _blocker.ClosestPoint(origin) : (Vector2)transform.position;
    public bool Intersects(Vector2 origin, Vector2 goal, out float fraction) =>
        PlayerBarrierGeometry.Intersects(transform, _start, _end, _width, origin, goal, out fraction);
    public static PlayerFieldDevice Resolve(Vector2 origin, Vector2 originalGoal) => PlayerFieldDeviceRegistry.Resolve(origin, originalGoal);
    public static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
    private void OnEnable() { if (_initialized) PlayerFieldDeviceRegistry.Register(this); }
    private void OnDisable() => PlayerFieldDeviceRegistry.Unregister(this);
    private void OnDestroy() { PlayerFieldDeviceRegistry.Unregister(this); _visual?.Dispose(); }
}
