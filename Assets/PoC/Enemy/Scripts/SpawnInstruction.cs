using UnityEngine;

/// <summary>
/// 특정 스폰 지점에 지정된 적을 생성하도록 전달하는 일회성 명령 데이터다.
/// 생성 위치 조회와 실제 생성은 실행 계층에서 담당한다.
/// </summary>
public readonly struct SpawnInstruction
{
    /// <summary>
    /// 생성할 적 정의의 고유 식별 값을 반환한다.
    /// </summary>
    public string EnemyId { get; }

    /// <summary>
    /// 적을 생성할 스폰 지점의 고유 식별 값을 반환한다.
    /// </summary>
    public int SpawnPointId { get; }

    /// <summary>
    /// 해당 명령으로 생성할 적의 수를 반환한다.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// 생성할 적과 스폰 지점, 생성 수량을 지정해 명령을 생성한다.
    /// </summary>
    /// <param name="enemyId">생성할 적 정의의 고유 식별 값.</param>
    /// <param name="spawnPointId">적을 생성할 스폰 지점의 고유 식별 값.</param>
    /// <param name="count">생성할 적의 수.</param>
    public SpawnInstruction(
        string enemyId,
        int spawnPointId,
        int count)
    {
        EnemyId = enemyId;
        SpawnPointId = spawnPointId;
        Count = count;
    }
}
