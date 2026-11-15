using UnityEngine;

/// <summary>감지 영역에 들어온 플레이어를 소속 몬스터에게 전달한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class EnemyPlayerSensor : MonoBehaviour
{
    // Inspector 설정과 제작 참조
    [SerializeField] private EnemyController enemyController;

    // 실행 중 참조와 상태
    private CircleCollider2D _sensorCollider;
    private Transform _sensorTransform;
    private EnemyNetworkController _networkController;
    private EnemyTargeting _enemyTargeting;

    // Unity 생명주기

    /// <summary>감지 콜라이더를 Trigger로 설정하고 부모 몬스터를 연결한다.</summary>
    private void Reset()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
        enemyController = GetComponentInParent<EnemyController>();
    }

    /// <summary>반복 감지에서 재사용할 콜라이더·타깃·실행 권한 참조를 연결한다.</summary>
    private void Awake()
    {
        _sensorCollider = GetComponent<CircleCollider2D>();
        _sensorTransform = transform;
        _networkController = GetComponentInParent<EnemyNetworkController>();
        _enemyTargeting = GetComponentInParent<EnemyTargeting>();

        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();
    }

    // 외부 호출과 상태 변경

    /// <summary>현재 선택한 타깃의 위치가 감지 영역 안인지 확인한다.</summary>
    public bool IsTargetInside(Transform target) => IsTargetInside(target, 1f);

    /// <summary>플레이어가 감지 반경의 2배인 어그로 유지 범위 안에 있는지 확인한다.</summary>
    public bool IsTargetInsideAggroRange(Transform target) => IsTargetInside(target, 2f);

    // 내부 처리

    /// <summary>감지 중심과 월드 반경에 배율을 적용해 대상 위치를 비교한다.</summary>
    private bool IsTargetInside(Transform target, float radiusMultiplier)
    {
        if (!isActiveAndEnabled || _sensorCollider == null || target == null)
            return false;

        GetDetectionArea(out Vector2 center, out float radiusSquared);
        Vector2 difference = (Vector2)target.position - center;

        return difference.sqrMagnitude <= radiusSquared * radiusMultiplier * radiusMultiplier;
    }

    /// <summary>감지 영역에 새로 진입한 플레이어의 우선 타깃 전환을 요청한다.</summary>
    private void OnTriggerEnter2D(Collider2D other) => NotifyPlayer(other);

    /// <summary>영역 안에서 뒤늦게 분대에 합류하거나 타깃 전환에 실패한 경우 다시 전달한다.</summary>
    private void OnTriggerStay2D(Collider2D other) => NotifyPlayer(other);

    /// <summary>이미 플레이어를 추적 중이면 생략하고 유효한 감지 후보만 소속 분대에 전달한다.</summary>
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
