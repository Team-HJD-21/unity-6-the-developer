using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.Domain
{
    /// <summary>
    /// 씬에 배치된 스폰 지점 목록을 보관하고 고유 식별 값으로 조회할 수 있게 제공한다.
    /// </summary>
    public class SpawnPointRegistry
    {
        private readonly List<SpawnPoint> spawnPoints = new();

        /// <summary>
        /// Executor의 자식 오브젝트에서 찾은 스폰 지점으로 등록 목록을 교체한다.
        /// 점령 상태에 관계없이 모두 보관하며, 사용 가능 여부는 조회 시 확인한다.
        /// </summary>
        /// <param name="points">등록할 스폰 지점 목록.</param>
        public void Initialize(IEnumerable<SpawnPoint> points)
        {
            spawnPoints.Clear();
            spawnPoints.AddRange(points);
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
