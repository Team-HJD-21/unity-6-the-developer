using System.Collections.Generic;
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
    }
}

