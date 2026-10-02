using System.Collections.Generic;
using System.Text;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Domain
{
    /// <summary>
    /// Encounter 입력 계약이 정해지기 전, Planner의 임시 판단 결과로 스폰 흐름을 확인한다.
    /// 스폰 계획은 Planner에, 실제 네트워크 생성은 Executor에 위임한다.
    /// </summary>
    public class EncounterRuntime
    {
        private readonly SpawnCompositionPlanner _planner;
        private readonly EnemySpawnExecutor _spawnExecutor;

        /// <summary>
        /// 분대 프리셋 카탈로그가 로드되어 계획을 세울 수 있는지 반환한다.
        /// </summary>
        public bool HasCatalog => _planner.HasCatalog;

        /// <summary>
        /// 게임 시작 시 씬에서 Executor를 찾았는지 반환한다.
        /// </summary>
        public bool HasExecutor => _spawnExecutor != null;
        
        /// <summary>
        /// 기본 Planner를 준비하고 현재 씬의 Executor를 한 번 찾아 보관한다.
        /// </summary>
        public EncounterRuntime()
        {
            _planner = new SpawnCompositionPlanner();
            _spawnExecutor = Object.FindAnyObjectByType<EnemySpawnExecutor>();
        }

        /// <summary>
        /// Planner가 결정한 지점별 명령을 Executor에 전달해 적을 생성한다.
        /// 임시 분대 유형과 생성 상한은 Planner가 결정한다.
        /// </summary>
        public void Spawn()
        {
            if (_spawnExecutor == null)
            {
                Debug.LogError("Enemy spawn executor was not found.");
                return;
            }

            if (!_planner.TryPlan(_spawnExecutor.SpawnPointList,
                    out IReadOnlyList<SpawnInstruction> instructions))
            {
                Debug.LogWarning("No spawn plan: check the squad preset, budget, and available spawn points.");
                return;
            }

            // Planner는 여러 지점의 명령을 반환할 수 있으므로 각각 실행한다.
            foreach (SpawnInstruction instruction in instructions)
                _spawnExecutor.Execute(instruction);
        }

        /// <summary>
        /// Match 공간 snapshot을 Encounter 경계로 받아 요청한 PoC 분대를 계획하고 서버 Executor에 전달합니다.
        /// snapshot의 topology/Grid를 이용한 배치 정책은 Encounter 정책이 확정되기 전이므로 아직 적용하지 않습니다.
        /// </summary>
        public bool TrySpawn(
            BattlefieldSpatialSnapshot snapshot,
            string squadOrder,
            int maxEnemyCount,
            out string result)
        {
            if (snapshot == null || snapshot.MatchId.IsEmpty)
            {
                result = "활성 Match Battlefield snapshot이 없습니다.";
                return false;
            }

            if (_spawnExecutor == null)
            {
                result = "현재 Scene에서 EnemySpawnExecutor를 찾지 못했습니다.";
                return false;
            }

            if (!_spawnExecutor.CanSpawn)
            {
                result = "Encounter 실행에는 NGO Host/Server가 필요합니다. NetworkManager 상태를 확인하세요.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(squadOrder) || maxEnemyCount <= 0)
            {
                result = "분대 명령과 최대 생성 수를 확인하세요.";
                return false;
            }

            var request = new SpawnRequest(squadOrder.Trim(), maxEnemyCount);
            if (!_planner.TryPlan(request, _spawnExecutor.SpawnPointList,
                    out IReadOnlyList<SpawnInstruction> instructions))
            {
                result = $"SpawnPlan 생성 실패: preset '{request.SquadOrder}', budget {request.MaxCount}, " +
                         $"available points {_spawnExecutor.SpawnPointList.GetAvailableSpawnPoints().Count}.";
                return false;
            }

            var executionSummary = new StringBuilder();
            int spawnedCount = 0;
            foreach (SpawnInstruction instruction in instructions)
            {
                int instructionSpawned = _spawnExecutor.Execute(instruction);
                spawnedCount += instructionSpawned;
                if (executionSummary.Length > 0) executionSummary.Append(" | ");
                executionSummary.Append(instruction.SpawnPointId).Append(": ");
                for (int index = 0; index < instruction.Enemies.Count; index++)
                {
                    if (index > 0) executionSummary.Append(", ");
                    EnemySpawnEntry entry = instruction.Enemies[index];
                    executionSummary.Append(entry.EnemyId).Append(" planned ").Append(entry.Count);
                }
                executionSummary.Append(" / spawned ").Append(instructionSpawned);
            }

            result = $"Match {snapshot.MatchId} r{snapshot.Revision} → Encounter: " +
                     $"{snapshot.Triangles.Count} triangles, {snapshot.FrontlineEdges.Count} frontline edges, " +
                     $"{snapshot.Grid.OccupiedCells.Count} occupied cells; " +
                     $"{instructions.Count} plan(s), {spawnedCount} spawned. {executionSummary}";
            Debug.Log(result);
            return spawnedCount > 0;
        }
    }
}

