// 짧은 Editor 표시가 실제 개체 키를 바꾸지 않는지 검사합니다.
using NUnit.Framework;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class EntityLabelFormatterTests
    {
        [Test]
        public void GuidTurretLabelIsShortButTheSourceKeyIsUnchanged()
        {
            const string key = "turret/0123456789abcdef0123456789abcdef";
            var id = new EntityId(key);
            Assert.That(EntityLabelFormatter.Format(id), Is.EqualTo("T:01234567"));
            Assert.That(id.Value, Is.EqualTo(key));
        }
        [Test]
        public void ExistingShortLabelsRemainReadable()
            => Assert.That(EntityLabelFormatter.Format(new EntityId("turret-7")), Is.EqualTo("turret-7"));
        [Test]
        public void EmptyAndLongCustomIdentifiersHaveBoundedDisplay()
        {
            Assert.That(EntityLabelFormatter.Format(default), Is.EqualTo("—"));
            Assert.That(EntityLabelFormatter.Format(new EntityId(new string('a', 80))).Length, Is.EqualTo(21));
        }
    }
}
