// 비행/폭발 대기 중인 미사일의 물리·표적·남은 지연을 저장합니다.
// 전체 actor 재생성 후 표적 claim을 연결하며 피해나 발사 효과를 재발행하지 않습니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public partial class FieldTurretMissile
{
    internal string CheckpointOwnerId;
    internal string CheckpointSourceDefinition;

    internal State CaptureCheckpoint()
    {
        if (!_isInitialized || _hasAppliedExplosion || !isActiveAndEnabled) throw new InvalidOperationException("Capture only live missiles.");
        var simulation = GetComponent<FieldSimulationActor>();
        return new State(CheckpointSourceDefinition, CheckpointTransformState.Capture(transform), simulation != null ? simulation.CaptureBody(rb) : CheckpointRigidbody2DState.Capture(rb),
            CheckpointSceneReferences.Capture(_target), _bulletDamage, _currentSpeed, _initialDirection, _isHoming, _isExploding,
            Math.Max(0, _homingStartsAt - FieldSimulationTime.Seconds(this)), Math.Max(0, _expiresAt - FieldSimulationTime.Seconds(this)),
            Math.Max(0, _explosionDamageAt - FieldSimulationTime.Seconds(this)));
    }

    internal void RestoreCheckpoint(State state)
    {
        CheckpointSourceDefinition = state.SourceDefinition;
        state.Pose.Apply(transform); _bulletDamage = state.Damage; _currentSpeed = state.Speed;
        _initialDirection = state.Direction; _isHoming = state.Homing; _isExploding = state.Exploding;
        double now = FieldSimulationTime.Seconds(this);
        _homingStartsAt = now + state.HomingRemaining; _expiresAt = now + state.LifeRemaining; _explosionDamageAt = now + state.ExplosionRemaining;
        _isInitialized = true; _hasAppliedExplosion = false;
        var simulation = GetComponent<FieldSimulationActor>();
        if (simulation != null) simulation.RestoreBody(rb, state.Body); else state.Body.Apply(rb);
        if (_sr != null) _sr.enabled = !_isExploding;
        if (minimapMissile != null) minimapMissile.enabled = !_isExploding;
    }

    internal void ResolveCheckpointTarget(State state)
    {
        ReleaseTargetReservation();
        _target = CheckpointSceneReferences.Resolve(gameObject.scene, state.Target);
        if (_target != null) _targetReservation = MissileTargetReservation.RestoreCheckpointClaim(_target);
    }

    internal sealed class State
    {
        public readonly string SourceDefinition;
        public readonly CheckpointTransformState Pose;
        public readonly CheckpointRigidbody2DState Body;
        public readonly string Target;
        public readonly float Damage, Speed;
        public readonly Vector2 Direction;
        public readonly bool Homing, Exploding;
        public readonly double HomingRemaining, LifeRemaining, ExplosionRemaining;
        public State(string sourceDefinition, CheckpointTransformState pose, CheckpointRigidbody2DState body, string target, float damage, float speed,
            Vector2 direction, bool homing, bool exploding, double homingRemaining, double lifeRemaining, double explosionRemaining)
        {
            if (!float.IsFinite(damage) || damage < 0 || !float.IsFinite(speed) || speed < 0 || !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                !Valid(homingRemaining) || !Valid(lifeRemaining) || !Valid(explosionRemaining) || lifeRemaining > 10 || explosionRemaining > .1001 || (exploding && !string.IsNullOrEmpty(target)))
                throw new InvalidDataException("Invalid missile flight state.");
            SourceDefinition = sourceDefinition;
            if (string.IsNullOrWhiteSpace(sourceDefinition)) throw new InvalidDataException("Missing projectile source definition.");
            Pose = pose; Body = body; Target = target; Damage = damage; Speed = speed; Direction = direction;
            Homing = homing; Exploding = exploding; HomingRemaining = homingRemaining; LifeRemaining = lifeRemaining; ExplosionRemaining = explosionRemaining;
        }
        private static bool Valid(double value) => double.IsFinite(value) && value >= 0;
        public void Write(BinaryWriter w, ICheckpointPayloadCodec codec)
        {
            codec.WriteText(w, SourceDefinition); Pose.Write(w); Body.Write(w); codec.WriteText(w, Target); w.Write(Damage); w.Write(Speed); w.Write(Direction.x); w.Write(Direction.y);
            w.Write(Homing); w.Write(Exploding); w.Write(HomingRemaining); w.Write(LifeRemaining); w.Write(ExplosionRemaining);
        }
        public static State Read(BinaryReader r, ICheckpointPayloadCodec codec) => new(codec.ReadText(r), CheckpointTransformState.Read(r), CheckpointRigidbody2DState.Read(r),
            codec.ReadText(r), r.ReadSingle(), r.ReadSingle(), new Vector2(r.ReadSingle(), r.ReadSingle()), r.ReadBoolean(), r.ReadBoolean(),
            r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
    }
}
