using Unity.Netcode;
using UnityEngine;

public class EnemyHealth : NetworkBehaviour
{
    [SerializeField] private int currentHealth;
    [SerializeField, Min(1)] private int maxHealth = 20;
    private bool _isDead;
    
    
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        currentHealth = Mathf.Max(1, maxHealth);
        _isDead = false;
    }

    public void TakeDamage(int damage)
    {
        if(!IsServer || !IsSpawned || _isDead || damage <= 0) return;
        
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        currentHealth = 0;
        _isDead = true;
        NetworkObject.Despawn();
    }
}
