using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;

/// <summary>플레이어 감지 Trigger 안의 적을 EnemyDetection에 등록합니다. Client는 상태를 쓰지 않습니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CircleCollider2D))]
public sealed class PlayerEnemyDetector : MonoBehaviour
{
    [SerializeField] private Transform _owner;
    [SerializeField] private CircleCollider2D _range;
    [SerializeField] private LayerMask _enemyLayers = 1 << 23; // EnemyHitBox
    private PlayerInfo _health;
    private PlayerMatchPresence _presence;
    private int _detectorId;
    private readonly List<Collider2D> _hits = new(16);
    private readonly HashSet<EnemyDetection> _registered = new();
    private readonly HashSet<EnemyDetection> _current = new();
    private readonly List<EnemyDetection> _stale = new();

    private void Awake()
    {
        if (_range == null) _range = GetComponent<CircleCollider2D>();
        if (_owner == null && _range.attachedRigidbody != null) _owner = _range.attachedRigidbody.transform;
        if (_owner != null)
        {
            _health = _owner.GetComponent<PlayerInfo>();
            _presence = _owner.GetComponent<PlayerMatchPresence>();
        }
        _detectorId = GetInstanceID();
    }

    private void OnEnable() { if (_health != null) _health.onDeath.AddListener(ClearRegistrations); }
    private void OnDisable()
    {
        if (_health != null) _health.onDeath.RemoveListener(ClearRegistrations);
        ClearRegistrations();
    }

    private bool CanDetect
    {
        get
        {
            if (!isActiveAndEnabled || _owner == null || !_owner.gameObject.activeInHierarchy ||
                _range == null || !_range.enabled || !_range.isTrigger ||
                (_health != null && (!_health.isActiveAndEnabled || _health.IsDestroyed))) return false;
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening && !manager.IsServer) return false;
            if (_presence != null && (_presence.GameplayCancellation.IsCancellationRequested ||
                (_presence.TimeSource != null && (!_presence.CanReport ||
                    _presence.Location != TeamHJD.Game.Domain.MatchPlayerLocation.Field)))) return false;
            return true;
        }
    }

    private void FixedUpdate() => RefreshDetection();

    /// <summary>현재 물리 겹침을 재조정해 이탈·Collider 교체·풀 재활성화에서도 등록을 보존/정리합니다.</summary>
    public void RefreshDetection()
    {
        if (!CanDetect) { ClearRegistrations(); return; }
        // Match 정지로 Rigidbody가 잠깐 빠질 때는 기존 탐지 상태를 유지합니다.
        if (!FieldSimulationTime.CanAdvance(this)) return;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(_enemyLayers);
        _hits.Clear();
        _range.Overlap(filter, _hits);
        _current.Clear();
        foreach (var hit in _hits)
        {
            var target = hit != null ? hit.GetComponentInParent<EnemyDetection>() : null;
            if (IsEligible(target)) _current.Add(target);
        }
        _stale.Clear();
        foreach (var target in _registered)
            if (target == null || !_current.Contains(target)) _stale.Add(target);
        foreach (var target in _stale)
        {
            _registered.Remove(target);
            if (target != null) target.UnregisterDetector(_detectorId);
        }
        foreach (var target in _current)
        {
            if (!CanDetect) { ClearRegistrations(); return; }
            _registered.Add(target);
            // EnemyDetection clears its IDs on disable. Idempotent registration
            // also restores the same pooled target while it is still in range.
            target.RegisterDetector(_detectorId);
        }
    }

    private bool IsEligible(EnemyDetection target)
    {
        if (target == null || !target.isActiveAndEnabled || target.gameObject.scene != _owner.gameObject.scene) return false;
        var health = target.GetComponent<EnemyHealth>();
        if (health != null && (!health.isActiveAndEnabled || health.CurrentHealth <= 0)) return false;
        var network = target.GetComponent<EnemyNetworkDetection>();
        if (network != null && network.NetworkManager != null && network.NetworkManager.IsListening &&
            (!network.IsSpawned || !network.IsServer)) return false;
        var playerTime = _presence != null ? _presence.TimeSource : null;
        var enemyTime = target.GetComponent<FieldSimulationActor>()?.TimeSource;
        if (playerTime == null) return enemyTime == null;
        if (enemyTime != null) return playerTime.Snapshot.MatchId.Equals(enemyTime.Snapshot.MatchId);
        var driver = target.GetComponent<EnemyNetworkController>();
        return driver != null && playerTime.Snapshot.MatchId.Value == driver.SimulationMatchId;
    }

    private void ClearRegistrations()
    {
        if (_registered.Count == 0) return;
        var targets = new EnemyDetection[_registered.Count];
        _registered.CopyTo(targets);
        _registered.Clear();
        foreach (var target in targets)
            if (target != null) target.UnregisterDetector(_detectorId);
    }
}
