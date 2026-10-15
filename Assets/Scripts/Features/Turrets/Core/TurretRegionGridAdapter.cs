// D의 정적 거리 구역을 Core 지역 Grid의 값으로 변환합니다.
// 잠금·전력·터렛을 변경하지 않는 읽기 어댑터이며 두 Grid의 셀 ID를 혼용하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Turrets
{
    public static class TurretRegionGridAdapter
    {
        public static RegionGridDefinition CreateDefinition(TurretUnlockZones zones)
        {
            if (zones == null) throw new ArgumentNullException(nameof(zones));
            TurretGridConfiguration source = zones.Grid;
            var geometry = new BattlefieldGridConfiguration(
                new BattlefieldPoint(source.Origin.x, source.Origin.y), source.OriginZ,
                source.MapWidth, source.MapHeight, source.CellsX, source.CellsY);
            var ids = new int[geometry.TotalCellCount];
            for (int y = 0; y < geometry.CellsY; y++)
                for (int x = 0; x < geometry.CellsX; x++)
                    ids[y * geometry.CellsX + x] = zones.GetZoneForCell(x, y);
            return new RegionGridDefinition(geometry, ids);
        }

        public static IReadOnlyList<int> GetUnlockedCells(TurretUnlockZones zones, int unlockedZoneCount)
        {
            if (zones == null) throw new ArgumentNullException(nameof(zones));
            if (unlockedZoneCount < 0 || unlockedZoneCount > zones.ZoneCount)
                throw new ArgumentOutOfRangeException(nameof(unlockedZoneCount));
            var cells = new List<int>();
            for (int y = 0; y < zones.Grid.CellsY; y++)
                for (int x = 0; x < zones.Grid.CellsX; x++)
                    if (zones.GetZoneForCell(x, y) <= unlockedZoneCount)
                        cells.Add(y * zones.Grid.CellsX + x);
            return cells.AsReadOnly();
        }

        /// <summary>같은 Scene/전력 공급자의 현재 상태만 복사합니다. ID는 현행 Match-local 등록 ID입니다.</summary>
        public static IReadOnlyList<RegionTurretInput> CaptureTurrets(TurretController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            var inputs = new List<RegionTurretInput>();
            foreach (TurretBase turret in TurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || !turret.isActiveAndEnabled ||
                    turret.gameObject.scene != controller.gameObject.scene || !turret.UsesPowerSource(controller))
                    continue;
                inputs.Add(new RegionTurretInput(new EntityId($"turret-{turret.InstanceId}"),
                    new BattlefieldPoint(turret.transform.position.x, turret.transform.position.y),
                    turret.IsActivated, turret.IsDestroyed));
            }
            inputs.Sort((left, right) => StringComparer.Ordinal.Compare(left.EntityId.Value, right.EntityId.Value));
            return inputs.AsReadOnly();
        }
    }
}
