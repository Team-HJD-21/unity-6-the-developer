// Match가 시작되면 첫 Encounter를 자동으로 요청합니다.
// 기존 네트워크 테스트 UI의 수동 요청도 같은 Encounter 경로로 전달합니다.

using System;
using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Match 시작과 테스트 UI의 요청을 Host 권한의 EncounterRuntime에 전달한다.
/// 첫 자동 요청은 Stage 1의 임시 정책이며 이후 진행 이벤트 계약으로 교체할 수 있다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class EnemyNetworkTestLauncher : MonoBehaviour, IEncounterDebugCommandTarget, IEncounterMatchConsumer, IGameDiagnosticsSource
{
    private const int TargetFrameRate = 120;
    private const float HostReadyTimeout = 10f;

    [SerializeField] private string initialSquadOrder = "Normal";
    [SerializeField, Min(1)] private int initialEnemyLimit = 10;
    [SerializeField, Min(0f)] private float initialEncounterDelay = 0.5f;

    private NetworkManager _networkManager;
    private EncounterRuntime _encounterRuntime;
    private MatchId _boundMatchId;
    private Func<BattlefieldSpatialSnapshot> _snapshotProvider;
    private Coroutine _initialEncounterRoutine;
    private bool _initialEncounterPending;
    private readonly Queue<GameDiagnosticEvent> _diagnostics = new Queue<GameDiagnosticEvent>();
    private int _retainedDecisionCount;
    private long _diagnosticSequence;
    private long _requestSequence;
    private double _gameStartTime;
    private double _realStartTime;
    public event Action<GameDiagnosticEvent> DiagnosticEmitted;
    public IReadOnlyList<GameDiagnosticEvent> RecentDiagnostics => _diagnostics.ToArray();
    public long DroppedDiagnosticCount { get; private set; }
    public MatchId BoundMatchId => _boundMatchId;
    public double GameSeconds => _snapshotProvider == null ? 0d : Math.Max(0d, Time.timeAsDouble - _gameStartTime);
    public double RealSeconds => _snapshotProvider == null ? 0d : Math.Max(0d, Time.realtimeSinceStartupAsDouble - _realStartTime);
    public int ActiveEnemyCount => _encounterRuntime?.Executor != null ? _encounterRuntime.Executor.CountForMatch(_boundMatchId) : 0;
    public int UnattributedEnemyCount => _encounterRuntime?.Executor != null ? _encounterRuntime.Executor.SpawnedEnemyCount - ActiveEnemyCount : 0;
    public bool IsServer => _networkManager != null && _networkManager.IsServer;
    public bool IsClient => _networkManager != null && _networkManager.IsClient;
    public bool IsListening => _networkManager != null && _networkManager.IsListening;

    /// <summary>
    /// 네트워크 관리자와 테스트 실행 환경을 초기화한다.
    /// Encounter가 사용할 분대 프리셋 카탈로그는 생성 시 한 번 로드한다.
    /// </summary>
    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        _encounterRuntime = null;

        // Host와 Client 창이 포커스를 잃어도 네트워크 테스트를 계속 실행한다.
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

    private void Start()
    {
        StartInitialEncounterIfBound();
    }

    private void OnEnable()
    {
        if (_networkManager != null) StartInitialEncounterIfBound();
    }

    private void OnDisable()
    {
        if (_initialEncounterRoutine != null) StopCoroutine(_initialEncounterRoutine);
        _initialEncounterRoutine = null;
    }

    /// <summary>Scene의 Match 수명에 묶인 첫 Encounter를 예약합니다.</summary>
    public void BindMatch(MatchId matchId, Func<BattlefieldSpatialSnapshot> snapshotProvider)
    {
        if (matchId.IsEmpty) throw new ArgumentException("Encounter requires a Match ID.", nameof(matchId));
        if (snapshotProvider == null) throw new ArgumentNullException(nameof(snapshotProvider));
        if (_snapshotProvider != null) throw new InvalidOperationException("Encounter is already bound to a Match.");

        _boundMatchId = matchId;
        _snapshotProvider = snapshotProvider;
        _gameStartTime = Time.timeAsDouble;
        _realStartTime = Time.realtimeSinceStartupAsDouble;
        _requestSequence = 0;
        EnsureEncounterRuntime();
        Emit(GameDiagnosticKind.MatchStarted, null, message: "Match bound");
        _initialEncounterPending = true;
        if (_networkManager != null) StartInitialEncounterIfBound();
    }

    /// <summary>Match 종료 시 자동 요청과 이전 snapshot 조회 경로를 해제합니다.</summary>
    public void UnbindMatch(MatchId matchId)
    {
        if (_snapshotProvider == null || _boundMatchId != matchId) return;
        CancelPendingInitialEncounter();
        Emit(GameDiagnosticKind.MatchEnded, null, message: "Match unbound; Enemy despawn is Feature-owned");
        if (_encounterRuntime?.Executor != null)
            _encounterRuntime.Executor.LifecycleObserved -= OnEnemyLifecycleObserved;
        _encounterRuntime = null;
        _snapshotProvider = null;
        _boundMatchId = default;
    }

    private void OnDestroy()
    {
        if (_snapshotProvider != null) UnbindMatch(_boundMatchId);
        if (_encounterRuntime?.Executor != null)
            _encounterRuntime.Executor.LifecycleObserved -= OnEnemyLifecycleObserved;
    }

    private void StartInitialEncounterIfBound()
    {
        if (!_initialEncounterPending || _initialEncounterRoutine != null || !isActiveAndEnabled) return;
        _initialEncounterRoutine = StartCoroutine(RunInitialEncounter());
    }

    private void CancelPendingInitialEncounter()
    {
        _initialEncounterPending = false;
        if (_initialEncounterRoutine != null) StopCoroutine(_initialEncounterRoutine);
        _initialEncounterRoutine = null;
    }

    private IEnumerator RunInitialEncounter()
    {
        // Scene Awake와 첫 동적 Grid 샘플이 끝난 뒤 최신 Match snapshot을 읽는다.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, initialEncounterDelay));
        if (!_initialEncounterPending) yield break;

        if (!CanRequestEncounter(out string reason))
        {
            Emit(GameDiagnosticKind.RequestFailed, new EncounterExecutionContext(_boundMatchId, ++_requestSequence, 0, EncounterRequestSource.MatchStart, GetInitialSquadOrder()), message: reason);
            Debug.LogError($"Initial Encounter cannot start: {reason}", this);
            _initialEncounterPending = false;
            _initialEncounterRoutine = null;
            yield break;
        }

        if (!_networkManager.IsListening && !_networkManager.StartHost())
        {
            Emit(GameDiagnosticKind.RequestFailed, new EncounterExecutionContext(_boundMatchId, ++_requestSequence, 0, EncounterRequestSource.MatchStart, GetInitialSquadOrder()), message: "Initial Host start failed");
            Debug.LogError("Initial Encounter could not start the NGO Host.", this);
            _initialEncounterPending = false;
            _initialEncounterRoutine = null;
            yield break;
        }

        float deadline = Time.unscaledTime + HostReadyTimeout;
        while (_initialEncounterPending && !_encounterRuntime.CanExecute && Time.unscaledTime < deadline)
            yield return null;

        if (_initialEncounterPending)
        {
            BattlefieldSpatialSnapshot snapshot = null;
            try { snapshot = _snapshotProvider?.Invoke(); }
            catch (ObjectDisposedException) { Debug.LogWarning("Initial Encounter Match was already disposed.", this); }
            if (snapshot == null || snapshot.MatchId != _boundMatchId || !_encounterRuntime.CanExecute)
            {
                Emit(GameDiagnosticKind.RequestFailed, new EncounterExecutionContext(_boundMatchId, ++_requestSequence, snapshot?.Revision ?? 0, EncounterRequestSource.MatchStart, GetInitialSquadOrder()), message: "Initial snapshot/Executor readiness failed");
                Debug.LogError("Initial Encounter has no current Match snapshot or server Spawn Executor.", this);
            }
            else if (!TryRequestEncounter(snapshot, GetInitialSquadOrder(),
                         Mathf.Max(1, initialEnemyLimit), EncounterRequestSource.MatchStart, out string result))
                Debug.LogWarning($"Initial Encounter failed: {result}", this);
        }

        _initialEncounterPending = false;
        _initialEncounterRoutine = null;
    }

    /// <summary>
    /// 네트워크 실행 버튼과 연결 상태를 표시한다.
    /// </summary>
    private void OnGUI()
    {
        // 네트워크 테스트 UI 영역을 생성한다.
        GUILayout.BeginArea(
            new Rect(20f, 20f, 240f, 180f),
            GUI.skin.box);

        GUILayout.Label("Enemy Network Test");

        // 실행 상태에 따라 연결 정보 또는 시작 버튼을 표시한다.
        if (_networkManager.IsListening)
            DrawConnectionStatus();
        else
            DrawLaunchButtons();

        GUILayout.EndArea();
    }

    /// <summary>
    /// 네트워크가 실행 중이 아닐 때 Host와 Client 시작 버튼을 표시한다.
    /// </summary>
    private void DrawLaunchButtons()
    {
        if (GUILayout.Button("Start Host"))
            StartHost();

        if (GUILayout.Button("Start Client"))
            StartClient();
    }

    /// <summary>
    /// 현재 네트워크 모드와 연결 정보를 표시한다.
    /// </summary>
    private void DrawConnectionStatus()
    {
        // 현재 인스턴스의 네트워크 상태를 표시한다.
        GUILayout.Label($"Mode: {GetCurrentMode()}");
        GUILayout.Label($"Client ID: {_networkManager.LocalClientId}");

        // 서버에서만 전체 접속 인원을 확인할 수 있다.
        if (_networkManager.IsServer)
        {
            GUILayout.Label($"Connected Clients: {_networkManager.ConnectedClientsIds.Count}");

            // 점령 상태를 바꾼 뒤에도 같은 Host에서 다시 계획해 확인할 수 있다.
            if (GUILayout.Button("Plan & Spawn"))
            {
                if (!TryRequestCurrentEncounter(GetInitialSquadOrder(), Mathf.Max(1, initialEnemyLimit), EncounterRequestSource.RuntimeUI, out string result))
                    Debug.LogWarning(result, this);
            }
        }

        if (GUILayout.Button("Shutdown"))
            _networkManager.Shutdown();
    }

    /// <summary>
    /// 네트워크 호스트만 시작한다. Encounter 요청은 Battlefield Debug Window 명령으로 분리한다.
    /// </summary>
    private void StartHost()
    {
        EnsureEncounterRuntime();
        if (!_encounterRuntime.HasExecutor || !_encounterRuntime.HasCatalog)
        {
            Debug.LogError("Spawner or squad preset catalog is missing for the spawn test.");
            return;
        }

        // Host 시작에 실패하면 스폰 계획을 실행하지 않는다.
        if (!_networkManager.StartHost())
        {
            Debug.LogError("Failed to start the network host.");
            return;
        }
    }

    /// <summary>Debug Window가 현재 Match snapshot을 전달하고 Encounter 요청을 시작합니다.</summary>
    public bool CanRequestEncounter(out string reason)
    {
        if (_networkManager == null)
        {
            reason = "NetworkManager가 없습니다.";
            return false;
        }

        EnsureEncounterRuntime();
        if (!_encounterRuntime.HasExecutor || !_encounterRuntime.HasCatalog)
        {
            reason = "EnemySpawnExecutor 또는 EnemySquadPresetCatalog가 없습니다.";
            return false;
        }

        if (_networkManager.IsListening && !_networkManager.IsServer)
        {
            reason = "현재 Client 인스턴스에서는 Encounter를 요청할 수 없습니다.";
            return false;
        }

        reason = "준비됨. 요청 시 Host를 시작한 뒤 Encounter를 실행합니다.";
        return true;
    }

    /// <summary>Host 권한을 확보한 뒤 Match 공간 snapshot과 테스트 요청을 EncounterRuntime에 전달합니다.</summary>
    public bool TryRequestEncounter(
        BattlefieldSpatialSnapshot snapshot,
        string squadOrder,
        int maxEnemyCount,
        out string result)
        => TryRequestEncounter(snapshot, squadOrder, maxEnemyCount, EncounterRequestSource.DebugWindow, out result);

    public bool TryRequestCurrentEncounter(string squadOrder, int maxEnemyCount,
        EncounterRequestSource source, out string result)
    {
        BattlefieldSpatialSnapshot snapshot = null;
        try { snapshot = _snapshotProvider?.Invoke(); }
        catch (ObjectDisposedException) { /* 종료된 provider는 snapshot 없는 요청으로 명확히 거부합니다. */ }
        return TryRequestEncounter(snapshot, squadOrder, maxEnemyCount, source, out result);
    }

    public bool TryRequestEncounter(BattlefieldSpatialSnapshot snapshot, string squadOrder,
        int maxEnemyCount, EncounterRequestSource source, out string result)
    {
        var context = new EncounterExecutionContext(_boundMatchId, ++_requestSequence, snapshot?.Revision ?? 0, source, squadOrder);
        Emit(GameDiagnosticKind.RequestStarted, context, message: $"preset={squadOrder}, budget={maxEnemyCount}");
        if (snapshot == null || _snapshotProvider == null || _boundMatchId.IsEmpty)
        {
            result = "활성 Match Battlefield snapshot이 없습니다.";
            Emit(GameDiagnosticKind.RequestFailed, context, message: result);
            return false;
        }

        if (_snapshotProvider != null && snapshot.MatchId != _boundMatchId)
        {
            result = "Encounter 요청의 Match ID가 현재 바인딩된 Match와 다릅니다.";
            Emit(GameDiagnosticKind.RequestFailed, context, message: result);
            return false;
        }

        if (!CanRequestEncounter(out result))
        {
            Emit(GameDiagnosticKind.RequestFailed, context, message: result);
            return false;
        }

        if (!_networkManager.IsListening && !_networkManager.StartHost())
        {
            result = "NGO Host를 시작하지 못했습니다.";
            Emit(GameDiagnosticKind.RequestFailed, context, message: result);
            return false;
        }

        EnsureEncounterRuntime();
        bool succeeded = _encounterRuntime.TrySpawn(snapshot, squadOrder, maxEnemyCount, context, out result, out EncounterExecutionSummary summary);
        Emit(succeeded ? GameDiagnosticKind.RequestCompleted : GameDiagnosticKind.RequestFailed, context,
            planned: summary.PlannedCount, actual: summary.SpawnedCount, message: summary.ResultCode + ": " + result,
            spawnDecision: summary.SpawnDecision);
        if (succeeded && snapshot.MatchId == _boundMatchId && source != EncounterRequestSource.MatchStart)
            CancelPendingInitialEncounter();
        return succeeded;
    }

    private void EnsureEncounterRuntime()
    {
        if (_encounterRuntime != null && _encounterRuntime.HasExecutor) return;
        if (_encounterRuntime?.Executor != null)
            _encounterRuntime.Executor.LifecycleObserved -= OnEnemyLifecycleObserved;
        _encounterRuntime = new EncounterRuntime();
        if (_encounterRuntime.Executor != null)
            _encounterRuntime.Executor.LifecycleObserved += OnEnemyLifecycleObserved;
    }

    private void OnEnemyLifecycleObserved(EnemyLifecycleObservation observation)
    {
        if (observation.Context == null || observation.Context.MatchId != _boundMatchId || _snapshotProvider == null) return;
        Emit(observation.Kind, observation.Context, actual: 1, enemyType: observation.EnemyType,
            point: observation.SpawnPointId, entity: observation.EntityId, message: observation.Reason);
    }

    private void Emit(GameDiagnosticKind kind, EncounterExecutionContext context, int planned = 0, int actual = 0,
        string enemyType = "", string point = "", string entity = "", string message = "", SpawnDecisionSnapshot spawnDecision = null)
    {
        var observation = new GameDiagnosticEvent(++_diagnosticSequence, _boundMatchId,
            context?.Revision ?? 0, context?.RequestId ?? 0, context?.Source ?? EncounterRequestSource.Unspecified,
            kind, GameSeconds, RealSeconds, enemyType, point, entity, planned, actual, message, context?.SquadOrder ?? "",spawnDecision,
            hasDeathEvents: true);
        if (_diagnostics.Count >= 4096)
        {
            if(_diagnostics.Dequeue().SpawnDecision!=null)_retainedDecisionCount--;
            DroppedDiagnosticCount++;
        }
        if(spawnDecision!=null)
        {
            // 오래된 이벤트 수량은 유지하되 무거운 공간 payload만 제한합니다.
            if(_retainedDecisionCount>=SpawnDecisionSnapshot.MaximumRecordedDecisions)
            {
                var buffered=_diagnostics.ToArray();_diagnostics.Clear();bool removed=false;
                foreach(var item in buffered)
                {
                    if(!removed&&item.SpawnDecision!=null)
                    {_diagnostics.Enqueue(item.WithoutSpawnDecision());removed=true;_retainedDecisionCount--;}
                    else _diagnostics.Enqueue(item);
                }
            }
            _retainedDecisionCount++;
        }
        _diagnostics.Enqueue(observation);
        if (DiagnosticEmitted == null) return;
        foreach (Action<GameDiagnosticEvent> observer in DiagnosticEmitted.GetInvocationList())
            try { observer(observation); } catch (Exception exception) { Debug.LogException(exception, this); }
    }

    /// <summary>
    /// 클라이언트 연결을 시작한다.
    /// </summary>
    private void StartClient()
    {
        if (!_networkManager.StartClient())
        {
            Debug.LogError("Failed to start the network client.");
        }
    }

    /// <summary>
    /// 현재 실행 중인 네트워크 모드 이름을 반환한다.
    /// </summary>
    private string GetCurrentMode()
    {
        if (_networkManager.IsHost)
            return "Host";

        if (_networkManager.IsServer)
            return "Server";

        if (_networkManager.IsClient)
            return "Client";

        return "Offline";
    }

    private string GetInitialSquadOrder() =>
        string.IsNullOrWhiteSpace(initialSquadOrder) ? "Normal" : initialSquadOrder.Trim();
}
