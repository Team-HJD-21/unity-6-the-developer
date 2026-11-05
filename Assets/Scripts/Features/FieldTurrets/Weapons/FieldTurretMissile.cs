using System;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "TowerMissile")]
public class FieldTurretMissile : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject explodePrefab;
    [SerializeField] private Transform explosionPosition;
    [SerializeField] private SpriteRenderer minimapMissile;

    [Header("Movement Settings")] 
    [SerializeField] private float initialSpeed = 10f;     // 초기 직진 속도
    [SerializeField] private float maxSpeed = 15f;         // 최대 속도
    [SerializeField] private float turnSpeed = 180f;       // 회전 속도 (도/초)
    [SerializeField] private float acceleration = 5f;      // 가속도
    [SerializeField] private float initialStraightTime = 1f; // 초기 직진 시간
    
    [Header("Combat Settings")]
    [SerializeField] private float explosionRange = 20f;

    public LayerMask layerMask;
    private float _bulletDamage;
    private Transform _target;
    private MissileTargetReservation _targetReservation;
    private Vector2 _initialDirection;
    private SpriteRenderer _sr;
    private int _maxReassign;
    private float _currentSpeed;
    private bool _isHoming = false;
    private float _distanceToTarget;
    private string _missileSoundId; // MissileFlying SFX ID 저장
    private string _missileDetectId; // MissileDetect SFX ID 저장
    private bool _isInitialized;
    private bool _isExploding;
    private bool _hasAppliedExplosion;
    private float _homingStartsAt;
    private float _expiresAt;
    private float _explosionDamageAt;
    
    public void Initialize(Transform target, float damage)
    {
        ValidateInitialization(damage);
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (!MissileTargetReservation.TryAcquire(target, out var reservation))
            throw new InvalidOperationException("The missile target is unavailable or already reserved.");
        try
        {
            Initialize(reservation, damage);
        }
        finally
        {
            reservation.Dispose();
        }
    }

    private void ValidateInitialization(float damage)
    {
        if (_isInitialized)
            throw new InvalidOperationException("FieldTurretMissile has already been initialized.");
        if (!isActiveAndEnabled)
            throw new InvalidOperationException("FieldTurretMissile must be active before initialization.");
        if (damage < 0f)
            throw new ArgumentOutOfRangeException(nameof(damage));
        if (rb == null)
            throw new InvalidOperationException("FieldTurretMissile requires a Rigidbody2D reference.");
    }

    public void Initialize(MissileTargetReservation reservation, float damage)
    {
        ValidateInitialization(damage);
        if (reservation == null || !reservation.IsValid)
            throw new ArgumentException("A valid missile target reservation is required.", nameof(reservation));
        _targetReservation = reservation.Transfer();
        _target = _targetReservation.Target;
        _bulletDamage = damage;
        _initialDirection = transform.up;
        _currentSpeed = initialSpeed;
        rb.linearVelocity = _initialDirection * _currentSpeed;
        _homingStartsAt = Time.time + initialStraightTime;
        _expiresAt = Time.time + 10f;
        _isInitialized = true;

        // MissileFlying SFX 재생 및 ID 저장
        Collider2D hits = Physics2D.OverlapCircle(transform.position, 80, layerMask);
        if (hits != null)
        {
            float distance = Vector2.Distance(transform.position, hits.transform.position);
            // _missileSoundId = AudioManager.Instance.PlaySfx(AudioManager.Sfx.MissileFlying, distance, 80);
        }
    }

    private void Awake()
    {
        _sr = gameObject.GetComponent<SpriteRenderer>();
    }
    private void FixedUpdate()
    {
        if (!_isInitialized) return;
        if (_isExploding)
        {
            if (!_hasAppliedExplosion && Time.time >= _explosionDamageAt)
                ApplyExplosionDamage();
            return;
        }
        if (Time.time >= _expiresAt)
        {
            BeginExplosion();
            return;
        }

        _isHoming = Time.time >= _homingStartsAt;
        DrawTargetLineToTarget();
        SearchForNewTarget();
        AccelerateToTarget();
        FlyingSoundMove();
    }
    private void SearchForNewTarget()//목표하던 목표가 사라지면 호출
    {
        if (_target != null && _targetReservation != null && _targetReservation.IsValid &&
            (!_target.TryGetComponent(out EnemyController currentEnemy) || currentEnemy.isActiveAndEnabled))
            return;

        ReleaseTargetReservation();
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 300, enemyMask);
        foreach (Collider2D candidate in hits)
        {
            EnemyController enemy = candidate.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            if (!MissileTargetReservation.TryAcquire(enemy.transform, out var reservation))
                continue;

            _targetReservation = reservation;
            _target = reservation.Target;
            break;
        }
    }

    private void ReleaseTargetReservation()
    {
        _targetReservation?.Dispose();
        _targetReservation = null;
        _target = null;
    }

    private void OnDisable() => ReleaseTargetReservation();
    private void OnDestroy() => ReleaseTargetReservation();
    private void AccelerateToTarget()
    {
        if (!_isHoming || !_target) return;
        // 현재 진행 방향과 목표 방향 사이의 각도 계산
        Vector2 directionToTarget = ((Vector2)_target.position - rb.position).normalized;
        Vector2 currentDirection = rb.linearVelocity.normalized;
        
        // 회전 방향 결정 (양수: 시계방향, 음수: 반시계방향)
        float angleToTarget = Vector2.SignedAngle(currentDirection, directionToTarget);
        
        // 한 프레임당 최대 회전 각도 제한
        float rotationThisFrame = Mathf.Clamp(angleToTarget, -turnSpeed * Time.fixedDeltaTime, turnSpeed * Time.fixedDeltaTime);
        
        // 현재 속도 벡터를 회전
        Vector2 newDirection = Quaternion.Euler(0, 0, rotationThisFrame) * currentDirection;
        
        // 가속도 적용
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed, acceleration * Time.fixedDeltaTime);
        
        // 새로운 속도 적용
        rb.linearVelocity = newDirection * _currentSpeed;
        
        // 미사일 스프라이트 회전
        float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void FlyingSoundMove()
    {
        Collider2D hits = Physics2D.OverlapCircle(transform.position, 80, layerMask);
        if (hits != null&&_missileSoundId!=null)
        {
            float distance = Vector2.Distance(transform.position, hits.transform.position);
            // Debug.Log("Distance with player" + distance);
            // AudioManager.Instance.ChangeVolume(_missileSoundId,distance,80);
        }

    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (_isInitialized && isActiveAndEnabled)
            BeginExplosion();
    }

    private void BeginExplosion()
    {
        if (_isExploding)
            return;
        _isExploding = true;
        // Release synchronously, before effects or delayed damage can be interrupted.
        ReleaseTargetReservation();
        _explosionDamageAt = Time.time + 0.1f;
        rb.linearVelocity = Vector2.zero;
        if (_sr != null) _sr.enabled = false;
        if (minimapMissile != null) minimapMissile.enabled = false;
        if (explodePrefab != null)
        {
            Vector3 position = explosionPosition != null ? explosionPosition.position : transform.position;
            GameObject explosion = Instantiate(explodePrefab, position, Quaternion.identity);
            explosion.GetComponent<Explode>().TriggerExplosion();
        }
    }

    private void ApplyExplosionDamage()
    {
        _hasAppliedExplosion = true;
        // MissileFlying SFX 중단
        // if (!string.IsNullOrEmpty(_missileDetectId))
        // {
        //     AudioManager.Instance.StopSfx(_missileDetectId); 
        //     Debug.Log("missiledetect delete");
        // }
        if (!string.IsNullOrEmpty(_missileSoundId))
        {
            // AudioManager.Instance.StopSfx(_missileSoundId);
            // Debug.Log("missilesound delete");
        }
        try
        {
            Collider2D[] monsters = Physics2D.OverlapCircleAll(rb.position, explosionRange);
            HashSet<EnemyHealth> damagedEnemies = new();
            foreach (Collider2D candidate in monsters)
            {
                EnemyHealth enemyHealth = candidate.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null && damagedEnemies.Add(enemyHealth))
                    enemyHealth.TakeDamage(Mathf.RoundToInt(_bulletDamage));
            }
        }
        finally
        {
            Destroy(gameObject);
        }
    }
    //for debug-------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        Handles.color = Color.cyan;
        Handles.DrawWireDisc(transform.position, transform.forward, explosionRange);
#endif

    }
    private void DrawTargetLineToTarget()
    {
        if (_target != null)
        {
            
        }
            // Debug.DrawLine(transform.position, _target.position, Color.blue, 0.1f);
    }
}
