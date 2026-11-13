// 한 요청에 필요한 Scene 대상·현재 지역·실제 Enemy 수를 한 번 수집합니다.
// 외부 조회는 이 Step에 모으며 다음 Step을 직접 호출하지 않습니다.
using System;
using System.Linq;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Enemies.Encounter
{
    internal sealed class EncounterCollector
    {
        private readonly EnemySpawnExecutor _executor;

        public EncounterCollector(EnemySpawnExecutor executor) => _executor = executor;

        public EncounterCollection Collect(BattlefieldSpatialSnapshot battlefield, RegionGridSnapshot region, double margin, bool collectInternalAreas = false, double playerDistance = 8)
        {
            if (region == null || region.MatchId != battlefield.MatchId)
                throw new ArgumentException("Collect the current Match's region snapshot.", nameof(region));
            var points = _executor.SpawnPointList;
            points.ApplyRegionSnapshot(region, margin);
            // 다른 additive 전장/테스트 Scene의 Player·CU를 편성 판단에 섞지 않습니다.
            var scene = _executor.gameObject.scene;
            TargetableComponent[] targets = UnityEngine.Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None)
                .Where(target => target != null && target.gameObject.scene == scene).ToArray();
            return new EncounterCollection(battlefield, region, points, targets,
                _executor.CountForMatch(battlefield.MatchId), collectInternalAreas
                    ? EncounterInternalAreaCollector.Collect(battlefield, region, targets, playerDistance) : null);
        }
    }
}
