using UnityEngine;
using TeamHJD.Game.Enemy.Contracts;

public class ArcaneOrbProjectile : EnemyProjectileWithImpactEffect
{
    [SerializeField, Range(0.05f, 1f)]
    private float slowMultiplier = 0.8f;

    [SerializeField, Min(0.01f)]
    private float slowDuration = 2f;
    protected override void OnHit(ITargetable target)
    {
        base.OnHit(target); // EnemyDamageProjectile의 기본 피해
        ApplySlow(target);
    }

    private void ApplySlow(ITargetable target)
    {
        if (target == null ||
            !target.CanBeTargeted ||
            target.TargetTransform == null)
            return;

        var receiver = target.TargetTransform
            .GetComponentInParent<ISlowEffectReceiver>();

        receiver?.ApplySlow(slowMultiplier, slowDuration);
    }
}
