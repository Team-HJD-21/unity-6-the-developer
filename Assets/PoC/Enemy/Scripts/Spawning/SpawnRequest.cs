/// <summary>
/// Encounter가 한 스폰 지점에 어떤 분대를 최대 몇 마리까지 투입할지 전달한다.
/// 실제 적 종류별 생성 수는 <see cref="SpawnCompositionPlanner"/>가 프리셋을 적용해 결정한다.
/// </summary>
public readonly struct SpawnRequest
{
    /// <summary>
    /// 사용할 <see cref="EnemySquadPreset"/>을 찾는 분대 명령 값을 반환한다.
    /// </summary>
    public string SquadOrder { get; }

    /// <summary>
    /// 분대를 생성할 <see cref="SpawnPoint"/>의 고유 식별 값을 반환한다.
    /// </summary>
    public string SpawnPointId { get; }

    /// <summary>
    /// 해당 지점에 이번 요청으로 투입할 적의 상한을 반환한다.
    /// 프리셋 비율을 완전하게 채우지 못하는 나머지 수량은 생성하지 않는다.
    /// </summary>
    public int MaxCount { get; }

    /// <summary>
    /// 분대 명령과 스폰 지점, 생성 상한을 묶어 계획 계층에 전달할 요청을 만든다.
    /// </summary>
    /// <param name="squadOrder">적용할 분대 구성 프리셋의 명령 값.</param>
    /// <param name="spawnPointId">분대를 생성할 스폰 지점의 고유 식별 값.</param>
    /// <param name="maxCount">이번 요청에서 허용하는 적의 최대 수. 정확한 생성 수는 아니다.</param>
    public SpawnRequest(
        string squadOrder,
        string spawnPointId,
        int maxCount)
    {
        SquadOrder = squadOrder;
        SpawnPointId = spawnPointId;
        MaxCount = maxCount;
    }
}
