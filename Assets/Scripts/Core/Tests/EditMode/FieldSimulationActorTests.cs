// 필드 정지가 물리·애니메이션을 보존하고 취소/시계 폐기 뒤에도 안전한지 확인합니다.
using System;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class FieldSimulationActorTests
    {
        private sealed class Source : ITimestampSource
        { public double Now; public bool TryReadSeconds(out double seconds) { seconds = Now; return true; } }
        [Test]
        public void PauseRestoresPhysicalAndAnimationStateWithoutEnablingDisabledComponents()
        {
            using var clock = new MatchClock(new MatchId("field"), new Source());
            var actor = new GameObject("field");
            try
            {
                var body = actor.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.linearVelocity = Vector2.right * 3;
                var animator = actor.AddComponent<Animator>(); animator.enabled = false; animator.speed = .5f;
                var binding = FieldSimulationActor.Attach(actor, clock);
                clock.SetPaused(true); binding.Synchronize();
                Assert.That(body.simulated, Is.False); Assert.That(animator.speed, Is.Zero);
                clock.SetPaused(false); binding.Synchronize();
                Assert.That(body.simulated, Is.True); Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 3));
                Assert.That(animator.speed, Is.EqualTo(.5f)); Assert.That(animator.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(actor); }
        }
        [Test]
        public void CancellationAndDisposedClockCannotResumeOrThrowFromFieldClockReads()
        {
            var source = new Source(); var clock = new MatchClock(new MatchId("field"), source);
            using var lifetime = new CancellationTokenSource(); var actor = new GameObject("field");
            try
            {
                var binding = FieldSimulationActor.Attach(actor, clock, lifetime.Token);
                source.Now = 10; clock.Pump(); Assert.That(binding.Seconds, Is.EqualTo(10));
                lifetime.Cancel(); clock.Dispose(); binding.Synchronize();
                Assert.That(binding.CanAdvance, Is.False); Assert.That(binding.Seconds, Is.EqualTo(10));
            }
            finally { Object.DestroyImmediate(actor); clock.Dispose(); }
        }
        [Test]
        public void ActorCannotBeStolenByAnotherClockAndUnbindRestoresOriginallyDisabledPhysics()
        {
            using var first = new MatchClock(new MatchId("first"), new Source());
            using var second = new MatchClock(new MatchId("second"), new Source());
            var actor = new GameObject("field");
            try
            {
                var body = actor.AddComponent<Rigidbody2D>(); body.simulated = false;
                var binding = FieldSimulationActor.Attach(actor, first);
                first.SetPaused(true); binding.Synchronize();
                Assert.Throws<InvalidOperationException>(() => FieldSimulationActor.Attach(actor, second));
                binding.Unbind(first); Assert.That(body.simulated, Is.False);
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }
}
