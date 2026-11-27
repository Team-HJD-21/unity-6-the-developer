// 제어 장치의 체력·피해 이벤트와 Host 명령 검증용 실제 상호작용 위치를 제공합니다.
// 조작 요청·Player 인증·전력 소비를 이 컴포넌트가 대신 처리하지 않습니다.
using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed partial class ControlUnitHealth : MonoBehaviour, IDamageableTarget, IControlUnitInteractionTarget, IControlUnitVitals,
    IControlUnitArmorConsumer, ITurretCommandConsumer
{
    [Header("Definition")]
    [SerializeField] private FieldTurretDefinition _definition;

    [Header("Access Points")]
    public Transform[] accessPoints;

    public UnityEvent<int, int, float> onCUHpChange = new UnityEvent<int, int, float>();

    public event Action<int, int> HealthChanged;
    public event Action Destroyed;

    private int _currentHealth;
    private bool _hasCampaignArmor;
    private ControlUnitDamageFx _damageFx;
    private MatchId _damagePresentationMatch;
    private ITurretPresentationSource _damagePresentation;
    private bool _useDamagePresentation;

    public FieldTurretDefinition Definition => _definition;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth
    {
        get
        {
            int baseHealth = _definition != null ? _definition.MaxHealth : 0;
            return _hasCampaignArmor ? (int)Math.Min(int.MaxValue, baseHealth + (baseHealth + 9L) / 10) : baseHealth;
        }
    }
    public bool IsDestroyed => _currentHealth <= 0;

    /// <summary>Host의 전장 조립부가 체크포인트 복원 전에 캠페인 연구를 적용합니다.</summary>
    public void ApplyCampaignArmor(bool researched)
    {
        if (_definition == null || _hasCampaignArmor == researched) return;
        int previousMax = MaxHealth;
        float previousRatio = previousMax > 0 ? _currentHealth / (float)previousMax : 0;
        _hasCampaignArmor = researched;
        if (previousMax > 0 && _currentHealth > 0)
            _currentHealth = FieldTurretController.RestoreHealthRatio(_currentHealth, previousMax, MaxHealth, false);
        onCUHpChange.Invoke(_currentHealth, MaxHealth, previousRatio);
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

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

        var identity = GetComponent<TeamHJD.Game.Presentation.Scene.SceneEntityIdentity>() ??
            gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.SceneEntityIdentity>();
        if (identity.EntityId.IsEmpty) identity.Initialize(new TeamHJD.Game.Domain.EntityId("world/control-unit"));
        _currentHealth = MaxHealth;
        _damageFx = new ControlUnitDamageFx(this);
    }

    private void Start()
    {
        onCUHpChange.Invoke(_currentHealth, MaxHealth, 1f);
    }

    void ITurretCommandConsumer.BindTurretCommands(MatchId matchId, PlayerId playerId,
        ITurretCommandRequester requester, ITurretPresentationSource presentation)
    {
        _useDamagePresentation = true;
        _damagePresentationMatch = matchId;
        _damagePresentation = presentation;
    }

    void ITurretCommandConsumer.UnbindTurretCommands(MatchId matchId)
    {
        if (_damagePresentationMatch != matchId) return;
        _damagePresentationMatch = default;
        _damagePresentation = null;
        _damageFx?.Clear();
    }

    private void LateUpdate()
    {
        if (_damageFx == null) return;
        if (!_useDamagePresentation)
        {
            _damageFx.Update(CurrentHealth, MaxHealth);
            return;
        }

        var state = _damagePresentation?.Snapshot?.Controller;
        if (state == null || !state.IsControlUnitAvailable)
        {
            _damageFx.Clear();
            return;
        }
        _damageFx.Update(state.ControlUnitHealth, state.ControlUnitMaxHealth);
    }

    private void OnDisable() => _damageFx?.Clear();

    public bool ApplyDamage(int damage)
    {
        if (damage <= 0 || IsDestroyed || _definition == null ||
            !TeamHJD.Game.Presentation.Scene.FieldSimulationTime.CanReceiveDamage(this))
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
