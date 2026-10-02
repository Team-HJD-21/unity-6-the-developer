using UnityEngine;

/// <summary>
/// 적 한 종류의 식별 정보와 생성에 필요한 제작 데이터를 정의한다.
/// </summary>
[CreateAssetMenu(menuName = "The Developer/Enemy/Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [SerializeField] private string _enemyId;
    [SerializeField] private GameObject _prefab;
    [SerializeField, Min(1)] private int _spawnCost = 1;

    /// <summary>
    /// 적 정의를 식별하는 고유 값을 반환한다.
    /// </summary>
    public string EnemyId => _enemyId;

    /// <summary>
    /// 적 생성에 사용할 프리팹을 반환한다.
    /// </summary>
    public GameObject Prefab => _prefab;

    /// <summary>
    /// Encounter의 Spawn Budget에서 사용할 생성 비용을 반환한다.
    /// </summary>
    public int SpawnCost => _spawnCost;
}
