using Unity.Netcode;
using UnityEngine;

/// <summary>
/// PoC 적의 체력과 사망을 서버 권한으로 관리한다.
/// 서버에서 받은 피해로 체력이 0이 되면 NGO를 통해 적을 네트워크에서 제거한다.
/// </summary>
public class EnemyHealth : NetworkBehaviour
{
    // 현재 체력은 서버에서만 갱신하며, 이 필드 자체는 클라이언트에 동기화하지 않는다.
    [SerializeField] private int currentHealth;
    // 적 프리팹에서 설정하는 최대 체력이다.
    [SerializeField, Min(1)] private int maxHealth = 20;
    // 같은 적에 사망 처리가 여러 번 실행되는 것을 막는다.
    private bool _isDead;
    
    /// <summary>
    /// 서버가 관리하는 현재 체력을 반환한다.
    /// </summary>
    public int CurrentHealth => currentHealth;

    /// <summary>
    /// 프리팹에 설정된 최대 체력을 반환한다.
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// 적이 네트워크에 스폰되면 서버에서 현재 체력과 사망 상태를 초기화한다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        currentHealth = Mathf.Max(1, maxHealth);
        _isDead = false;
    }

    /// <summary>
    /// 서버에서 유효한 피해를 적용하고 체력이 0 이하가 되면 사망 처리한다.
    /// 스폰되지 않았거나 이미 사망한 적에 대한 요청은 무시한다.
    /// </summary>
    /// <param name="damage">적에게 적용할 양수 피해량.</param>
    public void TakeDamage(int damage)
    {
        // 피해 확정과 사망 처리는 서버에서 한 번만 수행한다.
        if(!IsServer || !IsSpawned || _isDead || damage <= 0) return;
        
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 체력을 0으로 확정하고 서버에서 NetworkObject를 Despawn한다.
    /// NGO가 클라이언트에도 오브젝트 제거를 전파한다.
    /// </summary>
    private void Die()
    {
        currentHealth = 0;
        _isDead = true;
        NetworkObject.Despawn();
    }
}
