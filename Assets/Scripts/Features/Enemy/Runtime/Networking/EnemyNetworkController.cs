// 서버가 적 행동을 실행하고 생성 시 전달받은 Match 시간/수명에 연결합니다.
// Client는 같은 Match의 읽기 시계만 사용하며 HP·행동을 결정하지 않습니다.
using System;
using TeamHJD.Game.Presentation.Scene;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Unity.Profiling;

/// <summary>
/// 네트워크에 스폰된 적의 행동을 서버에서 실행한다.
/// 위치와 행동 결과는 서버가 결정하고 네트워크 동기화 컴포넌트가 클라이언트에 전달한다.
/// </summary>
public partial class EnemyNetworkController : NetworkBehaviour
{
    [SerializeField] private EnemyController enemyController;
    [SerializeField] private EnemyHealth enemyHealth;
    private EnemyExplosionAbility _explosionAbility;
    private FieldSimulationActor _simulationActor;

    // 프로파일링
    private static readonly ProfilerMarker CanExecuteMarker =
        new("TeamHJD.Enemy.CanExecuteBehavior");

    private static readonly ProfilerMarker RefreshTargetMarker =
        new("TeamHJD.Enemy.RefreshTarget");

    private static readonly ProfilerMarker ExecuteBehaviorMarker =
        new("TeamHJD.Enemy.ExecuteBehavior");
    
    
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

            return _simulationActor == null || _simulationActor.CanAdvance;
        }
    }

    /// <summary>
    /// 적의 행동을 실행할 컨트롤러를 가져온다.
    /// </summary>
    private void Awake()
    {
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();

        _explosionAbility = GetComponent<EnemyExplosionAbility>();
    }

    /// <summary>
    /// 서버 또는 명시적으로 켠 임시 로컬 테스트에서 적의 행동을 실행한다.
    /// </summary>
    // private void FixedUpdate()
    // {
    //     if (!CanExecuteBehavior)
    //         return;
    //
    //     enemyController.RefreshTarget();
    //     enemyController.ExecuteBehavior();
    // }

    private void FixedUpdate()
    {
        using (CanExecuteMarker.Auto())
        {
            if (!CanExecuteBehavior)
                return;
        }

        using (RefreshTargetMarker.Auto())
        {
            enemyController.RefreshTarget();
        }

        using (ExecuteBehaviorMarker.Auto())
        {
            enemyController.ExecuteBehavior();
        }
    }
    
    /// <summary>
    /// 적이 네트워크에 스폰되면 서버에서 현재 체력과 사망 상태를 초기화한다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        NotifySpawned();
        if (!IsServer || !enemyHealth)
            return;

        enemyHealth.Initialize();
        enemyHealth.Died += OnHealthDied;
        SubscribeExplosionAbility();
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
    /// 서버에서 고유 사망 반응과 사망 기록 알림을 처리한 뒤 네트워크 개체를 제거한다.
    /// </summary>
    private void OnHealthDied(EnemyHealth health)
    {
        if (health != enemyHealth || !IsServer)
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

        if (IsSpawned)
            NetworkObject.Despawn();
    }

    /// <summary>
    /// 사망뿐 아니라 다른 원인으로 Despawn되어도 분대에서 해제할 수 있도록 통지한다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        NotifyDespawned();
        if (_explosionAbility != null)
            _explosionAbility.RemovalRequested -= OnExplosionRemovalRequested;

        if (enemyHealth != null)
        {
            enemyHealth.Died -= OnHealthDied;
            enemyHealth.Deactivate();
        }

        if (enemyController != null)
            enemyController.LeaveSquad();

        Despawned?.Invoke(NetworkObjectId);
        base.OnNetworkDespawn();
    }

    #region TEMP: 로컬 테스트 (검증 후 제거 가능)

    [Header("임시 로컬 테스트 — 추후 제거 가능")]
    [SerializeField, InspectorName("로컬 테스트 실행")]
    [Tooltip("네트워크 없이 이동·애니메이션을 확인합니다. EnemyTestDriver와 동시에 켜지 마세요.")]
    private bool runOffline;

    [SerializeField, InspectorName("테스트 타깃")]
    [Tooltip("테스트 씬의 타깃을 연결합니다. Play 시작 시 타깃과 이동 경로를 초기화합니다.")]
    private Transform testTarget;

    private bool IsNetworkRunning =>
        Unity.Netcode.NetworkManager.Singleton != null &&
        Unity.Netcode.NetworkManager.Singleton.IsListening;

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

    #endregion
}
