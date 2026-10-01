using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 분대 명령에 맞는 프리셋을 찾아 구성 비율을 실제 생성 수로 변환한다.
/// 스폰 지점별 상한 안에서 완전한 구성 세트만 반복하며, 몬스터 생성 자체는 담당하지 않는다.
/// </summary>
public sealed class SpawnCompositionPlanner : MonoBehaviour
{
    // Inspector에서 분대 명령별 프리셋을 등록한다. 요청이 오면 순서대로 일치하는 프리셋을 찾는다.
    [SerializeField] private List<EnemySquadPreset> enemySquadPresets = new();

    /// <summary>
    /// 요청에 맞는 프리셋을 조회해 한 분대의 적별 생성 수를 확정한다.
    /// 프리셋이 없거나 한 구성 세트도 채울 수 없으면 명령을 만들지 않는다.
    /// </summary>
    /// <param name="spawnRequest">분대 명령과 스폰 지점, 최대 생성 수가 담긴 요청.</param>
    /// <param name="instruction">계획에 성공한 경우 반환할 분대 생성 명령.</param>
    /// <returns>유효한 프리셋으로 최소 한 세트를 구성하면 <see langword="true"/>. 그렇지 않으면 <see langword="false"/>.</returns>
    public bool TryPlan(SpawnRequest spawnRequest, out SpawnInstruction instruction)
    {
        instruction = default;

        // 식별 값이나 생성 상한이 유효하지 않은 요청은 프리셋 조회 전에 제외한다.
        if (string.IsNullOrWhiteSpace(spawnRequest.SquadOrder) ||
            string.IsNullOrWhiteSpace(spawnRequest.SpawnPointId) ||
            spawnRequest.MaxCount <= 0)
            return false;

        EnemySquadPreset squadPreset = null;
        // 요청의 분대 명령과 일치하는 첫 번째 프리셋을 사용한다.
        foreach (EnemySquadPreset preset in enemySquadPresets)
        {
            if (preset != null && preset.SquadOrder == spawnRequest.SquadOrder)
            {
                squadPreset = preset;
                break;
            }
        }

        if (squadPreset == null || squadPreset.Composition == null ||
            squadPreset.Composition.Count == 0)
            return false;

        // 전체 가중치는 한 구성 세트를 완성하는 데 필요한 최소 생성 수다.
        int totalWeight = squadPreset.TotalWeight;
        if (totalWeight <= 0 || spawnRequest.MaxCount < totalWeight)
            return false;

        // 예: 상한 10, 구성 1:1:1이면 3세트(9마리)를 만들고 남는 1마리는 생성하지 않는다.
        int setCount = spawnRequest.MaxCount / totalWeight;
        List<EnemySpawnEntry> enemies = new(squadPreset.Composition.Count);

        foreach (EnemySquadCompositionEntry entry in squadPreset.Composition)
        {
            // 식별 값이나 가중치가 잘못된 항목이 있으면 부분적인 명령을 반환하지 않는다.
            if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyId) ||
                entry.Weight <= 0)
                return false;

            // 적별 가중치에 완전한 세트 수를 곱해 최종 생성 수를 확정한다.
            enemies.Add(new EnemySpawnEntry(entry.EnemyId, entry.Weight * setCount));
        }

        // 공통 분대 정보와 모든 적별 수량을 하나의 명령으로 묶는다.
        instruction = new SpawnInstruction(
            spawnRequest.SquadOrder,
            spawnRequest.SpawnPointId,
            enemies);
        return true;
    }
}
