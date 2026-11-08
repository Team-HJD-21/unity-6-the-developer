// 원거리 능력의 발사 전/후·완료 대기와 정지 중 도착한 이벤트를 저장합니다.
// 복원은 투사체를 새로 발사하지 않고 원래 Animation Event 또는 지연 이벤트에 맡깁니다.
using System.IO;
using UnityEngine;

public sealed partial class EnemyProjectileAbility
{
    internal AttackState CaptureAttackState() => new(fireOnAnimationEvent, _isExecuting, _shotPending,
        _pendingDirection, _fireEventDeferred, _finishEventDeferred);

    internal void ValidateAttackState(AttackState state)
    {
        if (state == null || state.UsesAnimationEvents != fireOnAnimationEvent)
            throw new InvalidDataException("Saved ranged attack timing differs from its prefab.");
    }

    internal void RestoreAttackState(AttackState state)
    {
        ValidateAttackState(state);
        _isExecuting = state.Executing;
        _shotPending = state.ShotPending;
        _pendingDirection = state.Direction;
        _fireEventDeferred = state.FireDeferred;
        _finishEventDeferred = state.FinishDeferred;
    }

    internal sealed class AttackState
    {
        public readonly bool UsesAnimationEvents, Executing, ShotPending, FireDeferred, FinishDeferred;
        public readonly Vector2 Direction;
        public AttackState(bool usesAnimationEvents, bool executing, bool shotPending, Vector2 direction,
            bool fireDeferred, bool finishDeferred)
        {
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                (!executing && (shotPending || fireDeferred || finishDeferred)) ||
                (executing && !usesAnimationEvents) || (fireDeferred && !shotPending) ||
                (shotPending && direction.sqrMagnitude < .0001f))
                throw new InvalidDataException("Invalid saved ranged attack phase.");
            UsesAnimationEvents = usesAnimationEvents;
            Executing = executing;
            ShotPending = shotPending;
            Direction = direction;
            FireDeferred = fireDeferred;
            FinishDeferred = finishDeferred;
        }
        public void Write(BinaryWriter writer)
        {
            writer.Write(UsesAnimationEvents);
            writer.Write(Executing);
            writer.Write(ShotPending);
            writer.Write(Direction.x);
            writer.Write(Direction.y);
            writer.Write(FireDeferred);
            writer.Write(FinishDeferred);
        }
        public static AttackState Read(BinaryReader reader) => new(reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()), reader.ReadBoolean(), reader.ReadBoolean());
    }
}
