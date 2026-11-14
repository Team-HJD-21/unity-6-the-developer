using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerAutomaticRecoveryTests
{
    private GameObject _actor;
    private PlayerInfo _health;
    private Clock _clock;
    private CancellationTokenSource _lifetime;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp]
    public void SetUp()
    {
        _actor=new GameObject("Automatic health recovery"); _health=_actor.AddComponent<PlayerInfo>();
        typeof(PlayerInfo).GetMethod("Awake",Private).Invoke(_health,null);
        _clock=new Clock(); _lifetime=new CancellationTokenSource();
        FieldSimulationActor.Attach(_actor,_clock,_lifetime.Token,false);
        _actor.AddComponent<PlayerActorIdentity>().Bind(new PlayerId("recovery-player"),0);
    }
    [TearDown]
    public void TearDown() { Object.DestroyImmediate(_actor); _lifetime.Dispose(); }
    private void Tick(double seconds)
    { _clock.Now=seconds; typeof(PlayerInfo).GetMethod("FixedUpdate",Private).Invoke(_health,null); }
    [Test]
    public void WaitsFiveSecondsThenRecoversTwoPercentPerSecondAndNotifiesExistingHud()
    {
        int damageEvents=0, shownHealth=0; _health.Damaged+=_=>damageEvents++;
        _health.onHpChange.AddListener((current,maximum)=>shownHealth=current);
        Assert.That(_health.ApplyDamage(100),Is.True);
        Tick(4.99); Assert.That(_health.curHp,Is.EqualTo(300));
        Tick(5); Assert.That(_health.curHp,Is.EqualTo(300));
        Tick(6); Assert.That(_health.curHp,Is.EqualTo(308));Assert.That(shownHealth,Is.EqualTo(308));
        Tick(7); Assert.That(_health.curHp,Is.EqualTo(316));Assert.That(damageEvents,Is.EqualTo(1));
    }
    [Test]
    public void ActualDamageRestartsWaitAndDiscardsFractionButRejectedDamageDoesNot()
    {
        _health.ApplyDamage(100);Tick(5.0625); // half a hit point has accumulated.
        Assert.That(_health.curHp,Is.EqualTo(300)); _health.ApplyDamage(10);
        Tick(10.0625);Assert.That(_health.curHp,Is.EqualTo(290));
        Assert.That(_health.ApplyDamage(0),Is.False);
        Tick(10.125);Assert.That(_health.curHp,Is.EqualTo(290));
        Tick(10.1875);Assert.That(_health.curHp,Is.EqualTo(291));
    }
    [TestCase(50,1)] [TestCase(400,8)] [TestCase(1000,20)]
    public void RecoveryScalesWithMaximumHealth(int maximum,int expected)
    {
        _health.maxHp=_health.curHp=maximum;_health.ApplyDamage(maximum/2);Tick(6);
        Assert.That(_health.curHp,Is.EqualTo(maximum-maximum/2+expected));
    }
    [Test]
    public void SmallHealthValuesRetainFractionAndLargeTimeStepsStopAtFullHealth()
    {
        _health.maxHp=_health.curHp=10;_health.ApplyDamage(5);
        for(int i=1;i<=9;i++)Tick(i);
        Assert.That(_health.curHp,Is.EqualTo(5));Tick(10);Assert.That(_health.curHp,Is.EqualTo(6));
        Tick(1000);Assert.That(_health.curHp,Is.EqualTo(10));
    }
    [Test]
    public void PauseUnavailableClockAndMatchCompletionDoNotWriteHealth()
    {
        _health.ApplyDamage(100);Tick(4);_clock.Paused=true;
        for(int i=0;i<3;i++)Tick(4);
        Assert.That(_health.curHp,Is.EqualTo(300));_clock.Paused=false;Tick(6);Assert.That(_health.curHp,Is.EqualTo(308));
        _clock.Available=false;Tick(7);Assert.That(_health.curHp,Is.EqualTo(308));
        _clock.Available=true;_lifetime.Cancel();Tick(20);Assert.That(_health.curHp,Is.EqualTo(308));
    }
    [Test]
    public void DeathAndNonAuthorityNeverAutomaticallyRecover()
    {
        _health.ApplyDamage(20);
        typeof(PlayerInfo).GetField("_matchBound",Private).SetValue(_health,true);
        typeof(PlayerInfo).GetField("_healthAuthority",Private).SetValue(_health,false);
        Tick(20);Assert.That(_health.curHp,Is.EqualTo(380));
        typeof(PlayerInfo).GetField("_healthAuthority",Private).SetValue(_health,true);
        Assert.That(_health.ApplyDamage(400),Is.True);Tick(100);Assert.That(_health.curHp,Is.Zero);
    }
    private CheckpointCaptureContext Context()=>new(new MatchConfig(new MatchId("recovery-test"),MatchMode.Story,new StageId("stage"),
        new DefinitionId("normal"),"test",1,new[]{new PlayerLoadoutSnapshot(new PlayerId("recovery-player"),null)}),_clock.Now,Guid.NewGuid());
    private static CheckpointRestoreContext Restore(CheckpointCaptureContext current,CheckpointSection section)=>new(current,
        new CheckpointData(Guid.NewGuid(),DateTimeOffset.UtcNow,current.Config.MatchId,current.Config.StageId,current.Config.DifficultyProfileId,
            current.Config.Mode,current.Config.RandomSeed,current.Config.ContentVersion,current.GameSeconds,1,new[]{section}),CheckpointRestorePolicy.Default);
    [Test]
    public void CheckpointRestoresRemainingWaitAndRollbackRestoresPartialProgressWithoutDamageEvents()
    {
        var codec=new BinaryCheckpointPayloadCodec();var writer=(ICheckpointParticipant)_health;
        _health.ApplyDamage(100);Tick(2);var saved=writer.Capture(Context(),codec);
        Tick(5.0625);Assert.That(_health.curHp,Is.EqualTo(300));
        using(var step=writer.PrepareRestore(Restore(Context(),saved),saved,codec))
        {
            int damage=0;_health.Damaged+=_=>damage++;step.Apply();
            Tick(8.0625);Assert.That(_health.curHp,Is.EqualTo(300));
            Tick(9.0625);Assert.That(_health.curHp,Is.EqualTo(308));Assert.That(damage,Is.Zero);
            step.Rollback();Tick(9.125);Assert.That(_health.curHp,Is.EqualTo(301),"Rollback preserves the half point in its backup.");
        }
    }
    [Test]
    public void VersionOneInjuredActorStartsFreshDelayAndCorruptAutomaticStateIsRejectedBeforeApply()
    {
        var codec=new BinaryCheckpointPayloadCodec();var writer=(ICheckpointParticipant)_health;
        _health.ApplyDamage(100);var current=writer.Capture(Context(),codec);
        // v2 appends a bool, two doubles and two config floats to the unchanged v1 payload.
        var legacy=codec.Write(current.Key,1,CheckpointSectionKind.World,w=>w.Write(current.CopyPayload(),0,current.PayloadLength-25));
        using(var step=writer.PrepareRestore(Restore(Context(),legacy),legacy,codec))
        { step.Apply();Tick(5);Assert.That(_health.curHp,Is.EqualTo(300));Tick(6);Assert.That(_health.curHp,Is.EqualTo(308)); }
        var bytes=current.CopyPayload(); // corrupt the stored recovery delay to NaN.
        Buffer.BlockCopy(BitConverter.GetBytes(double.NaN),0,bytes,bytes.Length-24,8);
        var invalid=codec.Write(current.Key,2,CheckpointSectionKind.World,w=>w.Write(bytes));
        Assert.Throws<InvalidDataException>(()=>writer.PrepareRestore(Restore(Context(),invalid),invalid,codec));
        Assert.That(_health.curHp,Is.EqualTo(308));
    }
    internal sealed class Clock:IMatchTime
    {
        public double Now;public bool Paused;public bool Available=true;private readonly Guid _epoch=Guid.NewGuid();
        public bool IsAvailable=>Available;public double ElapsedSeconds=>Now;
        public MatchTimeSnapshot Snapshot=>new(new MatchId("recovery-test"),_epoch,0,0,0,Paused?0:1,Paused,true);
    }
}
