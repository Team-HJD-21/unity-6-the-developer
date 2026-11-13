// 시간/중복/가림/전역 연결 경계를 검증합니다. 실제 게임 상태를 변경하지 않습니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class AlertServiceTests
    {
        [Test]
        public void RepeatedInputDoesNotExtendTheCurrentAlert()
        {
            using var alerts = new AlertService();
            Assert.That(alerts.Show("스킬을 장착하세요.", key: "skill/0"), Is.True);
            alerts.Tick(10);
            Assert.That(alerts.Show("다른 문구", key: "skill/0"), Is.False);
            alerts.Tick(12.19); Assert.That(alerts.Current, Is.Not.Null);
            alerts.Tick(12.21); Assert.That(alerts.Current, Is.Null);
        }

        [Test]
        public void QueueIsBoundedAndPromotesOnlyOneMessageAtATime()
        {
            using var alerts = new AlertService();
            alerts.Show("첫 결과"); alerts.Tick(0);
            for (int i = 0; i < AlertService.MaximumPending; i++) Assert.That(alerts.Show("대기 " + i), Is.True);
            Assert.That(alerts.Show("넘침"), Is.False);
            Assert.That(alerts.Show("대기 1"), Is.False);
            alerts.Tick(2.3);
            Assert.That(alerts.Current.Text, Is.EqualTo("대기 0"));
            Assert.That(alerts.CurrentAge, Is.Zero);
            Assert.That(alerts.PendingCount, Is.EqualTo(2));
        }

        [Test]
        public void FullScreenTransitionDropsVisibleAndPendingFeedback()
        {
            using var alerts = new AlertService();
            alerts.Show("이전 Scene"); alerts.Tick(0); alerts.Show("이전 대기");
            alerts.Tick(1, suppressed: true);
            Assert.That(alerts.Current, Is.Null); Assert.That(alerts.PendingCount, Is.Zero);
            Assert.That(alerts.Show("가림 중 결과"), Is.False);
            alerts.Tick(2);
            Assert.That(alerts.Current, Is.Null);
            Assert.That(alerts.Show("새 Scene"), Is.True);
            alerts.Tick(2.1); Assert.That(alerts.Current.Text, Is.EqualTo("새 Scene"));
        }

        [Test]
        public void OldAppUnbindCannotRemoveTheNewAppBinding()
        {
            using var first = new AlertService(); using var second = new AlertService();
            using var oldBinding = GameAlerts.Bind(first);
            using var nextBinding = GameAlerts.Bind(second);
            oldBinding.Dispose();
            Assert.That(GameAlerts.Show("현재 App"), Is.True);
            Assert.That(first.PendingCount, Is.Zero); Assert.That(second.PendingCount, Is.EqualTo(1));
            nextBinding.Dispose(); Assert.That(GameAlerts.Show("종료 뒤"), Is.False);
        }

        [Test]
        public void InvalidInputAndDisposedServiceDoNotEmit()
        {
            using var alerts = new AlertService();
            Assert.That(alerts.Show("  "), Is.False);
            Assert.That(alerts.Show("bad", (AlertTone)99), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => alerts.Tick(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => alerts.Tick(0, 0));
            alerts.Show("결과"); alerts.Tick(1); alerts.Dispose();
            Assert.That(alerts.Current, Is.Null); Assert.That(alerts.Show("끝"), Is.False);
        }
    }
}
