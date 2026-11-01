// 발사자 writer가 소유 탄환의 실제 비행·피해·남은 수명을 보존하도록 연결합니다.
// 종료 예정 탄환은 재발사하지 않으며 복원 시 FieldSimulationActor의 정지 보관 값도 함께 갱신합니다.
using System;
using System.IO;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerBullet
{
    private Color _launchColor = Color.white;
    private bool _cryogenic;
    internal event Action<PlayerBullet> Retired;
    internal bool IsCheckpointAlive => this != null && isActiveAndEnabled && !_hasHit && GameSeconds < _expiresAt;
    private void OnDestroy() { Retired?.Invoke(this); Retired = null; }

    internal CheckpointState CaptureCheckpoint()
    {
        if (!IsCheckpointAlive) throw new InvalidOperationException("Capture only a live owned projectile.");
        var field = GetComponent<FieldSimulationActor>();
        return new CheckpointState(_damage, _launchColor, _cryogenic, Math.Max(0, _expiresAt - GameSeconds),
            CheckpointTransformState.Capture(transform), field != null ? field.CaptureBody(_body) : CheckpointRigidbody2DState.Capture(_body));
    }
    internal void RestoreCheckpoint(CheckpointState state)
    {
        if (_body == null || _hitboxes.Count == 0) throw new InvalidOperationException("The authored Player projectile has no valid physics shape.");
        _damage = state.Damage; _launchColor = state.Color; _cryogenic = state.Cryogenic; _hasHit = false;
        state.Pose.Apply(transform);
        var field = GetComponent<FieldSimulationActor>();
        if (field != null) field.RestoreBody(_body, state.Body); else state.Body.Apply(_body);
        _expiresAt = GameSeconds + state.Remaining;
        if (_spriteRenderer != null) _spriteRenderer.color = state.Color;
        if (_view != null) _view.Configure(state.Cryogenic, state.Color);
    }

    internal sealed class CheckpointState
    {
        public readonly float Damage;
        public readonly Color Color;
        public readonly bool Cryogenic;
        public readonly double Remaining;
        public readonly CheckpointTransformState Pose;
        public readonly CheckpointRigidbody2DState Body;
        public CheckpointState(float damage, Color color, bool cryogenic, double remaining, CheckpointTransformState pose, CheckpointRigidbody2DState body)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0 ||
                !Finite(color.r) || !Finite(color.g) || !Finite(color.b) || !Finite(color.a) ||
                double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0)
                throw new InvalidDataException("Invalid saved Player projectile.");
            Damage = damage; Color = color; Cryogenic = cryogenic; Remaining = remaining; Pose = pose; Body = body;
        }
        public void Write(BinaryWriter writer)
        {
            writer.Write(Damage); writer.Write(Color.r); writer.Write(Color.g); writer.Write(Color.b); writer.Write(Color.a);
            writer.Write(Cryogenic); writer.Write(Remaining); Pose.Write(writer); Body.Write(writer);
        }
        public static CheckpointState Read(BinaryReader reader) => new(reader.ReadSingle(),
            new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            reader.ReadBoolean(), reader.ReadDouble(), CheckpointTransformState.Read(reader), CheckpointRigidbody2DState.Read(reader));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
