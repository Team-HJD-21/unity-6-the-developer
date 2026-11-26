// 서버가 적 행동을 실행하고 생성 시 전달받은 Match 시간/수명에 연결합니다.
// Client는 같은 Match의 읽기 시계만 사용하며 HP·행동을 결정하지 않습니다.
using System;
using TeamHJD.Game.Presentation.Scene;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크에 스폰된 적의 행동을 서버에서 실행한다.
/// 위치와 행동 결과는 서버가 결정하고 네트워크 동기화 컴포넌트가 클라이언트에 전달한다.
/// </summary>
public partial class EnemyNetworkController : NetworkBehaviour
{
    // 상수와 공통 식별자
    private const double SpawnFootDelay = 1.5d;

    // Inspector 설정과 제작 참조
    [SerializeField] private EnemyController enemyController;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyHitDeathVisual hitDeathVisual;

    [Header("스폰 충돌 유예")]
    [SerializeField] private Collider2D footCollider;
    // 기존 독립 시험에서 사용하는 로컬 실행 설정이다.

    [Header("임시 로컬 테스트 — 추후 제거 가능")]
    [SerializeField, InspectorName("로컬 테스트 실행")]
    [Tooltip("네트워크 없이 이동·애니메이션을 확인합니다. EnemyTestDriver와 동시에 켜지 마세요.")]
    private bool runOffline;
    [SerializeField, InspectorName("테스트 타깃")]
    [Tooltip("테스트 씬의 타깃을 연결합니다. Play 시작 시 타깃과 이동 경로를 초기화합니다.")]
    private Transform testTarget;

    // 실행 중 참조와 상태
    private readonly NetworkVariable<double> _footEnableAt = new(-1d);
    private bool _isWaitingForFootEnable;
    private EnemyExplosionAbility _explosionAbility;
    private EnemyMovement _agentMovement;
    private FieldSimulationActor _simulationActor;
    private readonly NetworkVariable<double> _deathStartedAt = new(-1d);
    private bool _deathPresentationStarted;
    private bool _deathDespawnRequested;

    /// <summary>서버가 체력에 의한 사망을 확정하면 생성 주체에게 한 번 알린다.</summary>
    public event Action<ulong> DeathConfirmed;

    /// <summary>
    /// 이 적이 네트워크에서 제거될 때 생성 주체에게 알린다.
    /// 서버의 스포너가 구독하며, 적은 스포너를 직접 참조하지 않는다.
    /// </summary>
    public event Action<ulong> Despawned;

    /// <summary>
    /// 서버 또는 로컬 테스트에서 현재 행동을 실행할 수 있는지 반환한다.
    /// </summary>
    public bool CanExecuteBehavior
    {
        get
        {
            if (!isActiveAndEnabled)
                return false;

            var manager = Unity.Netcode.NetworkManager.Singleton;

            if (manager != null && manager.IsListening)
            {
                if (!IsSpawned || !IsServer)
                    return false;
            }
            else if (!runOffline)
            {
                return false;
            }

            // 생성 이후 연결되는 시뮬레이션 컴포넌트도 찾을 수 있게 한다.
            // 한 번 찾으면 이후 검사에서는 저장된 참조를 사용한다.
            if (_simulationActor == null)
                TryGetComponent(out _simulationActor);

            return !(_deathPresentationStarted || (enemyHealth != null && enemyHealth.IsDead)) &&
                   (_simulationActor == null || _simulationActor.CanAdvance);
        }
    }

    private bool IsNetworkRunning =>
        Unity.Netcode.NetworkManager.Singleton != null &&
        Unity.Netcode.NetworkManager.Singleton.IsListening;

    // Unity 생명주기

