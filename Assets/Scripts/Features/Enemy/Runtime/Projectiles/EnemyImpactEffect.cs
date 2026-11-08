using UnityEngine;

/// <summary>
/// 적중 효과 클립의 마지막 Animation Event에서 FX를 제거한다.
/// </summary>
public sealed class EnemyImpactEffect : MonoBehaviour
{
    /// <summary>
    /// 효과 클립의 종료 이벤트에서 생성된 효과 오브젝트를 제거한다.
    /// </summary>
    public void OnAnimationFinished()
    {
        Destroy(gameObject);
    }
}
