using System.Collections.Generic;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>폭발 범위의 적에게 대상당 한 번만 피해를 적용합니다.</summary>
public sealed class PlayerBombAreaDamage
{
    private readonly HashSet<EnemyHealth> _hitEnemies = new();
    // Legacy 제거로 인해 로직 무효화: 구형 Monster별 피해 중복 방지 목록.
    // private readonly HashSet<Monster> _hitMonsters = new();
    public void Reset() { _hitEnemies.Clear(); }
    public void Apply(Vector3 position, float radius, float damage)
    {
        foreach (var hit in Physics2D.OverlapCircleAll(position, Mathf.Max(.01f, radius)))
        {
            if (hit == null) continue;
            var health = hit.GetComponentInParent<EnemyHealth>();
            if (health != null)
            {
                if (health.CurrentHealth > 0 && _hitEnemies.Add(health))
                    health.TryTakeDamage(Mathf.RoundToInt(Mathf.Max(0f, damage)), AttackSourceType.Player);
                continue;
            }
            // Legacy 제거로 인해 로직 무효화: 구형 Monster 피해 폴백.
            // var monster = hit.GetComponentInParent<Monster>();
            // if (monster != null && !monster.isDead && _hitMonsters.Add(monster)) monster.TakeDamage(Mathf.Max(0f, damage));
        }
    }
}
