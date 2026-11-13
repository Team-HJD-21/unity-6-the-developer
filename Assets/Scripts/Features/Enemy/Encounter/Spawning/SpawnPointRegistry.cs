// Scene에서 발견한 생성 지점을 고유 ID로 등록하고 조회합니다.
// 비활성/점령 상태도 보관하되 Planner에는 현재 생성 가능한 지점만 제공합니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    /// <summary>
    /// 씬에 배치된 스폰 지점 목록을 보관하고 고유 식별 값으로 조회할 수 있게 제공한다.
    /// </summary>
    public class SpawnPointRegistry
    {
        public sealed class RegionGroup
        {
            public int RegionId { get; }
            public IReadOnlyList<SpawnPoint> SpawnPoints { get; }

            internal RegionGroup(int regionId, List<SpawnPoint> spawnPoints)
            {
                RegionId = regionId;
                SpawnPoints = spawnPoints.AsReadOnly();
            }
        }

        private readonly List<SpawnPoint> spawnPoints = new();
        private IReadOnlyList<RegionGroup> regionGroups = Array.Empty<RegionGroup>();

        public IReadOnlyList<SpawnPoint> GetRegisteredSpawnPoints() => spawnPoints.AsReadOnly();

        /// <summary>초기화된 Region 그룹을 반환한다. 그룹을 생성하거나 상태를 변경하지 않는다.</summary>
        public IReadOnlyList<RegionGroup> GetRegionGroups() => regionGroups;

        /// <summary>지점 등록 후 최초 Core Region Snapshot으로 정적 Region 그룹을 구성한다.</summary>
        public void InitializeRegionGroups(RegionGridSnapshot regionSnapshot)
        {
            if (regionSnapshot == null)
                throw new ArgumentNullException(nameof(regionSnapshot));

            var groups = new SortedDictionary<int, List<SpawnPoint>>();
            foreach (SpawnPoint spawnPoint in spawnPoints)
            {
                if (spawnPoint == null)
                    continue;

                var position = spawnPoint.Position;
                var point = new BattlefieldPoint(position.x, position.y);
                if (!regionSnapshot.Definition.TryGetCellIndex(point, out int cellIndex))
                    continue;

                int regionId = regionSnapshot.Definition.RegionIds[cellIndex];
                if (regionId == 0)
                    continue;

                if (!groups.TryGetValue(regionId, out List<SpawnPoint> points))
                {
                    points = new List<SpawnPoint>();
                    groups.Add(regionId, points);
                }

                points.Add(spawnPoint);
            }

            var result = new List<RegionGroup>(groups.Count);
            foreach (KeyValuePair<int, List<SpawnPoint>> group in groups)
                result.Add(new RegionGroup(group.Key, group.Value));

            regionGroups = result.AsReadOnly();
        }

        /// <summary>해당 Region에 등록된 모든 지점의 적 스폰 허용 여부를 변경한다.</summary>
        /// <returns>해당 Region 그룹이 있으면 true.</returns>
        public bool SetRegionSpawnEnabled(int regionId, bool enabled)
        {
            foreach (RegionGroup group in regionGroups)
            {
                if (group.RegionId != regionId)
                    continue;

                foreach (SpawnPoint point in group.SpawnPoints)
                {
                    if (point != null)
                        point.SetEnemySpawnEnabled(enabled);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Executor가 같은 Scene에서 찾은 스폰 지점으로 등록 목록을 교체한다.
        /// 점령 상태에 관계없이 모두 보관하며, 사용 가능 여부는 조회 시 확인한다.
        /// 지점 목록이 바뀌면 Region 그룹을 다시 초기화해야 한다.
        /// </summary>
        /// <param name="points">등록할 스폰 지점 목록.</param>
        public void Initialize(IEnumerable<SpawnPoint> points)
        {
            var nextSpawnPoints = new List<SpawnPoint>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<SpawnPoint>();
            if (points != null)
            {
                foreach (SpawnPoint point in points)
                {
                    if (point == null || !seen.Add(point)) continue;
                    point.EnsureUniqueRegistrationId(ids);
                    nextSpawnPoints.Add(point);
                }
            }

            if (HasSameSpawnPoints(nextSpawnPoints))
                return;

            spawnPoints.Clear();
            spawnPoints.AddRange(nextSpawnPoints);
            regionGroups = Array.Empty<RegionGroup>();
        }

        private bool HasSameSpawnPoints(IReadOnlyList<SpawnPoint> other)
        {
            if (spawnPoints.Count != other.Count)
                return false;

            for (int i = 0; i < spawnPoints.Count; i++)
                if (spawnPoints[i] != other[i])
                    return false;

            return true;
        }

        /// <summary>
        /// 등록된 지점은 유지하면서 현재 적을 생성할 수 있는 지점만 조회한다.
        /// 점령 및 재점령 이후에도 매번 최신 상태를 반영한다.
        /// </summary>
        /// <returns>활성화되어 있고 적 스폰이 허용된 지점 목록.</returns>
        public IReadOnlyList<SpawnPoint> GetAvailableSpawnPoints()
        {
            List<SpawnPoint> availablePoints = new();
            foreach (SpawnPoint point in spawnPoints)
            {
                if (point != null && point.CanSpawnEnemies &&
                    !string.IsNullOrWhiteSpace(point.SpawnPointId))
                    availablePoints.Add(point);
            }

            return availablePoints;
        }

        /// <summary>
        /// 고유 식별 값과 일치하는 스폰 지점을 조회한다.
        /// </summary>
        /// <param name="spawnPointId">조회할 스폰 지점의 고유 식별 값.</param>
        /// <param name="spawnPoint">조회에 성공한 스폰 지점.</param>
        /// <returns>일치하는 스폰 지점이 있으면 <see langword="true"/>, 없으면 <see langword="false"/>.</returns>
        public bool TryGetSpawnPoint(string spawnPointId, out SpawnPoint spawnPoint)
        {
            foreach (SpawnPoint sp in spawnPoints)
            {
                if (sp != null && sp.SpawnPointId == spawnPointId)
                {
                    spawnPoint = sp;
                    return true;
                }
            }

            spawnPoint = null;
            return false;
        }
    }
}
