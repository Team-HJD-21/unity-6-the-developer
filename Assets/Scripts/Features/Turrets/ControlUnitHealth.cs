using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ControlUnitHealth : MonoBehaviour, IDamageableTarget
{
    [Header("Definition")]
    [SerializeField] private TurretDefinition _definition;

    [Header("Access Points")]
    public Transform[] accessPoints;

    public UnityEvent<int, int, float> onCUHpChange = new UnityEvent<int, int, float>();

    public event Action<int, int> HealthChanged;
    public event Action Destroyed;

    private int _currentHealth;

    public TurretDefinition Definition => _definition;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _definition != null ? _definition.MaxHealth : 0;
    public bool IsDestroyed => _currentHealth <= 0;

    private void Awake()
    {
        if (_definition == null)
        {
            Debug.LogError("Control Unit Definition is missing.", this);
            enabled = false;
            return;
        }

        if (_definition.Role != TurretRole.ControlUnit)
        {
            Debug.LogError($"Definition '{_definition.name}' is not a Control Unit definition.", this);
            enabled = false;
            return;
        }

        _currentHealth = MaxHealth;
    }

    private void Start()
    {
        onCUHpChange.Invoke(_currentHealth, MaxHealth, 1f);
    }

    public bool ApplyDamage(int damage)
    {
        if (damage <= 0 || IsDestroyed || _definition == null)
        {
            return false;
        }

        float previousRatio = MaxHealth > 0 ? _currentHealth / (float)MaxHealth : 0f;
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        onCUHpChange.Invoke(_currentHealth, MaxHealth, previousRatio);
        HealthChanged?.Invoke(_currentHealth, MaxHealth);

        if (IsDestroyed)
        {
            Destroyed?.Invoke();
        }

        return true;
    }

    // UnityEvent persistent calls require a void method.
    public void ApplyDamageFromEvent(int damage)
    {
        ApplyDamage(damage);
    }

}
