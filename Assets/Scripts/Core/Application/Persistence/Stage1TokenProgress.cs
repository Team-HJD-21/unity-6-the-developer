using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    /// <summary>로컬 Host의 첫 보스 토큰과 CU 장갑 구매를 보존합니다. 월드 체크포인트 복원과는 별도입니다.</summary>
    public sealed class Stage1TokenProgress
    {
        public const int FirstBossReward = 1;
        public const int CuArmorCost = 1;
        private const string SectionKey = "profile/stage1-tokens";
        private const int Version = 1;

        private sealed class Snapshot
        {
            public readonly int Balance;
            public readonly bool FirstBossRewardReceived;
            public readonly CheckpointSlot ArmorCampaign;

            public Snapshot(int balance, bool firstBossRewardReceived, CheckpointSlot armorCampaign)
            {
                Balance = balance;
                FirstBossRewardReceived = firstBossRewardReceived;
                ArmorCampaign = armorCampaign;
            }
        }

        private readonly ICheckpointStore _store;
        private readonly ICheckpointPayloadCodec _codec;
        private readonly PlayerId _owner;
        private readonly CheckpointSlot _slot;
        private readonly SemaphoreSlim _transaction = new(1, 1);
        private volatile Snapshot _current;
        private volatile Exception _storageError;
        private bool _rewardPending;

        public bool IsLoaded => _current != null;
        public Exception StorageError => _storageError;
        public int Balance => _current?.Balance ?? 0;
        public bool FirstBossRewardReceived => _current?.FirstBossRewardReceived == true;
        public bool HasCuArmor(CheckpointSlot campaign) =>
            !campaign.IsEmpty && _current?.ArmorCampaign.Equals(campaign) == true;

        public Stage1TokenProgress(ICheckpointStore store, ICheckpointPayloadCodec codec, PlayerId owner)
        {
            if (owner.IsEmpty) throw new ArgumentException("A local profile owner is required.", nameof(owner));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _owner = owner;
            _slot = GrowthSaveService.SlotForOwner("stage1-tokens/" + owner.Value);
        }

        /// <summary>잔액을 불러오고 대기 중인 처치 보상을 재시도합니다. 재시도에서 새로 지급하면 true입니다.</summary>
        public Task<bool> LoadAsync(CancellationToken cancellation = default) =>
            ApplyAsync(default, false, cancellation);

        /// <summary>Stage 1 최종 보스의 권위 승리 경로가 호출합니다. 이미 받은 보상에는 false를 반환합니다.</summary>
        public Task<bool> GrantFirstBossRewardAsync(CancellationToken cancellation = default) =>
            ApplyAsync(default, true, cancellation);

        /// <summary>우주선의 구매 요청을 처리합니다. 잔액 부족과 이미 구매한 경우에는 false를 반환합니다.</summary>
        public Task<bool> BuyCuArmorAsync(CheckpointSlot campaign, CancellationToken cancellation = default)
        {
            if (campaign.IsEmpty) throw new ArgumentException("A Host campaign is required.", nameof(campaign));
            return ApplyAsync(campaign, false, cancellation);
        }

        private async Task<bool> ApplyAsync(CheckpointSlot purchaseCampaign, bool recordVictory, CancellationToken cancellation)
        {
            await _transaction.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                // 이 기록은 저장에 성공하거나 이미 수령한 것이 확인될 때까지 App 수명에 남습니다.
                if (recordVictory) _rewardPending = true;
                if (_current == null) _current = Read(await _store.LoadAsync(_slot, cancellation).ConfigureAwait(false));
                Snapshot next = _current;
                if (purchaseCampaign.IsEmpty)
                {
                    if (_rewardPending && !_current.FirstBossRewardReceived)
                        next = new Snapshot(checked(_current.Balance + FirstBossReward), true, _current.ArmorCampaign);
                }
                else if (_storageError == null && _current.ArmorCampaign.IsEmpty && _current.Balance >= CuArmorCost)
                {
                    next = new Snapshot(_current.Balance - CuArmorCost, _current.FirstBossRewardReceived, purchaseCampaign);
                }

                bool changed = !ReferenceEquals(next, _current);
                if (changed)
                {
                    // 잔액과 지급·구매 기록을 같은 파일에 확정한 뒤 UI와 다음 출격이 읽도록 공개합니다.
                    await _store.SaveAsync(_slot, Capture(next), cancellation).ConfigureAwait(false);
                    _current = next;
                }
                if (_current.FirstBossRewardReceived) _rewardPending = false;
                if (purchaseCampaign.IsEmpty) _storageError = null;
                return changed;
            }
            catch (Exception error) when (purchaseCampaign.IsEmpty &&
                error is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
            {
                _storageError = error;
                return false;
            }
            finally { _transaction.Release(); }
        }

        private Snapshot Read(CheckpointData data)
        {
            if (data == null) return new Snapshot(0, false, default);
            if (data.Sections.Count != 1 || data.Sections[0].Key != SectionKey ||
                data.Sections[0].Kind != CheckpointSectionKind.Currency)
                throw new InvalidDataException("The slot does not contain Stage 1 token progress.");

            return _codec.Read(data.Sections[0], Version, reader =>
            {
                if (!StringComparer.Ordinal.Equals(_codec.ReadText(reader), _owner.Value))
                    throw new InvalidDataException("Token progress belongs to a different profile.");
                int balance = reader.ReadInt32();
                bool rewardReceived = reader.ReadBoolean();
                CheckpointSlot armorCampaign = default;
                if (reader.ReadBoolean())
                {
                    string savedCampaign = _codec.ReadText(reader);
                    try { armorCampaign = new CheckpointSlot(savedCampaign); }
                    catch (ArgumentException error) { throw new InvalidDataException("Invalid CU armor campaign.", error); }
                }
                if (balance < 0 || !armorCampaign.IsEmpty && !rewardReceived)
                    throw new InvalidDataException("Invalid Stage 1 token balance or research receipt.");
                return new Snapshot(balance, rewardReceived, armorCampaign);
            });
        }

        private CheckpointData Capture(Snapshot state)
        {
            var tokens = _codec.Write(SectionKey, Version, CheckpointSectionKind.Currency, writer =>
            {
                _codec.WriteText(writer, _owner.Value);
                writer.Write(state.Balance);
                writer.Write(state.FirstBossRewardReceived);
                writer.Write(!state.ArmorCampaign.IsEmpty);
                if (!state.ArmorCampaign.IsEmpty) _codec.WriteText(writer, state.ArmorCampaign.Value);
            });
            // 월드 저장과 같은 원자적 파일 저장소를 사용하되 Match나 월드 진행을 재개할 자료는 담지 않습니다.
            return new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, new MatchId("stage1-token-progress"),
                new StageId("stage-1"), new DefinitionId("profile"), MatchMode.Story, 0,
                "local-token-progress", 0, 1, new[] { tokens });
        }
    }
}
