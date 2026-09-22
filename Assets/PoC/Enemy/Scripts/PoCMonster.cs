using System;
using UnityEngine;

public class PoCMonster : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Animator animator;
    [SerializeField] private float animationReferenceSpeed = 2f;
    private Transform _target;
    public Transform Target => _target;
    [SerializeField] private float attackRange = 0.4f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1f;

    private float _nextAttackTime;
    
    private Vector2 _moveDirection;

    
    public event Action AttackStarted;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
            
    }

    public void Tick()
    {
        if (!_target)
            return;
        
        Vector2 toTarget =
            (Vector2)_target.position - (Vector2)transform.position;

        _moveDirection = toTarget.normalized;
        
        UpdateFacingDirection();

        if (IsInAttackRange())
        {
            StopMoving();
            TryAttack();
            return;
        }
        
        MoveTowardsTarget();
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    private void MoveTowardsTarget()
    {
        animator.SetBool("IsMoving", true);
        
        float animationSpeed =
            moveSpeed / animationReferenceSpeed;
        animator.SetFloat("MoveAnimSpeed", animationSpeed);
        
        transform.position +=
            (Vector3)_moveDirection * (moveSpeed * Time.deltaTime);
    }
    
    private void StopMoving()
    {
        animator.SetBool("IsMoving", false);
    }
    
    private void UpdateFacingDirection()
    {
        Vector2 animationDirection =
            GetAnimationDirection(_moveDirection);

        animator.SetFloat("MoveX", animationDirection.x);
        animator.SetFloat("MoveY", animationDirection.y);
    }

    private Vector2 GetAnimationDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return new Vector2(Mathf.Sign(direction.x), 0f);
        }

        return new Vector2(0f, Mathf.Sign(direction.y));
    }

    private void TryAttack()
    {
        if (!_target)
            return;

        if (!IsInAttackRange())
            return;

        if (Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + attackCooldown;

        AttackStarted?.Invoke();
        
        // TakeDamage(attackDamage);
    }

    private bool IsInAttackRange()
    {
        float distanceSqr =
            (_target.position - transform.position).sqrMagnitude;

        return distanceSqr <= attackRange * attackRange;
    }
}
