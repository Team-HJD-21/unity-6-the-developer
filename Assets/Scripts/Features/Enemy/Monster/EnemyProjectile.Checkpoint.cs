// 생성자가 사망해도 남아 있는 적 탄환의 비행·피해·남은 수명을 보존합니다.
// 저장은 Enemy 콘텐츠 ID를 쓰고 NGO 소유 ID는 현재 실행에서만 재설정합니다.
using System;
using System.IO;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public partial class EnemyProjectile
{
    private ulong _checkpointExecutorId = ulong.MaxValue;
    private string _checkpointEnemyType;
    internal string CheckpointEnemyType => _checkpointEnemyType;
    internal bool BelongsToCheckpoint(EnemySpawnExecutor owner) => owner != null && _checkpointExecutorId == owner.NetworkObjectId &&
        owner.NetworkManager == NetworkManager && owner.gameObject.scene == gameObject.scene && IsSpawned && !_hasHit;

    internal State CaptureCheckpoint()
        => new(CheckpointTransformState.Capture(transform), _simulation.CaptureBody(rigidbody2D), _projectileDamage,
            _direction, Math.Max(0, _expiresAt - FieldSimulationTime.Seconds(this)));

    internal void RestoreCheckpoint(State state, EnemySpawnExecutor executor, string enemyType)
    {
        _simulation = executor.BindCheckpointProjectile(gameObject);
        _ownerId.Value = executor.NetworkObjectId;
        _checkpointExecutorId = executor.NetworkObjectId; _checkpointEnemyType = enemyType;
        _projectileDamage = state.Damage; _direction = state.Direction; _hasHit = false;
        _expiresAt = FieldSimulationTime.Seconds(this) + state.Remaining;
        state.Pose.Apply(transform); _simulation.RestoreBody(rigidbody2D, state.Body);
        ApplyAnimationDirection(_direction);
    }

    internal sealed class State
    {
        public readonly CheckpointTransformState Pose;
        public readonly CheckpointRigidbody2DState Body;
        public readonly int Damage;
        public readonly Vector2 Direction;
        public readonly double Remaining;
        public State(CheckpointTransformState pose, CheckpointRigidbody2DState body, int damage, Vector2 direction, double remaining)
        {
            if (damage < 0 || !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !double.IsFinite(remaining) || remaining < 0)
                throw new InvalidDataException("Invalid enemy projectile state.");
            Pose = pose; Body = body; Damage = damage; Direction = direction; Remaining = remaining;
        }
        public void Write(BinaryWriter w)
        { Pose.Write(w); Body.Write(w); w.Write(Damage); w.Write(Direction.x); w.Write(Direction.y); w.Write(Remaining); }
        public static State Read(BinaryReader r) => new(CheckpointTransformState.Read(r), CheckpointRigidbody2DState.Read(r),
            r.ReadInt32(), new Vector2(r.ReadSingle(), r.ReadSingle()), r.ReadDouble());
    }
}
