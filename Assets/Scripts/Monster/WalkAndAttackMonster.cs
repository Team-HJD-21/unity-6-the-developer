using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;

public class WalkAndAttackMonster : Monster
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private bool isMoving = false;
    private float debugTimer = 0f;
    private float debugInterval = 0.5f;
    private int currentDirection = 0;

    // 방향 상수 정의
    private const int DIRECTION_DOWN = 0;
    private const int DIRECTION_UP = 1;
    private const int DIRECTION_LEFT = 2;
    private const int DIRECTION_RIGHT = 3;

    // 현재 타겟을 추적하기 위한 변수
    [SerializeField] private Transform currentTarget;

    protected override void Start()
    {
        base.Start();

        // 필요한 스탯 설정
        if (maxHealth == 0) maxHealth = 150f;
        if (attackDamage == 0) attackDamage = 15f;
        if (moveSpeed == 0) moveSpeed = 3f;
        if (attackRange == 0) attackRange = 1.5f;
        if (detectionRange == 0) detectionRange = 5f; // 기본값 설정
        if (attackCooldown == 0) attackCooldown = 1f; // Default value for attack cooldown

        // Animator 및 SpriteRenderer 설정
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        SetDirection(DIRECTION_DOWN);

        // ControlUnitHealth가 Monster.cs에서 자동 할당되었는지 확인
        if (controlUnitHealth == null)
        {
            // Debug.LogWarning($"{monsterName}의 Control Unit이 할당되지 않았습니다.");
        }
    }

    private void Update()
    {
        if (player == null || controlUnitHealth == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // 플레이어가 탐지 범위 내에 있는 경우 플레이어를 타겟으로 설정
        if (distanceToPlayer <= detectionRange)
        {
            currentTarget = player;
        }
        else
        {
            // ControlUnit의 접근 포인트 중 가장 가까운 포인트를 찾기
            currentTarget = FindClosestAccessPoint();
        }

        Vector2 directionToTarget = (currentTarget.position - transform.position).normalized;
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);

        UpdateState(distanceToTarget, directionToTarget);

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
    }

    private Transform FindClosestAccessPoint()
    {
        Transform closestPoint = null;
        float closestDistance = Mathf.Infinity;

        foreach (Transform accessPoint in controlUnitHealth.accessPoints)
        {
            float distance = Vector2.Distance(transform.position, accessPoint.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = accessPoint;
            }
        }

        return closestPoint;
    }

    private void UpdateState(float distanceToTarget, Vector2 directionToTarget)
    {
        UpdateDirection(directionToTarget);

        if (distanceToTarget <= attackRange)
        {
            SetMoving(false);
            if (attackTimer <= 0 && !isAttacking)
            {
                StartAttack(directionToTarget);
            }
        }
        else
        {
            SetMoving(true);
            if (!isAttacking)
            {
                MoveTowards(currentTarget.position);
            }
        }

        UpdateAnimationState();
    }

    private void UpdateDirection(Vector2 moveDirection)
    {
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;

        int newDirection;
        if (angle > -45 && angle <= 45) // 오른쪽
        {
            newDirection = DIRECTION_RIGHT;
        }
        else if (angle > 45 && angle <= 135) // 위
        {
            newDirection = DIRECTION_UP;
        }
        else if (angle > -135 && angle <= -45) // 아래
        {
            newDirection = DIRECTION_DOWN;
        }
        else // 왼쪽
        {
            newDirection = DIRECTION_LEFT;
        }

        if (currentDirection != newDirection)
        {
            SetDirection(newDirection);
        }
    }

    private void SetDirection(int direction)
    {
        currentDirection = direction;
        animator.SetInteger("direction", direction);
    }

    private void SetMoving(bool moving)
    {
        isMoving = moving;
        animator.SetBool("isMoving", moving && !isAttacking);
    }

    private void UpdateAnimationState()
    {
        animator.SetBool("isMoving", isMoving && !isAttacking);
        animator.SetBool("isAttacking", isAttacking);
    }

    private void Move(Vector2 targetPosition)
    {
        base.Move(targetPosition);
    }

    private void MoveTowards(Vector2 targetPosition)
    {
        Move(targetPosition);
    }

    private void StartAttack(Vector2 attackDirection)
    {
        isAttacking = true;
        SetMoving(false);
        attackTimer = attackCooldown;

        UpdateAnimationState();

        // 플레이어 또는 제어 장치에게 데미지
        if (currentTarget.CompareTag("Player"))
        {
            PlayerInfo playerInfo = player.GetComponent<PlayerInfo>();
            if (playerInfo != null)
            {
                playerInfo.TakeDamage((int)attackDamage);
            }
        }
        //  아마 이 코드는 사용이 어려울 것 같습니다. - 현석
        else if (currentTarget.CompareTag("CU"))
        {
            IDamageableTarget controlUnit = controlUnitHealth;
            if (controlUnit != null)
            {
                // Debug.LogError("Attack CU");
                controlUnit.ApplyDamage((int)attackDamage);
            }
            else
            {
                // Debug.LogError("Can not attack CU");
            }
        }
        //  요 아래껄로 대체해주세요.
        else
        {
            IDamageableTarget controlUnit = controlUnitHealth;
            if (controlUnit == null)
            {
                // Debug.LogError("Control Unit 을 불러올 수 없습니다. 공격 불가");
                return;
            }
            foreach (Transform accessPoint in controlUnitHealth.accessPoints)
            {
                if (accessPoint == currentTarget)
                {
                    controlUnit.ApplyDamage((int)attackDamage);
                    break;
                }
            }
        }

        Invoke(nameof(FinishAttack), 1f);
    }

    private void FinishAttack()
    {
        isAttacking = false;
        UpdateAnimationState();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, player.position);
        }

        // 현재 타겟이 설정되어 있는 경우, 타겟으로 가는 선 생성
        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}