using UnityEngine;

/// <summary>
/// 폭발 클립의 마지막 Animation Event에서 FX를 제거한다.
/// </summary>
public sealed class EnemyExplosionEffect : MonoBehaviour
{
    public void OnAnimationFinished()
    {
        Destroy(gameObject);
    }
}
