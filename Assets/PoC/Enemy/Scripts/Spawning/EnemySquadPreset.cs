using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 구성 세트에 포함할 적 종류와 상대적인 생성 수량을 정의한다.
/// 예를 들어 가중치 1과 2는 한 세트마다 두 적을 각각 1마리와 2마리 생성한다는 뜻이다.
/// </summary>
[Serializable]
public sealed class EnemySquadCompositionEntry
{
    [SerializeField] private string _enemyId;
    [SerializeField, Min(1)] private int _weight = 1;

    /// <summary>
    /// <see cref="EnemyCatalog"/>에서 생성 프리팹을 찾을 적 정의의 식별 값을 반환한다.
    /// </summary>
    public string EnemyId => _enemyId;

    /// <summary>
    /// 한 구성 세트에 포함할 해당 적의 수를 반환한다.
    /// 전체 생성 수가 늘어나면 Planner가 세트 수만큼 이 값을 곱한다.
    /// </summary>
    public int Weight => _weight;
}

/// <summary>
/// 분대 명령에 대응하는 몬스터 구성 세트를 Inspector에서 설정하는 프리셋이다.
/// 적 프리팹과 스폰 위치는 직접 보유하지 않으며, 요청의 최대 수에 맞춘 실제 생성 수는 Planner가 결정한다.
/// </summary>
[CreateAssetMenu(
    fileName = "EnemySquadPreset",
    menuName = "The Developer/Enemy/Squad Preset")]
public sealed class EnemySquadPreset : ScriptableObject
{
    [SerializeField] private string _squadOrder;
    [SerializeField] private List<EnemySquadCompositionEntry> _composition = new();

    /// <summary>
    /// <see cref="SpawnRequest.SquadOrder"/>와 비교해 이 프리셋을 선택할 명령 값을 반환한다.
    /// </summary>
    public string SquadOrder => _squadOrder;

    /// <summary>
    /// 한 구성 세트에 포함할 적 종류와 종류별 가중치 목록을 반환한다.
    /// 목록의 순서는 생성 명령에도 유지된다.
    /// </summary>
    public IReadOnlyList<EnemySquadCompositionEntry> Composition => _composition;

    /// <summary>
    /// 한 구성 세트를 완성하는 데 필요한 최소 적 수를 반환한다.
    /// 구성 항목이 비어 있거나 가중치가 0 이하이면 계획을 중단할 수 있도록 0을 반환한다.
    /// </summary>
    public int TotalWeight
    {
        get
        {
            int total = 0;
            foreach (var entry in _composition)
            {
                if (entry == null || entry.Weight <= 0)
                    return 0;

                total += entry.Weight;
            }

            return total;
        }
    }
}
