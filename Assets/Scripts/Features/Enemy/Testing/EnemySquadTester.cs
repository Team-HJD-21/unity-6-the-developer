using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>씬 배치 몬스터 자신을 1인 분대에 등록하는 테스트 전용 컴포넌트다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("The Developer/Enemy/Testing/Enemy Squad Tester")]
public sealed class EnemySquadTester : MonoBehaviour
{
    [Header("테스트 전용 — 1인 분대 등록")]
    [SerializeField] private EnemyTargetRule targetRule = EnemyTargetRule.PlayerOrTurret;

    [Tooltip("비워 두면 분대 중심에서 가장 가까운 유효 타깃을 찾습니다.")]
    [SerializeField] private Transform initialTarget;

    private IEnumerator Start()
    {
        // 각 몬스터의 Start에서 로컬 테스트 타깃 설정이 끝난 뒤 분대에 등록한다.
        yield return null;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer)
            yield break;

        if (!TryGetComponent(out EnemyController enemy))
        {
            Debug.LogWarning("EnemySquadTester를 몬스터 오브젝트에 부착하세요.", this);
            yield break;
        }

        var squad = new EnemySquad(EnemySquadMode.Individual, targetRule, initialTarget);
        enemy.JoinSquad(squad);
    }
}
