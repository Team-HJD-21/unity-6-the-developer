using TeamHJD.Game.Turrets.Contracts;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class BossMonster2 : Monster
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float actionTimer = 0f;
    private bool isAttacking = false;
    private bool isMoving = false;
    private bool isTreading = false;
    private bool isSpawning = false;
    private int currentDirection = 0;

    public float treadDamage; // 밟기 데미지 추가

    // 방향 상수 정의
    private const int DIRECTION_DOWN = 0;
    private const int DIRECTION_UP = 1;
    private const int DIRECTION_LEFT = 2;
    private const int DIRECTION_RIGHT = 3;

    [Header("Spawner Setting")] [SerializeField]
    private GameObject monsterSpawnerInBoss;

    // 생성된 스포너 오브젝트를 추적
    private GameObject spawnerInstance;

    // 현재 타겟을 추적하기 위한 변수
    [SerializeField] private Transform currentTarget;

    protected override void Start()
    {
        base.Start();

        // 필요한 스탯 설정
        if (maxHealth == 0) maxHealth = 150f;
        if (attackDamage == 0) attackDamage = 15f;
        if (treadDamage == 0) treadDamage = 25f; // 밟기 데미지 설정
        if (moveSpeed == 0) moveSpeed = 3f;
        if (attackRange == 0) attackRange = 1.5f;
        if (detectionRange == 0) detectionRange = 5f;
        if (attackCooldown == 0) attackCooldown = 1f;

        // Animator 및 SpriteRenderer 설정
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        animator.speed = 0.4f; // 원하는 속도로 설정 (예: 0.5f로 설정하면 속도가 절반으로 줄어듭니다)

        SetDirection(DIRECTION_DOWN);

        // ControlUnitHealth가 Monster.cs에서 자동 할당되었는지 확인
        if (controlUnitHealth == null)
        {
            // Debug.LogWarning($"{monsterName}의 Control Unit이 할당되지 않았습니다.");
        }

        GeneralManager.Instance.inGameManager.ListenBossSpawn();
        
        //  Spawner 위치 설정
        monsterSpawnerInBoss.transform.position = transform.position;
        
        //  Spawn
        StartCoroutine(MonsterSpawnCoroutine());
    }

    private void Update()
    {
        if (player == null || controlUnitHealth == null || isDead) return;

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

        if (actionTimer > 0)
        {
            actionTimer -= Time.deltaTime;
        }
        
        //  Spawner 위치 설정
        monsterSpawnerInBoss.transform.position = transform.position;
    }

    private void UpdateState(float distanceToTarget, Vector2 directionToTarget)
    {
        UpdateDirection(directionToTarget);

        if (distanceToTarget <= attackRange)
        {
            SetMoving(false);
            if (actionTimer <= 0 && !isAttacking && !isTreading && !isSpawning)
            {
                StartRandomAction(directionToTarget);
            }
        }
        else
        {
            SetMoving(true);
            if (!isAttacking && !isTreading && !isSpawning)
            {
                MoveTowards(currentTarget.position);
            }
        }

        UpdateAnimationState();
    }

    private void StartRandomAction(Vector2 actionDirection)
    {
        int randomAction = Random.Range(0, 3); // 0: Attack, 1: Tread, 2: Spawn
        // 디버깅용
        // int randomAction = 0;
        
        // Debug.Log("Boss" + randomAction);

        switch (randomAction)
        {
            case 0:
                StartAttack(actionDirection);
                break;
            case 1:
                StartTread(actionDirection);
                break;
            case 2:
                StartSpawn(actionDirection);
                break;
        }
    }

    private void StartAttack(Vector2 attackDirection)
    {
        isAttacking = true;
        SetMoving(false);
        actionTimer = attackCooldown;

        UpdateAnimationState();

        // 공격 사운드를 1초 뒤에 재생하도록 설정
        AudioManager.Instance.PlaySfx(AudioManager.Sfx.WolfBite);

        // 플레이어 또는 제어 장치에게 데미지
        DealDamageToTarget((int)attackDamage);

        // 공격 종료를 애니메이션 길이에 맞게 호출
        Invoke(nameof(FinishAttack), 1f); // 공격 애니메이션 길이에 맞춰서 설정
    }

    /*private void PlayBossPunchSound()
    {
        AudioManager.Instance.PlaySfx(AudioManager.Sfx.WolfBite);
    }*/


    private void FinishAttack()
    {
        isAttacking = false;
        UpdateAnimationState();
    }

    private void StartTread(Vector2 treadDirection)
    {
        isTreading = true;
        SetMoving(false);
        actionTimer = attackCooldown; // 밟기도 쿨다운 공유
        AudioManager.Instance.PlaySfx(AudioManager.Sfx.WolfBark);

        UpdateAnimationState();
        // 플레이어 또는 제어 장치에게 데미지
        DealDamageToTarget((int)treadDamage);

        Invoke(nameof(FinishTread), 0.6f); // 밟기 애니메이션 길이에 맞춰서 설정
    }

    private void FinishTread()
    {

        // 카메라 흔들림 효과 트리거
        CameraController cameraController = Camera.main.GetComponent<CameraController>();
        if (cameraController != null)
        {
            StartCoroutine(cameraController.Shake(0.5f, 0.3f)); // 0.5초 동안, 0.3 강도로 흔들림
        }
        isTreading = false;
        UpdateAnimationState();
    }

    private void StartSpawn(Vector2 spawnDirection)
    {
        isSpawning = true;
        SetMoving(false);
        actionTimer = attackCooldown; // 밟기도 쿨다운 공유

        UpdateAnimationState();
        AudioManager.Instance.PlaySfx(AudioManager.Sfx.WolfSpawn);

        Invoke(nameof(FinishSpawn), 1f); // 스폰 애니메이션 길이에 맞춰서 설정
    }

    private void FinishSpawn()
    {
        // 스폰 로직 구현 (예: 새로운 몬스터 생성)
        SpawnMonster();

        isSpawning = false;
        UpdateAnimationState();
    }

    private void SpawnMonster()
    {
        if (monsterSpawnerInBoss == null)
        {
            // Debug.LogWarning("Monster Spawner is Null");
        }

        monsterSpawnerInBoss.GetComponent<MonsterSpawner>().BossSkillSpawn();
    }
    
    // private void SpawnMonster()
    // {
    //     // 이미 스폰 중인 상태라면 리턴
    //     if (!isSpawning || monsterSpawnData.monsterPrefab == null)
    //     {
    //         Debug.LogWarning("Spawning is not allowed or Monster prefab is null!");
    //         return;
    //     }
    //
    //     // 보스 주변에 스폰 위치 계산
    //     float randomOffsetX = Random.Range(-2f, 2f); // X축 랜덤 오프셋
    //     float randomOffsetY = Random.Range(-2f, 2f); // Y축 랜덤 오프셋
    //     Vector3 spawnPosition = new Vector3(
    //         transform.position.x + randomOffsetX,
    //         transform.position.y + randomOffsetY,
    //         transform.position.z
    //     );
    //
    //     // 몬스터 생성
    //     GameObject monster = Instantiate(monsterSpawnData.monsterPrefab, spawnPosition, Quaternion.identity);
    //     Debug.Log(monster);
    //     
    //     // 생성된 몬스터가 플레이어를 타겟팅하도록 설정
    //     GameObject player = GameObject.FindGameObjectWithTag("Player");
    //     if (player != null)
    //     {
    //         Monster monsterComponent = monster.GetComponent<Monster>();
    //         if (monsterComponent != null)
    //         {
    //             monsterComponent.player = player.transform;
    //         }
    //     }
    //
    //     Destroy(monster, 0.1f); // 1초 후 스포너 오브젝트 제거
    // }


    private void DealDamageToTarget(int damage)
    {
        if (currentTarget.CompareTag("Player"))
        {
            PlayerInfo playerInfo = player.GetComponent<PlayerInfo>();
            if (playerInfo != null)
            {
                playerInfo.TakeDamage(damage);
            }
        }
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
                    controlUnit.ApplyDamage(damage);
                    break;
                }
            }
        }
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
        animator.SetBool("isMoving", moving && !isAttacking && !isTreading && !isSpawning);
    }

    private void UpdateAnimationState()
    {
        animator.SetBool("isMoving", isMoving && !isAttacking && !isTreading && !isSpawning);
        animator.SetBool("isAttacking", isAttacking);
        animator.SetBool("isTread", isTreading);
        animator.SetBool("isSpawn", isSpawning);
    }

    private void Move(Vector2 targetPosition)
    {
        base.Move(targetPosition);
    }

    private void MoveTowards(Vector2 targetPosition)
    {
        Move(targetPosition);
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
    
    private IEnumerator MonsterSpawnCoroutine()
    {
        StartSpawn(transform.position);
        yield return new WaitForSeconds(30f);
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