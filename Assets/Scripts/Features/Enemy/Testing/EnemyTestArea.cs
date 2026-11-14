using UnityEngine;

/// <summary>테스트 창에서 함께 선택할 출현 지점과 표적을 묶고 Scene 뷰에 관찰 구역을 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class EnemyTestArea : MonoBehaviour
{
    [SerializeField] private SpawnPoint spawnPoint;
    [SerializeField] private TargetableComponent initialTarget;
    [SerializeField] private Vector2 areaSize = new(28f, 20f);
    public SpawnPoint SpawnPoint => spawnPoint;
    public TargetableComponent InitialTarget => initialTarget;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, areaSize.y, 0f));
        if (spawnPoint != null && initialTarget != null)
            Gizmos.DrawLine(spawnPoint.Position, initialTarget.transform.position);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (areaSize.y * 0.5f), name);
#endif
    }
}
