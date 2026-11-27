using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class Stage1TokenProgressTests
    {
        private static readonly CheckpointSlot Campaign = new("campaign-1");

        [Test]
        public void FirstBossAndArmorTransactionsRemainUniqueAfterReload()
        {
            var store = new MemoryStore();
            var progress = Progress(store, "host");
            Assert.That(Complete(progress.BuyCuArmorAsync(Campaign)), Is.False);
            Assert.That(store.Writes, Is.Zero);

            var grants = Complete(Task.WhenAll(progress.GrantFirstBossRewardAsync(), progress.GrantFirstBossRewardAsync()));
            Assert.That(grants.Count(granted => granted), Is.EqualTo(1));
            Assert.That(progress.Balance, Is.EqualTo(Stage1TokenProgress.FirstBossReward));

            var purchases = Complete(Task.WhenAll(progress.BuyCuArmorAsync(Campaign), progress.BuyCuArmorAsync(Campaign)));
            Assert.That(purchases.Count(purchased => purchased), Is.EqualTo(1));
            Assert.That(store.Writes, Is.EqualTo(2));

            var reopened = Progress(store, "host");
            reopened.LoadAsync().GetAwaiter().GetResult();
            Assert.That(reopened.HasCuArmor(Campaign) && reopened.FirstBossRewardReceived, Is.True);
            Assert.That(reopened.HasCuArmor(new CheckpointSlot("campaign-2")), Is.False);
            Assert.That(reopened.Balance, Is.Zero);
            Assert.That(Complete(reopened.GrantFirstBossRewardAsync()), Is.False);
            Assert.That(Complete(reopened.BuyCuArmorAsync(Campaign)), Is.False);
            Assert.That(store.Writes, Is.EqualTo(2));

            var other = Progress(store, "guest");
            other.LoadAsync().GetAwaiter().GetResult();
            Assert.That(other.Balance, Is.Zero);
            Assert.That(other.HasCuArmor(Campaign) || other.FirstBossRewardReceived, Is.False);
        }

        [Test]
        public void FailedPurchasePreservesThePreviousBalanceAndCanRetry()
        {
            var store = new MemoryStore();
            var progress = Progress(store, "host");
            Assert.That(Complete(progress.GrantFirstBossRewardAsync()), Is.True);

            store.RejectWrites = true;
            Assert.Throws<IOException>(() => Complete(progress.BuyCuArmorAsync(Campaign)));
            Assert.That(progress.Balance, Is.EqualTo(Stage1TokenProgress.FirstBossReward));
            Assert.That(progress.HasCuArmor(Campaign), Is.False);
            Assert.That(store.Writes, Is.EqualTo(1));

            store.RejectWrites = false;
            Assert.That(Complete(progress.BuyCuArmorAsync(Campaign)), Is.True);
            Assert.That(progress.Balance, Is.Zero);
            Assert.That(progress.HasCuArmor(Campaign), Is.True);
        }

        [Test]
        public void FailedBossRewardIsRetriedOnceWhenStorageReturns()
        {
            var store = new MemoryStore { RejectWrites = true };
            var progress = Progress(store, "host");
            Assert.That(Complete(progress.GrantFirstBossRewardAsync()), Is.False);
            Assert.That(progress.StorageError, Is.TypeOf<IOException>());
            Assert.That(progress.FirstBossRewardReceived, Is.False);
            Assert.That(Complete(progress.LoadAsync()), Is.False, "A hub should remain usable while the retry also fails.");
            Assert.That(Complete(progress.BuyCuArmorAsync(Campaign)), Is.False);
            Assert.That(store.Writes, Is.Zero);

            store.RejectWrites = false;
            var retries = Complete(Task.WhenAll(progress.LoadAsync(), progress.LoadAsync()));
            Assert.That(retries.Count(granted => granted), Is.EqualTo(1));
            Assert.That(progress.StorageError, Is.Null);
            Assert.That(progress.Balance, Is.EqualTo(Stage1TokenProgress.FirstBossReward));
            Assert.That(store.Writes, Is.EqualTo(1));
            Assert.That(Complete(Progress(store, "host").GrantFirstBossRewardAsync()), Is.False);
        }

        [Test]
        public void CorruptWalletDoesNotBecomeAnEmptyWalletOrGetOverwritten()
        {
            var store = new MemoryStore();
            Assert.That(Complete(Progress(store, "host").GrantFirstBossRewardAsync()), Is.True);
            store.ReadError = new InvalidDataException("The token file failed validation.");
            var reopened = Progress(store, "host");

            Assert.That(Complete(reopened.LoadAsync()), Is.False);
            Assert.That(reopened.StorageError, Is.SameAs(store.ReadError));
            Assert.That(reopened.IsLoaded, Is.False);
            Assert.That(Complete(reopened.GrantFirstBossRewardAsync()), Is.False);
            Assert.That(store.Writes, Is.EqualTo(1));

            store.ReadError = null;
            Assert.That(Complete(reopened.LoadAsync()), Is.False);
            Assert.That(reopened.StorageError, Is.Null);
            Assert.That(reopened.Balance, Is.EqualTo(Stage1TokenProgress.FirstBossReward));
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        private static Stage1TokenProgress Progress(MemoryStore store, string owner) =>
            new(store, new BinaryCheckpointPayloadCodec(), new PlayerId(owner));

        private static T Complete<T>(Task<T> transaction) => transaction.GetAwaiter().GetResult();

        private sealed class MemoryStore : ICheckpointStore
        {
            private readonly Dictionary<CheckpointSlot, CheckpointData> _slots = new();
            public System.Exception ReadError { get; set; }
            public bool RejectWrites { get; set; }
            public int Writes { get; private set; }

            public Task<CheckpointData> LoadAsync(CheckpointSlot slot, CancellationToken cancellationToken = default) =>
                ReadError != null ? Task.FromException<CheckpointData>(ReadError) :
                    Task.FromResult(_slots.TryGetValue(slot, out var data) ? data : null);

            public async Task SaveAsync(CheckpointSlot slot, CheckpointData data, CancellationToken cancellationToken = default)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (RejectWrites) throw new IOException("The test store rejected the save.");
                _slots[slot] = data;
                Writes++;
            }
        }
    }
}
