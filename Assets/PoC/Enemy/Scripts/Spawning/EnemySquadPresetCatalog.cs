using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 분대 명령별 프리셋을 하나의 에셋에서 관리한다.
/// Planner는 이 카탈로그를 로드한 뒤 명령 값으로 프리셋을 조회한다.
/// </summary>
[CreateAssetMenu(
    fileName = "EnemySquadPresetCatalog",
    menuName = "The Developer/Enemy/Squad Preset Catalog")]
public sealed class EnemySquadPresetCatalog : ScriptableObject
{
    /// <summary>
    /// 기본 카탈로그를 찾을 Resources 기준 경로다.
    /// </summary>
    public const string ResourcesPath = "Spawning/EnemySquadPresetCatalog";

    [SerializeField] private List<EnemySquadPreset> squadPresets = new();

    /// <summary>
    /// <see cref="ResourcesPath"/>에 등록된 프리셋 카탈로그를 로드한다.
    /// </summary>
    /// <returns>등록된 카탈로그. 경로에 에셋이 없으면 null.</returns>
    public static EnemySquadPresetCatalog Load()
    {
        return Resources.Load<EnemySquadPresetCatalog>(ResourcesPath);
    }

    /// <summary>
    /// 분대 명령과 일치하는 첫 번째 프리셋을 조회한다.
    /// </summary>
    /// <param name="squadOrder">찾을 분대 명령 값.</param>
    /// <param name="preset">조회에 성공한 프리셋. 없으면 null.</param>
    /// <returns>일치하는 프리셋이 있으면 <see langword="true"/>, 없으면 <see langword="false"/>.</returns>
    public bool TryGetPreset(string squadOrder, out EnemySquadPreset preset)
    {
        foreach (EnemySquadPreset candidate in squadPresets)
        {
            if (candidate != null && candidate.SquadOrder == squadOrder)
            {
                preset = candidate;
                return true;
            }
        }

        preset = null;
        return false;
    }
}
