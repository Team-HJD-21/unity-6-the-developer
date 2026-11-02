using UnityEngine;

public class EnemyTargeting : MonoBehaviour
{
    private Transform _target;
    private Collider2D[] _targetColliders;

    public Transform Target => _target;
    public Collider2D[] TargetColliders => _targetColliders;
    
    /// <summary>
    /// 몬스터가 추적할 타겟을 지정한다.
    /// </summary>
    /// <param name="target">추적할 타겟의 Transform.</param>
    public void SetTarget(Transform target)
    {
        _target = target;
        _targetColliders = target != null
            ? target.GetComponentsInChildren<Collider2D>(true)
            : null;
    }
}
