// 기존 탄환 피해·방향을 유지하고 수명/이동을 소유 Match의 필드 시간으로 진행합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "TowerBullet")]
public partial class FieldTurretBullet : MonoBehaviour
{
    [Header("Attributes")]
    [SerializeField] private float bulletSpeed; // 일정한 속도
    [SerializeField] private PlayerSpriteEffect _impactPrefab;
    private float _bulletDamage; // 총알 피해량
    private Transform _target;

    private Vector3 _direction;
    private bool _isInitialized;
    private bool _hasHit;
    private double _expiresAt;
    private Collider2D _hitbox;
    private ContactFilter2D _flightFilter;
    private readonly List<RaycastHit2D> _flightHits = new(4);

    private void Awake()
    {
        _hitbox = GetComponent<Collider2D>();
        _flightFilter = new ContactFilter2D { useTriggers = true };
        _flightFilter.SetLayerMask(FieldTurretTargetingUtility.TerrainObstacleMask | FieldTurretTargetingUtility.EnemyHitBoxMask);
    }

    public void Initialize(Transform target, float damage)
        => Initialize(target, damage, target != null ? target.position : Vector3.zero);

    public void Initialize(Transform target, float damage, Vector3 aimPosition)
    {
        if (_isInitialized)
            throw new InvalidOperationException("FieldTurretBullet has already been initialized.");
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (damage < 0f)
            throw new ArgumentOutOfRangeException(nameof(damage));

        _target = target;
        _bulletDamage = damage;
        _direction = (aimPosition - transform.position).normalized; // 기준점을 옮기지 않고 이번 탄환의 방향만 계산
        _isInitialized = true;
        _expiresAt = FieldSimulationTime.Seconds(this) + 3;
    }

    private void Update()
    {
        if (!_isInitialized || _hasHit || !FieldSimulationTime.CanAdvance(this)) return;
        if (FieldSimulationTime.Seconds(this) >= _expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        if (_target == null)
        {
            Destroy(gameObject); // 타겟이 없으면 총알 파괴
            return;
        }

        Vector3 step = _direction * bulletSpeed * FieldSimulationTime.DeltaTime(this);
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        if (TryHitFlightPath(step, angle)) return;

        transform.position += step;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool TryHitFlightPath(Vector2 step, float angle)
    {
        float distance = step.magnitude;
        if (_hitbox == null || _hitbox.attachedRigidbody == null || distance <= 0f) return false;

        _hitbox.Cast(transform.position, angle, step / distance, _flightFilter, _flightHits, distance); // 빠른 탄환이 장애물을 건너뛰지 않도록 이동 전 검사
        foreach (RaycastHit2D hit in _flightHits)
        {
            Collider2D collider = hit.collider;
            if (collider == null || collider.gameObject.scene != gameObject.scene) continue;

            bool blocksBullet = !collider.isTrigger && collider.CompareTag(FieldTurretTargetingUtility.TerrainObstacleTag);
            EnemyHealth enemy = FieldTurretTargetingUtility.IsEnemyHitBox(collider) ? collider.GetComponentInParent<EnemyHealth>() : null;
            if (!blocksBullet && enemy == null) continue;

            Hit(enemy, hit.point);
            return true;
        }

        return false;
    }

    // 충돌 처리
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isInitialized || _hasHit || !FieldSimulationTime.CanAdvance(this))
            return;
        Collider2D collider = collision.collider;
        EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null && !FieldTurretTargetingUtility.IsEnemyHitBox(collider)) return;

        Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        Hit(enemy, impactPoint);
    }

    private void Hit(EnemyHealth enemy, Vector2 impactPoint)
    {
        _hasHit = true;
        if (enemy != null)
            enemy.TakeDamage(Mathf.RoundToInt(_bulletDamage));

        if (_impactPrefab != null)
        {
            var position = new Vector3(impactPoint.x, impactPoint.y, transform.position.z);
            var effect = Instantiate(_impactPrefab, position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(effect.gameObject, gameObject.scene);
            effect.SetTint(new Color(1f, .5f, .1f), GetComponent<SpriteRenderer>());
        }

        Destroy(gameObject); // 충돌 시 총알 파괴
    }
}
