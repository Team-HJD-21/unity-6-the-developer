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
    [SerializeField] private TurretBase _turret;
    [SerializeField, Range(0f, 1f)] private float _fakeFirepowerRatio = 0.5f;

    private PoCTargetable _targetable;

    private void Reset()
    {
        _turret = GetComponentInParent<TurretBase>();
    }

    private void Awake()
    {
        _targetable = GetComponent<PoCTargetable>();
        if (_turret == null)
            _turret = GetComponentInParent<TurretBase>();

        _targetable.SetTargetType(TargetType.Turret);
        Synchronize();
    }

    private void OnEnable()
    {
        TurretInstanceRegistry.ActivationChanged += OnActivationChanged;
        TurretInstanceRegistry.HealthChanged += OnHealthChanged;
        TurretInstanceRegistry.Registered += OnTurretChanged;
        TurretInstanceRegistry.Unregistered += OnTurretChanged;
        TurretInstanceRegistry.Destroyed += OnTurretChanged;
        TurretInstanceRegistry.Restored += OnTurretChanged;
        Synchronize();
    }

    private void Start() => Synchronize();

    // Also captures initialization order, Inspector edits and component enable changes.
    private void LateUpdate() => Synchronize();

    private void OnDisable()
    {
        TurretInstanceRegistry.ActivationChanged -= OnActivationChanged;
        TurretInstanceRegistry.HealthChanged -= OnHealthChanged;
        TurretInstanceRegistry.Registered -= OnTurretChanged;
        TurretInstanceRegistry.Unregistered -= OnTurretChanged;
        TurretInstanceRegistry.Destroyed -= OnTurretChanged;
        TurretInstanceRegistry.Restored -= OnTurretChanged;
        if (_targetable != null)
            _targetable.enabled = false;
    }

    private void OnActivationChanged(TurretBase turret, bool active) => OnTurretChanged(turret);
    private void OnHealthChanged(TurretBase turret, int current, int maximum) => OnTurretChanged(turret);

    private void OnTurretChanged(TurretBase turret)
    {
        if (turret == _turret)
            Synchronize();
    }

    private void Synchronize()
    {
        if (_targetable == null)
            return;

        _targetable.SetHealth(_turret != null ? _turret.CurrentHealth : 0,
            _turret != null ? _turret.MaxHealth : 0);
        _targetable.SetFirepower(Mathf.Clamp01(_fakeFirepowerRatio), 1f);
        _targetable.enabled = isActiveAndEnabled &&
            _turret != null && _turret.isActiveAndEnabled &&
            _turret.IsActivated && !_turret.IsDestroyed && _turret.CurrentHealth > 0;
    }
}
