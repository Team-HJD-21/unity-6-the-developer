using UnityEngine;

public enum PlayerFieldDeviceKind { Barrier, Beacon, PlayerTurret }

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
    [Header("Collision")]
    [SerializeField] private BoxCollider2D _blocker;
    [SerializeField] private PlayerFieldBodyCollision _bodyCollision;
    [SerializeField] private PlayerSkillDepth _depth;
    private Vector2 _start, _end;
    private float _width, _maxHealth, _health, _remaining, _radius, _hitFlash;
    private bool _initialized;
    private PlayerFieldDeviceVisual _visual;
    private PlayerFieldTargetCollision _collision;
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
        float width, float health, float duration, float radius = 0, Transform owner = null)
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
            if (_blocker != null)
            {
                _blocker.size = new Vector2(Vector2.Distance(start, end), _width);
                _blocker.enabled = true;
            }
        }
        _visual = new PlayerFieldDeviceVisual(transform, _firstEmitter, _secondEmitter, _membrane, VisualScale);
        _visual.Initialize(kind, start, end, _width, _radius);
        _bodyCollision?.AllowInitialExit();
        _collision = new PlayerFieldTargetCollision(_blocker, _bodyCollision != null ? _bodyCollision.Bodies : null);
        _depth?.Bind(owner);
        _initialized = true;
        PlayerFieldDeviceRegistry.Register(this);
    }
    private void Update()
    {
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
    public Vector2 ApproachPoint(Vector2 origin)
    {
        Vector2 point = _bodyCollision != null ? _bodyCollision.ClosestPoint(origin) : (Vector2)transform.position;
        if (_kind != PlayerFieldDeviceKind.Barrier || _blocker == null) return point;
        Vector2 membranePoint = _blocker.ClosestPoint(origin);
        return (membranePoint - origin).sqrMagnitude < (point - origin).sqrMagnitude ? membranePoint : point;
    }
    public bool TryGetContact(Collider2D body, out ColliderDistance2D contact)
    {
        contact = default;
        return _collision != null && _collision.TryGetContact(body, out contact);
    }
    public bool Intersects(Vector2 origin, Vector2 goal, out float fraction, Collider2D body = null)
    {
        if (_collision != null && _collision.HasColliders)
            return _collision.Intersects(origin, goal, out fraction, body);
        // 콜라이더 없이 조립한 기존 테스트 장비의 선분 계약을 유지합니다.
        return PlayerBarrierGeometry.Intersects(transform, _start, _end, _width, origin, goal, out fraction);
    }
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
