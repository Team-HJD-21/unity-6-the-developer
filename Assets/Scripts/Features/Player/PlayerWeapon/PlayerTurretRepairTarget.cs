using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

/// <summary>필드 터렛과 전력을 사용하지 않는 플레이어 터렛을 같은 R 수리 흐름으로 연결합니다.</summary>
internal readonly struct PlayerTurretRepairTarget
{
    public MonoBehaviour Source { get; }
    public FieldTurretBase Field => Source as FieldTurretBase;
    public PlayerTurretHealth Player => Source as PlayerTurretHealth;
    public bool Exists => Source != null;
    public Transform Transform => Exists ? Source.transform : null;
    public float CurrentHealth => Field != null ? Field.CurrentHealth : Player != null ? Player.Health : 0;
    public float MaxHealth => Field != null ? Field.MaxHealth : Player != null ? Player.MaxHealth : 0;
    public bool IsDestroyed => Field != null ? Field.IsDestroyed : Player == null || !Player.IsAlive;
    public bool CanRepair => Field != null ? Field.CanRepair : Player != null && Player.CanRepair;
    public PlayerTurretRepairTarget(MonoBehaviour source) => Source = source;
    public FieldTurretRepairResult Repair(float rate, float seconds) => Field != null ? Field.Repair(rate, seconds)
        : Player != null ? Player.Repair(rate, seconds) : FieldTurretRepairResult.Inactive;
}
