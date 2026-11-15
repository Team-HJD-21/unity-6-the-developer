using System;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>같은 커서를 향하는 두 번째 총을 제한 시간 동안 활성화합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDualWieldSkill : PlayerSkillBase
{
    [SerializeField, Min(.1f)] private float _duration = 8f;
    [SerializeField, Min(.1f)] private float _cooldown = 20f;
    private double _activeUntil, _readyAt;
    private PlayerWeaponSelection _selection;
    private PlayerWeaponVisual _visual;
    private double Now => FieldSimulationTime.Seconds(this);
    public override PlayerSkillId Id => PlayerSkillId.DualWield;
    public override bool HasPrefab => Visual != null && Visual.IsDualWieldConfigured;
    private PlayerWeaponVisual Visual => _visual != null ? _visual : (_visual = GetComponent<PlayerWeaponVisual>());
    public float ActiveRemaining => (float)Math.Max(0, _activeUntil - Now);
    public float EffectDuration => _duration * UpgradeMultiplier;
    public bool IsActive => isActiveAndEnabled && ActiveRemaining > 0 && HasPrefab &&
        GetComponent<PlayerInfo>() is PlayerInfo info && !info.IsDestroyed && info.curHp > 0;
    public override int AvailableCount => RemainingCooldown <= 0 ? 1 : 0;
    public override float CooldownDuration => _cooldown;
    public override float RemainingCooldown => (float)Math.Max(0, _readyAt - Now);
    public override bool IsReady => base.IsReady && CurrentWeaponIsGun;
    private bool CurrentWeaponIsGun
    { get { if (_selection == null) _selection = GetComponent<PlayerWeaponSelection>(); return _selection != null && _selection.CurrentWeapon != WeaponType.Spanner; } }
    protected override bool TryActivate()
    {
        if (!CurrentWeaponIsGun || !HasPrefab || (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening && !Unity.Netcode.NetworkManager.Singleton.IsServer)) return false;
        _activeUntil = Now + EffectDuration; _readyAt = Now + _cooldown; return true;
    }
    private void Update()
    { if (GetComponent<PlayerInfo>() is PlayerInfo info && (info.IsDestroyed || info.curHp <= 0)) _activeUntil = 0; }
    private void OnDisable() => _activeUntil = 0;
    protected override Action<BinaryWriter> CaptureCheckpointState(ICheckpointPayloadCodec codec)
    {
        float active = ActiveRemaining, cooldown = RemainingCooldown;
        return writer => { writer.Write(active); writer.Write(cooldown); };
    }
    protected override ICheckpointRestoreStep PrepareCheckpointState(BinaryReader reader, ICheckpointPayloadCodec codec)
    {
        float active = reader.ReadSingle(), cooldown = reader.ReadSingle();
        // Prepare 시점에는 저장된 연구 레벨이 아직 적용되지 않았습니다.
        if (!float.IsFinite(active) || !float.IsFinite(cooldown) || active < 0 || active > _duration * PlayerSkillResearch.MultiplierAtLevel(PlayerSkillResearch.MaximumLevel) || cooldown < active || cooldown > _cooldown)
            throw new InvalidDataException("Invalid dual-wield timers.");
        float beforeActive = ActiveRemaining, beforeCooldown = RemainingCooldown;
        return new CheckpointRestoreStep(() => { _activeUntil=Now+active; _readyAt=Now+cooldown; },
            () => { _activeUntil=Now+beforeActive; _readyAt=Now+beforeCooldown; });
    }
}
