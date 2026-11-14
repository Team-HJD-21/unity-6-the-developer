// Host의 확정 지역 상태 전체를 세대·revision과 함께 직렬화합니다.
// 점령은 셀 bitset, 소속은 공용 Grid RLE이며 삼각형을 보내거나 Client에서 재계산하지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using static TeamHJD.Game.Infrastructure.NetworkWireValues;
namespace TeamHJD.Game.Infrastructure
{
    public static class RegionReplicationCodec
    {
        private const int Magic = 0x32524A48;
        public static byte[] Encode(RegionReplicationFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write(Magic); writer.Write(frame.MatchDescriptorId.ToByteArray()); writer.Write(frame.GridGeneration);
            RegionGridSnapshot snapshot = frame.Snapshot;
            WriteText(writer, snapshot.MatchId.Value); writer.Write(snapshot.GridId.ToByteArray());
            writer.Write(snapshot.Revision); writer.Write((int)snapshot.Mode); WriteRegionDefinition(writer, snapshot.Definition);
            writer.Write(snapshot.UnlockedRegionIds.Count);
            foreach (int region in snapshot.UnlockedRegionIds) writer.Write(region);
            byte[] capture = new byte[(snapshot.CapturedCells.Count + 7) / 8];
            for (int i = 0; i < snapshot.CapturedCells.Count; i++)
                if (snapshot.CapturedCells[i]) capture[i / 8] |= (byte)(1 << (i % 8));
            writer.Write(capture);
            if (stream.Length > NetworkMessageLimits.MaximumMessageBytes) throw new ArgumentException("Region payload exceeds the protocol limit.");
            return stream.ToArray();
        }
        public static RegionReplicationFrame Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length > NetworkMessageLimits.MaximumMessageBytes) throw new FormatException("Invalid region payload size.");
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Utf8, true);
            try
            {
                if (reader.ReadInt32() != Magic) throw new FormatException("Unknown region protocol.");
                Guid descriptor = ReadGuid(reader); long generation = reader.ReadInt64();
                var match = new MatchId(ReadText(reader)); Guid grid = ReadGuid(reader);
                long revision = reader.ReadInt64(); var mode = (RegionPolicyMode)reader.ReadInt32();
                RegionGridDefinition definition = ReadRegionDefinition(reader); int count = definition.Geometry.TotalCellCount;
                int unlockedCount = ReadCount(reader, count);
                var unlocked = new int[unlockedCount];
                for (int i = 0; i < unlocked.Length; i++)
                {
                    unlocked[i] = reader.ReadInt32();
                    if (unlocked[i] <= 0 || (i > 0 && unlocked[i] <= unlocked[i - 1]))
                        throw new FormatException("Invalid or duplicate unlocked region.");
                }
                int byteCount = (count + 7) / 8; byte[] capture = reader.ReadBytes(byteCount);
                if (capture.Length != byteCount || stream.Position != stream.Length) throw new FormatException("Invalid capture bitset length.");
                int remaining = count % 8;
                if (remaining != 0 && (capture[byteCount - 1] >> remaining) != 0) throw new FormatException("Capture bits exceed the grid.");
                var cells = new List<int>();
                for (int i = 0; i < count; i++) if ((capture[i / 8] & (1 << (i % 8))) != 0) cells.Add(i);
                return new RegionReplicationFrame(descriptor, generation,
                    RegionGridSnapshot.FromCapturedCells(match, grid, revision, mode, definition, cells, unlocked));
            }
            catch (Exception error) when (error is EndOfStreamException || error is ArgumentException || error is OverflowException)
            { throw new FormatException("Invalid region payload.", error); }
        }
    }
}
