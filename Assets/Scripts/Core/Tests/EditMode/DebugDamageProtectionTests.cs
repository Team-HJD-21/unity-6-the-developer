// 검증 표식이 붙은 객체와 자식의 피해만 막고 다른 객체/행동에는 영향을 주지 않는지 확인합니다.
using NUnit.Framework;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class DebugDamageProtectionTests
    {
        [Test]
        public void ProtectionIsExplicitAndDoesNotPauseSimulation()
        {
            var actor = new GameObject("Protected QA actor");
            var other = new GameObject("Unprotected actor");
            var child = new GameObject("Actor child");
            child.transform.SetParent(actor.transform);
            try
            {
                var marker = actor.AddComponent<DebugDamageProtection>();
                Assert.That(FieldSimulationTime.CanReceiveDamage(actor.transform), Is.False);
                Assert.That(FieldSimulationTime.CanReceiveDamage(child.transform), Is.False);
                Assert.That(FieldSimulationTime.CanReceiveDamage(other.transform), Is.True);
                Assert.That(FieldSimulationTime.CanAdvance(actor.transform), Is.True);
                marker.enabled = false;
                Assert.That(FieldSimulationTime.CanReceiveDamage(actor.transform), Is.True);
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(other); }
        }
    }
}
