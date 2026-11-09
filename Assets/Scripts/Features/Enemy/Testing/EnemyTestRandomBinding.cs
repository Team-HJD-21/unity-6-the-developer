using System;
using TeamHJD.Game.Contracts;
using UnityEngine;

/// <summary>씬에 주입된 Core 난수를 영속 테스트 런처에 전달한다.</summary>
[DisallowMultipleComponent]
public sealed class EnemyTestRandomBinding : MonoBehaviour, IRandomConsumer
{
    [SerializeField] private EnemyNetworkTestLauncher launcher;

    public IDisposable BindRandom(IRandomStreams random)
    {
        if (launcher == null)
            throw new InvalidOperationException("테스트 런처 참조가 필요합니다.");

        return launcher.BindRandom(random);
    }
}
