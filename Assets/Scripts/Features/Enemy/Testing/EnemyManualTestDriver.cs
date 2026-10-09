using UnityEngine;

/// <summary>
/// 씬에 직접 배치한 Enemy의 타겟 추적을 확인하는 임시 테스트 드라이버.
/// 정식 분대/네트워크 스폰 흐름을 거치지 않고 Inspector에서 지정한 타겟을 따라간다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
public sealed class EnemyManualTestDriver : MonoBehaviour
{
    [SerializeField] private Transform target;

    private EnemyController enemy;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
    }

    private void Update()
    {
        if (enemy == null)
            return;

        // Play 중 Inspector에서 target을 바꾸면 다음 프레임부터 새 타겟을 추적한다.
        enemy.SetTarget(target);
        enemy.ExecuteBehavior();
    }
}
