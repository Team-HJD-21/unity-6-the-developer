// Feature별 명시 schema를 제한된 메모리 바이너리로 기록/해석합니다.
// 게임 상태·파일·Unity 객체를 직접 조회하지 않으며 payload 길이와 읽기 완료를 확인합니다.
using System;
using System.IO;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class BinaryCheckpointPayloadCodec : ICheckpointPayloadCodec
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public CheckpointSection Write(string key, int version, CheckpointSectionKind kind, Action<BinaryWriter> write)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            using var stream = new LimitedStream();
            using (var writer = new BinaryWriter(stream, Utf8, true)) { write(writer); writer.Flush(); }
            return new CheckpointSection(key, version, kind, stream.ToArray());
        }
        public T Read<T>(CheckpointSection section, int expectedVersion, Func<BinaryReader, T> read)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (section.Version != expectedVersion) throw new NotSupportedException($"Unsupported '{section.Key}' schema version {section.Version}.");
            try
            {
                using var stream = new MemoryStream(section.CopyPayload(), false); using var reader = new BinaryReader(stream, Utf8, true);
                T result = read(reader);
                if (stream.Position != stream.Length) throw new InvalidDataException("Feature payload contains unconsumed data.");
                return result;
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("Truncated Feature payload.", error); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid Feature UTF-8 data.", error); }
        }
        public void WriteText(BinaryWriter writer, string text, int maximumBytes = 1024)
        {
            if (maximumBytes < 0 || maximumBytes > CheckpointSection.MaximumPayloadBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            text ??= string.Empty;
            if (Utf8.GetByteCount(text) > maximumBytes) throw new InvalidDataException("Feature text exceeds its size limit.");
            byte[] bytes = Utf8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes);
        }
        public string ReadText(BinaryReader reader, int maximumBytes = 1024)
        {
            int count = ReadCount(reader, maximumBytes);
            if (count > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Truncated Feature text.");
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new InvalidDataException("Truncated Feature text.");
            return Utf8.GetString(bytes);
        }
        public int ReadCount(BinaryReader reader, int maximum)
        {
            if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum) throw new InvalidDataException("Feature collection count exceeds its size limit.");
            return count;
        }
        private sealed class LimitedStream : Stream
        {
            private readonly MemoryStream _inner = new MemoryStream();
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set { Check(value); _inner.Position = value; } }
            public byte[] ToArray() => _inner.ToArray();
            private static void Check(long length)
            { if (length < 0 || length > CheckpointSection.MaximumPayloadBytes) throw new InvalidDataException("Feature payload exceeds its size limit."); }
            public override void Write(byte[] buffer, int offset, int count) { Check(checked(Position + count)); _inner.Write(buffer, offset, count); }
            public override void WriteByte(byte value) { Check(checked(Position + 1)); _inner.WriteByte(value); }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override void Flush() => _inner.Flush();
            public override void SetLength(long value) { Check(value); _inner.SetLength(value); }
            public override long Seek(long offset, SeekOrigin origin)
            {
                long start = origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? Position :
                    origin == SeekOrigin.End ? Length : throw new ArgumentOutOfRangeException(nameof(origin));
                Position = checked(start + offset); return Position;
            }
            protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
        }
    }
}
