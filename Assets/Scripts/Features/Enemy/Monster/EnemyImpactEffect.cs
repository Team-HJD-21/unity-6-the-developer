using UnityEngine;

/// <summary>
/// 적중 효과 클립의 마지막 Animation Event에서 FX를 제거한다.
/// </summary>
public sealed class EnemyImpactEffect : MonoBehaviour
{
    public void OnAnimationFinished()
    {
        Destroy(gameObject);
    }
}
