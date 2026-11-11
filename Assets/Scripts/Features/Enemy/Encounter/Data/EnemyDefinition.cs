using UnityEngine;

/// <summary>
/// 생성한 적이 공동 목표를 공유할지 한 마리씩 독립 분대로 행동할지 지정한다.
/// </summary>
public enum EnemySquadMode
{
    Shared,
    Individual
}

/// <summary>
/// 분대가 새 목표를 고를 때 허용할 타깃 종류를 지정한다.
/// </summary>
public enum EnemyTargetRule
{
    PlayerOrTurret,
    PlayerOnly
}

/// <summary>
/// 적 한 종류의 식별 정보와 생성에 필요한 제작 데이터를 정의한다.
/// </summary>
[CreateAssetMenu(menuName = "The Developer/Enemy/Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [SerializeField] private string _enemyId;
    [SerializeField] private GameObject _prefab;
    [SerializeField, Min(1)] private int _spawnCost = 1;

    [Header("분대 설정")]
    [SerializeField] private EnemySquadMode _squadMode;
    [SerializeField] private EnemyTargetRule _targetRule;

    [Header("공격 인원 제한")]
    [SerializeField] private bool _isIncludedInAttackerLimit = true;

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

    /// <summary>공동 분대 또는 한 마리씩 독립 분대를 구성할지 반환한다.</summary>
    public EnemySquadMode SquadMode => _squadMode;

    /// <summary>분대가 선택할 수 있는 타깃 유형을 반환한다.</summary>
    public EnemyTargetRule TargetRule => _targetRule;

    /// <summary>최초 분대 등록 때 전달할 공격 인원 집계 여부를 반환한다.</summary>
    /// <remarks>false인 몬스터도 분대의 공동 목표와 재검색 흐름을 유지한다.</remarks>
    public bool IsIncludedInAttackerLimit => _isIncludedInAttackerLimit;
}
