using UnityEngine;

/// <summary>장비까지의 이동과 장비 공격 간격만 처리합니다.</summary>
public sealed class PlayerFieldEnemyBehaviour
{
    private float _nextAttack;
    public bool Step(Transform actor, Collider2D body, IPlayerFieldTarget device, float speed, float range,
        float damage, float cooldown, float now, float elapsed, out Vector2 direction, out bool moving)
    {
        direction = Vector2.zero;
        moving = false;
        if (!device.TryGetContact(body, out var contact)) return false;
        // pointA는 적 몸통, pointB는 장비 표면입니다. normal은 B → A이므로 반대로 접근합니다.
        direction = -contact.normal;
        float attackRange = Mathf.Max(.1f, range);
        moving = contact.distance > attackRange + .001f;
        if (moving)
        {
            float step = Mathf.Min(Mathf.Max(0, speed) * Mathf.Max(0, elapsed),
                Mathf.Max(0, contact.distance - attackRange));
            actor.position += (Vector3)direction * step;
            return false;
        }
        if (now < _nextAttack) return false;
        _nextAttack = now + Mathf.Max(.1f, cooldown);
        device.TakeDamage(Mathf.Max(1, damage));
        return true;
    }
}
