// Core가 제공한 환경·추가 생성 source·공용 시간과 명시 요청 API를 연결합니다.
// 생성 위치/분대 규칙은 Planner, 실제 NGO 생성은 Scene의 Executor가 담당합니다.
// 예약은 게임 절대 시각을 기다리며 Match/source 종료에 취소됩니다.
using System;
using TeamHJD.Game.Enemies.Encounter;
using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

/// <summary>
/// Core의 환경·생성 source·Match 시계와 지연 요청을 연결한다.
/// </summary>
public sealed partial class EncounterSceneBinding : IEncounterEnvironmentConsumer, IEncounterSpawnContextConsumer, IEncounterOriginRequestTarget
{
    [SerializeField] private EncounterEnvironmentProfile environmentProfile;
    private EnemySpawnExecutor _sceneExecutor;
    private MatchId _spawnContextMatch;
    private IEncounterSpawnSourceRegistry _spawnSources;
    private IMatchClock _spawnClock;
    private int? _spawnSeed;
    private Func<RegionCaptureTrend> _captureTrend;
    private System.Threading.CancellationToken _spawnGameplay;
    private sealed class PendingSpawn
    {
        public EncounterRequest Request;
        public EncounterSpawnReservation Reservation;
        public Coroutine Routine;
        public double Delay;
        public bool CancellationRecorded;
        public EncounterDeferredSpawn Prepared;
        public EncounterWarningView Warning;
        public Guid GridId;
    }
    private readonly Dictionary<Guid, PendingSpawn> _pendingSpawns = new Dictionary<Guid, PendingSpawn>();
    public int PendingSpawnRequests
    {
        get { int count = 0; foreach (var pending in _pendingSpawns.Values) if (pending.Reservation.IsPending) count++; return count; }
    }
    /// <summary>Host의 게임 사건/디버그 호출자가 명시 origin으로 요청합니다. 요청 수량은 상한이며 결과로 실제 생성을 확인합니다.</summary>
    public EncounterRequestResult RequestOrigin(string preset, int maximum, EncounterRequestSource source, EncounterSpawnOrigin origin, Guid cause)
    {
        Guid id = Guid.NewGuid();
        if (_boundMatchId.IsEmpty || _authorityIssuer.IsEmpty) return new EncounterRequestResult(id, 0, 0, EncounterRequestStatus.MatchUnavailable);
        var request = new EncounterRequest(new MatchRequestContext(_boundMatchId, id, _authorityIssuer), new DefinitionId(preset), maximum, source, origin, cause);
        return Submit(request, _authorityIssuer);
    }
    /// <summary>Host가 게임 초 지연 요청을 등록합니다. Match/source 해제 시 취소되며 대기 중에는 생존 적 수에 포함하지 않습니다.</summary>
    public bool TryScheduleOrigin(string preset, int maximum, EncounterRequestSource source, EncounterSpawnOrigin origin, Guid cause,
        double delayGameSeconds, out Guid requestId)
    {
        requestId = Guid.Empty;
        if (_boundMatchId.IsEmpty || _authorityIssuer.IsEmpty) return false;
        var request = new EncounterRequest(new MatchRequestContext(_boundMatchId, Guid.NewGuid(), _authorityIssuer),
            new DefinitionId(preset), maximum, source, origin, cause);
        if (!TryScheduleEncounter(request, delayGameSeconds, out var reservation)) return false;
        requestId = reservation.RequestId; return true;
    }
    private EncounterExecutionContext ReservationContext(EncounterRequest request) => new EncounterExecutionContext(
        request.Context.MatchId, 0, 0, request.Source, request.PresetId.Value, requestGuid: request.Context.RequestId,
        causeEventId: request.CauseEventId, origin: request.Origin);
    /// <summary>
    /// Match 게임 시간 기준으로 요청을 예약하고 취소·결과 조회용 핸들을 반환한다.
    /// </summary>
    public bool TryScheduleEncounter(EncounterRequest request, double delayGameSeconds, out EncounterSpawnReservation reservation)
        => TryScheduleEncounterCore(request, delayGameSeconds, out reservation, null);

