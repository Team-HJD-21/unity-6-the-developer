using TeamHJD.Game.Turrets;
using UnityEngine;

/// <summary>
/// Copies turret state into the Enemy PoC target component.
/// Firepower remains a temporary ratio until its calculation is agreed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PoCTargetable))]
public sealed class TurretTargetableAdapter : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _fakeFirepowerRatio = 0.5f;

    private PoCTargetable _targetable;
    private int _instanceId;

    private void Awake()
    {
        _targetable = GetComponent<PoCTargetable>();
        _targetable.SetTargetType(TargetType.Turret);
        Synchronize();
    }

    private void OnEnable()
    {
        TurretInstanceRegistry.SnapshotChanged += OnSnapshotChanged;
        Synchronize();
    }

    private void Start() => Synchronize();

    // Also captures initialization order, Inspector edits and component enable changes.
    private void LateUpdate() => Synchronize();

    private void OnDisable()
    {
        TurretInstanceRegistry.SnapshotChanged -= OnSnapshotChanged;
        if (_targetable != null)
            _targetable.enabled = false;
    }

    private void OnSnapshotChanged(int instanceId)
    {
        if (_instanceId == 0)
            TurretInstanceRegistry.TryGetInstanceId(transform, out _instanceId);

        if (instanceId == _instanceId)
            Synchronize();
    }

    private void Synchronize()
    {
        if (_targetable == null)
            return;

        if (_instanceId == 0)
            TurretInstanceRegistry.TryGetInstanceId(transform, out _instanceId);

        TurretSnapshot snapshot = default;
        bool hasSnapshot = _instanceId > 0 &&
            TurretInstanceRegistry.TryGetSnapshot(_instanceId, out snapshot);
        _targetable.SetHealth(hasSnapshot ? snapshot.CurrentHealth : 0,
            hasSnapshot ? snapshot.MaxHealth : 0);
        _targetable.SetFirepower(Mathf.Clamp01(_fakeFirepowerRatio), 1f);
        _targetable.enabled = isActiveAndEnabled &&
            hasSnapshot && snapshot.IsActivated && !snapshot.IsDestroyed &&
            snapshot.CurrentHealth > 0;
    }
}
