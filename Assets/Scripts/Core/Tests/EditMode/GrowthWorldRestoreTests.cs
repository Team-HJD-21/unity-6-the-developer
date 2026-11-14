// 성장을 보존하는 월드 복원의 HP 비율과 캠페인 식별 경계를 검사합니다.
using System;
using System.IO;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Infrastructure;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GrowthWorldRestoreTests
    {
        [TestCase(50,100,200,false,100)]
        [TestCase(1,100,20,false,1)]
        [TestCase(0,100,200,true,0)]
        [TestCase(100,100,90,false,90)]
        public void HealthRatioPreservesCombatDamageAndDestruction(int hp,int previous,int current,bool destroyed,int expected)
            => Assert.That(FieldTurretController.RestoreHealthRatio(hp,previous,current,destroyed),Is.EqualTo(expected));
        [Test]
        public void InvalidHealthCannotBeMadeIntoAValidGrowthRestore()
            => Assert.Throws<ArgumentOutOfRangeException>(()=>FieldTurretController.RestoreHealthRatio(101,100,200,false));
        [Test]
        public void CampaignReferenceIsValidatedBeforeRestoringItsWorld()
        {
            var codec=new BinaryCheckpointPayloadCodec();var run=Guid.NewGuid();var writer=new CampaignCheckpointParticipant(new CheckpointSlot("campaign-a"),run);
            var c=new MatchConfig(new MatchId("growth"),MatchMode.Story,new StageId("stage-1"),new DefinitionId("normal"),"v1",1,
                new[]{new PlayerLoadoutSnapshot(new PlayerId("host"),null)});
            var capture=new CheckpointCaptureContext(c,10,Guid.NewGuid());var section=writer.Capture(capture,codec);
            var data=new CheckpointData(Guid.NewGuid(),DateTimeOffset.UtcNow,c.MatchId,c.StageId,c.DifficultyProfileId,c.Mode,1,"v1",10,1,new[]{section});
            Assert.That(CampaignCheckpointParticipant.Read(data,codec),Is.EqualTo(new CheckpointSlot("campaign-a")));
            Assert.That(CampaignCheckpointParticipant.ReadRun(data,codec),Is.EqualTo(run));
            var other=new CampaignCheckpointParticipant(new CheckpointSlot("campaign-b"),run);
            Assert.Throws<InvalidDataException>(()=>other.PrepareRestore(new CheckpointRestoreContext(capture,data,CheckpointRestorePolicy.Default),section,codec));
        }
    }
}
