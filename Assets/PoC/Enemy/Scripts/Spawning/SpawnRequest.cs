/// <summary>
/// Planner가 임시로 결정한 분대 명령과 전체 생성 상한을 내부 계획 단계에 전달한다.
/// 실제 스폰 지점과 적 종류별 수량은 <see cref="SpawnCompositionPlanner"/>가 결정한다.
/// </summary>
public readonly struct SpawnRequest
{
    /// <summary>
    /// 사용할 <see cref="EnemySquadPreset"/>을 찾는 분대 명령 값을 반환한다.
    /// </summary>
    public string SquadOrder { get; }

    /// <summary>
    /// 이번 요청에서 모든 스폰 지점에 투입할 적의 총 상한을 반환한다.
    /// 프리셋 비율을 완전하게 채우지 못하는 나머지 수량은 생성하지 않는다.
    /// </summary>
    public int MaxCount { get; }

    /// <summary>
    /// 특정 지점을 지정하지 않고 Planner가 스폰 가능한 지점들에 예산을 나누도록 요청한다.
    /// </summary>
    /// <param name="squadOrder">적용할 분대 구성 프리셋의 명령 값.</param>
    /// <param name="maxCount">모든 지점에 허용하는 적의 총 상한. 정확한 생성 수는 아니다.</param>
    public SpawnRequest(string squadOrder, int maxCount)
    {
        SquadOrder = squadOrder;
        MaxCount = maxCount;
    }
}
