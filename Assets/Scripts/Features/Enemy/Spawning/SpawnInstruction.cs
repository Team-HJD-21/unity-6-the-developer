using System;
using System.Collections.Generic;

/// <summary>
/// 분대 생성 명령에 포함되는 적 한 종류와 확정된 생성 수를 나타낸다.
/// 프리셋의 가중치와 달리 <see cref="Count"/>는 Executor가 실제로 반복 생성할 수량이다.
/// </summary>
public readonly struct EnemySpawnEntry
{
    /// <summary>
    /// 생성할 적 정의의 고유 식별 값을 반환한다.
    /// </summary>
    public string EnemyId { get; }

    /// <summary>
    /// 해당 적 종류의 실제 생성 수를 반환한다.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// 적 종류와 생성 수를 지정해 분대 구성 항목을 생성한다.
    /// </summary>
    /// <param name="enemyId">생성할 적 정의의 고유 식별 값.</param>
    /// <param name="count">해당 적 종류의 생성 수.</param>
    public EnemySpawnEntry(string enemyId, int count)
    {
        EnemyId = enemyId;
        Count = count;
    }
}

/// <summary>
/// Planner가 확정한 한 스폰 지점의 분대 구성 전체를 Executor에 전달한다.
/// 공통 스폰 지점과 분대 명령은 한 번만 보관하고, 적 종류별 수량은 <see cref="Enemies"/>에 묶는다.
/// 실제 스폰 위치 조회와 네트워크 생성은 이 데이터가 아닌 Executor가 담당한다.
/// </summary>
public readonly struct SpawnInstruction
{
    /// <summary>
    /// 이 분대에 적용된 프리셋의 명령 값을 반환한다.
    /// 이후 분대 행동을 연결할 때도 같은 명령을 참조할 수 있다.
    /// </summary>
    public string SquadOrder { get; }

    /// <summary>
    /// 분대 전체가 공유하는 <see cref="SpawnPoint"/>의 고유 식별 값을 반환한다.
    /// </summary>
    public string SpawnPointId { get; }

    /// <summary>
    /// 분대에 포함할 적 종류별 확정 생성 수를 읽기 전용 목록으로 반환한다.
    /// 생성 시 전달한 원본 목록과 별도로 복사되어 이후 원본을 수정해도 바뀌지 않는다.
    /// </summary>
    public IReadOnlyList<EnemySpawnEntry> Enemies { get; }

    /// <summary>
    /// 공통 분대 정보와 적별 확정 수량을 묶어 완성된 생성 명령을 만든다.
    /// </summary>
    /// <param name="squadOrder">적용된 분대 구성 명령 값.</param>
    /// <param name="spawnPointId">분대를 생성할 스폰 지점의 고유 식별 값.</param>
    /// <param name="enemies">분대에 포함할 적 종류별 생성 수.</param>
    /// <exception cref="ArgumentNullException"><paramref name="enemies"/>가 null일 때 발생한다.</exception>
    public SpawnInstruction(
        string squadOrder,
        string spawnPointId,
        IReadOnlyList<EnemySpawnEntry> enemies)
    {
        SquadOrder = squadOrder;
        SpawnPointId = spawnPointId;

        if (enemies == null)
            throw new ArgumentNullException(nameof(enemies));

        // 호출자가 전달한 목록을 나중에 수정해도 명령 내용은 바뀌지 않도록 복사한다.
        EnemySpawnEntry[] copiedEnemies = new EnemySpawnEntry[enemies.Count];
        for (int i = 0; i < enemies.Count; i++)
            copiedEnemies[i] = enemies[i];

        Enemies = Array.AsReadOnly(copiedEnemies);
    }
}
