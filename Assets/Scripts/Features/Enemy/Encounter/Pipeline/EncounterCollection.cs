// Collector가 요청당 한 번 읽은 실제 전장·지역·Scene 대상을 보관합니다.
// 정책 판단이나 생성 결과를 섞지 않고 다음 Step에 같은 자료를 전달합니다.
using TeamHJD.Game.Contracts;
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Enemies.Encounter
{
    internal sealed class EncounterCollection
    {
        public BattlefieldSpatialSnapshot Battlefield { get; }
        public RegionGridSnapshot Region { get; }
        public SpawnPointRegistry Points { get; }
        public TargetableComponent[] Targets { get; }
        public int EnemyCount { get; }
        public IReadOnlyList<SpawnArea> InternalAreas { get; }

        public EncounterCollection(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region,
            SpawnPointRegistry points, TargetableComponent[] targets, int enemyCount, IReadOnlyList<SpawnArea> internalAreas = null)
        {
            Battlefield = battlefield;
            Region = region;
            Points = points;
            Targets = targets;
            EnemyCount = enemyCount;
            InternalAreas = internalAreas ?? Array.Empty<SpawnArea>();
        }
    }
}
