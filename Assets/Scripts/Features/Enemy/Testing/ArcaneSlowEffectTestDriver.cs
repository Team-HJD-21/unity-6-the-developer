using TeamHJD.Game.Enemy.Effects;
using UnityEngine;

/// <summary>테스트 씬에서 리시버 없이 감속 연출을 켠다. Play 중 대상을 움직여 잔상을 확인한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ArcaneSlowEffect))]
public sealed class ArcaneSlowEffectTestDriver : MonoBehaviour
{
    [SerializeField] private ArcaneSlowEffect slowEffect;

    private void Reset()
    {
        slowEffect = GetComponent<ArcaneSlowEffect>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (slowEffect == null)
            slowEffect = GetComponent<ArcaneSlowEffect>();

        slowEffect.SetSlowActive(true);
    }

    private void OnDisable()
    {
        if (slowEffect != null)
            slowEffect.SetSlowActive(false);
    }
}
