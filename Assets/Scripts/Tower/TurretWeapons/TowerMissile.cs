using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TowerMissile : MonoBehaviour
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
    private Vector2 _initialDirection;
    private SpriteRenderer _sr;
    private int _maxReassign;
    private float _currentSpeed;
    private bool _isHoming = false;
    private float _distanceToTarget;
    private string _missileSoundId; // MissileFlying SFX ID 저장
    private string _missileDetectId; // MissileDetect SFX ID 저장
    
    public void SetTarget(Transform target)//각 미사일 터렛에서 호출됨
    {
        _target = target;
        _initialDirection = transform.up;
        _currentSpeed = initialSpeed;
        rb.linearVelocity = _initialDirection * _currentSpeed;

        // MissileFlying SFX 재생 및 ID 저장
        Collider2D hits = Physics2D.OverlapCircle(transform.position, 80, layerMask);
        if (hits != null)
        {
            float distance = Vector2.Distance(transform.position, hits.transform.position);
            _missileSoundId = AudioManager.Instance.PlaySfx(AudioManager.Sfx.MissileFlying, distance, 80);
        }
        StartCoroutine(InitialStraightMovement());
        StartCoroutine(ExplodeMissileIfNotHit());
    }
    private void Awake()
    {
        _sr = gameObject.GetComponent<SpriteRenderer>();
        _bulletDamage = DataManager.GetAttributeData(AttributeType.TurretMissile);
    }
    private void FixedUpdate()
    {
        DrawTargetLineToTarget();
        SearchForNewTarget();
        AccelerateToTarget();
        FlyingSoundMove();
    }
    private void SearchForNewTarget()//목표하던 목표가 사라지면 호출
    {
        if (_target == null)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 300,enemyMask);
            foreach (var monster in hits)
            {
                if(_target == null&&!monster.GetComponent<Monster>().isTargeted)
                {
                    // _missileDetectId =  AudioManager.Instance.PlaySfx(AudioManager.Sfx.MissileFinalDetect);
                    monster.GetComponent<Monster>().isTargeted = true; 
                    _target = monster.transform;
                }
            }
        }
    }
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
            AudioManager.Instance.ChangeVolume(_missileSoundId,distance,80);
        }

    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        StartCoroutine(DestroyObject());
        GameObject explosion = Instantiate(explodePrefab, explosionPosition.position, Quaternion.identity);
        explosion.GetComponent<Explode>().TriggerExplosion();
    }
    //for Coroutine Methods------------------------------------------------------------
    private IEnumerator InitialStraightMovement()//일정시간 직진 후 목표를 향해 가속
    {
        yield return new WaitForSeconds(initialStraightTime);
        _isHoming = true;
        // _missileDetectId =  AudioManager.Instance.PlaySfx(AudioManager.Sfx.MissileFinalDetect);
    }

    private IEnumerator ExplodeMissileIfNotHit()//미사일 임무 시간 내에 
    {
        yield return new WaitForSeconds(10f);
        StartCoroutine(DestroyObject());
        GameObject explosion = Instantiate(explodePrefab, explosionPosition.position, Quaternion.identity);
        explosion.GetComponent<Explode>().TriggerExplosion();
    }
    private IEnumerator DestroyObject()
    {
        _sr.enabled = false;
        minimapMissile.enabled = false;
        yield return new WaitForSeconds(0.1f);
        if(_target != null)
        {
            _target.GetComponent<Monster>().isTargeted = false;
        }
        // MissileFlying SFX 중단
        // if (!string.IsNullOrEmpty(_missileDetectId))
        // {
        //     AudioManager.Instance.StopSfx(_missileDetectId); 
        //     Debug.Log("missiledetect delete");
        // }
        if (!string.IsNullOrEmpty(_missileSoundId))
        {
            AudioManager.Instance.StopSfx(_missileSoundId);
            // Debug.Log("missilesound delete");
        }
        Collider2D[] monsters = Physics2D.OverlapCircleAll(rb.position, explosionRange);
        foreach (var monster in monsters)
        {
            if (monster.CompareTag("Enemy"))
            {
                monster.GetComponent<Monster>().TakeDamage(_bulletDamage);
            }
        }
        Destroy(gameObject);
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
