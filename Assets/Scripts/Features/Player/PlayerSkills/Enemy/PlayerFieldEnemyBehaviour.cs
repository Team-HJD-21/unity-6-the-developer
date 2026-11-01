using UnityEngine;

/// <summary>장비까지의 이동과 장비 공격 간격만 처리합니다.</summary>
public sealed class PlayerFieldEnemyBehaviour
{
    private double _nextAttack;
    public bool Step(Transform actor, IPlayerFieldTarget device, float speed, float range,
        float damage, float cooldown, double now, float elapsed, out Vector2 direction, out bool moving)
    {
        Vector2 delta = device.ApproachPoint(actor.position) - (Vector2)actor.position;
        direction = delta.normalized;
        moving = delta.magnitude > Mathf.Max(.1f, range);
        if (moving)
        {
            float step = Mathf.Min(Mathf.Max(0, speed) * Mathf.Max(0, elapsed),
                Mathf.Max(0, delta.magnitude - Mathf.Max(.1f, range)));
            actor.position += (Vector3)direction * step;
            return false;
        }
        if (now < _nextAttack) return false;
        _nextAttack = now + Mathf.Max(.1f, cooldown);
        device.TakeDamage(Mathf.Max(1, damage));
        return true;
    }
}
