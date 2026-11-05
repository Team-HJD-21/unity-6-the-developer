// 시한폭탄의 논리 ID·진행 시간·폭발 여부를 보존합니다.
// 이미 폭발한 상태를 재구성할 때 범위 피해는 다시 적용하지 않습니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;

public sealed partial class PlayerBombExplosion
{
    internal event Action<PlayerBombExplosion> Retired;
    internal bool IsCheckpointAlive => this != null && isActiveAndEnabled && (!_exploded || _elapsed < _fuseSeconds + _effectSeconds);
    private void OnDestroy() { Retired?.Invoke(this); Retired = null; }
    internal State CaptureCheckpoint()
    {
        if (!IsCheckpointAlive) throw new InvalidOperationException("Capture a live owned time bomb.");
        if (_applyingExplosion) throw new InvalidOperationException("Capture after the complete area-damage operation, not inside a damage callback.");
        var id = GetComponent<SceneEntityIdentity>().EntityId;
        return new State(id, _elapsed, _exploded, CheckpointTransformState.Capture(transform));
    }
    internal void RestoreCheckpoint(State state)
    {
        if (GetComponent<SceneEntityIdentity>().EntityId != state.Id) throw new InvalidOperationException("The restored bomb identity differs.");
        _elapsed = state.Elapsed; _exploded = state.Exploded; state.Pose.Apply(transform);
        if (_hasExplosionParameter) _animator.SetBool(ExplosionHash, state.Exploded);
        if (_presentation != null)
        {
            if (_exploded) _presentation.PlayExplosion(_radius, Math.Max(.01f, _fuseSeconds + _effectSeconds - _elapsed));
            else { _presentation.BeginFuse(_fuseSeconds, _radius); _presentation.UpdateFuse(_elapsed, _fuseSeconds, _radius); }
        }
    }
    internal sealed class State
    {
        public readonly EntityId Id;
        public readonly float Elapsed;
        public readonly bool Exploded;
        public readonly CheckpointTransformState Pose;
        public State(EntityId id, float elapsed, bool exploded, CheckpointTransformState pose)
        {
            if (id.IsEmpty || id.Value.Length > 128 || !PlayerSkillCheckpointIO.Finite(elapsed) || elapsed < 0)
                throw new InvalidDataException("Invalid saved bomb state.");
            Id = id; Elapsed = elapsed; Exploded = exploded; Pose = pose;
        }
        public void Write(BinaryWriter writer, ICheckpointPayloadCodec codec)
        { codec.WriteText(writer, Id.Value, 128); writer.Write(Elapsed); writer.Write(Exploded); Pose.Write(writer); }
        public static State Read(BinaryReader reader, ICheckpointPayloadCodec codec)
            => new(new EntityId(codec.ReadText(reader, 128)), reader.ReadSingle(), reader.ReadBoolean(), CheckpointTransformState.Read(reader));
    }
}
