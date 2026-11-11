using UnityEngine;

/// <summary>감지 영역에 들어온 플레이어를 소속 몬스터에게 전달한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class EnemyPlayerSensor : MonoBehaviour
{
    [SerializeField] private EnemyController enemyController;
    private CircleCollider2D _sensorCollider;
    private EnemyNetworkController _networkController;

    private void Reset()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
        enemyController = GetComponentInParent<EnemyController>();
    }

    private void Awake()
    {
        _sensorCollider = GetComponent<CircleCollider2D>();
        _networkController = GetComponentInParent<EnemyNetworkController>();

        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();
    }

    /// <summary>현재 선택한 타깃이 감지 영역과 접촉 중인지 확인한다.</summary>
    public bool IsTargetInside(Collider2D[] targetColliders)
    {
        if (!isActiveAndEnabled ||
            _sensorCollider == null ||
            !_sensorCollider.enabled ||
            targetColliders == null)
        {
            return false;
        }

        foreach (Collider2D targetCollider in targetColliders)
        {
            if (targetCollider == null ||
                !targetCollider.enabled ||
                !targetCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (_sensorCollider.IsTouching(targetCollider))
                return true;
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        NotifyPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 영역 안에서 분대에 합류하거나 기존 플레이어가 무효화된 경우에도 전달한다.
        NotifyPlayer(other);
    }

    private void NotifyPlayer(Collider2D other)
    {
        if (enemyController == null ||
            _networkController == null ||
            !_networkController.CanExecuteBehavior)
            return;

        ITargetable player = other.GetComponentInParent<ITargetable>();
        if (player == null ||
            !player.CanBeTargeted ||
            player.TargetTransform == null)
        {
            return;
        }

        enemyController.NotifyPlayerDetected(player);
    }
}
