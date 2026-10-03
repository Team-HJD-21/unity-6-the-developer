using UnityEngine;

/// <summary>
/// 서버에서 호출되는 적의 행동 흐름을 관리한다.
/// 분대가 지정한 공동 목표에 따라 이동·공격 행동을 실행한다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyAIBrain : MonoBehaviour
{
    private EnemyController _monster;
    private EnemySquad _squad;

    /// <summary>
    /// 이동·공격을 담당할 컴포넌트를 찾는다.
    /// </summary>
    private void Awake()
    {
        _monster = GetComponent<EnemyController>();
    }

    /// <summary>
    /// 서버의 EnemyNetworkController가 매 프레임 호출한다.
    /// 분대에 등록되지 않은 적은 개별 목표를 선택하지 않고 대기한다.
    /// </summary>
    public void UpdateAI()
    {
        if (_squad == null)
            return;

        _monster.ExecuteBehavior();
    }

    /// <summary>
    /// 분대에 등록하고 해당 분대가 공동 목표를 지정할 때까지 기다린다.
    /// </summary>
    /// <param name="squad">이 적을 관리할 분대.</param>
    public void AssignSquad(EnemySquad squad)
    {
        _squad = squad;
        _monster.SetTarget(null);
    }
}
