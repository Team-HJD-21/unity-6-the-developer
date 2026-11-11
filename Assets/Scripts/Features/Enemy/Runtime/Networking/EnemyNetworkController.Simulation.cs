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
    private FixedString512Bytes _pendingSimulationMatch;
    private ulong _pendingSimulationOwner = ulong.MaxValue;
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

    /// <summary>
    /// 서버 Executor가 Spawn 전에 귀속 자료를 준비합니다. NGO 변수는 초기화 후 반영하며 Client는 수정할 수 없습니다.
    /// </summary>
    internal void SetSimulationMatch(MatchId match, ulong executorId)
    {
        if (IsSpawned) throw new InvalidOperationException("Assign Match before network spawn.");
        _pendingSimulationMatch = new FixedString512Bytes(match.Value ?? string.Empty);
        _pendingSimulationOwner = executorId;
    }

    private void NotifySpawned()
    {
        // NGO는 변수 초기화 후 OnNetworkSpawn을 호출하고, 이후 최초 Spawn 패킷을 만듭니다.
        // 귀속 값을 먼저 확정해야 Host 구독자와 Client의 최초 통지가 같은 값을 읽습니다.
        if (IsServer)
        {
            _simulationMatch.Value = _pendingSimulationMatch;
            _simulationOwner.Value = _pendingSimulationOwner;
            _pendingSimulationMatch = default;
            _pendingSimulationOwner = ulong.MaxValue;
        }

        Notify(SpawnedInMatch);
    }
    private void NotifyDespawned() => Notify(RemovedFromMatch);

    private void Notify(Action<EnemyNetworkController> observers)
    {
        if (observers == null) return;
        foreach (Action<EnemyNetworkController> observer in observers.GetInvocationList())
            try { observer(this); }
            catch (Exception error) { UnityEngine.Debug.LogException(error, this); }
    }
}
