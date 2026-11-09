// 생성 위치에 사용하는 난수를 이번 Match의 이름별 흐름으로 연결합니다.
// 저장/복원은 Core 난수 writer가 담당하고 생성기는 따로 seed를 만들지 않습니다.
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;

/// <summary>
/// Core에서 받은 Match 난수 흐름을 생성 위치 계산에 연결한다.
/// </summary>
public partial class EnemySpawnExecutor : IRandomConsumer
{
    private IRandomSource _spawnRandom;

    /// <summary>Core Scene 조립부가 첫 생성 전에 호출합니다. 반환 lease를 Match 종료에 해제합니다.</summary>
    public IDisposable BindRandom(IRandomStreams random)
        => RandomBinding.Bind(random, "encounter/placement", ref _spawnRandom);
}
