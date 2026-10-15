// 기존 D 거리 구역을 Core Grid로 변환해도 셀 소속과 해금 결과가 같은지 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Turrets;
using UnityEngine;

namespace TeamHJD.Game.Turrets.Tests
{
    public sealed class TurretRegionGridAdapterTests
    {
        [TestCase(4, 4, 8f, 8f)]
        [TestCase(7, 3, 35f, 9f)]
        [TestCase(1, 1, 2f, 2f)]
        public void StaticProjection_PreservesEveryZoneAndUnlockedCell(int cellsX, int cellsY, float width, float height)
        {
            var center = new Vector2(12, -9);
            var zones = new TurretUnlockZones(new TurretGridConfiguration(center, width, height, cellsX, cellsY), center, 4);
            RegionGridDefinition definition = TurretRegionGridAdapter.CreateDefinition(zones);
            var state = new RegionGridState(new MatchId("test"), Guid.NewGuid(), definition);
            for (int unlocked = 0; unlocked <= zones.ZoneCount; unlocked++)
            {
                state.Apply(TurretRegionGridAdapter.GetUnlockedCells(zones, unlocked), RegionCaptureUpdateMode.Replace);
                for (int y = 0; y < cellsY; y++)
                    for (int x = 0; x < cellsX; x++)
                    {
                        int index = definition.GetCellIndex(x, y);
                        Assert.That(definition.RegionIds[index], Is.EqualTo(zones.GetZoneForCell(x, y)));
                        Assert.That(state.Snapshot.IsCaptured(x, y), Is.EqualTo(zones.GetZoneForCell(x, y) <= unlocked));
                    }
            }
            Assert.That(definition.Geometry.Origin, Is.EqualTo(new BattlefieldPoint(12, -9)));
        }

        [Test]
        public void StaticProjection_RejectsInvalidProgressWithoutChangingFeature()
        {
            var zones = new TurretUnlockZones(new TurretGridConfiguration(Vector2.zero, 8, 8, 4, 4), Vector2.zero, 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => TurretRegionGridAdapter.GetUnlockedCells(zones, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TurretRegionGridAdapter.GetUnlockedCells(zones, zones.ZoneCount + 1));
            Assert.Throws<ArgumentNullException>(() => TurretRegionGridAdapter.CreateDefinition(null));
        }
    }
}
