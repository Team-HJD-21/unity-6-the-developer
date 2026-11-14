// 수집된 자료를 일반 스폰 후보와 지점별 전투 상황으로 가공합니다.
// 점유 그룹은 예산 배분 전에 제외하며 AI의 실제 공격 타깃을 변경하지 않습니다.
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using RuntimeTargetSelector = TeamHJD.Game.Enemies.Targeting.EnemyTargetSelector;

namespace TeamHJD.Game.Enemies.Encounter
{
    internal sealed class EncounterProcessor
    {
        public EncounterSituation Process(EncounterCollection collection)
        {
            var situation = new EncounterSituation(collection);
            foreach (TargetableComponent target in collection.Targets)
                if (target.TargetType == TargetType.Player && target.CanBeTargeted && target.TargetTransform != null)
                {
                    Vector3 position = target.TargetTransform.position;
                    situation.PlayerPositions.Add(new BattlefieldPoint(position.x, position.y));
                }

            foreach (SpawnPoint point in collection.Points.GetRegisteredSpawnPoints())
            {
                if (point == null) continue;
                Vector3 position = point.Position;
                var xy = new BattlefieldPoint(position.x, position.y);
                if (!point.CanSpawnEnemies)
                {
                    situation.ExcludedPoints.Add(new SpawnDecisionSnapshot.Candidate(point.SpawnPointId, xy,
                        -1, -1, 0, 0, 0, false, point.SpawnExclusionReason));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(point.SpawnPointId) || situation.TargetPositions.ContainsKey(point.SpawnPointId)) continue;
                if (!RuntimeTargetSelector.TryFindNearest(position, collection.Targets, TargetType.Turret, out ITargetable target)) continue;
                Vector3 targetPosition = target.TargetTransform.position;
                var targetXY = new BattlefieldPoint(targetPosition.x, targetPosition.y);
                if (!collection.Battlefield.Grid.TryGetCellAt(targetXY, out _)) continue;

                situation.Candidates.Add(new SpatialSpawnCandidate(point.SpawnPointId, xy));
                situation.TargetPositions.Add(point.SpawnPointId, targetXY);
                situation.TargetNames.Add(point.SpawnPointId, target.TargetTransform.name);
                DamageableTargetAdapter adapter = target.TargetTransform.GetComponent<DamageableTargetAdapter>();
                situation.TargetAttackTypes.Add(point.SpawnPointId, adapter != null ? adapter.AttackType : TargetAttackType.Unknown);
            }
            return situation;
        }
    }
}
