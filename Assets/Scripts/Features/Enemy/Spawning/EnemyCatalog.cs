using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 정의 목록을 보관하고 고유 식별 값으로 조회할 수 있게 제공한다.
/// 실제 적 생성과 Encounter 판단은 담당하지 않는다.
/// </summary>
[CreateAssetMenu(
    fileName = "EnemyCatalog",
    menuName = "The Developer/Enemy/Catalog")]
public sealed class EnemyCatalog : ScriptableObject
{
    [SerializeField]
    private List<EnemyDefinition> enemyDefinitions = new();

    /// <summary>
    /// 고유 식별 값과 일치하는 적 정의를 조회한다.
    /// </summary>
    /// <param name="enemyId">조회할 적 정의의 고유 식별 값.</param>
    /// <param name="definition">조회에 성공한 적 정의.</param>
    /// <returns>일치하는 적 정의가 있으면 <see langword="true"/>, 없으면 <see langword="false"/>.</returns>
    public bool TryGetDefinition(string enemyId, out EnemyDefinition definition)
    {
        foreach(EnemyDefinition  enemyDefinition in enemyDefinitions)
        {
            if (enemyDefinition != null && enemyDefinition.EnemyId == enemyId)
            {
                definition = enemyDefinition;
                return true;
            }
                
        }

        definition = null;
        return false;
    }
}
