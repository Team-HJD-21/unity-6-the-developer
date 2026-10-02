// Match 중 변하는 Player·Enemy 위치를 불변 입력으로 전달합니다.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldDynamicSpatialInput
    {
        public IReadOnlyList<PlayerSpatialInput> Players { get; }
        public IReadOnlyList<EnemySpatialInput> Enemies { get; }

        public static BattlefieldDynamicSpatialInput Empty =>
            new BattlefieldDynamicSpatialInput(Array.Empty<PlayerSpatialInput>(), Array.Empty<EnemySpatialInput>());

        public BattlefieldDynamicSpatialInput(
            IEnumerable<PlayerSpatialInput> players,
            IEnumerable<EnemySpatialInput> enemies)
        {
            Players = CopyAndValidate(players, nameof(players), item => item.EntityId);
            Enemies = CopyAndValidate(enemies, nameof(enemies), item => item.EntityId);
        }

        private static IReadOnlyList<T> CopyAndValidate<T>(
            IEnumerable<T> source,
            string parameterName,
            Func<T, EntityId> getEntityId)
            where T : class
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var copy = new List<T>();
            var ids = new HashSet<EntityId>();
            foreach (var item in source)
            {
                if (item == null) throw new ArgumentException("Spatial input cannot contain null entities.", parameterName);
                var id = getEntityId(item);
                if (!ids.Add(id)) throw new ArgumentException($"Duplicate spatial entity ID '{id}'.", parameterName);
                copy.Add(item);
            }
            return new ReadOnlyCollection<T>(copy);
        }
    }
}
