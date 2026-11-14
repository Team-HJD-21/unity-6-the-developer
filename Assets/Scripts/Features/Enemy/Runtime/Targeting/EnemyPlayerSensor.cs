using UnityEngine;

/// <summary>감지 영역에 들어온 플레이어를 소속 몬스터에게 전달한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class EnemyPlayerSensor : MonoBehaviour
{
    [SerializeField] private EnemyController enemyController;
    private CircleCollider2D _sensorCollider;
    private Transform _sensorTransform;
    private EnemyNetworkController _networkController;
    private EnemyTargeting _enemyTargeting;

    private void Reset()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
        enemyController = GetComponentInParent<EnemyController>();
    }

    private void Awake()
    {
        _sensorCollider = GetComponent<CircleCollider2D>();
        _sensorTransform = transform;
        _networkController = GetComponentInParent<EnemyNetworkController>();
        _enemyTargeting = GetComponentInParent<EnemyTargeting>();

        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();
    }

    /// <summary>현재 선택한 타깃의 위치가 감지 영역 안인지 확인한다.</summary>
    public bool IsTargetInside(Transform target)
    {
        if (!isActiveAndEnabled || _sensorCollider == null || target == null)
            return false;

        GetDetectionArea(out Vector2 center, out float radiusSquared);
        Vector2 difference = (Vector2)target.position - center;

        return difference.sqrMagnitude <= radiusSquared;
    }

    private void OnTriggerEnter2D(Collider2D other) => NotifyPlayer(other);

    private void OnTriggerStay2D(Collider2D other) => NotifyPlayer(other);

    private void NotifyPlayer(Collider2D other)
    {
        TargetableComponent currentTarget = _enemyTargeting != null
            ? _enemyTargeting.TargetComponent
            : null;
        if (currentTarget != null && currentTarget.TargetType == TargetType.Player)
            return;

        if (enemyController == null || _networkController == null ||
            !_networkController.CanExecuteBehavior)
            return;

        ITargetable player = other.GetComponentInParent<ITargetable>();
        if (player == null || !player.CanBeTargeted || player.TargetTransform == null)
            return;

        enemyController.NotifyPlayerDetected(player);
    }

    /// <summary>기존 Offset과 스케일을 반영한 월드 감지 범위를 구한다.</summary>
    private void GetDetectionArea(out Vector2 center, out float radiusSquared)
    {
        center = _sensorTransform.TransformPoint(_sensorCollider.offset);
        Vector3 scale = _sensorTransform.lossyScale;
        float radius = _sensorCollider.radius *
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        radiusSquared = radius * radius;
    }
}
