using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

public static class EnemyDamage
{
#if UNITY_EDITOR
    public static bool ProtectTestTargets { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTestProtection() => ProtectTestTargets = false;
#endif

    internal static bool IsTestProtectionActive
    {
        get
        {
#if UNITY_EDITOR
            return ProtectTestTargets && Application.isPlaying;
#else
            return false;
#endif
        }
    }

    internal static bool TryApply(IDamageableTarget target, int damage)
    {
        if (target == null)
            return false;

        if (IsTestProtectionActive)
            return damage > 0 && !target.IsDestroyed;

        return target.ApplyDamage(damage);
    }
}
