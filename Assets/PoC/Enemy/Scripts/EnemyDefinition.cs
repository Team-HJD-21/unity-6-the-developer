using UnityEngine;

[CreateAssetMenu(menuName = "The Developer/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [SerializeField] private string _enemyId;
    [SerializeField] private GameObject _prefab;
    [SerializeField, Min(1)] private int _spawnCost = 1;

    public string EnemyId => _enemyId;
    public GameObject Prefab => _prefab;
    public int SpawnCost => _spawnCost;
}
