// 기존 탄환 피해·방향을 유지하고 수명/이동을 소유 Match의 필드 시간으로 진행합니다.
using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "TowerBullet")]
public partial class FieldTurretBullet : MonoBehaviour
{
    [Header("Attributes")]
    [SerializeField] private float bulletSpeed; // 일정한 속도
    private float _bulletDamage; // 총알 피해량
    private Transform _target;

    private Vector3 _direction;
    private bool _isInitialized;
    private bool _hasHit;
    private double _expiresAt;

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
        if (!_isInitialized || !FieldSimulationTime.CanAdvance(this)) return;
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

        // 일정한 속도로 총알 이동
        transform.position += _direction * bulletSpeed * FieldSimulationTime.DeltaTime(this);

        // 총알의 회전 방향 설정
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    // 충돌 처리
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isInitialized || _hasHit || !FieldSimulationTime.CanAdvance(this))
            return;
        _hasHit = true;
        EnemyHealth enemyHealth = collision.collider.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(Mathf.RoundToInt(_bulletDamage));
        }

        Destroy(gameObject); // 충돌 시 총알 파괴
    }
}
