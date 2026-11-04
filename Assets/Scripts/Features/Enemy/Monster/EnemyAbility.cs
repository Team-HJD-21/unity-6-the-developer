using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 몬스터 능력의 발동 조건과 실행을 정의하고 공통 쿨타임을 관리한다.
/// </summary>
public abstract class EnemyAbility : MonoBehaviour
{
    [Header("능력 설정")]
    [FormerlySerializedAs("attackCooldown")]
    [SerializeField, Min(0f)] private float cooldown = 1f;

    private float _nextUseTime;

    /// <summary>
    /// 능력별 발동 조건을 확인한다. 쿨타임은 이 판정에 포함하지 않는다.
    /// </summary>
    public abstract bool CheckCondition();

    /// <summary>
    /// 발동에 성공한 경우에만 쿨타임을 시작한다.
    /// </summary>
    public bool TryUse()
    {
        if (!isActiveAndEnabled || Time.time < _nextUseTime ||
            !CheckCondition() || !Activate())
            return false;

        _nextUseTime = Time.time + cooldown;
        return true;
    }

    /// <summary>
    /// 피해 적용, 투사체 생성 등 능력의 발동에 성공하면 true를 반환한다.
    /// </summary>
    protected abstract bool Activate();
}
