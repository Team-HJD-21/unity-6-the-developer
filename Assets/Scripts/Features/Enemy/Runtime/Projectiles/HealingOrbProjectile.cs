using UnityEngine;

/// <summary>선택한 아군을 추적하고 도착하면 체력을 회복한다.</summary>
public class HealingOrbProjectile : EnemyProjectile
{
    [Header("이동")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.1f;

    
    private EnemyHealth _target;
    private int _healAmount;

    /// <summary>
    /// 추적할 아군과 회복량을 저장하고 구슬의 수명을 시작한다.
    /// </summary>
    public void Initialize(EnemyHealth target, int healAmount)
    {
        _target = target;
        _healAmount = healAmount;
        
        BeginLifetime();
    }

    /// <summary>
    /// 회복 대상을 XY 평면에서 추적하고 도착 시 한 번 회복한다. 대상이 무효화되면 제거한다.
    /// </summary>
    private void Update()
    {
        if (!CanSimulate)
            return;
        
        if (_target == null ||
            !_target.CanBeHealed ||
            _healAmount <= 0)
        {
            Remove();
            return;
        }
        
        Vector2 targetPosition = _target.transform.position;

        Vector2 nextPosition = Vector2.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this));

        // 깊이 정렬에 사용하는 Z를 유지하고 XY 평면에서 추적한다.
        Vector3 position = transform.position;
        position.x = nextPosition.x;
        position.y = nextPosition.y;
        transform.position = position;

        if ((targetPosition - nextPosition).sqrMagnitude > arrivalDistance * arrivalDistance)
            return;

        _target.Heal(_healAmount);
        Remove();
    }
}