    /// <summary>
    /// 적의 행동을 실행할 컨트롤러를 가져온다.
    /// </summary>
    private void Awake()
    {
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();
        if (hitDeathVisual == null)
            hitDeathVisual = GetComponent<EnemyHitDeathVisual>();

        _explosionAbility = GetComponent<EnemyExplosionAbility>();
        if (TryGetComponent(out EnemyMovement movement) && movement.UsesNavMeshAgent)
            _agentMovement = movement;

        if (footCollider == null)
        {
            Transform foot = transform.Find("Foot");
            if (foot != null)
                footCollider = foot.GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// 임시 로컬 실행을 켠 경우 체력과 폭발 제거 이벤트, 시험 타깃을 초기화한다.
    /// </summary>
    private void Start()
    {
        if (!runOffline || IsNetworkRunning)
            return;

        if (enemyHealth != null)
            enemyHealth.Initialize();

        SubscribeExplosionAbility();
        enemyController.AssignTarget(testTarget);
    }

    /// <summary>
    /// 적이 네트워크에 스폰되면 서버에서 현재 체력과 사망 상태를 초기화한다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _deathPresentationStarted = false;
        _deathDespawnRequested = false;
        hitDeathVisual?.ResetPresentation();
        if (IsServer)
        {
            _deathStartedAt.Value = -1d;
            _footEnableAt.Value = NetworkManager.ServerTime.Time + SpawnFootDelay;
        }

        _deathStartedAt.OnValueChanged += OnDeathStartedAtChanged;
        if (_deathStartedAt.Value >= 0d)
            BeginDeathPresentation(_deathStartedAt.Value);

        NotifySpawned();
        BeginSpawnFootDelay();
        if (!IsServer || !enemyHealth)
            return;

        enemyHealth.Initialize();
        enemyHealth.Died += OnHealthDied;
        enemyHealth.DamageApplied += OnHealthDamageApplied;
        SubscribeExplosionAbility();
    }

    /// <summary>
    /// 서버 또는 명시적으로 켠 임시 로컬 테스트에서 적의 행동을 실행한다.
    /// </summary>

    private void FixedUpdate()
    {
        if (!CanExecuteBehavior)
        {
            if (_agentMovement != null && enemyHealth != null && enemyHealth.IsDead)
                _agentMovement.DisableAgentMovement();
            return;
        }

        _agentMovement?.EnableAgentMovement();

        // 분대 대표만 타깃 재검색 갱신을 요청한다.
        if (enemyController.IsSquadRepresentative)
        {
            enemyController.RefreshTarget();
        }

        enemyController.ExecuteBehavior();
    }

    /// <summary>스폰 충돌 유예를 갱신하고 서버에서 사망 페이드 종료 후 제거한다.</summary>
    private void Update()
    {
        UpdateSpawnFootDelay();

        if (!IsSpawned || !IsServer || _deathStartedAt.Value < 0d || _deathDespawnRequested)
            return;

        double fadeDuration = hitDeathVisual != null
            ? hitDeathVisual.DeathFadeDuration
            : 0d;

        if (NetworkManager.ServerTime.Time - _deathStartedAt.Value < fadeDuration)
            return;

        _deathDespawnRequested = true;
        NetworkObject.Despawn();
    }

    /// <summary>
    /// 사망뿐 아니라 다른 원인으로 Despawn되어도 분대에서 해제할 수 있도록 통지한다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _agentMovement?.DisableAgentMovement();
        _isWaitingForFootEnable = false;
        _deathStartedAt.OnValueChanged -= OnDeathStartedAtChanged;
        NotifyDespawned();
        if (_explosionAbility != null)
            _explosionAbility.RemovalRequested -= OnExplosionRemovalRequested;

        if (enemyHealth != null)
        {
            enemyHealth.Died -= OnHealthDied;
            enemyHealth.DamageApplied -= OnHealthDamageApplied;
            enemyHealth.Deactivate();
        }

        if (enemyController != null)
            enemyController.LeaveSquad();

        Despawned?.Invoke(NetworkObjectId);
        base.OnNetworkDespawn();
    }

    // 내부 처리

    /// <summary>서버가 지정한 유예 시각까지 Foot만 끈다. 늦게 수신한 개체는 남은 시간만 적용한다.</summary>
    private void BeginSpawnFootDelay()
    {
        _isWaitingForFootEnable = false;
        if (footCollider == null || _deathPresentationStarted)
            return;

        _isWaitingForFootEnable = NetworkManager.ServerTime.Time < _footEnableAt.Value;
        footCollider.enabled = !_isWaitingForFootEnable;
    }

