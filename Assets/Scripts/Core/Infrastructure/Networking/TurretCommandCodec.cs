// 터렛 명령/결과를 작은 버전 지정 바이너리 메시지로 직렬화합니다.
// 길이·열거형·불필요한 필드·잘림·후행 바이트를 거부하며 객체/프리팹은 전달하지 않습니다.
using System;
using System.IO;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using static TeamHJD.Game.Infrastructure.NetworkWireValues;
namespace TeamHJD.Game.Infrastructure
{
    public static class TurretCommandCodec
    {
        public const int MaximumMessageBytes = 8192;
        private const int Magic = 0x31544A48;
        public static byte[] Encode(Guid descriptorId, TurretCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            WriteHeader(writer, descriptorId, 0, request.Context.MatchId, request.Context.RequestId, request.Kind, request.TargetId);
            WriteText(writer, request.Context.IssuerId.Value);
            writer.Write(request.ShouldActivate); WriteText(writer, request.UpgradeId.Value, 512);
            return Finish(stream);
        }
        public static byte[] Encode(Guid descriptorId, TurretCommandResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            WriteHeader(writer, descriptorId, 1, result.MatchId, result.RequestId, result.Kind, result.TargetId);
            writer.Write(result.ExecutionSequence); writer.Write((byte)result.Status);
            WriteText(writer, result.FeatureCode, 512); WriteText(writer, result.Reason, 2048); writer.Write(result.IsReplay);
            return Finish(stream);
        }
        private static void WriteHeader(BinaryWriter writer, Guid descriptorId, byte kind,
            MatchId matchId, Guid requestId, TurretCommandKind command, EntityId target)
        {
            if (descriptorId == Guid.Empty) throw new ArgumentException("A preparation ID is required.", nameof(descriptorId));
            writer.Write(Magic); writer.Write(kind); writer.Write(descriptorId.ToByteArray());
            WriteText(writer, matchId.Value); writer.Write(requestId.ToByteArray());
            writer.Write((byte)command); WriteText(writer, target.Value, 512);
        }
        public static void Decode(byte[] bytes, out Guid descriptorId, out TurretCommandRequest request, out TurretCommandResult result)
        {
            if (bytes == null || bytes.Length < 1 || bytes.Length > MaximumMessageBytes) throw new FormatException("Invalid command length.");
            descriptorId = Guid.Empty; request = null; result = null;
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Utf8, true);
            try
            {
                if (reader.ReadInt32() != Magic) throw new FormatException("Unknown command protocol.");
                byte tag = reader.ReadByte(); descriptorId = ReadGuid(reader);
                if (descriptorId == Guid.Empty) throw new FormatException("Missing preparation ID.");
                var match = new MatchId(ReadText(reader)); Guid id = ReadGuid(reader);
                var command = (TurretCommandKind)reader.ReadByte(); var target = new EntityId(ReadText(reader, 512));
                if (tag == 0)
                {
                    var context = new MatchRequestContext(match, id, new PlayerId(ReadText(reader)));
                    bool on = ReadBoolean(reader); var upgrade = new DefinitionId(ReadText(reader, 512));
                    request = new TurretCommandRequest(context, command, target, on, upgrade);
                }
                else if (tag == 1)
                {
                    long sequence = reader.ReadInt64(); var status = (TurretCommandStatus)reader.ReadByte();
                    string code = ReadText(reader, 512), reason = ReadText(reader, 2048); bool replay = ReadBoolean(reader);
                    if ((!target.IsEmpty && target.Value.Length > 128) ||
                        (command == TurretCommandKind.StartAutoActivation ? !target.IsEmpty : target.IsEmpty))
                        throw new FormatException("Invalid command target.");
                    result = new TurretCommandResult(match, id, command, target, sequence, status, code, reason, replay);
                }
                else throw new FormatException("Unknown command message.");
                if (stream.Position != stream.Length) throw new FormatException("Unexpected command data.");
            }
            catch (Exception error) when (error is EndOfStreamException || error is ArgumentException ||
                error is OverflowException || error is DecoderFallbackException)
            { throw new FormatException("Invalid command payload.", error); }
        }
        private static bool ReadBoolean(BinaryReader reader)
        { byte value = reader.ReadByte(); if (value > 1) throw new FormatException("Invalid boolean."); return value == 1; }
        private static byte[] Finish(MemoryStream stream)
        { if (stream.Length > MaximumMessageBytes) throw new ArgumentException("Command exceeds its limit."); return stream.ToArray(); }
    }
}
