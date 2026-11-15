using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>몸통 Trigger에 진입한 플레이어에게 서버에서 접촉 피해를 전달한다.</summary>
[DisallowMultipleComponent]
public sealed class EnemyContactDamage : MonoBehaviour
{
    // Inspector 설정과 제작 참조

    [Header("참조")]
    [SerializeField] private EnemyNetworkController networkController;
    [SerializeField] private EnemyHealth enemyHealth;

    [Header("접촉 피해")]
    [SerializeField, Min(0)] private int contactDamage = 10;
    [SerializeField, Min(0.01f)] private float contactDamageInterval = 0.5f;

    // 실행 중 참조와 상태
    private int _playerHurtboxLayer;
    private Dictionary<PlayerInfo, double> _nextDamageTimes;

    // Unity 생명주기

    /// <summary>부모 몬스터의 실행 권한·체력과 플레이어 몸통 레이어를 연결한다.</summary>
    private void Awake()
    {
        if (networkController == null)
            networkController = GetComponentInParent<EnemyNetworkController>();
        if (enemyHealth == null)
            enemyHealth = GetComponentInParent<EnemyHealth>();

        _playerHurtboxLayer = LayerMask.NameToLayer("PlayerHurtbox");
    }

    /// <summary>비활성화 전 접촉한 플레이어의 피해 간격 기록을 비운다.</summary>
    private void OnDisable()
    {
        _nextDamageTimes?.Clear();
    }

    // 내부 처리

    /// <summary>진입 시에만 처리하며, Stay나 주기적인 주변 물리 검색은 수행하지 않는다.</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || other.gameObject.layer != _playerHurtboxLayer ||
            contactDamage <= 0 || networkController == null ||
            !networkController.CanExecuteBehavior || enemyHealth == null ||
            !enemyHealth.isActiveAndEnabled || enemyHealth.CurrentHealth <= 0)
            return;

        // 상대 몸통도 Trigger이므로 isTrigger 조건으로 제외하지 않는다.
        PlayerInfo player = other.GetComponentInParent<PlayerInfo>();
        if (player == null || !player.isActiveAndEnabled || player.IsDestroyed)
            return;

        double now = FieldSimulationTime.Seconds(networkController);
        if (_nextDamageTimes != null &&
            _nextDamageTimes.TryGetValue(player, out double nextDamageTime) &&
            now < nextDamageTime)
            return;

        // 방향별 Hurtbox 교체와 여러 콜라이더의 동시 진입으로 인한 연속 피해를 제한한다.
        if (player.ApplyDamage(contactDamage))
        {
            _nextDamageTimes ??= new Dictionary<PlayerInfo, double>();
            _nextDamageTimes[player] = now + Mathf.Max(0.01f, contactDamageInterval);
        }
    }
}
