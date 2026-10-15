using UnityEngine;
using UnityEngine.Serialization;

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

    private void Reset() => CacheReferences();
    private void Awake() => CacheReferences();

    private void CacheReferences()
    {
        if (_body == null || _body.gameObject != gameObject) _body = GetComponent<Rigidbody2D>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>색상·피해량·월드 방향으로 초기화합니다. 발사자의 이동속도는 전달받지 않습니다.</summary>
    public bool Initialize(Color color, float damage, Vector2 direction)
    {
        if (_body == null || !PlayerAimUtility.IsValidDirection(direction) ||
            !IsFinite(_speed) || _speed < 0f || !IsFinite(_lifetime) || _lifetime <= 0f ||
            !IsFinite(damage) || damage < 0f)
        {
            Debug.LogWarning("[PlayerBullet] Rigidbody2D, finite direction, non-negative speed/damage and positive lifetime are required.", this);
            return false;
        }
        _damage = damage;
        _hasHit = false;
        if (_spriteRenderer != null) _spriteRenderer.color = color;
        Launch(direction.normalized);
        return true;
    }

    private void Launch(Vector2 direction)
    {
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

    private void OnCollisionEnter2D(Collision2D collision) => ProcessHit(collision.gameObject);
    private void OnTriggerEnter2D(Collider2D other) => ProcessHit(other.gameObject);

    private void ProcessHit(GameObject target)
    {
        if (_hasHit || target == null || target.CompareTag("Player") || target.CompareTag("Bullet")) return;
        bool isEnemy = target.CompareTag(_targetTag) || target.transform.root.CompareTag(_targetTag);
        if (isEnemy)
        {
            var health = target.GetComponentInParent<EnemyHealth>();
            if (health != null) health.TakeDamage(Mathf.RoundToInt(_damage));
        }
        _hasHit = true;
        Destroy(gameObject);
    }
}
