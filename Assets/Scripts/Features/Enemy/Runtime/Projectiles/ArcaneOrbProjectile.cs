using UnityEngine;
using TeamHJD.Game.Enemy.Contracts;

/// <summary>
/// 직격 피해를 준 뒤 대상의 감속 수신 계약에 효과를 전달한다.
/// </summary>
public class ArcaneOrbProjectile : EnemyProjectileWithImpactEffect
{
    [SerializeField, Range(0.05f, 1f)]
    private float slowMultiplier = 0.8f;

    [SerializeField, Min(0.01f)]
    private float slowDuration = 2f;
    /// <summary>
    /// 기본 직격 피해를 적용한 뒤 대상의 감속 수신자에 효과를 전달한다.
    /// </summary>
    protected override void OnHit(ITargetable target)
    {
        base.OnHit(target); // EnemyDamageProjectile의 기본 피해
        ApplySlow(target);
    }

    /// <summary>
    /// 타깃의 부모 계층에서 감속 수신자를 찾아 배율과 지속시간을 전달한다.
    /// </summary>
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
