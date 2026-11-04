using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

/// <summary>설정된 고정 속도로 이동하며 수명과 충돌 피해를 처리하는 플레이어 탄환입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerBullet : MonoBehaviour
{
    [Header("References")]
    [FormerlySerializedAs("rb")]
    [SerializeField] private Rigidbody2D _body;
    [FormerlySerializedAs("spriteRenderer")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("Flight")]
    [FormerlySerializedAs("bulletSpeed")]
    [SerializeField, Min(0f)] private float _speed = 6f;
    [FormerlySerializedAs("lifeTime")]
    [SerializeField, Min(.01f)] private float _lifetime = 2f;

    [Header("Damage")]
    [FormerlySerializedAs("bulletDamage")]
    [SerializeField, Min(0f)] private float _damage = 13f;
    [FormerlySerializedAs("targetTag")]
    [SerializeField] private string _targetTag = "Enemy";
    private bool _hasHit;
    private PlayerProjectileView _view;
    private readonly List<Collider2D> _hitboxes = new();
    private readonly List<RaycastHit2D> _flightHits = new(8);
    private ContactFilter2D _hitFilter;
    private Vector2 _flightDirection;
    private static readonly Dictionary<int, Tilemap> SceneGrounds = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneGroundCache()
    {
        SceneGrounds.Clear();
        SceneManager.sceneUnloaded -= ForgetSceneGround;
        SceneManager.sceneUnloaded += ForgetSceneGround;
    }

    private static void ForgetSceneGround(Scene scene) => SceneGrounds.Remove(scene.handle);

    private void Reset() => CacheReferences();
    private void Awake() => CacheReferences();

    private void CacheReferences()
    {
        if (_body == null || _body.gameObject != gameObject) _body = GetComponent<Rigidbody2D>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        _view = GetComponent<PlayerProjectileView>();
        ConfigureHitboxes();
    }

    private void ConfigureHitboxes()
    {
        _hitboxes.Clear();
        int hitLayers = 0;
        foreach (Collider2D hitbox in GetComponentsInChildren<Collider2D>(true))
        {
            if (hitbox.attachedRigidbody != _body) continue;
            hitbox.isTrigger = true;
            _hitboxes.Add(hitbox);
            int included = Physics2D.GetLayerCollisionMask(hitbox.gameObject.layer) |
                hitbox.includeLayers.value | _body.includeLayers.value;
            hitLayers |= included & ~(hitbox.excludeLayers.value | _body.excludeLayers.value);
        }
        _hitFilter = new ContactFilter2D { useTriggers = true };
        _hitFilter.SetLayerMask(hitLayers);
    }

    /// <summary>색상·피해량·월드 방향으로 초기화합니다. 발사자의 이동속도는 전달받지 않습니다.</summary>
    public bool Initialize(Color color, float damage, Vector2 direction)
        => Initialize(color, damage, direction, false);

    public bool Initialize(Color color, float damage, Vector2 direction, bool cryogenic)
    {
        if (_body == null || _hitboxes.Count == 0 || !PlayerAimUtility.IsValidDirection(direction) ||
            !IsFinite(_speed) || _speed < 0f || !IsFinite(_lifetime) || _lifetime <= 0f ||
            !IsFinite(damage) || damage < 0f)
        {
            Debug.LogWarning("[PlayerBullet] Rigidbody2D, attached Collider2D, finite direction, non-negative speed/damage and positive lifetime are required.", this);
            return false;
        }
        _damage = damage;
        _hasHit = false;
        if (_spriteRenderer != null) _spriteRenderer.color = color;
        if (_view != null) _view.Configure(cryogenic, color);
        Launch(direction.normalized);
        return true;
    }

    private void Launch(Vector2 direction)
    {
        _flightDirection = direction;
#if UNITY_6000_0_OR_NEWER
        _body.linearVelocity = direction * _speed;
#else
        _body.velocity = direction * _speed;
#endif
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        Destroy(gameObject, _lifetime);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void FixedUpdate()
    {
        if (_hasHit || _flightDirection == Vector2.zero || _body == null) return;
        // 빠른 Trigger 탄환은 한 물리 틱 안에 얇은 본체를 지나칠 수 있어 이동 경로를 먼저 검사합니다.
        _body.Cast(_flightDirection, _hitFilter, _flightHits, _speed * Time.fixedDeltaTime);
        RaycastHit2D nearest = default;
        foreach (RaycastHit2D hit in _flightHits)
        {
            if (!TryGetHitTarget(hit.collider, out _, out _)) continue;
            if (nearest.collider == null || hit.distance < nearest.distance) nearest = hit;
        }
        if (nearest.collider == null) return;
        Vector2 stopPosition = _body.position + _flightDirection * nearest.distance;
        transform.position = new Vector3(stopPosition.x, stopPosition.y, transform.position.z);
        ProcessHit(nearest.collider);
    }

    private void OnTriggerEnter2D(Collider2D other) => ProcessHit(other);

    private bool TryGetHitTarget(Collider2D hitbox, out GameObject target, out bool isEnemy)
    {
        target = null;
        isEnemy = false;
        if (hitbox == null || hitbox.attachedRigidbody == _body) return false;
        // 경로 Cast는 물리 접촉 제외와 별개로 장벽을 찾습니다. 양끝 본체는 계속 적중시킵니다.
        var device = hitbox.GetComponentInParent<PlayerFieldDevice>();
        if (device != null && device.IsBarrierMembrane(hitbox)) return false;
        // Rigidbody 소유자를 사용하되 자식 Collider와 부모의 태그도 함께 확인합니다.
        target = hitbox.attachedRigidbody != null ? hitbox.attachedRigidbody.gameObject : hitbox.gameObject;
        if (hitbox.CompareTag("Ground") || target.CompareTag("Ground") || IsSceneGround(hitbox)) return false;
        for (Transform ancestor = hitbox.transform; ancestor != null; ancestor = ancestor.parent)
        {
            if (ancestor.CompareTag("Player") || ancestor.CompareTag("Bullet")) return false;
            isEnemy |= ancestor.CompareTag(_targetTag);
        }
        // 적 피격 Trigger는 유지하고 포탑 사거리·상호작용 등 감지용 Trigger는 통과합니다.
        return isEnemy || !hitbox.isTrigger;
    }

    private static bool IsSceneGround(Collider2D hitbox)
    {
        if (!hitbox.TryGetComponent(out Tilemap map)) return false;
        Scene scene = hitbox.gameObject.scene;
        if (!SceneGrounds.TryGetValue(scene.handle, out Tilemap ground) || ground == null || !ground.isActiveAndEnabled)
        {
            // 실제 Initial_Stage의 지면은 Default/Untagged입니다. 기존 설치 판정과 같은 지면만 제외합니다.
            ground = PlayerTurretPlacementRules.FindGround(scene);
            if (ground != null) SceneGrounds[scene.handle] = ground;
        }
        return map == ground;
    }

    private void ProcessHit(Collider2D other)
    {
        if (_hasHit || !TryGetHitTarget(other, out GameObject target, out bool isEnemy)) return;
        _hasHit = true;
#if UNITY_6000_0_OR_NEWER
        _body.linearVelocity = Vector2.zero;
#else
        _body.velocity = Vector2.zero;
#endif
        foreach (Collider2D hitbox in _hitboxes)
            if (hitbox != null) hitbox.enabled = false;
        if (isEnemy)
        {
            var health = target.GetComponentInParent<EnemyHealth>();
            if (health != null) health.TakeDamage(Mathf.RoundToInt(_damage));
        }
        if (_view != null) _view.ShowImpact();
        Destroy(gameObject);
    }
}
