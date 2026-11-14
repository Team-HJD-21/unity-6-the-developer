// 발사자 writer가 소유 탄환의 실제 비행·피해·남은 수명을 보존하도록 연결합니다.
// 종료 예정 탄환은 재발사하지 않으며 복원 시 FieldSimulationActor의 정지 보관 값도 함께 갱신합니다.
using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using System.Text;
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
            CheckpointTransformState.Capture(transform), field != null ? field.CaptureBody(_body) : CheckpointRigidbody2DState.Capture(_body), SourceWeapon,
            _piercedTargets.OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }
    internal void RestoreCheckpoint(CheckpointState state)
    {
        if (_body == null || _hitboxes.Count == 0) throw new InvalidOperationException("The authored Player projectile has no valid physics shape.");
        SourceWeapon = state.SourceWeapon;
        _flightDirection = state.Body.Velocity.normalized;
        _damage = state.Damage; _launchColor = state.Color; _cryogenic = state.Cryogenic; _hasHit = false;
        _piercedTargets.Clear(); foreach (string id in state.PiercedTargets) _piercedTargets.Add(id);
        state.Pose.Apply(transform);
        var body = state.Body;
        double remaining = state.Remaining;
        var field = GetComponent<FieldSimulationActor>();
        if (field != null) field.RestoreBody(_body, body); else body.Apply(_body);
        _expiresAt = GameSeconds + remaining;
        if (_spriteRenderer != null) _spriteRenderer.color = state.Color;
        if (_view != null) _view.Configure(state.Cryogenic, state.Color);
    }

    internal sealed class CheckpointState
    {
        public readonly WeaponType SourceWeapon;
        public readonly float Damage;
        public readonly Color Color;
        public readonly bool Cryogenic;
        public readonly double Remaining;
        public readonly CheckpointTransformState Pose;
        public readonly CheckpointRigidbody2DState Body;
        public readonly string[] PiercedTargets;
        public CheckpointState(float damage, Color color, bool cryogenic, double remaining, CheckpointTransformState pose, CheckpointRigidbody2DState body,
            WeaponType sourceWeapon = WeaponType.DefaultGun, string[] piercedTargets = null)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0 ||
                !Finite(color.r) || !Finite(color.g) || !Finite(color.b) || !Finite(color.a) ||
                double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0)
                throw new InvalidDataException("Invalid saved Player projectile.");
            if (!Enum.IsDefined(typeof(WeaponType), sourceWeapon) || sourceWeapon == WeaponType.Spanner)
                throw new InvalidDataException("Invalid saved projectile source weapon.");
            SourceWeapon = sourceWeapon;
            PiercedTargets = piercedTargets == null ? Array.Empty<string>() : (string[])piercedTargets.Clone();
            if (PiercedTargets.Length > 4096 || PiercedTargets.Any(id => string.IsNullOrWhiteSpace(id) || Encoding.UTF8.GetByteCount(id) > 128) ||
                PiercedTargets.Distinct(StringComparer.Ordinal).Count() != PiercedTargets.Length)
                throw new InvalidDataException("Invalid saved sniper hit history.");
            Damage = damage; Color = color; Cryogenic = cryogenic; Remaining = remaining; Pose = pose; Body = body;
        }
        public void Write(BinaryWriter writer)
        {
            writer.Write(Damage); writer.Write(Color.r); writer.Write(Color.g); writer.Write(Color.b); writer.Write(Color.a);
            writer.Write(Cryogenic); writer.Write(Remaining); Pose.Write(writer); Body.Write(writer); writer.Write((int)SourceWeapon);
            writer.Write(PiercedTargets.Length);
            foreach (string id in PiercedTargets) { byte[] bytes = Encoding.UTF8.GetBytes(id); writer.Write(bytes.Length); writer.Write(bytes); }
        }
        public static CheckpointState Read(BinaryReader reader, int version, ICheckpointPayloadCodec codec)
        {
            float damage = reader.ReadSingle(); var color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            bool cryogenic = reader.ReadBoolean(); double remaining = reader.ReadDouble();
            var pose = CheckpointTransformState.Read(reader); var body = CheckpointRigidbody2DState.Read(reader); var weapon = (WeaponType)reader.ReadInt32();
            int count = version >= 3 ? reader.ReadInt32() : 0;
            if (count < 0 || count > 4096) throw new InvalidDataException("Invalid saved sniper hit count.");
            var hits = new string[count]; for (int i = 0; i < count; i++) hits[i] = codec.ReadText(reader, 128);
            return new(damage, color, cryogenic, remaining, pose, body, weapon, hits);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
