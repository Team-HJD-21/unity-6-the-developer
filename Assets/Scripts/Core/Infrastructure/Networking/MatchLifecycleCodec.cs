// Match 시작 설정과 준비/시작/종료 메시지를 제한된 바이너리 형식으로 변환합니다.
// 문자열·참가자·셀 수를 검증하며 지역 소속은 RLE로 기록하고 무제한 입력 할당을 허용하지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static TeamHJD.Game.Infrastructure.NetworkWireValues;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Infrastructure
{
    public static class MatchLifecycleCodec
    {
        public const int MaximumMessageBytes = NetworkMessageLimits.MaximumMessageBytes;
        private const int Magic = 0x324D4A48;
        public static byte[] Encode(NetworkMatchDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write(Magic); writer.Write((byte)0);
            writer.Write(descriptor.DescriptorId.ToByteArray()); writer.Write(descriptor.PreparationSequence);
            MatchConfig config = descriptor.Config;
            WriteText(writer, config.MatchId.Value); WriteText(writer, config.StageId.Value);
            WriteText(writer, config.DifficultyProfileId.Value); WriteText(writer, config.ContentVersion, 4096);
            writer.Write((int)config.Mode); writer.Write(config.RandomSeed);
            if (descriptor.Participants.Count > 16) throw new ArgumentException("Protocol supports at most 16 participants.");
            writer.Write(descriptor.Participants.Count);
            foreach (var binding in descriptor.Participants)
            {
                WriteText(writer, binding.PlayerId.Value); writer.Write(binding.ConnectionId); writer.Write(binding.IsHost);
                var loadout = config.Players.First(item => item.PlayerId == binding.PlayerId);
                if (loadout.UpgradeLevels.Count > 256) throw new ArgumentException("Too many upgrade entries.");
                writer.Write(loadout.UpgradeLevels.Count);
                foreach (var upgrade in loadout.UpgradeLevels.OrderBy(item => item.Key.Value, StringComparer.Ordinal))
                {
                    if (upgrade.Value < 0) throw new ArgumentException("Upgrade level cannot be negative.");
                    WriteText(writer, upgrade.Key.Value); writer.Write(upgrade.Value);
                }
            }
            writer.Write((int)descriptor.RegionMode);
            WriteRegionDefinition(writer, descriptor.RegionGrid);
            return Finish(stream);
        }
        public static byte[] Encode(MatchLifecycleSignalKind kind, MatchLifecycleSignal signal)
        {
            if (!Enum.IsDefined(typeof(MatchLifecycleSignalKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (signal.MatchId.IsEmpty || signal.DescriptorId == Guid.Empty) throw new ArgumentException("Invalid signal.", nameof(signal));
            if ((kind == MatchLifecycleSignalKind.Completed) != (signal.Completion != null))
                throw new ArgumentException("Only a Completed signal must contain a final completion.", nameof(signal));
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write(Magic); writer.Write((byte)((int)kind + 1));
            WriteText(writer, signal.MatchId.Value); writer.Write(signal.DescriptorId.ToByteArray());
            if (signal.Completion != null) WriteCompletion(writer, signal.Completion);
            return Finish(stream);
        }
        public static void Decode(byte[] bytes, out NetworkMatchDescriptor descriptor,
            out MatchLifecycleSignalKind kind, out MatchLifecycleSignal signal)
        {
            if (bytes == null || bytes.Length > MaximumMessageBytes) throw new FormatException("Invalid message length.");
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Utf8, true);
            descriptor = null; kind = default; signal = default;
            try
            {
                if (reader.ReadInt32() != Magic) throw new FormatException("Unknown protocol.");
                int tag = reader.ReadByte();
                if (tag == 0)
                {
                    Guid id = ReadGuid(reader); long sequence = reader.ReadInt64();
                    var match = new MatchId(ReadText(reader)); var stage = new StageId(ReadText(reader));
                    var difficulty = new DefinitionId(ReadText(reader)); string content = ReadText(reader, 4096);
                    var mode = (MatchMode)reader.ReadInt32(); int seed = reader.ReadInt32();
                    int playerCount = ReadCount(reader, 16); if (playerCount < 1) throw new FormatException("Missing participants.");
                    var players = new List<PlayerLoadoutSnapshot>(); var bindings = new List<MatchParticipantBinding>();
                    for (int i = 0; i < playerCount; i++)
                    {
                        var player = new PlayerId(ReadText(reader)); ulong connection = reader.ReadUInt64(); bool host = reader.ReadBoolean();
                        int upgradeCount = ReadCount(reader, 256); var upgrades = new Dictionary<DefinitionId, int>();
                        for (int u = 0; u < upgradeCount; u++)
                        {
                            var upgrade = new DefinitionId(ReadText(reader)); int level = reader.ReadInt32();
                            if (upgrade.IsEmpty || level < 0 || upgrades.ContainsKey(upgrade)) throw new FormatException("Invalid upgrade.");
                            upgrades.Add(upgrade, level);
                        }
                        players.Add(new PlayerLoadoutSnapshot(player, upgrades)); bindings.Add(new MatchParticipantBinding(connection, player, host));
                    }
                    var regionMode = (RegionPolicyMode)reader.ReadInt32();
                    RegionGridDefinition definition = ReadRegionDefinition(reader);
                    descriptor = new NetworkMatchDescriptor(id, sequence,
                        new MatchConfig(match, mode, stage, difficulty, content, seed, players),
                        regionMode, definition, bindings);
                }
                else if (tag >= 1 && tag <= 4)
                {
                    kind = (MatchLifecycleSignalKind)(tag - 1);
                    var match = new MatchId(ReadText(reader));
                    Guid preparation = ReadGuid(reader);
                    signal = new MatchLifecycleSignal(match, preparation,
                        kind == MatchLifecycleSignalKind.Completed ? ReadCompletion(reader, match) : null);
                }
                else throw new FormatException("Unknown message kind.");
                if (stream.Position != stream.Length) throw new FormatException("Unexpected trailing payload.");
            }
            catch (Exception error) when (error is EndOfStreamException || error is ArgumentException || error is OverflowException)
            { throw new FormatException("Invalid lifecycle payload.", error); }
        }
        private static byte[] Finish(MemoryStream stream)
        {
            if (stream.Length > MaximumMessageBytes) throw new ArgumentException("Lifecycle payload exceeds the protocol limit.");
            return stream.ToArray();
        }

        private static void WriteCompletion(BinaryWriter writer, MatchCompletion completion)
        {
            writer.Write((int)completion.Result.Outcome);
            writer.Write(completion.Result.CompletedWave);
            writer.Write(completion.Result.CompletedAt.UtcDateTime.Ticks);
            if (completion.RewardReceipt.CurrencyDeltas.Count > MatchLifecycleSignal.MaximumCurrencyEntries)
                throw new ArgumentException("Too many proposed currency entries.", nameof(completion));
            writer.Write(completion.RewardReceipt.CurrencyDeltas.Count);
            foreach (CurrencyDelta delta in completion.RewardReceipt.CurrencyDeltas)
            {
                WriteText(writer, delta.CurrencyId.Value);
                writer.Write(delta.Amount);
            }
        }

        private static MatchCompletion ReadCompletion(BinaryReader reader, MatchId match)
        {
            var outcome = (MatchOutcome)reader.ReadInt32();
            int wave = reader.ReadInt32();
            var completedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
            int count = ReadCount(reader, MatchLifecycleSignal.MaximumCurrencyEntries);
            var deltas = new CurrencyDelta[count];
            for (int i = 0; i < count; i++)
                deltas[i] = new CurrencyDelta(new CurrencyId(ReadText(reader)), reader.ReadInt64());
            return new MatchCompletion(new MatchResult(match, outcome, wave, completedAt),
                new RewardReceipt(match, outcome, deltas));
        }

    }
}
