// Player 충돌 입력을 현재 Match의 Battlefield 기반 공통 Encounter 요청으로 전달합니다.
// 독립 Runtime을 만들거나 공간 검사 없는 생성 경로로 우회하지 않습니다.
using System.Linq;
using TeamHJD.Game.Contracts;
using UnityEngine;

public sealed class SpawnerTester : MonoBehaviour
{
    [SerializeField] private string _squadOrder = "Normal";
    [SerializeField, Min(1)] private int _maxEnemyCount = 10;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.gameObject.layer != LayerMask.NameToLayer("PlayerFeetBox")) return;
        
        if (!TryRequestEncounter(out string result)) Debug.LogWarning(result, this);
    }

    /// <summary>충돌과 통합 검사에서 사용하는 동일한 Scene 요청 경계입니다.</summary>
    public bool TryRequestEncounter(out string result)
    {
        IEncounterDebugCommandTarget[] targets = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(component => component != null && component.isActiveAndEnabled)
            .OfType<IEncounterDebugCommandTarget>().ToArray();
        if (targets.Length != 1)
        {
            result = $"Scene 요청에는 활성 Encounter 대상 하나가 필요합니다. 현재 {targets.Length}개.";
            return false;
        }
        return targets[0].TryRequestCurrentEncounter(_squadOrder, _maxEnemyCount,
            EncounterRequestSource.SceneCollision, out result);
    }
}
