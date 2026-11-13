// 월드 저장이 어느 Host 전장 성장 슬롯에 속하는지 보존합니다.
// 재실행 조립부가 성장부터 읽도록 식별자만 제공하며 자동 저장 사건은 만들지 않습니다.
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
        public CampaignCheckpointParticipant(CheckpointSlot campaign) { if (campaign.IsEmpty) throw new ArgumentException("A campaign is required."); _campaign = campaign; }
        public string Key => "world/campaign";
        public int Version => 1;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 0;
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
            => codec.Write(Key, 1, Kind, w => codec.WriteText(w, _campaign.Value));
        public static CheckpointSlot Read(CheckpointData data, ICheckpointPayloadCodec codec)
        {
            var section = data.Sections.SingleOrDefault(x => x.Key == "world/campaign");
            if (section == null || section.Kind != CheckpointSectionKind.World) throw new InvalidDataException("Campaign identity is missing; explicit legacy save migration is required.");
            return codec.Read(section, 1, r => new CheckpointSlot(codec.ReadText(r)));
        }
        public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            var saved = codec.Read(section, 1, r => new CheckpointSlot(codec.ReadText(r)));
            if (!saved.Equals(_campaign)) throw new InvalidDataException("Load the matching campaign growth before restoring this world.");
            return new CheckpointRestoreStep(() => { }, () => { });
        }
    }
}