    private bool TryScheduleEncounterCore(EncounterRequest request, double delayGameSeconds,
        out EncounterSpawnReservation reservation, EncounterDeferredSpawn prepared)
    {
        reservation = null;
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (double.IsNaN(delayGameSeconds) || double.IsInfinity(delayGameSeconds) || delayGameSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(delayGameSeconds));
        if (!isActiveAndEnabled || _spawnClock == null || _requests == null || _networkManager == null || !_networkManager.IsServer ||
            request.Context.MatchId != _boundMatchId || request.Context.IssuerId != _authorityIssuer || _spawnGameplay.IsCancellationRequested) return false;
        if (_pendingSpawns.TryGetValue(request.Context.RequestId, out var previous))
        {
            if (previous.Request.GetPayloadKey() != request.GetPayloadKey() || previous.Delay != delayGameSeconds) return false;
            reservation = previous.Reservation; return true;
        }
        if (prepared != null && !CanShowInternalWarning) return false;
        if (_pendingSpawns.Count >= 64 || (request.Origin.Kind != SpawnOriginKind.Automatic && _spawnSources.Resolve(request.Origin) == null)) return false;
        double due = _spawnClock.ElapsedSeconds + delayGameSeconds;
        if (double.IsInfinity(due)) throw new ArgumentOutOfRangeException(nameof(delayGameSeconds));
        reservation = new EncounterSpawnReservation(request.Context.RequestId, due);
        var pending = new PendingSpawn { Request = request, Reservation = reservation, Delay = delayGameSeconds, Prepared = prepared };
        if (prepared != null)
        {
            pending.GridId = regionBinding.Source.Snapshot.GridId;
            pending.Warning = EncounterWarningView.Create(_warningSprite, prepared.Instruction.Placement.Area,
                _spawnClock, ExecutionTarget.gameObject.scene);
        }
        _pendingSpawns.Add(request.Context.RequestId, pending);
        Emit(GameDiagnosticKind.RequestScheduled, ReservationContext(request),
            planned: prepared == null ? 0 : request.MaximumEnemyCount, spawnDecision: CapturePreparedDecision(pending),
            message: prepared == null ? $"Pending until Field game time {due:F2}s; not an alive enemy."
                : $"내부 추가 출현 예고 → Field {due:F2}s / {prepared.Reason}. 아직 생존 적에 포함하지 않습니다.");
        if (!reservation.IsPending)
        { RecordReservationCancellation(pending); ReleaseWarning(pending); _pendingSpawns.Remove(request.Context.RequestId); return true; }
        try { pending.Routine = StartCoroutine(RunScheduledEncounter(pending)); }
        catch { _pendingSpawns.Remove(request.Context.RequestId); ReleaseWarning(pending); reservation.Cancel("Failed to start the Feature wait."); throw; }
        return true;
    }
    /// <summary>
    /// Match 시계의 예약 시점까지 기다리고 취소되지 않은 요청을 공통 서비스에 제출한다.
    /// </summary>
    private IEnumerator RunScheduledEncounter(PendingSpawn pending)
    {
        var token = pending.Request.Origin.Kind == SpawnOriginKind.DynamicSource
            ? _spawnSources.GetSourceCancellation(pending.Request.Origin.Source) : _spawnGameplay;
        using (token.Register(() => pending.Reservation.Cancel("Match/source released before execution.")))
            try
            {
                while (pending.Reservation.IsPending && _spawnClock != null && (!FieldCanAdvance || _spawnClock.ElapsedSeconds < pending.Reservation.DueGameSeconds))
                {
                    if (pending.Prepared != null && regionBinding?.Source?.Snapshot.GridId != pending.GridId)
                        pending.Reservation.Cancel("Region Grid changed during the warning.");
                    yield return null;
                }
                if (pending.Reservation.IsPending)
                    pending.Reservation.Complete(Submit(pending.Request, _authorityIssuer));
            }
            finally
            {
                RecordReservationCancellation(pending);
                ReleaseWarning(pending);
                if (_pendingSpawns.TryGetValue(pending.Request.Context.RequestId, out var current) && ReferenceEquals(current, pending))
                    _pendingSpawns.Remove(pending.Request.Context.RequestId);
            }
    }
    private void CancelScheduledEncounters()
    {
        foreach (var pending in new List<PendingSpawn>(_pendingSpawns.Values))
        {
            pending.Reservation.Cancel("Match spawn scope released.");
            RecordReservationCancellation(pending);
            if (pending.Routine != null) StopCoroutine(pending.Routine);
            ReleaseWarning(pending);
        }
        _pendingSpawns.Clear();
    }
    private void RecordReservationCancellation(PendingSpawn pending)
    {
        if (pending.CancellationRecorded || !pending.Reservation.IsCancelled || _boundMatchId != pending.Request.Context.MatchId) return;
        pending.CancellationRecorded = true;
        Emit(GameDiagnosticKind.RequestCancelled, ReservationContext(pending.Request), message: pending.Reservation.Reason);
    }
    /// <summary>Core가 BindMatch 전에 source registry·공용 시계·seed·종료 토큰을 연결합니다.</summary>
    public void BindSpawnContext(MatchId match, IEncounterSpawnSourceRegistry sources, IMatchClock clock, int seed,
        System.Threading.CancellationToken gameplay, Func<RegionCaptureTrend> captureTrend = null)
    {
        if (match.IsEmpty || !_spawnContextMatch.IsEmpty || _snapshotProvider != null || gameplay.IsCancellationRequested)
            throw new InvalidOperationException("Bind one active Match spawn scope before the consumer.");
        _spawnSources = sources ?? throw new ArgumentNullException(nameof(sources));
        _spawnClock = clock ?? throw new ArgumentNullException(nameof(clock));
        _spawnContextMatch = match; _spawnSeed = seed; _spawnGameplay = gameplay; _captureTrend = captureTrend;
    }
    /// <summary>
    /// 해당 Match의 지연 요청과 source·시계 연결을 해제한다.
    /// </summary>
    public void UnbindSpawnContext(MatchId match)
    {
        if (_spawnContextMatch != match) return;
        CancelScheduledEncounters();
        _spawnContextMatch = default; _spawnSources = null; _spawnClock = null; _spawnSeed = null; _spawnGameplay = default; _captureTrend = null;
    }
    private bool FieldCanAdvance => _spawnClock is not IMatchTime time ||
        (time.IsAvailable && !time.Snapshot.IsPaused && time.Snapshot.Rate > 0);
    public EnemySpawnExecutor ExecutionTarget => _pipeline?.Executor ?? _sceneExecutor;
    /// <summary>
    /// 씬에서 지정한 실행기와 환경 설정을 현재 연결부의 제작 참조로 사용한다.
    /// </summary>
    public void ConfigureScene(EnemySpawnExecutor executor, EncounterEnvironmentProfile profile, Sprite warningSprite = null)
    {
        if (_snapshotProvider != null) throw new InvalidOperationException("Configure the Enemy Scene before binding a Match.");
        if (executor == null) throw new ArgumentNullException(nameof(executor));
        if (_pipeline?.Executor != null) _pipeline.Executor.LifecycleObserved -= OnEnemyLifecycleObserved;
        _pipeline = null; _sceneExecutor = executor; _spawnExecutor = executor; environmentProfile = profile; _warningSprite = warningSprite;
        regionBinding = null;
        foreach (var root in executor.gameObject.scene.GetRootGameObjects())
        {
            regionBinding = root.GetComponentInChildren<TeamHJD.Game.Bootstrap.SceneComposition.RegionSceneBinding>(true);
            if (regionBinding != null) break;
        }
    }
    private MatchId _environmentMatch;
    private Func<MatchEnvironmentSnapshot> _environmentProvider;

    /// <summary>Core가 같은 Match의 최신 환경 조회만 주입합니다. 날씨 전환 자체로 Spawn을 추가 요청하지 않습니다.</summary>
    public void BindEnvironment(MatchId match, Func<MatchEnvironmentSnapshot> current)
    {
        if (match.IsEmpty || _snapshotProvider != null || _environmentProvider != null)
            throw new InvalidOperationException("Configure one environment query before binding the Encounter Match.");
        _environmentMatch = match; _environmentProvider = current ?? throw new ArgumentNullException(nameof(current));
    }
    /// <summary>
    /// 현재 Match와 일치하는 환경 자료의 연결을 해제한다.
    /// </summary>
    public void UnbindEnvironment(MatchId match)
    { if (_environmentMatch == match) { _environmentProvider = null; _environmentMatch = default; } }
}
