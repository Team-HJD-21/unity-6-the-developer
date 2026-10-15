// 전체 표시 투영을 유한 길이의 버전 지정 바이너리로 변환합니다.
// 문자열·행 개수·숫자·표식·bool·후행 데이터를 검증하며 D 객체를 직렬화하지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using static TeamHJD.Game.Infrastructure.NetworkWireValues;
namespace TeamHJD.Game.Infrastructure
{
    public static class TurretPresentationCodec
    {
        public const int MaximumMessageBytes = NetworkMessageLimits.MaximumMessageBytes;
        private const int Magic = 0x31504A48;
        public static byte[] Encode(TurretPresentationFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write(Magic); WriteText(writer, frame.MatchId.Value); writer.Write(frame.DescriptorId.ToByteArray());
            writer.Write(frame.Revision); writer.Write(frame.ConfirmedCommandSequence);
            var controller = frame.Controller;
            writer.Write(controller.MaximumPower); writer.Write(controller.AvailablePower); writer.Write(controller.ReservedPower); writer.Write(controller.PendingRecoveryPower);
            writer.Write(controller.ControlUnitHealth); writer.Write(controller.ControlUnitMaxHealth);
            writer.Write(controller.ControlUnitPosition.X); writer.Write(controller.ControlUnitPosition.Y);
            writer.Write(controller.IsControlUnitAvailable); writer.Write(controller.IsAutoActivationRunning);
            writer.Write(controller.AutoActivationCooldown); writer.Write((int)controller.RegionMode);
            writer.Write(frame.Turrets.Count);
            foreach (var row in frame.Turrets)
            {
                WriteText(writer, row.EntityId.Value, 512); WriteText(writer, row.DefinitionId.Value, 512); WriteText(writer, row.DisplayName, 1024);
                writer.Write(row.Position.X); writer.Write(row.Position.Y); writer.Write(row.Level); writer.Write(row.CurrentHealth); writer.Write(row.MaxHealth);
                writer.Write(row.Damage); writer.Write(row.Power); writer.Write(row.Range); writer.Write(row.CoolingRemaining);
                WriteText(writer, row.SelectedUpgradeId.Value, 512); writer.Write(row.UpgradeLevel); writer.Write((byte)row.Flags);
            }
            if (stream.Length > MaximumMessageBytes) throw new ArgumentException("Presentation exceeds its wire limit.");
            return stream.ToArray();
        }
        public static TurretPresentationFrame Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1 || bytes.Length > MaximumMessageBytes) throw new FormatException("Invalid presentation length.");
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Utf8, true);
            try
            {
                if (reader.ReadInt32() != Magic) throw new FormatException("Unknown presentation protocol.");
                var matchId = new MatchId(ReadText(reader)); Guid descriptorId = ReadGuid(reader);
                long revision = reader.ReadInt64(), confirmed = reader.ReadInt64();
                int maximum = reader.ReadInt32(), available = reader.ReadInt32(), reserved = reader.ReadInt32(), pending = reader.ReadInt32();
                int health = reader.ReadInt32(), maxHealth = reader.ReadInt32(); var position = new BattlefieldPoint(reader.ReadDouble(), reader.ReadDouble());
                bool cuAvailable = ReadBoolean(reader), autoRunning = ReadBoolean(reader);
                double autoCooldown = reader.ReadDouble(); var mode = (RegionPolicyMode)reader.ReadInt32();
                var controller = new TurretControllerPresentationSnapshot(maximum, available, reserved, pending, health, maxHealth,
                    position, autoRunning, autoCooldown, mode, cuAvailable);
                int count = ReadCount(reader, TurretPresentationFrame.MaximumTurrets); var rows = new List<TurretPresentationSnapshot>(count);
                for (int index = 0; index < count; index++)
                {
                    var id = new EntityId(ReadText(reader, 512)); var definition = new DefinitionId(ReadText(reader, 512)); string name = ReadText(reader, 1024);
                    var point = new BattlefieldPoint(reader.ReadDouble(), reader.ReadDouble());
                    int level = reader.ReadInt32(), hp = reader.ReadInt32(), maxHp = reader.ReadInt32(), damage = reader.ReadInt32(), power = reader.ReadInt32();
                    double range = reader.ReadDouble(), cooling = reader.ReadDouble(); var upgrade = new DefinitionId(ReadText(reader, 512));
                    int upgradeLevel = reader.ReadInt32(); var flags = (TurretPresentationFlags)reader.ReadByte();
                    rows.Add(new TurretPresentationSnapshot(id, definition, name, point, level, hp, maxHp, damage, power, range, cooling, upgrade, upgradeLevel, flags));
                }
                if (stream.Position != stream.Length) throw new FormatException("Unexpected presentation data.");
                return new TurretPresentationFrame(matchId, descriptorId, revision, controller, rows, confirmed);
            }
            catch (Exception error) when (error is EndOfStreamException || error is ArgumentException || error is OverflowException)
            { throw new FormatException("Invalid presentation payload.", error); }
        }
        private static bool ReadBoolean(BinaryReader reader)
        { byte value = reader.ReadByte(); if (value > 1) throw new FormatException("Invalid boolean."); return value == 1; }
    }
}
