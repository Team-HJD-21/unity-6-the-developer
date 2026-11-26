using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public partial class FieldLaserTurret : IFieldTurretCombatCheckpoint
{
    private const string CombatCheckpointKey = "turret/laser";
    private string _restoredTarget;

    CheckpointSection IFieldTurretCombatCheckpoint.CaptureCombat(ICheckpointPayloadCodec codec)
    {
        double now = FieldSeconds;
        Transform target = Target != null && Target.gameObject.activeInHierarchy ? Target : null;
        return codec.Write(CombatCheckpointKey, 1, CheckpointSectionKind.World, writer =>
        {
            writer.Write(FireTime);
            codec.WriteText(writer, CheckpointSceneReferences.Capture(target));
            writer.Write(_isBeamPlaying);
            writer.Write(_isBeamRetracting);
            writer.Write(_beamOrigin.x);
            writer.Write(_beamOrigin.y);
            writer.Write(_beamOrigin.z);
            writer.Write(_beamDirection.x);
            writer.Write(_beamDirection.y);
            writer.Write(_beamLength);
            writer.Write(_visibleLengthRatio);
            writer.Write(_visibleWidthRatio);
            writer.Write(_isBeamPlaying ? (float)Math.Max(0d, _beamEndsAt - now) : 0f);
            writer.Write(_isBeamPlaying ? (float)(_nextDamageAt - now) : 0f); // 이미 도래한 틱의 간격도 유지한다.
            writer.Write((float)Math.Max(0d, _nextBeamExtensionAt - now));
        });
    }

    void IFieldTurretCombatCheckpoint.ValidateCombat(CheckpointSection section, ICheckpointPayloadCodec codec)
        => ReadCombat(section, codec);

    private (float Heat, string Target, bool Playing, bool Retracting, Vector3 Origin, Vector2 Direction,
        float Length, float LengthRatio, float WidthRatio, float BeamRemaining, float DamageOffset, float RestartRemaining)
        ReadCombat(CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        if (section.Key != CombatCheckpointKey)
            throw new InvalidDataException("Wrong laser combat schema.");

        return codec.Read(section, 1, reader =>
        {
            float heat = reader.ReadSingle();
            string target = codec.ReadText(reader);
            bool playing = reader.ReadBoolean();
            bool retracting = reader.ReadBoolean();
            var origin = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var direction = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            float length = reader.ReadSingle();
            float lengthRatio = reader.ReadSingle();
            float widthRatio = reader.ReadSingle();
            float beamRemaining = reader.ReadSingle();
            float damageOffset = reader.ReadSingle();
            float restartRemaining = reader.ReadSingle();

            if (!IsNonNegative(heat) || !IsNonNegative(length) ||
                !IsNonNegative(beamRemaining) || !float.IsFinite(damageOffset) || !IsNonNegative(restartRemaining) ||
                !float.IsFinite(origin.x) || !float.IsFinite(origin.y) || !float.IsFinite(origin.z) ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                !IsNonNegative(lengthRatio) || lengthRatio > 1f ||
                !float.IsFinite(widthRatio) || widthRatio < ThinWidthRatio || widthRatio > 1f ||
                (playing && retracting) || ((playing || retracting) && lengthRatio <= 0f))
                throw new InvalidDataException("Invalid saved laser combat state.");

            if (lengthRatio > 0f && (beamRenderer == null || beamMuzzle == null || length <= 0f ||
                Mathf.Abs(direction.sqrMagnitude - 1f) > 0.001f))
                throw new InvalidDataException("Invalid saved laser beam or missing prefab references.");

            return (heat, target, playing, retracting, origin, direction, length, lengthRatio, widthRatio,
                beamRemaining, damageOffset, restartRemaining);
        });
    }

    private static bool IsNonNegative(float value) => float.IsFinite(value) && value >= 0f;

    void IFieldTurretCombatCheckpoint.ReleaseCombat()
    {
        StopFiring();
        HideBeam();
        Target = null;
    }

    void IFieldTurretCombatCheckpoint.RestoreCombat(CheckpointSection section, ICheckpointPayloadCodec codec)
    {
        var state = ReadCombat(section, codec);
        double now = FieldSeconds;
        FireTime = state.Heat;
        _restoredTarget = state.Target;
        _beamOrigin = state.Origin;
        _beamDirection = state.Direction;
        _beamAngle = Vector2.SignedAngle(Vector2.up, state.Direction);
        _beamLength = state.Length;
        _visibleLengthRatio = state.LengthRatio;
        _visibleWidthRatio = state.WidthRatio;
        _isBeamPlaying = state.Playing;
        _isBeamRetracting = state.Retracting;
        _beamEndsAt = now + state.BeamRemaining;
        _nextDamageAt = now + state.DamageOffset;
        _nextBeamExtensionAt = now + state.RestartRemaining;
    }

    void IFieldTurretCombatCheckpoint.ResolveCombatReferences()
    {
        Target = CheckpointSceneReferences.Resolve(gameObject.scene, _restoredTarget);
        _restoredTarget = null;

        float overHeatTime = Definition.OverHeatTime;
        SetGunHeatTint(overHeatTime > 0f ? FireTime / overHeatTime : 0f);
        if (!IsActivated && FireTime <= 0f && GunRenderer != null)
            GunRenderer.color = Color.gray;

        if (Animator != null)
        {
            Animator.SetBool("isOn", IsActivated);
            Animator.SetBool("isShoot", _isBeamPlaying);
        }
    }
}
