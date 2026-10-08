using UnityEngine;

/// <summary>스폰된 서버 적의 AI 실행권을 잠시 확보하고 원래 활성 상태를 복구합니다.</summary>
public sealed class PlayerFieldEnemyControlLease
{
    private readonly EnemyNetworkController _driver;
    private readonly EnemyHealth _health;
    private readonly Rigidbody2D _body;
    private bool _wasEnabled;
    public bool IsHeld { get; private set; }
    public PlayerFieldEnemyControlLease(GameObject actor)
    {
        _driver = actor.GetComponent<EnemyNetworkController>();
        _health = actor.GetComponent<EnemyHealth>();
        _body = actor.GetComponent<Rigidbody2D>();
    }
    public bool CanControl => _driver != null && _driver.IsSpawned && _driver.IsServer &&
        (_driver.enabled || IsHeld) && _health != null && _health.IsSpawned && _health.CurrentHealth > 0;
    public bool TryAcquire()
    {
        if (!CanControl) return false;
        if (IsHeld) return true;
        _wasEnabled = _driver.enabled;
        IsHeld = true;
        _driver.enabled = false;
        if (_body != null) _body.linearVelocity = Vector2.zero;
        return true;
    }
    public void Release()
    {
        if (!IsHeld) return;
        IsHeld = false;
        if (_driver != null) _driver.enabled = _wasEnabled;
    }
}
