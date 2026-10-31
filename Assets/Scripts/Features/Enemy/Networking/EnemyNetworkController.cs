using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크에 스폰된 적의 행동을 서버에서 실행한다.
/// 위치와 행동 결과는 서버가 결정하고 네트워크 동기화 컴포넌트가 클라이언트에 전달한다.
/// </summary>
public class EnemyNetworkController : NetworkBehaviour
{
    [SerializeField] private EnemyController enemyController;
    [SerializeField] private EnemyHealth enemyHealth;
    public event Action<ulong> DeathConfirmed;

    /// <summary>
    /// 이 적이 네트워크에서 제거될 때 생성 주체에게 알린다.
    /// 서버의 스포너가 구독하며, 적은 스포너를 직접 참조하지 않는다.
    /// </summary>
    public event Action<ulong> Despawned;

    /// <summary>
    /// 적의 행동을 실행할 컨트롤러를 가져온다.
    /// </summary>
    private void Awake()
    {
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();
    }

    /// <summary>
    /// 서버 또는 명시적으로 켠 임시 로컬 테스트에서 적의 행동을 실행한다.
    /// </summary>
    private void FixedUpdate()
    {
        // TEMP: 로컬 테스트 제거 시 기존 IsSpawned / IsServer 검사로 복원한다.
        if (IsNetworkRunning)
        {
            if (!IsSpawned || !IsServer)
                return;
        }
        else if (!runOffline)
        {
            return;
        }

        enemyController.ExecuteBehavior();
    }

    /// <summary>
    /// 적이 네트워크에 스폰되면 서버에서 현재 체력과 사망 상태를 초기화한다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer || !enemyHealth)
            return;

        enemyHealth.Initialize();
        enemyHealth.Died += OnHealthDied;
    }

    private void OnHealthDied(EnemyHealth health)
    {
        if (health != enemyHealth || !IsServer)
            return;

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
        if (enemyHealth != null)
        {
            enemyHealth.Died -= OnHealthDied;
            enemyHealth.Deactivate();
        }

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

    private void Start()
    {
        if (!runOffline || IsNetworkRunning)
            return;

        enemyController.AssignTarget(testTarget);
    }

    #endregion
}
