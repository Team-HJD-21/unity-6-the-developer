// 공용 식별자·문자열·지역 Grid 정의의 엄격한 바이너리 표현입니다.
// 길이·유효 수치·RLE 합계를 확인한 뒤만 불변 Domain 값으로 복원합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Infrastructure
{
    internal static class NetworkWireValues
    {
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal static int ReadCount(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum) throw new FormatException("Invalid collection length.");
            return count;
        }
        internal static Guid ReadGuid(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(16);
            if (bytes.Length != 16) throw new EndOfStreamException();
            return new Guid(bytes);
        }
        internal static void WriteText(BinaryWriter writer, string value, int maximumBytes = 1024)
        {
            byte[] bytes = Utf8.GetBytes(value ?? "");
            if (bytes.Length > maximumBytes) throw new ArgumentException("Text exceeds the protocol limit.");
            writer.Write((ushort)bytes.Length); writer.Write(bytes);
        }
        internal static string ReadText(BinaryReader reader, int maximumBytes = 1024)
        {
            int count = reader.ReadUInt16(); if (count > maximumBytes) throw new FormatException("Text exceeds the protocol limit.");
            byte[] bytes = reader.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException();
            return Utf8.GetString(bytes);
        }
        internal static void WriteRegionDefinition(BinaryWriter writer, RegionGridDefinition definition)
        {
            var grid = definition.Geometry;
            writer.Write(grid.Origin.X); writer.Write(grid.Origin.Y); writer.Write(grid.OriginZ);
            writer.Write(grid.MapWidth); writer.Write(grid.MapHeight); writer.Write(grid.CellsX); writer.Write(grid.CellsY);
            var runs = new List<(int length, int region)>();
            foreach (int region in definition.RegionIds)
            {
                if (runs.Count > 0 && runs[runs.Count - 1].region == region)
                { var last = runs[runs.Count - 1]; runs[runs.Count - 1] = (last.length + 1, region); }
                else runs.Add((1, region));
            }
            writer.Write(runs.Count);
            foreach (var run in runs) { writer.Write(run.length); writer.Write(run.region); }
        }
        internal static RegionGridDefinition ReadRegionDefinition(BinaryReader reader)
        {
            var origin = new BattlefieldPoint(reader.ReadDouble(), reader.ReadDouble());
            var grid = new BattlefieldGridConfiguration(origin, reader.ReadDouble(), reader.ReadDouble(),
                reader.ReadDouble(), reader.ReadInt32(), reader.ReadInt32());
            int runCount = ReadCount(reader, grid.TotalCellCount);
            var membership = new int[grid.TotalCellCount]; int offset = 0;
            for (int i = 0; i < runCount; i++)
            {
                int length = reader.ReadInt32(), region = reader.ReadInt32();
                if (length < 1 || region < 0 || length > membership.Length - offset) throw new FormatException("Invalid region run.");
                for (int cell = 0; cell < length; cell++) membership[offset++] = region;
            }
            if (offset != membership.Length) throw new FormatException("Incomplete region membership.");
            return new RegionGridDefinition(grid, membership);
        }
    }
}
