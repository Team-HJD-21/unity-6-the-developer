// 월드 저장이 어느 Host 성장 슬롯·Mission 실행에 속하는지 보존합니다.
// 새 실행에서 과거 안전장치를 초기화한 채 옛 월드를 이어가는 일을 막습니다.
using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class CampaignCheckpointParticipant : ICheckpointParticipant
    {
        private readonly CheckpointSlot _campaign;
        private readonly Guid _run;
        public CampaignCheckpointParticipant(CheckpointSlot campaign, Guid run)
        {
            if (campaign.IsEmpty || run == Guid.Empty) throw new ArgumentException("A campaign and Mission run are required.");
            _campaign = campaign; _run = run;
        }
        public string Key => "world/campaign";
        public int Version => 2;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 0;
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
            => codec.Write(Key, Version, Kind, w => { codec.WriteText(w, _campaign.Value); codec.WriteText(w, _run.ToString("N")); });
        public static CheckpointSlot Read(CheckpointData data, ICheckpointPayloadCodec codec) => ReadIdentity(data, codec).Campaign;
        public static Guid ReadRun(CheckpointData data, ICheckpointPayloadCodec codec) => ReadIdentity(data, codec).Run;
        private static (CheckpointSlot Campaign, Guid Run) ReadIdentity(CheckpointData data, ICheckpointPayloadCodec codec)
        {
            var section = data.Sections.SingleOrDefault(x => x.Key == "world/campaign");
            if (section == null || section.Kind != CheckpointSectionKind.World || section.Version != 2)
                throw new InvalidDataException("Campaign/run identity is missing; explicit legacy save migration is required.");
            return codec.Read(section, 2, r => (new CheckpointSlot(codec.ReadText(r)), Guid.ParseExact(codec.ReadText(r), "N")));
        }
        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            var saved = codec.Read(section, 2, r => (Campaign: new CheckpointSlot(codec.ReadText(r)), Run: Guid.ParseExact(codec.ReadText(r), "N")));
            if (!saved.Campaign.Equals(_campaign) || saved.Run != _run)
                throw new InvalidDataException("Load the matching campaign and recovery ledger before restoring this world.");
            return new CheckpointRestoreStep(() => { }, () => { });
        }
    }
}
