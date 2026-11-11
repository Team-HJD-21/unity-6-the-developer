using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>같은 터렛의 현장 복구 진행률을 공유합니다. 완료 전에는 실제 HP·파괴 상태를 바꾸지 않습니다.</summary>
public static class PlayerTurretRestoreProgress
{
    private sealed class Entry
    {
        public object TimeSource;
        public System.Guid Epoch;
        public double Fraction;
    }

    private static readonly Dictionary<FieldTurretBase, Entry> Entries = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Entries.Clear();

    public static float GetProgress(FieldTurretBase turret)
    {
        EnsureCleanupSubscriptions();
        if (turret == null) return 0f;
        if (!turret.IsDestroyed)
        {
            Entries.Remove(turret);
            return 0f;
        }
        if (Entries.TryGetValue(turret, out Entry entry))
        {
            var time = turret.GetComponent<FieldSimulationActor>()?.TimeSource;
            if (ReferenceEquals(entry.TimeSource, time) && entry.Epoch == (time?.Snapshot?.Epoch ?? System.Guid.Empty))
                return Mathf.Clamp01((float)entry.Fraction);
            Entries.Remove(turret);
        }
        return 0f;
    }

    /// <summary>복구 작업이 진행됐으면 true. 100%에서만 기존 Restore API를 호출합니다.</summary>
    public static bool TryAdvance(FieldTurretBase turret, float fractionPerSecond, float deltaTime)
    {
        if (turret == null || !turret.IsInitialized || !turret.isActiveAndEnabled ||
            !turret.HasStateAuthority || !turret.IsDestroyed || turret.IsLocked || turret.IsManualOperationBlocked ||
            !FieldSimulationTime.CanAdvance(turret) ||
            fractionPerSecond <= 0f || float.IsNaN(fractionPerSecond) || float.IsInfinity(fractionPerSecond) ||
            deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return false;

        GetProgress(turret);
        if (!Entries.TryGetValue(turret, out Entry entry))
        {
            var time = turret.GetComponent<FieldSimulationActor>()?.TimeSource;
            entry = new Entry { TimeSource = time, Epoch = time?.Snapshot?.Epoch ?? System.Guid.Empty };
            Entries.Add(turret, entry);
        }
        entry.Fraction = System.Math.Min(1d, entry.Fraction + (double)fractionPerSecond * deltaTime);
        if (entry.Fraction < 1d) return true;
        if (!turret.Restore()) return false;
        Entries.Remove(turret);
        return true;
    }

    private static void Clear(FieldTurretBase turret) => Entries.Remove(turret);

    private static void EnsureCleanupSubscriptions()
    {
        // Registry의 SubsystemRegistration 순서나 도메인 재로드 비활성 설정에도 중복 없이 연결합니다.
        FieldTurretInstanceRegistry.Destroyed -= Clear;
        FieldTurretInstanceRegistry.Destroyed += Clear;
        FieldTurretInstanceRegistry.Restored -= Clear;
        FieldTurretInstanceRegistry.Restored += Clear;
        FieldTurretInstanceRegistry.Unregistered -= Clear;
        FieldTurretInstanceRegistry.Unregistered += Clear;
    }
}
