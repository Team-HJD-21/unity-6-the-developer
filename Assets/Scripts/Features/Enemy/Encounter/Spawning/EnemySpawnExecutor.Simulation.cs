// Scene의 Enemy 생성/기존 개체를 주입된 Match 시간에 연결합니다.
// 전역 현재 Match를 보관하지 않고 NGO 생성 이벤트를 Match·Scene·Manager로 제한합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;

/// <summary>
/// 생성 실행기와 추적 중인 적을 Match 시계 및 실행 권한에 연결한다.
/// </summary>
public partial class EnemySpawnExecutor : IFieldSimulationConsumer
{
    private IMatchTime _fieldTime;
    private CancellationToken _gameplay;
    private readonly Dictionary<EnemyNetworkController, FieldSimulationActor> _fieldActors = new();
    private bool CanSimulateField => _fieldTime == null ||
        (!_gameplay.IsCancellationRequested && _fieldTime.IsAvailable && !_fieldTime.Snapshot.IsPaused && _fieldTime.Snapshot.Rate > 0);

    /// <summary>Core Scene scope가 Host/Client 양쪽에서 연결합니다. 반환 lease는 Match 해제 때 정리합니다.</summary>
    public IDisposable BindFieldSimulation(IMatchTime time, CancellationToken gameplay)
    {
        if (time == null) throw new ArgumentNullException(nameof(time));
        if (_fieldTime != null) throw new InvalidOperationException("Enemy executor is already bound.");
        _fieldTime = time;
        _gameplay = gameplay;
        EnemyNetworkController.SpawnedInMatch += BindEnemyTime;
        EnemyNetworkController.RemovedFromMatch += UnbindEnemyTime;
        BindExistingEnemyTimes();
        return new SimulationLease(this, time);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        BindExistingEnemyTimes();
    }

    private void BindExistingEnemyTimes()
    {
        if (_fieldTime == null || !IsSpawned || NetworkManager?.SpawnManager == null) return;
        foreach (var item in NetworkManager.SpawnManager.SpawnedObjects.Values)
            if (item.TryGetComponent<EnemyNetworkController>(out var enemy)) BindEnemyTime(enemy);
    }

    private void BindEnemyTime(EnemyNetworkController enemy)
    {
        if (_fieldTime == null || !IsSpawned || enemy == null || enemy.gameObject.scene != gameObject.scene ||
            enemy.SimulationOwnerId != NetworkObjectId ||
            enemy.NetworkManager != NetworkManager || enemy.SimulationMatchId != _fieldTime.Snapshot.MatchId.Value ||
            _fieldActors.ContainsKey(enemy)) return;
        // NGO 객체의 제거는 Host Executor가 담당합니다. Client에서 직접 Destroy하지 않습니다.
        _fieldActors.Add(enemy, FieldSimulationActor.Attach(enemy.gameObject, _fieldTime, _gameplay));
    }

    private void UnbindEnemyTime(EnemyNetworkController enemy)
    {
        if (!_fieldActors.Remove(enemy, out var actor)) return;
        if (actor != null) actor.Unbind(_fieldTime);
    }

    private void ReleaseSimulation(IMatchTime time)
    {
        if (!ReferenceEquals(_fieldTime, time)) return;
        EnemyNetworkController.SpawnedInMatch -= BindEnemyTime;
        EnemyNetworkController.RemovedFromMatch -= UnbindEnemyTime;
        foreach (var actor in _fieldActors.Values) if (actor != null) actor.Unbind(time);
        _fieldActors.Clear();
        _fieldTime = null;
        _gameplay = default;
    }

    private sealed class SimulationLease : IDisposable
    {
        private EnemySpawnExecutor _owner;
        private readonly IMatchTime _time;
        public SimulationLease(EnemySpawnExecutor owner, IMatchTime time) { _owner = owner; _time = time; }
        public void Dispose() { var owner = _owner; _owner = null; owner?.ReleaseSimulation(_time); }
    }
}
