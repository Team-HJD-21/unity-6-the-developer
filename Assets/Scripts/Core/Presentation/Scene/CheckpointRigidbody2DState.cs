// 실제 2D 물리 개체의 위치·속도·시뮬레이션·수면 상태를 값으로 보존합니다.
// 일시 정지 어댑터의 임시 simulated=false는 원래 상태와 구분해서 사용합니다.
using System;
using System.IO;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public readonly struct CheckpointRigidbody2DState
    {
        public Vector2 Position { get; }
        public float Rotation { get; }
        public Vector2 Velocity { get; }
        public float AngularVelocity { get; }
        public bool Simulated { get; }
        public bool Sleeping { get; }

        public CheckpointRigidbody2DState(Vector2 position, float rotation, Vector2 velocity,
            float angularVelocity, bool simulated, bool sleeping)
        {
            if (!Finite(position.x) || !Finite(position.y) || !Finite(rotation) ||
                !Finite(velocity.x) || !Finite(velocity.y) || !Finite(angularVelocity))
                throw new InvalidDataException("Saved Rigidbody2D values must be finite.");
            Position = position; Rotation = rotation; Velocity = velocity;
            AngularVelocity = angularVelocity; Simulated = simulated; Sleeping = sleeping;
        }

        /// <summary>정지 어댑터가 없는 Player 등에서 직접 캡처합니다. 필드 개체는 FieldSimulationActor.CaptureBody를 사용합니다.</summary>
        public static CheckpointRigidbody2DState Capture(Rigidbody2D body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return new CheckpointRigidbody2DState(body.position, body.rotation, body.linearVelocity,
                body.angularVelocity, body.simulated, body.IsSleeping());
        }

        /// <summary>정지 어댑터가 없는 소유 물리에 적용합니다. 필드 개체는 FieldSimulationActor.RestoreBody를 사용합니다.</summary>
        public void Apply(Rigidbody2D body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            body.simulated = Simulated;
            ApplyPose(body);
            body.linearVelocity = Velocity; body.angularVelocity = AngularVelocity;
            if (Sleeping) body.Sleep(); else body.WakeUp();
        }

        internal void ApplyPose(Rigidbody2D body)
        {
            // simulated=false에서는 재활성 시 Transform으로 물리가 재생성될 수 있으므로 두 원본을 함께 맞춥니다.
            Vector3 world = body.transform.position;
            body.transform.SetPositionAndRotation(new Vector3(Position.x, Position.y, world.z), Quaternion.Euler(0, 0, Rotation));
            body.position = Position; body.rotation = Rotation;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Position.x); writer.Write(Position.y); writer.Write(Rotation);
            writer.Write(Velocity.x); writer.Write(Velocity.y); writer.Write(AngularVelocity);
            writer.Write(Simulated); writer.Write(Sleeping);
        }

        public static CheckpointRigidbody2DState Read(BinaryReader reader) => new CheckpointRigidbody2DState(
            new Vector2(reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle(),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle(), reader.ReadBoolean(), reader.ReadBoolean());
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
