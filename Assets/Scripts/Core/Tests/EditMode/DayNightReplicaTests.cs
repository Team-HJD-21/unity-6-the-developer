// Client 낮밤 표시가 기존 공용 시계의 정지·배속·복원을 따르는지 검사합니다.
// 기준 미도착·과거 수신·같은 단계 Reset을 확인하며 Weather 추첨은 실행하지 않습니다.
using System;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class DayNightReplicaTests
    {
        private sealed class Timestamp : ITimestampSource
        {
            public double Now;
            public bool TryReadSeconds(out double seconds) { seconds = Now; return true; }
        }
        private readonly MatchId _match = new MatchId("day-night-replica");
        private GameObject _go;
        private MatchDayNightCycle _cycle;
        private MatchClock _host;
        private MatchTimeReplica _client;
        private Timestamp _stamp;
        private Guid _descriptor;
        private IDisposable _binding;
        private int _advanced;

        [Test]
        public void ReenableDiscardsIncompleteAuthorityBindingAndAllowsTheCurrentMatch()
        {
            _binding.Dispose();
            _go.SetActive(false);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // 재로드 중 delegate/시계가 사라지고 플래그만 남았던 실제 OnEnable 실패 조건입니다.
            typeof(MatchDayNightCycle).GetField("_bound", flags).SetValue(_cycle, true);
            typeof(MatchDayNightCycle).GetField("_authority", flags).SetValue(_cycle, true);
            _go.SetActive(true);
            // 일반 MonoBehaviour의 Player 콜백은 EditMode에서 자동 실행되지 않습니다.
            typeof(MatchDayNightCycle).GetMethod("OnEnable", flags).Invoke(_cycle, null);
            Assert.That(_cycle.IsAuthority, Is.False);
            using var current = _cycle.BindAuthority(_match, _host, 1, () => true, default);
            Assert.That(_cycle.CanRestartCurrentPhase, Is.True);
        }

        [SetUp]
        public void SetUp()
        {
            _advanced = 0;
            _go = new GameObject("Day night replica");
            _cycle = _go.AddComponent<MatchDayNightCycle>();
            _binding = _cycle.BindReplica(_match);
            _cycle.PhaseAdvanced += () => _advanced++;
            _descriptor = Guid.NewGuid();
            _stamp = new Timestamp();
            _host = new MatchClock(_match, _stamp);
            _client = new MatchTimeReplica(_match, _descriptor, _stamp);
        }

        [TearDown]
        public void TearDown()
        {
            _binding.Dispose();
            _client.Dispose();
            _host.Dispose();
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void MissingAnchorClockAndLegacyStateRemainUnavailable()
        {
            Assert.That(double.IsNaN(_cycle.RemainingSeconds), Is.True);
            _cycle.ApplyReplica(Snapshot(1, null));
            Assert.That(double.IsNaN(_cycle.RemainingSeconds), Is.True);
            BindClock();
            SyncClock();
            Assert.That(double.IsNaN(_cycle.RemainingSeconds), Is.True);
            _cycle.ApplyReplica(Snapshot(2, new DayNightCycle()));
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(300));
        }

        [Test]
        public void SharedClockPauseRateAndBoundaryDriveDisplayWithoutAdvancingWeather()
        {
            BindClock();
            _stamp.Now = 100;
            _host.Pump();
            SyncClock();
            _cycle.ApplyReplica(Snapshot(1, new DayNightCycle()));
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(200));
            _host.SetPaused(true);
            SyncClock();
            _stamp.Now = 130;
            _host.Pump(); _client.Pump();
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(200));
            _host.SetPaused(false); _host.SetRate(2);
            SyncClock();
            _stamp.Now = 140;
            _host.Pump(); _client.Pump();
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(180));
            _stamp.Now = 230;
            _host.Pump(); _client.Pump();
            Assert.That(_cycle.RemainingSeconds, Is.Zero, "확정 전 다음 밤을 먼저 표시하지 않습니다.");
            _cycle.ApplyReplica(Snapshot(2, new DayNightCycle(300, 300, 300, DayNightPhase.Night)));
            Assert.That(_cycle.Phase, Is.EqualTo(DayNightPhase.Night));
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(300));
            Assert.That(_advanced, Is.Zero);
        }

        [Test]
        public void RestoreWaitsForMatchingEpochAndRejectsOldRevision()
        {
            BindClock(); SyncClock();
            _cycle.ApplyReplica(Snapshot(1, new DayNightCycle()));
            _host.Restore(10); SyncClock();
            Assert.That(double.IsNaN(_cycle.RemainingSeconds), Is.True);
            _cycle.ApplyReplica(Snapshot(2, new DayNightCycle()));
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(290));
            _cycle.ApplyReplica(new MatchEnvironmentSnapshot(_match, 1, 10, default,
                new DefinitionId("night"), null, _host.Snapshot.Epoch, new DayNightCycle(17, 31, 0, DayNightPhase.Night)));
            Assert.That(_cycle.Phase, Is.EqualTo(DayNightPhase.Day));
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(290));
            _binding.Dispose();
            Assert.That(double.IsNaN(_cycle.RemainingSeconds), Is.True);
        }

        [Test]
        public void SamePhaseResetPublishesNewAnchorAndKeepsExistingUpdateSignature()
        {
            using var projection = new MatchEnvironmentRuntime(_match, _host, () => true);
            Assert.That(projection.TryUpdate(default, new DefinitionId("day"), null, new DayNightCycle()), Is.True);
            long before = projection.Snapshot.Revision;
            _stamp.Now = 100; _host.Pump();
            Assert.That(projection.TryUpdate(default, new DefinitionId("day"), null, new DayNightCycle(300, 300, 100)), Is.True);
            Assert.That(projection.Snapshot.Revision, Is.EqualTo(before + 1));
            BindClock(); SyncClock();
            _cycle.ApplyReplica(projection.Snapshot);
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(300));
            Func<DefinitionId, DefinitionId, System.Collections.Generic.IEnumerable<DefinitionId>, bool> oldCall = projection.TryUpdate;
            Assert.That(oldCall(default, new DefinitionId("day"), null), Is.True);
            Assert.That(projection.Snapshot.PhaseAnchor, Is.Null);
        }

        [Test]
        public void ActualAuthorityNaturalBoundaryPublishesOnlyItsConfirmedPhase()
        {
            var authorityObject = new GameObject("Authority day night");
            try
            {
                var authority = authorityObject.AddComponent<MatchDayNightCycle>();
                using var lease = authority.BindAuthority(_match, _host, 1, () => true, default);
                int transitions = 0;
                authority.PhaseAdvanced += () => transitions++;
                _stamp.Now = 301; _host.Pump();
                typeof(MatchDayNightCycle).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(authority, null);
                var anchor = (DayNightCycle)typeof(MatchDayNightCycle)
                    .GetMethod("CapturePhaseAnchor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(authority, null);
                Assert.That(anchor.EpochSeconds, Is.EqualTo(300));
                BindClock(); _cycle.ApplyReplica(Snapshot(1, anchor));
                Assert.That(_cycle.Phase, Is.EqualTo(DayNightPhase.Night));
                Assert.That(_cycle.RemainingSeconds, Is.EqualTo(299));
                Assert.That(transitions, Is.EqualTo(1));
                Assert.That(_advanced, Is.Zero);
            }
            finally { Object.DestroyImmediate(authorityObject); }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ExistingCheckpointVersionsRestoreWithoutAdvancingWeather(int version)
        {
            _binding.Dispose();
            _binding = _cycle.BindAuthority(_match, _host, 1, () => true, default);
            var codec = new BinaryCheckpointPayloadCodec();
            var config = new MatchConfig(_match, MatchMode.Story, new StageId("initial-stage"),
                new DefinitionId("normal"), "poc", 1,
                new[] { new PlayerLoadoutSnapshot(new PlayerId("local"), null) });
            double savedTime;
            CheckpointSection section;
            if (version == 1)
            {
                savedTime = 310;
                section = codec.Write(_cycle.Key, 1, _cycle.Kind,
                    writer => { writer.Write(300d); writer.Write(300d); writer.Write(0d); });
            }
            else
            {
                _stamp.Now = 350; _host.Pump();
                Assert.That(_cycle.TryAdvancePhase(), Is.True);
                savedTime = _host.ElapsedSeconds;
                section = _cycle.Capture(new CheckpointCaptureContext(config, savedTime, _host.Snapshot.Epoch), codec);
                Assert.That(section.Version, Is.EqualTo(2));
                _stamp.Now = 360; _host.Pump();
            }
            var target = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, _match, config.StageId,
                config.DifficultyProfileId, config.Mode, 1, config.ContentVersion, savedTime, 1, new[] { section });
            var context = new CheckpointRestoreContext(new CheckpointCaptureContext(config, _host.ElapsedSeconds,
                _host.Snapshot.Epoch), target, CheckpointRestorePolicy.Default);
            using var restore = _cycle.PrepareRestore(context, section, codec);
            _host.Restore(savedTime);
            restore.Apply();
            Assert.That(_cycle.Phase, Is.EqualTo(DayNightPhase.Night));
            Assert.That(_cycle.RemainingSeconds, Is.EqualTo(version == 1 ? 290 : 300));
            Assert.That(_advanced, Is.EqualTo(version == 1 ? 0 : 1), "복원은 새 Weather 전환을 발생시키지 않습니다.");
        }

        private MatchEnvironmentSnapshot Snapshot(long revision, DayNightCycle anchor) =>
            new MatchEnvironmentSnapshot(_match, revision, _host.ElapsedSeconds, default,
                new DefinitionId(anchor?.InitialPhase == DayNightPhase.Night ? "night" : "day"), null,
                _host.Snapshot.Epoch, anchor);

        private void BindClock()
        {
            // 실제 Scene도 첫 clock snapshot 수신 뒤에만 읽기 시계를 공개합니다.
            SyncClock();
            typeof(MatchDayNightCycle).GetMethod("SetReplicaClock", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_cycle, new object[] { _client });
        }

        private void SyncClock()
        {
            _client.TryApply(_descriptor, _host.Snapshot, true);
            _client.Pump();
        }
    }
}
