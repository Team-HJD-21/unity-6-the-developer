using System.Collections.Generic;
using TeamHJD.Game.Turrets;
using UnityEngine;

/// <summary>보조 포탑 전용 실탄. 플레이어 무기 효과와 독립적으로 적 한 명에게 피해를 적용합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class TemporaryTurretProjectile : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _speed = 70;
    [SerializeField, Min(.1f)] private float _lifetime = 3;
    private Rigidbody2D _body;
    private float _damage, _remaining;
    private bool _initialized, _hit;
    private readonly List<RaycastHit2D> _castResults = new List<RaycastHit2D>();

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.gravityScale = 0;
        _body.freezeRotation = true;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public bool Initialize(Vector2 direction, float damage)
    {
        if (_initialized || !isActiveAndEnabled || !PlayerAimUtility.IsValidDirection(direction) ||
            damage < 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
        if (_body == null) Awake();
        _damage = damage;
        _remaining = Mathf.Max(.1f, _lifetime);
        _body.linearVelocity = direction.normalized * Mathf.Max(.1f, _speed);
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        _initialized = true;
        return true;
    }

    private void Update()
    {
        if (!_initialized) return;
        _remaining -= Time.deltaTime;
        if (_remaining <= 0) Retire();
    }

    private void OnTriggerEnter2D(Collider2D other) => ProcessHit(other);
    private void OnCollisionEnter2D(Collision2D collision) => ProcessHit(collision.collider);

    private void FixedUpdate()
    {
        if (!_initialized || _hit || _body == null) return;
        var velocity = _body.linearVelocity;
        if (!PlayerAimUtility.IsValidDirection(velocity)) return;
        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer)
        };
        _castResults.Clear();
        _body.Cast(velocity.normalized, filter, _castResults, velocity.magnitude * Time.fixedDeltaTime);
        RaycastHit2D nearest = default;
        float distance = float.PositiveInfinity;
        foreach (var hit in _castResults)
        {
            if (hit.collider == null || IsPassThrough(hit.collider) || hit.distance >= distance) continue;
            nearest = hit;
            distance = hit.distance;
        }
        if (nearest.collider != null) ProcessHit(nearest.collider);
    }

    private static bool IsPassThrough(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInputHandler>() != null ||
            other.GetComponentInParent<TemporaryTurret>() != null ||
            other.GetComponentInParent<TurretBase>() != null ||
            other.GetComponentInParent<PlayerFieldDevice>() != null ||
            other.CompareTag("Bullet") || other.CompareTag("Tower") || other.CompareTag("CU")) return true;
        return other.isTrigger && other.GetComponentInParent<EnemyHealth>() == null && other.GetComponentInParent<Monster>() == null;
    }

    private void ProcessHit(Collider2D other)
    {
        if (!_initialized || _hit || other == null) return;
        if (IsPassThrough(other)) return;
        var health = other.GetComponentInParent<EnemyHealth>();
        var monster = other.GetComponentInParent<Monster>();
        if (health != null) health.TakeDamage(Mathf.RoundToInt(_damage));
        else if (monster != null && !monster.isDead) monster.TakeDamage(_damage);
        else if (other.isTrigger) return; // 탐색·상호작용 영역은 통과합니다.
        Retire();
    }

    private void Retire()
    {
        if (_hit) return;
        _hit = true;
        gameObject.SetActive(false);
        PlayerFieldDevice.Dispose(gameObject);
    }

    private void OnDisable() { if (_body != null) _body.linearVelocity = Vector2.zero; }
}
