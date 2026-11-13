// Scene의 실제 Enemy 구성과 Core Match·요청·진단·저장 연결을 조립합니다.
// 네 Step의 실행은 EncounterPipeline이 맡고 수동 테스트 UI는 별도 도구로 분리합니다.
// App 소유 연결을 직접 개설·종료하지 않으며 Match 해제 때 구독과 요청을 정리합니다.

using TeamHJD.Game.Enemies.Encounter;
using System;
using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap.SceneComposition;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 현재 Scene의 Match 바인딩과 검증된 요청을 EncounterPipeline에 전달한다.
/// 정식 생산은 Core MatchCoordinator가 소유하고 독립 시험 Scene만 첫 자동 요청을 사용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed partial class EncounterSceneBinding : MonoBehaviour, IEncounterDebugCommandTarget, IAppOwnedEncounterMatchConsumer, IGameDiagnosticsSource, IOriginAwareEncounterExecutor, IEncounterRequestService, IRandomConsumer, IEncounterProductionConsumer
{
    private const float HostReadyTimeout = 10f;

    [SerializeField, InspectorName("자동 Host 시작")]
    [Tooltip("끄면 Start Host / Start Client 버튼에서 실행 방식을 직접 선택합니다.")]
    private bool autoStartHost = true;

    [SerializeField] private string initialSquadOrder = "Normal";
    [SerializeField, Min(1)] private int initialEnemyLimit = 500;

    [Header("전체 스폰 예산")]
    [SerializeField] private RegionSceneBinding regionBinding;
    [Tooltip("점유율 0%일 때 사용하는 요청당 예산입니다.")]
    [SerializeField, Min(1), InspectorName("기본 예산")] private int spawnBudget = 20;
    [SerializeField, Min(1), InspectorName("최대 예산")] private int maximumSpawnBudget = 100;
    [SerializeField, Min(1)]
    [Tooltip("Static 모드에서 스폰 요청 1회당 증가할 수 있는 최대 예산")]
    private int staticBudgetIncreasePerRequest = 5;
    [SerializeField, Min(0f)] private float initialEncounterDelay = 0.5f;

    [Header("편성 선택 규칙")]
    [SerializeField] private SquadSelectionSettings selectionSettings = new();
    public SquadSelectionSettings SelectionSettings => selectionSettings;

    private NetworkManager _networkManager;
    private EncounterPipeline _pipeline;
    private MatchId _boundMatchId;
    private Func<BattlefieldSpatialSnapshot> _snapshotProvider;
    private EncounterRequestService _requests;
    private IRandomSource _squadRandom;
    private readonly EncounterBudgetSettings _runtimeBudgetSettings = new();
    private EnemySpawnExecutor _spawnExecutor;
    private EnemySquadPresetCatalog _squadCatalog;
    private static readonly PlayerId LocalIssuer = new PlayerId("local-player");
    private PlayerId _authorityIssuer = LocalIssuer;
    private bool _appOwnedNetwork;
    private EnemyNetworkTestLauncher _standalone;
    private Coroutine _initialEncounterRoutine;
    private bool _initialEncounterPending;
    private bool _matchOwnsProduction;
    private double _initialEncounterDue;
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
    public double GameSeconds => _snapshotProvider == null ? 0d : _spawnClock?.ElapsedSeconds ?? Math.Max(0d, Time.timeAsDouble - _gameStartTime);
    public double RealSeconds => _snapshotProvider == null ? 0d : Math.Max(0d, Time.realtimeSinceStartupAsDouble - _realStartTime);
    public int ActiveEnemyCount => _pipeline?.Executor != null ? _pipeline.Executor.CountForMatch(_boundMatchId) : 0;
    public int UnattributedEnemyCount => _pipeline?.Executor != null ? _pipeline.Executor.SpawnedEnemyCount - ActiveEnemyCount : 0;
    public bool IsServer => _networkManager != null && _networkManager.IsServer;
    public bool IsClient => _networkManager != null && _networkManager.IsClient;
    public bool IsListening => _networkManager != null && _networkManager.IsListening;

    /// <summary>편성 선택에 사용할 Match 난수를 연결한다.</summary>
    public IDisposable BindRandom(IRandomStreams random)
    {
        return RandomBinding.Bind(random, "enemy/squad-selection", ref _squadRandom);
    }

    /// <summary>
    /// 네트워크 관리자와 테스트 실행 환경을 초기화한다.
    /// Encounter가 사용할 분대 프리셋 카탈로그는 생성 시 한 번 로드한다.
    /// </summary>
    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        _pipeline = null;

        _standalone = new EnemyNetworkTestLauncher(this, _networkManager);
    }

    private void OnGUI() { if (!_appOwnedNetwork) _standalone?.Draw(); }

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
        CancelScheduledEncounters();
        if (_initialEncounterRoutine != null) StopCoroutine(_initialEncounterRoutine);
        _initialEncounterRoutine = null;
    }

    /// <summary>Scene의 Match 수명에 묶인 첫 Encounter를 예약합니다.</summary>
    public void BindMatch(MatchId matchId, Func<BattlefieldSpatialSnapshot> snapshotProvider)
        => BindAuthorityMatch(matchId, LocalIssuer, snapshotProvider);

    /// <summary>
    /// App이 네트워크 시작과 Match 수명을 관리하도록 런처의 독립 실행 경로를 전환한다.
    /// </summary>
    public void UseAppNetworkOwnership()
    {
        if (_snapshotProvider != null && !_appOwnedNetwork)
            throw new InvalidOperationException("Transfer connection ownership before binding a Match.");
        _appOwnedNetwork = true;
        _standalone?.Dispose();
    }

    /// <summary>
    /// Core에서 전달한 권위 Match와 최신 전장 조회 경로를 연결한다.
    /// </summary>
    public void BindAuthorityMatch(MatchId matchId, PlayerId authorityPlayerId,
        Func<BattlefieldSpatialSnapshot> snapshotProvider)
    {
        if (matchId.IsEmpty) throw new ArgumentException("Encounter requires a Match ID.", nameof(matchId));
        if (authorityPlayerId.IsEmpty) throw new ArgumentException("An authority participant is required.", nameof(authorityPlayerId));
        if (snapshotProvider == null) throw new ArgumentNullException(nameof(snapshotProvider));
        if (_snapshotProvider != null) throw new InvalidOperationException("Encounter is already bound to a Match.");
        if (_environmentProvider != null && _environmentMatch != matchId)
            throw new InvalidOperationException("Environment and Encounter must belong to the same Match.");
        if (!_spawnContextMatch.IsEmpty && _spawnContextMatch != matchId)
            throw new InvalidOperationException("Spawn sources and Encounter must belong to the same Match.");

        _boundMatchId = matchId;
        _authorityIssuer = authorityPlayerId;
        _snapshotProvider = snapshotProvider;
        // 자동 Encounter는 Host 시스템 요청입니다. Client의 임의 적 생성 권한은 부여하지 않습니다.
        _requests = new EncounterRequestService(matchId, snapshotProvider,
            () => _networkManager != null && _networkManager.IsServer,
            issuer => issuer == _authorityIssuer, this, environment: _environmentProvider,
            resolveOrigin: origin => _spawnSources?.Resolve(origin));
        _gameStartTime = Time.timeAsDouble;
        _realStartTime = Time.realtimeSinceStartupAsDouble;
        _requestSequence = 0;
        EnsurePipeline();
        if (!_appOwnedNetwork && _pipeline?.Executor != null)
            _standalone.BindOwner(_pipeline.Executor.gameObject.scene);
        Emit(GameDiagnosticKind.MatchStarted, null, message: "Match bound");
        _initialEncounterPending = !_matchOwnsProduction;
        _initialEncounterDue = GameSeconds + Math.Max(0, initialEncounterDelay);
        if (_networkManager != null) StartInitialEncounterIfBound();
    }

    /// <summary>Match 종료 시 자동 요청과 이전 snapshot 조회 경로를 해제합니다.</summary>
    public void UnbindMatch(MatchId matchId)
    {
        if (_snapshotProvider == null || _boundMatchId != matchId) return;
        CancelPendingInitialEncounter();
        CancelScheduledEncounters();
        _requests?.Dispose();
        _requests = null;
        try
        {
            if (_appOwnedNetwork) _pipeline?.Executor?.DespawnForMatch(matchId);
            // standalone 테스트만 연결을 닫습니다. App의 연결은 다음 Match에서도 유지됩니다.
            else if (_standalone != null && _standalone.HasOwner && _networkManager != null)
                _networkManager.Shutdown();
            Emit(GameDiagnosticKind.MatchEnded, null, message: "Match unbound; Feature actors released");
        }
        finally
        {
            if (_pipeline?.Executor != null)
            {
                _pipeline.Executor.LifecycleObserved -= OnEnemyLifecycleObserved;
                if (ReferenceEquals(_pipeline.Executor.SummonRequests, this)) _pipeline.Executor.SummonRequests = null;
                _pipeline.Executor.SpawnPointList.ResetRegionGroups();
            }
            _pipeline = null;
            _snapshotProvider = null;
            UnbindEnvironment(matchId);
            UnbindSpawnContext(matchId);
            _boundMatchId = default;
        }
    }

    private void OnDestroy()
    {
        if (_snapshotProvider != null) UnbindMatch(_boundMatchId);
        _standalone?.Dispose();
        if (_pipeline?.Executor != null)
            _pipeline.Executor.LifecycleObserved -= OnEnemyLifecycleObserved;
    }

    /// <inheritdoc />
    public void UseMatchProduction()
    {
        if (_snapshotProvider != null) throw new InvalidOperationException("Select the production owner before binding a Match.");
        _matchOwnsProduction = true;
        CancelPendingInitialEncounter();
    }

    private void StartInitialEncounterIfBound()
    {
        if (_matchOwnsProduction || !_initialEncounterPending || _initialEncounterRoutine != null || !isActiveAndEnabled) return;
        _initialEncounterRoutine = StartCoroutine(RunInitialEncounter());
    }

    private void CancelPendingInitialEncounter()
    {
        _initialEncounterPending = false;
        if (_initialEncounterRoutine != null) StopCoroutine(_initialEncounterRoutine);
        _initialEncounterRoutine = null;
    }

    /// <summary>
    /// Match와 씬 실행 구성이 준비될 때까지 기다린 뒤 초기 고정 편성을 한 번 요청한다.
    /// </summary>
    private IEnumerator RunInitialEncounter()
    {
        // Scene Awake와 첫 동적 Grid 샘플이 끝난 뒤 최신 Match snapshot을 읽는다.
        while (_initialEncounterPending && (!FieldCanAdvance || GameSeconds < _initialEncounterDue)) yield return null;
        if (!_initialEncounterPending) yield break;

        // 임시 Scene 시작 정책만 연결을 명시적으로 엽니다. 개별 Spawn 요청은 연결을 열지 않습니다.
        if (!_appOwnedNetwork && _networkManager != null && !_networkManager.IsListening)
        {
            if (autoStartHost)
                _standalone.StartHost();
            else
            {
                // 기존 시작 버튼에서 연결 방식을 선택할 때까지 기다린다.
                while (_initialEncounterPending && !_networkManager.IsListening)
                    yield return null;

                if (!_initialEncounterPending)
                    yield break;
            }
        }

        // Client로 시작한 창은 서버의 스폰 결과를 수신한다.
        if (!_appOwnedNetwork && _networkManager != null &&
            _networkManager.IsListening && !_networkManager.IsServer)
        {
            _initialEncounterPending = false;
            _initialEncounterRoutine = null;
            yield break;
        }

        if (!CanRequestEncounter(out string reason))
        {
            Emit(GameDiagnosticKind.RequestFailed, new EncounterExecutionContext(_boundMatchId, ++_requestSequence, 0, EncounterRequestSource.MatchStart, GetInitialSquadOrder()), message: reason);
            Debug.LogError($"Initial Encounter cannot start: {reason}", this);
            _initialEncounterPending = false;
            _initialEncounterRoutine = null;
            yield break;
        }

        float deadline = Time.unscaledTime + HostReadyTimeout;
        while (_initialEncounterPending && !_pipeline.CanExecute && Time.unscaledTime < deadline)
            yield return null;

        if (_initialEncounterPending)
        {
            BattlefieldSpatialSnapshot snapshot = null;
            try { snapshot = _snapshotProvider?.Invoke(); }
            catch (ObjectDisposedException) { Debug.LogWarning("Initial Encounter Match was already disposed.", this); }
            if (snapshot == null || snapshot.MatchId != _boundMatchId || !_pipeline.CanExecute)
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

    /// <summary>Debug Window가 현재 Match snapshot을 전달하고 Encounter 요청을 시작합니다.</summary>
    public bool CanRequestEncounter(out string reason)
    {
        if (!FieldCanAdvance) { reason = "필드 정지 중에는 새 Encounter를 실행하지 않습니다."; return false; }
        if (_networkManager == null)
        {
            reason = "NetworkManager가 없습니다.";
            return false;
        }

        if (!ResolveSpawnSetup())
        {
            reason = "EnemySpawnExecutor 또는 EnemySquadPresetCatalog가 없습니다.";
            return false;
        }
        EnsurePipeline();

        if (!_networkManager.IsListening || !_networkManager.IsServer)
        {
            reason = "Host/Server를 명시적으로 시작한 뒤 Encounter를 요청하세요.";
            return false;
        }

        reason = "Host 준비됨. 현재 Match의 최신 Snapshot으로 요청합니다.";
        return true;
    }

    /// <summary>현재 전장 상황과 지역 점유율로 산출한 예산으로 분대 생성을 요청한다.</summary>
    public bool RequestSpawn(out string result)
    {
        if (!CanRequestEncounter(out result)) return false;
        if (_requests == null || _boundMatchId.IsEmpty || _authorityIssuer.IsEmpty)
        { result = "현재 Match 요청 서비스가 준비되지 않았습니다."; return false; }
        var request = EncounterRequest.Automatic(
            new MatchRequestContext(_boundMatchId, Guid.NewGuid(), _authorityIssuer),
            initialEnemyLimit, EncounterRequestSource.RuntimeUI);
        EncounterRequestResult response = Submit(request, _authorityIssuer);
        result = response.Reason;
        return response.HasSpawned;
    }

    /// <summary>
    /// 현재 Region Binding에서 읽은 자료가 권위 Match에 속하는지 확인한다.
    /// </summary>
    private bool TryReadRegion(out RegionGridSnapshot region, out string reason)
    {
        region = null;
        try
        {
            region = regionBinding != null ? regionBinding.Source?.Snapshot : null;
        }
        catch (ObjectDisposedException)
        {
            reason = "지역 정보의 Match가 종료되었습니다.";
            return false;
        }

        if (region == null)
        {
            reason = "현재 Match의 지역 정보가 연결되지 않았습니다.";
            return false;
        }

        // Registry가 지점 갱신을 처리하고, 새 Region 정의가 오면 그룹을 재구성한다.
        if (_pipeline?.Executor != null &&
            region.MatchId == _boundMatchId)
        {
            var registry = _pipeline.Executor.SpawnPointList;
            if (region.Mode == RegionPolicyMode.Static)
            {
                registry.InitializeRegionGroups(region);
            }
            else if (registry.IsRegionGroupsInitialized)
            {
                // Dynamic 전환 시 이전 Static Region의 그룹과 제한을 해제한다.
                registry.ResetRegionGroups();
            }
        }

        reason = null;
        return true;
    }

    /// <summary>Host 권한을 확보한 뒤 Match 공간 snapshot과 고정 프리셋 요청을 전달합니다.</summary>
    public bool TryRequestEncounter(
        BattlefieldSpatialSnapshot snapshot,
        string squadOrder,
        int maxEnemyCount,
        out string result)
        => TryRequestEncounter(snapshot, squadOrder, maxEnemyCount, EncounterRequestSource.DebugWindow, out result);

    /// <summary>
    /// 현재 Match의 최신 전장 자료로 지정된 편성 요청을 공통 서비스에 전달한다.
    /// </summary>
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
        if (snapshot == null || _requests == null || snapshot.MatchId != _boundMatchId)
        {
            result = "현재 바인딩된 Match가 아닙니다.";
            ReportRejectedRequest(source, squadOrder, result, Guid.NewGuid(), snapshot: snapshot);
            return false;
        }
        if (string.IsNullOrWhiteSpace(squadOrder) || squadOrder.Trim().Length > 128 || maxEnemyCount < 1 || !Enum.IsDefined(typeof(EncounterRequestSource), source))
        {
            result = "Preset·생성 예산·출처를 확인하세요.";
            ReportRejectedRequest(source, squadOrder, result, Guid.NewGuid(), snapshot: snapshot);
            return false;
        }
        var request = new EncounterRequest(new MatchRequestContext(_boundMatchId, Guid.NewGuid(), _authorityIssuer),
            new DefinitionId(squadOrder.Trim()), maxEnemyCount, source);
        EncounterRequestResult response = Submit(request, _authorityIssuer);
        result = response.Reason;
        return response.HasSpawned;
    }

    /// <summary>
    /// 검증된 요청자를 공통 Encounter 서비스에 전달하고 요청 결과를 반환한다.
    /// </summary>
    public EncounterRequestResult Submit(EncounterRequest request, PlayerId verifiedIssuer)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var response = _requests != null ? _requests.Submit(request, verifiedIssuer) :
            new EncounterRequestResult(request.Context.RequestId, 0, 0, EncounterRequestStatus.MatchUnavailable);
        if (!response.IsReplay && response.Status != EncounterRequestStatus.InProgress &&
            response.Status != EncounterRequestStatus.Spawned && response.Status != EncounterRequestStatus.PartialSpawn &&
            (response.ExecutionSequence == 0 || response.ExecutionSequence > _requestSequence || response.Status == EncounterRequestStatus.ExecutionFailed))
        {
            _requestSequence = Math.Max(_requestSequence, response.ExecutionSequence);
            ReportRejectedRequest(request.Source, request.PresetId.Value, response.Status + ": " + response.Reason,
                request.Context.RequestId, request.CauseEventId, request.Origin, response.ExecutionSequence, response.SnapshotRevision,
                countsUnknown: !response.HasKnownExecutionCounts);
        }
        return response;
    }
    /// <summary>
    /// 실행 전 거부된 요청을 실제 생성과 구분해 진단 자료로 전달한다.
    /// </summary>
    private void ReportRejectedRequest(EncounterRequestSource source, string preset, string reason, Guid request,
        Guid cause = default, EncounterSpawnOrigin origin = null, long executionSequence = 0, long revision = 0,
        BattlefieldSpatialSnapshot snapshot = null, bool countsUnknown = false)
    {
        // 실행 전 거부에는 가짜 실행 번호를 배정하지 않습니다. 닫힌 Match 기록도 다시 열지 않습니다.
        if (_requests == null || _snapshotProvider == null || _boundMatchId.IsEmpty) return;
        if (snapshot != null && snapshot.MatchId == _boundMatchId) revision = snapshot.Revision;
        if (!Enum.IsDefined(typeof(EncounterRequestSource), source)) source = EncounterRequestSource.Unspecified;
        var context = new EncounterExecutionContext(_boundMatchId, executionSequence, revision, source, preset ?? "",
            requestGuid: request, causeEventId: cause, origin: origin);
        Emit(GameDiagnosticKind.RequestFailed, context, message: reason, countsUnknown: countsUnknown);
    }

    EncounterRequestResult IEncounterExecutor.Execute(EncounterRequest request, BattlefieldSpatialSnapshot snapshot, long executionSequence)
        => Execute(request, snapshot, null, executionSequence);

    public EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot snapshot,
        MatchEnvironmentSnapshot environment, long executionSequence)
        => Execute(request, snapshot, environment, null, executionSequence);

    /// <summary>
    /// 검증된 Core 요청과 환경·배치 정보를 실행 Context로 묶어 Runtime에 전달하고 결과를 반환한다.
    /// </summary>
    public EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot snapshot,
        MatchEnvironmentSnapshot environment, EncounterSpawnPlacement placement, long executionSequence)
    {
        if (!FieldCanAdvance) return new EncounterRequestResult(request.Context.RequestId, executionSequence,
            snapshot.Revision, EncounterRequestStatus.NotReady, reason: "FieldPaused");
        var cancellation = request.Origin.Kind == SpawnOriginKind.DynamicSource
            ? _spawnSources.GetSourceCancellation(request.Origin.Source) : _spawnGameplay;
        var context = new EncounterExecutionContext(_boundMatchId, checked(++_requestSequence), snapshot.Revision,
            request.Source, request.PresetId.Value, environment, request.Context.RequestId, request.CauseEventId,
            placement, _spawnSeed, cancellation, request.Origin);
        Emit(GameDiagnosticKind.RequestStarted, context,
            message: $"request={request.Context.RequestId:N}, preset={request.PresetId}, " +
                     $"maxCount={request.MaximumEnemyCount}, baseBudget={spawnBudget}");
        EnsurePipeline();
        if (!TryReadRegion(out RegionGridSnapshot region, out string reason) || _pipeline == null)
        {
            if (_pipeline == null) reason = "현재 Match의 Enemy 실행 구성이 준비되지 않았습니다.";
            Emit(GameDiagnosticKind.RequestFailed, context, message: reason);
            return new EncounterRequestResult(request.Context.RequestId, executionSequence, snapshot.Revision,
                EncounterRequestStatus.NotReady, reason: reason);
        }
        string result;
        EncounterExecutionSummary summary;
        bool succeeded = request.UsesAutomaticComposition
            ? _pipeline.SpawnRequest(snapshot, region, request.MaximumEnemyCount, selectionSettings,
                _squadRandom, context, out result, out summary, environmentProfile)
            : _pipeline.SpawnRequest(snapshot, region, request.PresetId.Value, request.MaximumEnemyCount,
                context, out result, out summary, selectionSettings.logSelectionDecisions, environmentProfile);
        Emit(succeeded ? GameDiagnosticKind.RequestCompleted : GameDiagnosticKind.RequestFailed, context,
            planned: summary.PlannedCount, actual: summary.SpawnedCount, message: summary.ResultCode + ": " + result,
            spawnDecision: summary.SpawnDecision);
        if (succeeded && request.Source != EncounterRequestSource.MatchStart)
            CancelPendingInitialEncounter();
        EncounterRequestStatus status = summary.PlannedCount > 0 && summary.SpawnedCount == summary.PlannedCount
            ? EncounterRequestStatus.Spawned : summary.SpawnedCount > 0 ? EncounterRequestStatus.PartialSpawn :
            summary.ResultCode == "UnsupportedDelivery" ? EncounterRequestStatus.UnsupportedDelivery :
            summary.ResultCode == "OriginUnavailable" ? EncounterRequestStatus.OriginUnavailable :
            summary.ResultCode == "PlanRejected" ? EncounterRequestStatus.PlanRejected : EncounterRequestStatus.SpawnFailed;
        return new EncounterRequestResult(request.Context.RequestId, executionSequence, snapshot.Revision,
            status, summary.PlannedCount, summary.SpawnedCount, result, environmentRevision: environment?.Revision ?? 0);
    }

    /// <summary>
    /// 현재 Inspector 설정을 전달하고 준비된 Match의 실행기와 카탈로그로 Runtime을 구성한다.
    /// </summary>
    private void EnsurePipeline()
    {
        // Inspector 설정은 유지하고 Runtime에 전달할 데이터만 갱신한다.
        _runtimeBudgetSettings.baseBudget = spawnBudget;
        _runtimeBudgetSettings.maximumBudget = maximumSpawnBudget;
        _runtimeBudgetSettings.staticIncreasePerRequest = staticBudgetIncreasePerRequest;
        if (_boundMatchId.IsEmpty || _snapshotProvider == null) return;
        if (_pipeline != null && _pipeline.HasExecutor) return;
        if (!ResolveSpawnSetup()) return;
        if (_pipeline?.Executor != null)
        {
            _pipeline.Executor.LifecycleObserved -= OnEnemyLifecycleObserved;
            if (ReferenceEquals(_pipeline.Executor.SummonRequests, this)) _pipeline.Executor.SummonRequests = null;
        }
        _pipeline = new EncounterPipeline(_boundMatchId, _spawnExecutor, _squadCatalog, _runtimeBudgetSettings,
            () => regionBinding?.Source?.Snapshot);
        _pipeline.Executor?.SpawnPointList.ResetRegionGroups();
        TryReadRegion(out _, out _);
        if (_pipeline.Executor != null)
        {
            _pipeline.Executor.LifecycleObserved += OnEnemyLifecycleObserved;
            _pipeline.Executor.SummonRequests = this;
        }
    }

    internal bool ResolveSpawnSetup()
    {
        if (_sceneExecutor != null) _spawnExecutor = _sceneExecutor;
        if (_spawnExecutor == null) _spawnExecutor = UnityEngine.Object.FindAnyObjectByType<EnemySpawnExecutor>();
        if (_squadCatalog == null) _squadCatalog = EnemySquadPresetCatalog.Load();
        return _spawnExecutor != null && _spawnExecutor.Catalog != null && _squadCatalog != null;
    }

    private void OnEnemyLifecycleObserved(EnemyLifecycleObservation observation)
    {
        if (observation.Context == null || observation.Context.MatchId != _boundMatchId || _snapshotProvider == null) return;
        Emit(observation.Kind, observation.Context, actual: 1, enemyType: observation.EnemyType,
            point: observation.SpawnPointId, entity: observation.EntityId, message: observation.Reason);
    }

    /// <summary>
    /// 요청 Context와 계획·실제 생성 결과를 공통 진단 사건으로 전달한다.
    /// </summary>
    private void Emit(GameDiagnosticKind kind, EncounterExecutionContext context, int planned = 0, int actual = 0,
        string enemyType = "", string point = "", string entity = "", string message = "", SpawnDecisionSnapshot spawnDecision = null,
        bool countsUnknown = false)
    {
        var observation = new GameDiagnosticEvent(++_diagnosticSequence, _boundMatchId,
            context?.Revision ?? 0, context?.RequestId ?? 0, context?.Source ?? EncounterRequestSource.Unspecified,
            kind, GameSeconds, RealSeconds, enemyType, point, entity, planned, actual, message, context?.SquadOrder ?? "",spawnDecision,
            hasDeathEvents: true, environment: context?.Environment, executionContext: context, isExecutionCountUnknown: countsUnknown);
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

    private string GetInitialSquadOrder() =>
        string.IsNullOrWhiteSpace(initialSquadOrder) ? "Normal" : initialSquadOrder.Trim();
}
