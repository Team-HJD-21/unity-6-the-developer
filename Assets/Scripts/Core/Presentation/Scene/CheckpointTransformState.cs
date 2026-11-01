// Feature writer가 Transform을 명시적 바이너리 값으로 보존하는 공용 자료입니다.
// Unity 객체·부모 참조를 저장하지 않으며 적용 전에 유한 좌표와 회전을 검증합니다.
using System;
using System.IO;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public readonly struct CheckpointTransformState
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 LocalScale { get; }

        public CheckpointTransformState(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            if (!Finite(position) || !Finite(localScale) || !Finite(rotation.x) || !Finite(rotation.y) ||
                !Finite(rotation.z) || !Finite(rotation.w) || Math.Abs(Quaternion.Dot(rotation, rotation) - 1f) > 0.001f)
                throw new InvalidDataException("A saved Transform requires finite values and a normalized rotation.");
            Position = position; Rotation = rotation; LocalScale = localScale;
        }

        /// <summary>Feature 캡처 시 소유 Transform의 world pose와 local scale을 복사합니다.</summary>
        public static CheckpointTransformState Capture(Transform source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new CheckpointTransformState(source.position, source.rotation, source.localScale);
        }

        /// <summary>검증된 준비 단계가 같은 제작 계층의 대상에 적용합니다. Rigidbody 속도는 해당 Feature가 별도 복원합니다.</summary>
        public void Apply(Transform target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetPositionAndRotation(Position, Rotation);
            target.localScale = LocalScale;
        }

        public void Write(BinaryWriter writer)
        {
            WriteVector(writer, Position);
            writer.Write(Rotation.x); writer.Write(Rotation.y); writer.Write(Rotation.z); writer.Write(Rotation.w);
            WriteVector(writer, LocalScale);
        }

        public static CheckpointTransformState Read(BinaryReader reader) => new CheckpointTransformState(
            ReadVector(reader), new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), ReadVector(reader));
        private static void WriteVector(BinaryWriter writer, Vector3 value)
        { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
