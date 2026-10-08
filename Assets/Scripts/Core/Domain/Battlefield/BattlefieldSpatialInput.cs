// 한 번의 전장 분석에 사용할 터렛 공간 입력을 복사하고 검증합니다.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldSpatialInput
    {
        public IReadOnlyList<TurretSpatialInput> Turrets { get; }

        public BattlefieldSpatialInput(IEnumerable<TurretSpatialInput> turrets)
        {
            if (turrets == null) throw new ArgumentNullException(nameof(turrets));
            var copy = new List<TurretSpatialInput>();
            var ids = new HashSet<EntityId>();
            foreach (var turret in turrets)
            {
                if (turret == null) throw new ArgumentException("Spatial input cannot contain null turrets.", nameof(turrets));
                if (!ids.Add(turret.EntityId)) throw new ArgumentException($"Duplicate turret ID '{turret.EntityId}'.", nameof(turrets));
                copy.Add(turret);
            }
            Turrets = new ReadOnlyCollection<TurretSpatialInput>(copy);
        }

        public static BattlefieldSpatialInput Empty => new BattlefieldSpatialInput(Array.Empty<TurretSpatialInput>());
    }
}
