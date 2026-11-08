// NGO 생성/제거를 같은 Match의 시간 소비자에게 알리는 작은 어댑터입니다.
// Match ID는 서버 생성자가 지정하며 SDK 객체 ID와 별도로 보관합니다.
using System;
using TeamHJD.Game.Domain;
using Unity.Collections;

/// <summary>
/// 몬스터 행동을 Match의 실행 권한과 시계에 연결한다.
/// </summary>
public partial class EnemyNetworkController
{
    private readonly Unity.Netcode.NetworkVariable<FixedString512Bytes> _simulationMatch = new();
    private readonly Unity.Netcode.NetworkVariable<ulong> _simulationOwner = new(ulong.MaxValue);
    internal static event Action<EnemyNetworkController> SpawnedInMatch;
    internal static event Action<EnemyNetworkController> RemovedFromMatch;
    internal string SimulationMatchId => _simulationMatch.Value.ToString();
    internal ulong SimulationOwnerId => _simulationOwner.Value;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSimulationObservers()
    {
        SpawnedInMatch = null;
        RemovedFromMatch = null;
    }

    /// <summary>서버 Executor가 NetworkObject.Spawn 이전에 설정합니다. Client가 Match를 선택할 수 없습니다.</summary>
    internal void SetSimulationMatch(MatchId match, ulong executorId)
    {
        if (IsSpawned) throw new InvalidOperationException("Assign Match before network spawn.");
        _simulationMatch.Value = new FixedString512Bytes(match.Value ?? string.Empty);
        _simulationOwner.Value = executorId;
    }

    private void NotifySpawned() => Notify(SpawnedInMatch);
    private void NotifyDespawned() => Notify(RemovedFromMatch);

    private void Notify(Action<EnemyNetworkController> observers)
    {
        if (observers == null) return;
        foreach (Action<EnemyNetworkController> observer in observers.GetInvocationList())
            try { observer(this); }
            catch (Exception error) { UnityEngine.Debug.LogException(error, this); }
    }
}
