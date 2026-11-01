// Feature 바이너리 schema의 문자열/배열 길이·버전·읽기 완료와 크기 제한을 검증합니다.
using System;
using System.IO;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class BinaryCheckpointPayloadCodecTests
    {
        private readonly BinaryCheckpointPayloadCodec _codec = new();
        [Test] public void ExplicitFeatureSchemaRoundTripsUnicodeAndEmptyOptionalText()
        {
            var section = _codec.Write("feature", 2, CheckpointSectionKind.World,
                writer => { _codec.WriteText(writer, "영역/터렛"); _codec.WriteText(writer, ""); writer.Write(42); });
            int value = _codec.Read(section, 2, reader =>
            { Assert.That(_codec.ReadText(reader), Is.EqualTo("영역/터렛")); Assert.That(_codec.ReadText(reader), Is.Empty); return reader.ReadInt32(); });
            Assert.That(value, Is.EqualTo(42));
        }
        [Test] public void UnboundedLengthsWrongVersionAndTrailingDataAreRejected()
        {
            var bad = _codec.Write("feature", 1, CheckpointSectionKind.World, writer => writer.Write(int.MaxValue));
            Assert.Throws<InvalidDataException>(() => _codec.Read(bad, 1, reader => _codec.ReadText(reader, 64)));
            Assert.Throws<NotSupportedException>(() => _codec.Read(bad, 2, reader => reader.ReadInt32()));
            Assert.Throws<InvalidDataException>(() => _codec.Read(bad, 1, reader => reader.ReadByte()));
            var truncated = new CheckpointSection("feature", 1, CheckpointSectionKind.World, new byte[] { 1 });
            Assert.Throws<InvalidDataException>(() => _codec.Read(truncated, 1, reader => reader.ReadInt32()));
        }
        [Test] public void OversizedWritesAreStoppedBeforeGrowingThePayloadStream()
        {
            Assert.Throws<InvalidDataException>(() => _codec.Write("feature", 1, CheckpointSectionKind.World,
                writer => writer.Write(new byte[CheckpointSection.MaximumPayloadBytes + 1])));
        }
    }
}
