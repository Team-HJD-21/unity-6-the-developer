// 체크포인트를 길이 제한·파일 버전·무결성 해시가 있는 바이너리로 변환합니다.
// 해시는 파일 손상 검출이며 Backend 인증이나 부정행위 검증을 대신하지 않습니다.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Infrastructure
{
    internal static class CheckpointBinaryCodec
    {
        private const uint Magic = 0x50434243; // CBCP, little endian
        private const ushort FileVersion = 1;
        public const int HeaderBytes = 42, MaximumFileBytes = HeaderBytes + CheckpointData.MaximumPayloadBytes;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static byte[] Encode(CheckpointData data)
        {
            using var payload = new MemoryStream();
            using (var writer = new BinaryWriter(payload, Utf8, true))
            {
                writer.Write(data.CheckpointId.ToByteArray()); writer.Write(data.CapturedAtUtc.UtcDateTime.Ticks);
                WriteText(writer, data.SourceMatchId.Value, 512); WriteText(writer, data.StageId.Value, 512);
                WriteText(writer, data.DifficultyProfileId.Value, 512); WriteText(writer, data.ContentVersion, 1024);
                writer.Write((int)data.Mode); writer.Write(data.RandomSeed); writer.Write(data.GameSeconds);
                writer.Write(data.ParticipantCount); writer.Write(data.Sections.Count);
                foreach (var section in data.Sections)
                {
                    WriteText(writer, section.Key, 512); writer.Write(section.Version); writer.Write((byte)section.Kind);
                    writer.Write(section.PayloadLength);
                    if (payload.Length + section.PayloadLength > CheckpointData.MaximumPayloadBytes)
                        throw new InvalidDataException("The encoded checkpoint exceeds its size limit.");
                    writer.Write(section.CopyPayload());
                }
            }
            if (payload.Length > CheckpointData.MaximumPayloadBytes) throw new InvalidDataException("Checkpoint is too large.");
            byte[] body = payload.ToArray();
            using var result = new MemoryStream(HeaderBytes + body.Length);
            using (var writer = new BinaryWriter(result, Utf8, true))
            using (var hash = SHA256.Create())
            { writer.Write(Magic); writer.Write(FileVersion); writer.Write(body.Length); writer.Write(hash.ComputeHash(body)); writer.Write(body); }
            return result.ToArray();
        }
        public static CheckpointData Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderBytes || bytes.Length > MaximumFileBytes) throw new InvalidDataException("Invalid checkpoint size.");
            try
            {
                using var envelope = new MemoryStream(bytes, false);
                using var header = new BinaryReader(envelope, Utf8, true);
                if (header.ReadUInt32() != Magic) throw new InvalidDataException("Unknown checkpoint signature.");
                ushort version = header.ReadUInt16();
                if (version != FileVersion) throw new NotSupportedException($"Checkpoint file version {version} is not supported.");
                int length = header.ReadInt32();
                if (length < 0 || length > CheckpointData.MaximumPayloadBytes || bytes.Length != HeaderBytes + length)
                    throw new InvalidDataException("Checkpoint payload length does not match the file.");
                byte[] expected = ReadExact(header, 32);
                using (var hash = SHA256.Create())
                    if (!expected.SequenceEqual(hash.ComputeHash(bytes, HeaderBytes, length))) throw new InvalidDataException("Checkpoint checksum mismatch.");
                using var payload = new MemoryStream(bytes, HeaderBytes, length, false);
                using var reader = new BinaryReader(payload, Utf8, true);
                Guid id = new Guid(ReadExact(reader, 16)); var captured = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
                var match = new MatchId(ReadText(reader, 512)); var stage = new StageId(ReadText(reader, 512));
                var difficulty = new DefinitionId(ReadText(reader, 512)); string content = ReadText(reader, 1024);
                var mode = (MatchMode)reader.ReadInt32(); int seed = reader.ReadInt32(); double game = reader.ReadDouble();
                int participants = reader.ReadInt32(), count = reader.ReadInt32();
                if (count < 1 || count > CheckpointData.MaximumSections) throw new InvalidDataException("Invalid Feature section count.");
                var sections = new CheckpointSection[count];
                for (int i = 0; i < count; i++)
                {
                    string key = ReadText(reader, 512); int sectionVersion = reader.ReadInt32(); var kind = (CheckpointSectionKind)reader.ReadByte();
                    int size = reader.ReadInt32();
                    if (size < 0 || size > CheckpointSection.MaximumPayloadBytes || size > payload.Length - payload.Position)
                        throw new InvalidDataException("Invalid Feature payload length.");
                    sections[i] = new CheckpointSection(key, sectionVersion, kind, ReadExact(reader, size));
                }
                if (payload.Position != payload.Length) throw new InvalidDataException("Trailing checkpoint payload data.");
                return new CheckpointData(id, captured, match, stage, difficulty, mode, seed, content, game, participants, sections);
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("Truncated checkpoint.", error); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid checkpoint values.", error); }
        }
        private static void WriteText(BinaryWriter writer, string value, int maximumBytes)
        {
            byte[] bytes = Utf8.GetBytes(value);
            if (bytes.Length > maximumBytes) throw new InvalidDataException("Checkpoint text exceeds its size limit.");
            writer.Write(bytes.Length); writer.Write(bytes);
        }
        private static string ReadText(BinaryReader reader, int maximumBytes)
        {
            int count = reader.ReadInt32();
            if (count < 1 || count > maximumBytes || count > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid checkpoint text length.");
            return Utf8.GetString(ReadExact(reader, count));
        }
        private static byte[] ReadExact(BinaryReader reader, int count)
        { byte[] bytes = reader.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException(); return bytes; }
    }
}
