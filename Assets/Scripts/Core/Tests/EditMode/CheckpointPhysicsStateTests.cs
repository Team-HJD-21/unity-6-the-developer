// 체크포인트의 실제 물리 값이 필드 정지·재개에 덮어쓰이지 않는지 검증합니다.
// 물리 엔진 적분이나 전체 전투 재생을 대신하는 검사는 아닙니다.
using System;
using System.IO;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CheckpointPhysicsStateTests
    {
        private sealed class Timestamp : ITimestampSource
        { public bool TryReadSeconds(out double value) { value = 0; return true; } }

        [Test]
        public void HeldPhysicsCaptureAndRestoreUsesActualValuesWhenResumed()
        {
            var actor = new GameObject("field-physics-checkpoint");
            using var clock = new MatchClock(new MatchId("match"), new Timestamp());
            try
            {
                var body = actor.AddComponent<Rigidbody2D>();
                body.gravityScale = 0; body.position = new Vector2(4, 3);
                body.linearVelocity = new Vector2(7, -2); body.angularVelocity = 13;
                var binding = FieldSimulationActor.Attach(actor, clock);
                clock.SetPaused(true); binding.Synchronize();
                var saved = binding.CaptureBody(body);
                Assert.That(body.simulated, Is.False);
                Assert.That(saved.Simulated, Is.True);
                Assert.That(saved.Velocity, Is.EqualTo(new Vector2(7, -2)));
                var codec = new BinaryCheckpointPayloadCodec();
                var section = codec.Write("physics", 1, CheckpointSectionKind.World, saved.Write);
                var target = codec.Read(section, 1, CheckpointRigidbody2DState.Read);
                body.position = Vector2.zero;
                binding.RestoreBody(body, new CheckpointRigidbody2DState(new Vector2(11, 12), 45, new Vector2(-3, 5), 21, true, false));
                Assert.That(body.simulated, Is.False);
                Assert.That(binding.CaptureBody(body).Velocity, Is.EqualTo(new Vector2(-3, 5)));
                clock.SetPaused(false); binding.Synchronize();
                Assert.That(body.position, Is.EqualTo(new Vector2(11, 12)));
                Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(-3, 5)));
                Assert.That(body.angularVelocity, Is.EqualTo(21));
                Assert.That(body.simulated, Is.True);
                binding.RestoreBody(body, target);
                Assert.That(body.position, Is.EqualTo(new Vector2(4, 3)));
                Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(7, -2)));
            }
            finally { Object.DestroyImmediate(actor); }
        }

        [Test]
        public void OriginalSimulationDisabledAndSleepStateRemainExplicit()
        {
            var actor = new GameObject("disabled-body-checkpoint");
            using var clock = new MatchClock(new MatchId("match"), new Timestamp());
            try
            {
                var body = actor.AddComponent<Rigidbody2D>(); body.simulated = false;
                var binding = FieldSimulationActor.Attach(actor, clock);
                clock.SetPaused(true);
                var captured = binding.CaptureBody(body);
                Assert.That(captured.Simulated, Is.False);
                binding.RestoreBody(body, captured);
                clock.SetPaused(false); binding.Synchronize();
                Assert.That(body.simulated, Is.False);
            }
            finally { Object.DestroyImmediate(actor); }
        }

        [Test]
        public void OtherActorAndInvalidValuesAreRejected()
        {
            var owner = new GameObject("owner"); var other = new GameObject("other");
            using var clock = new MatchClock(new MatchId("match"), new Timestamp());
            try
            {
                var binding = FieldSimulationActor.Attach(owner, clock);
                var body = other.AddComponent<Rigidbody2D>();
                Assert.Throws<InvalidOperationException>(() => binding.CaptureBody(body));
                Assert.Throws<InvalidDataException>(() => new CheckpointRigidbody2DState(Vector2.zero, float.NaN, Vector2.zero, 0, true, false));
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(other); }
        }
    }
}
