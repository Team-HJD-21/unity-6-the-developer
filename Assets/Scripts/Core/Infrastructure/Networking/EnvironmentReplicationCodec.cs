// 제한된 불변 환경 전체 상태를 직렬화합니다. 알 수 없는 축은 빈 식별자로 유지합니다.
// 길이·forecast 수·유효 수치·후행 데이터 검증 뒤에만 값을 복원합니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using static TeamHJD.Game.Infrastructure.NetworkWireValues;
namespace TeamHJD.Game.Infrastructure
{
    public static class EnvironmentReplicationCodec
    {
        private const int Magic = 0x31454A48;
        public const int MaximumBytes = 16384;
        public static byte[] Encode(EnvironmentReplicationFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            var value = frame.Snapshot; writer.Write(Magic); writer.Write(frame.DescriptorId.ToByteArray());
            WriteText(writer, value.MatchId.Value); writer.Write(value.Revision); writer.Write(value.GameSeconds);
            WriteText(writer, value.WeatherId.Value, MatchEnvironmentSnapshot.MaximumIdBytes);
            WriteText(writer, value.TimeOfDayId.Value, MatchEnvironmentSnapshot.MaximumIdBytes);
            writer.Write(value.Forecast.Count);
            foreach (DefinitionId next in value.Forecast) WriteText(writer, next.Value, MatchEnvironmentSnapshot.MaximumIdBytes);
            if (stream.Length > MaximumBytes) throw new ArgumentException("Environment payload exceeds its wire limit.");
            return stream.ToArray();
        }
        public static EnvironmentReplicationFrame Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length > MaximumBytes) throw new FormatException("Invalid environment payload length.");
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Utf8, true);
            try
            {
                if (reader.ReadInt32() != Magic) throw new FormatException("Unknown environment protocol.");
                var preparation = ReadGuid(reader); var match = new MatchId(ReadText(reader));
                long revision = reader.ReadInt64(); double clock = reader.ReadDouble();
                DefinitionId weather = ReadId(reader), time = ReadId(reader);
                int count = ReadCount(reader, MatchEnvironmentSnapshot.MaximumForecast);
                var forecast = new DefinitionId[count];
                for (int i = 0; i < count; i++) forecast[i] = ReadId(reader);
                if (stream.Position != stream.Length) throw new FormatException("Unexpected trailing environment data.");
                return new EnvironmentReplicationFrame(preparation, new MatchEnvironmentSnapshot(match, revision, clock, weather, time, forecast));
            }
            catch (Exception error) when (error is EndOfStreamException || error is ArgumentException || error is OverflowException)
            { throw new FormatException("Invalid environment payload.", error); }
        }
        private static DefinitionId ReadId(BinaryReader reader)
        {
            string value = ReadText(reader, MatchEnvironmentSnapshot.MaximumIdBytes);
            return value.Length == 0 ? default : new DefinitionId(value);
        }
    }
}
