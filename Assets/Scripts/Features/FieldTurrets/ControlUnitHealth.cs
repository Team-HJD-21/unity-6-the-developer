// 제어 장치의 체력·피해 이벤트와 Host 명령 검증용 실제 상호작용 위치를 제공합니다.
// 조작 요청·Player 인증·전력 소비를 이 컴포넌트가 대신 처리하지 않습니다.
using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ControlUnitHealth : MonoBehaviour, IDamageableTarget, IControlUnitInteractionTarget, IControlUnitHealthSource
{
    [Header("Definition")]
    [SerializeField] private FieldTurretDefinition _definition;

    [Header("Access Points")]
    public Transform[] accessPoints;

    public UnityEvent<int, int, float> onCUHpChange = new UnityEvent<int, int, float>();

    public event Action<int, int> HealthChanged;
    public event Action Destroyed;

    private int _currentHealth;

    public FieldTurretDefinition Definition => _definition;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _definition != null ? _definition.MaxHealth : 0;
    public bool IsDestroyed => _currentHealth <= 0;
    public bool TryCaptureInteractionPosition(out BattlefieldPoint position)
    {
        position = default; Vector3 current = transform.position;
        if (!isActiveAndEnabled || _definition == null || IsDestroyed ||
            float.IsNaN(current.x) || float.IsInfinity(current.x) || float.IsNaN(current.y) || float.IsInfinity(current.y)) return false;
        position = new BattlefieldPoint(current.x, current.y); return true;
    }

    private void Awake()
    {
        if (_definition == null)
        {
            Debug.LogError("Control Unit Definition is missing.", this);
            enabled = false;
            return;
        }

        if (_definition.Role != FieldTurretRole.ControlUnit)
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
