// Player 소형 터렛의 실제 체력과 제거 상태를 소유합니다.
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

/// <summary>플레이어 터렛의 체력·적 충돌과 피격 틴트를 담당합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed partial class PlayerTurretHealth : MonoBehaviour, IDamageableTarget, TeamHJD.Game.Contracts.IEntityIdentity
{
    private static readonly System.Collections.Generic.HashSet<PlayerTurretHealth> _active = new();
    internal static System.Collections.Generic.IEnumerable<PlayerTurretHealth> ActiveInstances => _active;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearActive() => _active.Clear();
    [SerializeField, Min(1)] private float _maxHealth = 100;
    [SerializeField] private BoxCollider2D _blocker;
    [Tooltip("설치된 본체가 막을 레이어: PlayerFeetBox / EnemyFeetBox")]
    [SerializeField] private LayerMask _blockingLayers = (1 << 18) | (1 << 24);
    [Tooltip("몬스터 투사체가 공통 피해 계약에 도달하도록 허용합니다.")]
    [SerializeField] private LayerMask _incomingProjectileLayers = 1 << 16;
    private float _health, _hitRemaining;
    private bool _initialized;
    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;
    private PlayerDeviceContactQuery _collision;
    public float Health => _health;
    public float MaxHealth => Mathf.Max(1, _maxHealth);
    public float HealthRatio => Mathf.Clamp01(_health / MaxHealth);
    // 회수 비율을 HP로 복원할 때 생기는 미세 오차를 새 1 HP로 올림하지 않습니다.
    public int CurrentHealth => Mathf.Max(_health > 0 ? 1 : 0, Mathf.CeilToInt(_health - .0001f));
    int IDamageableTarget.MaxHealth => Mathf.CeilToInt(MaxHealth);
    public bool IsDestroyed => !IsAlive;
    public bool CanRepair => IsAlive && _health < MaxHealth && HasStateAuthority;
    public bool HasStateAuthority => Unity.Netcode.NetworkManager.Singleton == null ||
        !Unity.Netcode.NetworkManager.Singleton.IsListening || Unity.Netcode.NetworkManager.Singleton.IsServer;
    public bool IsAlive => this != null && _initialized && isActiveAndEnabled && _health > 0;
    public PlayerFieldDeviceKind Kind => PlayerFieldDeviceKind.PlayerTurret;
    public Vector3 Position => transform.position;
    public float Radius => _blocker != null ? ((Vector2)_blocker.bounds.extents).magnitude : 0f;
    private void Awake()
    {
        TeamHJD.Game.Presentation.Scene.CheckpointActorFactory.EnsureIdentity(gameObject);
        _health = MaxHealth;
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _originalColors[i] = _renderers[i].color;
        int layer = LayerMask.NameToLayer("PlayerFieldDevice");
        if (layer >= 0) gameObject.layer = layer;
        if (_blocker == null) _blocker = GetComponent<BoxCollider2D>();
        _blocker.isTrigger = false;
        // 충돌체의 크기와 오프셋은 프리팹에서 편집하며 기존 전역 충돌 제외 설정보다 우선합니다.
        int contacts = _blockingLayers.value | _incomingProjectileLayers.value;
        _blocker.includeLayers = contacts;
        _blocker.excludeLayers = ~contacts;
        _blocker.layerOverridePriority = 10;
        _collision = new PlayerDeviceContactQuery(_blocker);
        _initialized = true;
    }
    private void Update()
    {
        if (!FieldSimulationTime.CanAdvance(this)) return;
        _hitRemaining = Mathf.Max(0, _hitRemaining - FieldSimulationTime.DeltaTime(this));
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].color = _hitRemaining > 0 ? new Color(1, .6f, .6f) : _originalColors[i];
    }
    private void OnEnable() => _active.Add(this);
    private void OnDisable() => _active.Remove(this);
    private void OnDestroy() => _active.Remove(this);
    public void TakeDamage(float damage)
    {
        if (!IsAlive || !HasStateAuthority || !FieldSimulationTime.CanAdvance(this) || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        _health = Mathf.Max(0, _health - damage);
        _hitRemaining = .12f;
        if (_health > 0) { GetComponent<PlayerTurret>()?.NotifyHealthChanged(); return; }
        GetComponent<PlayerTurret>()?.ReleaseDestroyedOwner();
        if (_blocker != null) _blocker.enabled = false;
        gameObject.SetActive(false);
        PlayerFieldDevice.Dispose(gameObject);
    }
    public bool ApplyDamage(int damage)
    { float before = _health; TakeDamage(damage); return _health < before; }

    public FieldTurretRepairResult Repair(float healthPerSecond, float deltaTime)
    {
        if (!float.IsFinite(healthPerSecond) || healthPerSecond <= 0) return FieldTurretRepairResult.InvalidRepairRate;
        if (!float.IsFinite(deltaTime) || deltaTime <= 0) return FieldTurretRepairResult.InvalidDeltaTime;
        if (!HasStateAuthority) return FieldTurretRepairResult.Unauthorized;
        if (!isActiveAndEnabled || !FieldSimulationTime.CanAdvance(this)) return FieldTurretRepairResult.Inactive;
        if (!IsAlive) return FieldTurretRepairResult.Destroyed;
        if (_health >= MaxHealth) return FieldTurretRepairResult.FullHealth;
        _health = Mathf.Min(MaxHealth, _health + healthPerSecond * deltaTime);
        GetComponent<PlayerTurret>()?.NotifyHealthChanged();
        return FieldTurretRepairResult.Repaired;
    }

    internal void SetDeploymentHealth(float healthRatio)
    {
        if (!float.IsFinite(healthRatio) || healthRatio <= 0 || healthRatio > 1)
            throw new System.ArgumentOutOfRangeException(nameof(healthRatio));
        _health = MaxHealth * healthRatio;
    }
    public Vector2 ApproachPoint(Vector2 origin) => _blocker != null ? _blocker.ClosestPoint(origin) : (Vector2)transform.position;
    public bool TryGetContact(Collider2D body, out ColliderDistance2D contact)
    {
        contact = default;
        return _collision != null && _collision.TryGetContact(body, out contact);
    }
    public bool Intersects(Vector2 origin, Vector2 goal, out float fraction, Collider2D body = null)
    {
        fraction = 0;
        return _collision != null && _collision.Intersects(origin, goal, out fraction, body);
    }
}
