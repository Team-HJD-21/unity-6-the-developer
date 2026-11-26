using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed class FieldTurretCheckpointCompatibilityTests
{
    [TestCase(1, false, false, true)]
    [TestCase(1, true, false, false)]
    [TestCase(2, false, false, false)]
    [TestCase(2, false, true, true)]
    public void ReadsOffTurretActivationHistory(int version, bool locked, bool savedHistory, bool expectedHistory)
    {
        var codec = new BinaryCheckpointPayloadCodec();
        var position = new Vector3(3.25f, -7.5f, 1f);
        var section = codec.Write("test/field-turret-actor", version, CheckpointSectionKind.World, writer =>
        {
            codec.WriteText(writer, "turret-1");
            codec.WriteText(writer, "field-cannon");
            writer.Write(100);   // 최대 체력
            writer.Write(80);    // 현재 체력
            writer.Write(false); // 파괴
            writer.Write(false); // 현재 가동
            writer.Write(locked);
            writer.Write(false); // 일시 중단
            writer.Write(0);     // 추가 피해
            codec.WriteText(writer, string.Empty);
            writer.Write(0);     // 강화 레벨
            writer.Write(0f);    // 피해 보정
            writer.Write(0f);    // 전력 보정
            writer.Write(0f);    // 사거리 보정
            writer.Write(.25d);  // 남은 수리량
            if (version == 2)
                writer.Write(savedHistory);

            new CheckpointTransformState(position, Quaternion.identity, Vector3.one).Write(writer);
            writer.Write(false); // 포탑 헤드
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(1f);    // 헤드 회전
            writer.Write(.75f);  // 발사 경과
            writer.Write(0d);    // 남은 냉각 시간
            writer.Write(0f);    // 냉각 기간
            writer.Write(0f);    // 냉각 시작 열 비율
            writer.Write(false); // 초기 가동 대기
            writer.Write(0);     // 예약 전력
        });

        var readActor = typeof(FieldTurretController).GetMethod("ReadActor", BindingFlags.NonPublic | BindingFlags.Static);
        var actor = codec.Read(section, version, reader =>
            (FieldTurretActorState)readActor.Invoke(null, new object[] { reader, codec, version }));

        Assert.That(actor.Runtime.IsActivated, Is.False);
        Assert.That(actor.Runtime.IsLocked, Is.EqualTo(locked));
        Assert.That(actor.Runtime.HasEverActivated, Is.EqualTo(expectedHistory));
        Assert.That(actor.Pose.Position, Is.EqualTo(position));
        Assert.That(actor.FireElapsed, Is.EqualTo(.75f));
    }
}
