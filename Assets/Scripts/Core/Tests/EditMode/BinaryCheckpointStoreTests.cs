// 실제 파일의 바이너리 왕복·원자 교체·손상/버전/취소·저장 자료 불변성과 보존 정책을 검사합니다.
// 자동 저장 상황이나 실제 Scene Feature 복원은 실행 서비스의 별도 검사 범위입니다.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class BinaryCheckpointStoreTests
    {
        private string _directory, _testRoot;
        private readonly CheckpointSlot _slot = new CheckpointSlot("explicit-test-slot");
        private string FilePath => Path.Combine(_directory, _slot.Value + ".checkpoint");
        private static CheckpointData Data(byte value = 7) => new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow,
            new MatchId("captured-run"), new StageId("stage-1"), new DefinitionId("normal"), MatchMode.Story,
            42, "content-1", 123.456, 1, new[] { new CheckpointSection("world/시험", 1, CheckpointSectionKind.World, new[] { value }) });
        [SetUp] public void Setup()
        {
            _testRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Docs/Local/Validation/Sprint20261102/B4-Store/fixtures"));
            _directory = Path.GetFullPath(Path.Combine(_testRoot, Guid.NewGuid().ToString("N")));
        }
        [TearDown] public void Cleanup()
        {
            string resolved = Path.GetFullPath(_directory);
            if (!resolved.StartsWith(_testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to remove a directory outside this test fixture root.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
        // NUnit가 Editor 메인 스레드를 기다리는 동안 순수 IO continuation이 그 컨텍스트에 묶이지 않게 합니다.
        [Test] public async Task MissingSlotAndIndependentStoreInstanceUseActualDiskData()
        {
            var store = new BinaryCheckpointStore(_directory); Assert.That(await store.LoadAsync(_slot).ConfigureAwait(false), Is.Null);
            var data = Data(); await store.SaveAsync(_slot, data).ConfigureAwait(false);
            var loaded = await new BinaryCheckpointStore(_directory).LoadAsync(_slot).ConfigureAwait(false);
            Assert.That(loaded.CheckpointId, Is.EqualTo(data.CheckpointId)); Assert.That(loaded.StageId, Is.EqualTo(data.StageId));
            Assert.That(loaded.CapturedAtUtc, Is.EqualTo(data.CapturedAtUtc)); Assert.That(loaded.GameSeconds, Is.EqualTo(123.456));
            Assert.That(loaded.RandomSeed, Is.EqualTo(42)); Assert.That(loaded.Sections[0].CopyPayload(), Is.EqualTo(new byte[] { 7 }));
            Assert.That(loaded.Sections[0].Key, Is.EqualTo("world/시험"));
        }
        [Test] public async Task ReplacingAnExistingSlotLeavesOnlyOneCompleteFile()
        {
            var store = new BinaryCheckpointStore(_directory); var first = Data(1); var second = Data(2);
            await store.SaveAsync(_slot, first).ConfigureAwait(false); await store.SaveAsync(_slot, second).ConfigureAwait(false);
            Assert.That((await store.LoadAsync(_slot).ConfigureAwait(false)).CheckpointId, Is.EqualTo(second.CheckpointId));
            Assert.That(Directory.GetFiles(_directory).Select(Path.GetFileName), Is.EqualTo(new[] { _slot.Value + ".checkpoint" }));
        }
        [Test] public async Task CorruptionIsReportedWithoutDeletingOrRepairingTheFile()
        {
            var store = new BinaryCheckpointStore(_directory); await store.SaveAsync(_slot, Data()).ConfigureAwait(false);
            byte[] bytes = File.ReadAllBytes(FilePath); bytes[bytes.Length - 1] ^= 0x80; File.WriteAllBytes(FilePath, bytes);
            Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadAsync(_slot).ConfigureAwait(false));
            Assert.That(File.ReadAllBytes(FilePath), Is.EqualTo(bytes));
        }
        [Test] public async Task FutureVersionAndOversizedFilesAreRejectedBeforeDecodingPayloads()
        {
            var store = new BinaryCheckpointStore(_directory); await store.SaveAsync(_slot, Data()).ConfigureAwait(false);
            byte[] bytes = File.ReadAllBytes(FilePath); bytes[4] = 2; File.WriteAllBytes(FilePath, bytes);
            Assert.ThrowsAsync<NotSupportedException>(async () => await store.LoadAsync(_slot).ConfigureAwait(false));
            using (var file = new FileStream(FilePath, FileMode.Create)) file.SetLength(CheckpointData.MaximumPayloadBytes + 43L);
            Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadAsync(_slot).ConfigureAwait(false));
        }
        [Test] public async Task CancelledReplacementPreservesPreviousCommittedData()
        {
            var store = new BinaryCheckpointStore(_directory); var previous = Data(); await store.SaveAsync(_slot, previous).ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await store.SaveAsync(_slot, Data(2), cancellation.Token).ConfigureAwait(false));
            Assert.That((await store.LoadAsync(_slot).ConfigureAwait(false)).CheckpointId, Is.EqualTo(previous.CheckpointId));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }
        [Test] public async Task FailedFileReplacementRetainsPreviousCommitAndRemovesTemporaryFile()
        {
            var store = new BinaryCheckpointStore(_directory); var previous = Data(); await store.SaveAsync(_slot, previous).ConfigureAwait(false);
            using (var locked = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.ThrowsAsync<IOException>(async () => await store.SaveAsync(_slot, Data(2)).ConfigureAwait(false));
            Assert.That((await store.LoadAsync(_slot).ConfigureAwait(false)).CheckpointId, Is.EqualTo(previous.CheckpointId));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }
        [Test] public void InvalidSlotDuplicateSectionsAndMutableInputCannotEscapeTheContract()
        {
            Assert.Throws<ArgumentException>(() => new CheckpointSlot("../escape"));
            Assert.Throws<ArgumentException>(() => new CheckpointSlot("CON"));
            Assert.Throws<ArgumentException>(() => new CheckpointSlot("lpt1"));
            Assert.That(new CheckpointSlot("Save_1").Equals(new CheckpointSlot("save_1")), Is.True);
            Assert.Throws<ArgumentException>(() => new BinaryCheckpointStore(_directory).LoadAsync(default));
            var bytes = new byte[] { 1 }; var section = new CheckpointSection("world", 1, CheckpointSectionKind.World, bytes);
            bytes[0] = 9; var copy = section.CopyPayload(); copy[0] = 8;
            Assert.That(section.CopyPayload()[0], Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, new MatchId("run"),
                new StageId("stage"), new DefinitionId("normal"), MatchMode.Story, 1, "content", 0, 1, new[] { section, section }));
        }
        [Test] public void RestorePolicyAlwaysKeepsProgressionAndCurrencyRollbackIsExplicit()
        {
            Assert.That(CheckpointRestorePolicy.Default.ShouldRestore(CheckpointSectionKind.World), Is.True);
            Assert.That(CheckpointRestorePolicy.Default.ShouldRestore(CheckpointSectionKind.Currency), Is.False);
            var rollback = new CheckpointRestorePolicy(CurrencyRestorePolicy.RestoreCheckpoint);
            Assert.That(rollback.ShouldRestore(CheckpointSectionKind.Currency), Is.True);
            Assert.That(rollback.ShouldRestore(CheckpointSectionKind.Progression), Is.False);
        }
    }
}
