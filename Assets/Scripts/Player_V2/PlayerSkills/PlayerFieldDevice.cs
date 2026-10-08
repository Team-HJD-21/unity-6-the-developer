using System.Collections.Generic;
using UnityEngine;

public enum PlayerFieldDeviceKind { Barrier, Beacon }

/// <summary>현장 지원 장비의 수명, 체력, 방벽 교차 판정과 비콘 유인 범위를 관리합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerFieldDevice : MonoBehaviour
{
    private static readonly List<PlayerFieldDevice> Active = new List<PlayerFieldDevice>();
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
    private Material _material;
    private LineRenderer _energy;
    public PlayerFieldDeviceKind Kind => _kind;
    public float Health => _health;
    public bool IsAlive => _initialized && isActiveAndEnabled && _health > 0 && _remaining > 0;
    public static bool HasActiveDevices => Active.Count > 0;
    public Sprite EmitterSprite => _firstEmitter != null ? _firstEmitter.sprite : null;
    public Vector3 EmitterScale => _firstEmitter != null ? _firstEmitter.transform.localScale : Vector3.one;
    public float VisualScale => Mathf.Max(.1f, _visualScale);
    private float _scanRemaining;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();

    public void Initialize(PlayerFieldDeviceKind kind, Vector3 start, Vector3 end,
        float width, float health, float duration, float radius = 0)
    {
        if (_initialized) return;
        _kind = kind;
        _start = start;
        _end = end;
        _width = Mathf.Max(.1f, width);
        _health = _maxHealth = Mathf.Max(1, health);
        _remaining = Mathf.Max(.1f, duration);
        _radius = Mathf.Max(0, radius);
        transform.position = kind == PlayerFieldDeviceKind.Barrier ? (start + end) * .5f : start;
        _material = new Material(Shader.Find("Sprites/Default"));
        var color = kind == PlayerFieldDeviceKind.Barrier ? new Color(.15f, .95f, 1) : new Color(1, .65f, .15f);
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
            _energy = _membrane != null ? _membrane : Line("Energy Membrane", _material, _width + .18f, color);
            _energy.sharedMaterial = _material;
            _energy.useWorldSpace = true;
            _energy.startWidth = _energy.endWidth = _width + .18f;
            _energy.startColor = _energy.endColor = new Color(color.r, color.g, color.b, .5f);
            SetLine(_energy, start + Vector3.up * .3f, end + Vector3.up * .3f);
            SetLine(Line("Ground Edge", _material, .06f, Color.white), start, end);
            Pulse("First Pulse", start, color);
            Pulse("Second Pulse", end, color);
        }
        else
        {
            Pulse("Beacon Emitter", start, color);
            _energy = Line("Attraction Ring", _material, .035f, new Color(1, .65f, .15f, .35f));
            Circle(_energy, start, _radius);
        }
        PlaceEmitter(_firstEmitter, start);
        PlaceEmitter(_secondEmitter, end);
        _initialized = true;
        if (!Active.Contains(this)) Active.Add(this);
    }

    private static void PlaceEmitter(SpriteRenderer emitter, Vector3 position)
    {
        if (emitter == null) return;
        emitter.transform.position = position;
        // 본체는 고정된 탑다운 그림이므로 방벽 각도가 바뀌어도 뒤집지 않습니다.
        emitter.transform.rotation = Quaternion.identity;
    }

    private void Pulse(string name, Vector3 position, Color color)
    {
        var ring = Line(name, _material, .08f, color);
        Circle(ring, position, .23f * VisualScale, 6);
        SetLine(Line(name + " Core", _material, .16f * VisualScale, Color.white), position, position + Vector3.up * (.2f * VisualScale));
    }

    private void Update()
    {
        if (!_initialized) return;
        _remaining -= Time.deltaTime;
        _hitFlash = Mathf.Max(0, _hitFlash - Time.deltaTime);
        if (_remaining <= 0) { Retire(); return; }
        _scanRemaining -= Time.deltaTime;
        if (_scanRemaining <= 0)
        {
            _scanRemaining = .2f;
            float scanRadius = _kind == PlayerFieldDeviceKind.Beacon ? _radius + 1 : Vector2.Distance(_start, _end) * .5f + 2;
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, scanRadius))
            {
                var enemy = hit.GetComponentInParent<EnemyController>();
                var legacy = hit.GetComponentInParent<Monster>();
                var actor = enemy != null ? enemy.gameObject : legacy != null ? legacy.gameObject : null;
                if (actor != null && actor.GetComponent<PlayerFieldEnemyAdapter>() == null)
                    actor.AddComponent<PlayerFieldEnemyAdapter>();
            }
        }
        if (_kind == PlayerFieldDeviceKind.Barrier)
        {
            float ratio = _health / _maxHealth;
            var color = _hitFlash > 0 ? Color.white : Color.Lerp(new Color(1, .3f, .2f), new Color(.15f, .95f, 1), ratio);
            color.a = .45f + .15f * Mathf.Sin(Time.time * (ratio < .3f ? 20 : 4));
            _energy.startColor = _energy.endColor = color;
        }
        else Circle(_energy, transform.position, _radius * (.9f + .1f * Mathf.Sin(Time.time * 3)));
        Color bodyColor = _hitFlash > 0 ? new Color(1, .6f, .6f) : Color.white;
        if (_firstEmitter != null) _firstEmitter.color = bodyColor;
        if (_secondEmitter != null) _secondEmitter.color = bodyColor;
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
        Active.Remove(this);
        if (_blocker != null) _blocker.enabled = false;
        gameObject.SetActive(false);
        Dispose(gameObject);
    }

    public Vector2 ApproachPoint(Vector2 origin) => _kind == PlayerFieldDeviceKind.Barrier && _blocker != null ?
        _blocker.ClosestPoint(origin) : (Vector2)transform.position;

    /// <summary>비콘보다 진입 경로를 실제로 막는 방벽을 우선합니다. 기존 타깃 자체는 변경하지 않습니다.</summary>
    public static PlayerFieldDevice Resolve(Vector2 origin, Vector2 originalGoal)
    {
        PlayerFieldDevice beacon = null;
        float nearest = float.PositiveInfinity;
        foreach (var device in Active)
        {
            if (device == null || !device.IsAlive || device._kind != PlayerFieldDeviceKind.Beacon) continue;
            float distance = ((Vector2)device.transform.position - origin).sqrMagnitude;
            if (distance > device._radius * device._radius || distance >= nearest) continue;
            beacon = device;
            nearest = distance;
        }
        Vector2 goal = beacon != null ? (Vector2)beacon.transform.position : originalGoal;
        PlayerFieldDevice wall = null;
        float closestFraction = float.PositiveInfinity;
        foreach (var device in Active)
        {
            if (device == null || !device.IsAlive || device._kind != PlayerFieldDeviceKind.Barrier) continue;
            if (!device.Intersects(origin, goal, out float fraction) || fraction >= closestFraction) continue;
            wall = device;
            closestFraction = fraction;
        }
        return wall != null ? wall : beacon;
    }

    private bool Intersects(Vector2 origin, Vector2 goal, out float fraction)
    {
        // 적의 발밑 반경을 고려해 모서리를 비스듬히 통과하는 것을 방지합니다.
        Vector2 a = transform.InverseTransformPoint(origin);
        Vector2 b = transform.InverseTransformPoint(goal);
        Vector2 delta = b - a;
        var half = new Vector2(Vector2.Distance(_start, _end) * .5f + .15f, _width * .5f + .15f);
        float low = 0, high = 1;
        for (int axis = 0; axis < 2; axis++)
        {
            if (Mathf.Abs(delta[axis]) < .00001f)
            {
                if (Mathf.Abs(a[axis]) > half[axis]) { fraction = 0; return false; }
                continue;
            }
            float enter = (-half[axis] - a[axis]) / delta[axis];
            float leave = (half[axis] - a[axis]) / delta[axis];
            if (enter > leave) { float swap = enter; enter = leave; leave = swap; }
            low = Mathf.Max(low, enter);
            high = Mathf.Min(high, leave);
            if (low > high) { fraction = 0; return false; }
        }
        fraction = low;
        return true;
    }

    public static LineRenderer Line(string name, Material material, float width, Color color, Transform parent = null)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        if (parent != null) line.transform.SetParent(parent, false);
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCapVertices = 4;
        line.sortingOrder = 100;
        return line;
    }

    private LineRenderer Line(string name, Material material, float width, Color color) => Line(name, material, width, color, transform);

    public static void SetLine(LineRenderer line, Vector3 start, Vector3 end)
    {
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    public static void Circle(LineRenderer line, Vector3 center, float radius, int segments = 48)
    {
        line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }

    public static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private void OnEnable() { if (_initialized && !Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);
    private void OnDestroy() { Active.Remove(this); Dispose(_material); }
}
