// 최종 Scene의 지정 보스를 기존 Enemy 생성·분대·NGO·HP 사망 경계로 만듭니다.
// Field 정지와 별개인 공용 MatchTime에 연결하며 일반 편성/예산을 다시 뽑지 않습니다.
using System;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

[DisallowMultipleComponent]
public sealed class FinalBattleBossSource : MonoBehaviour, IFinalBattleBossSource
{
    [SerializeField] private EnemySpawnExecutor executor;
    [SerializeField] private EnemyDefinition definition;
    [SerializeField] private Transform spawn;
    private IDisposable _simulation;
    private MatchId _match;
    private EntityId _identity;

    /// <summary>Core 기본 Scene driver가 호출합니다. 제작값 누락/다른 Scene/다른 Match는 실제 생성 전에 거부합니다.</summary>
    public EntityId PrepareFinalBoss(IMatchTime time, CancellationToken gameplay)
    {
        if (time == null || !time.IsAvailable || gameplay.IsCancellationRequested || executor == null ||
            executor.gameObject.scene != gameObject.scene || spawn == null || spawn.gameObject.scene != gameObject.scene ||
            !spawn.gameObject.activeInHierarchy || definition == null || !executor.CanSpawn)
            throw new InvalidOperationException("최종 Scene의 실제 Host Executor·Definition·활성 도착점이 필요합니다.");
        if (_simulation != null)
        {
            if (_match != time.Snapshot.MatchId) throw new InvalidOperationException("다른 Match의 보스 수명이 남아 있습니다.");
            return _identity;
        }
        _match = time.Snapshot.MatchId;
        try
        {
            _simulation = executor.BindFieldSimulation(time, gameplay);
            if (!executor.TrySpawnFinalBoss(definition, spawn, _match, gameplay, out _identity, out var reason))
                throw new InvalidOperationException(reason);
            return _identity;
        }
        catch { ReleaseFinalBoss(); throw; }
    }

    /// <summary>이 Scene의 전용 Executor가 만든 현재 Match 개체와 시간 연결만 정리합니다. 다른 Field Executor는 건드리지 않습니다.</summary>
    public void ReleaseFinalBoss()
    {
        var simulation = _simulation;
        var match = _match;
        _simulation = null; _identity = default; _match = default;
        // Bind 실패는 기존 소유자의 수명입니다. 획득하지 못한 Executor의 개체를 정리하지 않습니다.
        try { if (simulation != null && executor != null && !match.IsEmpty) executor.DespawnForMatch(match); }
        finally { simulation?.Dispose(); }
    }
}