    /// <summary>유예가 끝나면 살아 있는 개체의 Foot을 켜고 대기를 종료한다.</summary>
    private void UpdateSpawnFootDelay()
    {
        if (!_isWaitingForFootEnable || !IsSpawned)
            return;

        if (NetworkManager.ServerTime.Time < _footEnableAt.Value)
            return;

        _isWaitingForFootEnable = false;
        if (footCollider == null || _deathPresentationStarted ||
            (enemyHealth != null && enemyHealth.IsDead))
            return;

        footCollider.enabled = true;
    }

    /// <summary>
    /// 폭발 종료에 따른 제거 요청을 중복 구독하지 않도록 연결한다.
    /// </summary>
    private void SubscribeExplosionAbility()
    {
        if (_explosionAbility == null)
            return;

        _explosionAbility.RemovalRequested -= OnExplosionRemovalRequested;
        _explosionAbility.RemovalRequested += OnExplosionRemovalRequested;
    }

    /// <summary>
    /// 폭발 완료 후 일반 사망 기록을 발생시키지 않고 제거한다.
    /// </summary>
    private void OnExplosionRemovalRequested()
    {
        if (IsNetworkRunning)
        {
            if (IsSpawned && IsServer)
                NetworkObject.Despawn();
        }
        else if (runOffline)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 서버에서 고유 사망 반응과 기록 알림을 처리한 뒤 페이드 종료 후 제거를 준비한다.
    /// </summary>
    private void OnHealthDied(EnemyHealth health)
    {
        if (health != enemyHealth || !IsServer || _deathStartedAt.Value >= 0d)
            return;

        // 고유 사망 반응이 실패해도 사망 기록과 네트워크 제거는 계속 처리한다.
        try
        {
            if (enemyController != null)
                enemyController.OnDeath();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        double deathTime = NetworkManager.ServerTime.Time;
        _deathStartedAt.Value = deathTime;
        BeginDeathPresentation(deathTime);

        if (DeathConfirmed != null)
            foreach (Action<ulong> observer in DeathConfirmed.GetInvocationList())
                try
                {
                    observer(NetworkObjectId);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

        if (hitDeathVisual == null || hitDeathVisual.DeathFadeDuration <= 0f)
        {
            _deathDespawnRequested = true;
            if (IsSpawned)
                NetworkObject.Despawn();
        }
    }

    /// <summary>서버에서 실제 피해가 적용된 생존 개체의 피격 연출을 전달한다.</summary>
    private void OnHealthDamageApplied(
        EnemyHealth health,
        int damage,
        TeamHJD.Game.Enemy.Contracts.AttackSourceType source)
    {
        if (health != enemyHealth || !IsServer || health.IsDead)
            return;

        hitDeathVisual?.PlayHitFlash();
        PlayHitFlashRpc();
    }

    /// <summary>서버에서 이미 실행한 피격 연출을 중복하지 않고 클라이언트에 적용한다.</summary>
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayHitFlashRpc()
    {
        if (IsServer)
            return;

        hitDeathVisual?.PlayHitFlash();
    }

    /// <summary>동기화된 사망 시각으로 각 수신 측의 연출을 시작한다.</summary>
    private void OnDeathStartedAtChanged(double previous, double current)
    {
        if (current >= 0d)
            BeginDeathPresentation(current);
    }

    /// <summary>사망 전환을 한 번만 실행하고 분대·행동을 해제한 뒤 페이드를 시작한다.</summary>
    private void BeginDeathPresentation(double deathTime)
    {
        if (_deathPresentationStarted)
            return;

        _deathPresentationStarted = true;
        _agentMovement?.DisableAgentMovement();
        enemyHealth?.Deactivate();
        enemyController?.LeaveSquad();
        GetComponent<EnemyAnimation>()?.StopMovementAnimation();

        // 고유 사망 처리를 마친 뒤 일반 능력 이벤트와 추가 발동을 막는다.
        foreach (EnemyAbility ability in GetComponents<EnemyAbility>())
            ability.enabled = false;

        hitDeathVisual?.BeginDeathFade(deathTime, NetworkManager.ServerTime.Time);
    }

}
