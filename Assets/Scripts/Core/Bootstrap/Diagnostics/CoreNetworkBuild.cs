// 개발 빌드에서만 Initial_Stage의 명시적 연결 API를 두 실행 파일로 검증합니다.
// 실제 Scene/설정/지역/NetworkObject를 사용하며 게임 규칙이나 권한 구현을 대체하지 않습니다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityApplication = UnityEngine.Application;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class CoreNetworkBuild : MonoBehaviour
    {
        [Serializable] private sealed class Probe
        {
            public string Run, Phase, Match, Descriptor, DescriptorHash, Grid, RegionHash, Player, TurretIdentityHash, PresentationHash;
            public int Seed, Captured, Mode, ActorCount; public long Revision, PresentationRevision; public ulong Anchor;
            public string EnvironmentHash,Weather,TimeOfDay,CompletionHash,ResultScreen;
            public long EnvironmentRevision;
            public double DayNightRemaining, MatchGameSeconds, GameRate;
            public bool LocalCameraVerified;
        }
        [Serializable] private sealed class Result
        {
            public bool Success; public string Role, Run, Step, Error, Unity, Build;
            public int ProcessId; public Probe[] Observations; public string[] Errors, TurretChecks; public string TurretFixtureScope;
        }
        private string _role, _run, _path, _step, _firstMatch, _lastMatch, _lastDescriptor;
        private ushort _port;
        private InitialStageNetworkBinding _binding;
        private NgoNetworkSession _session;
        private NgoMessageChannel _probe;
        private CoreNetworkTurretValidation _turretProbe;
        private readonly List<Probe> _observations = new List<Probe>();
        private readonly List<string> _errors = new List<string>();
        private readonly HashSet<string> _sent = new HashSet<string>();
        private readonly HashSet<string> _seenMatches = new HashSet<string>();
        private FieldTurretBase[] _selected;
        private float _deadline, _finishAt;
        private int _dynamicCount;
        private long _dynamicRevision;
        private ulong _firstAnchor;
        private long _rateEnvironmentRevision;
        private bool _terminal;
        private string _environmentDescriptor;
        private readonly Dictionary<long, string> _environmentHashes = new Dictionary<long, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            string role = Option(args, "--core-network-role");
            if (string.IsNullOrEmpty(role)) return;
            var binding = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
            if (binding == null) throw new InvalidOperationException("The build probe requires the authored Initial_Stage.");
            binding.ConfigureEntry(InitialStageNetworkEntry.Manual);
            var owner = new GameObject("Core Network Build Validation");
            DontDestroyOnLoad(owner);
            var runner = owner.AddComponent<CoreNetworkBuild>();
            runner._role = role; runner._run = Option(args, "--core-network-run");
            runner._path = Option(args, "--core-network-result");
            if ((role != "host" && role != "client") || string.IsNullOrEmpty(runner._run) ||
                string.IsNullOrEmpty(runner._path) || !ushort.TryParse(Option(args, "--core-network-port"), out runner._port))
            { runner.Fail("Invalid development probe arguments."); return; }
            runner._binding = binding; runner.Move("Connect");
            UnityApplication.logMessageReceived += runner.OnLog;
        }
        private static string Option(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
        }
        private void Move(string step)
        {
            _step = step; _deadline = Time.realtimeSinceStartup + 120;
            Debug.Log("CORE_NETWORK_PROBE " + _role + " " + step);
        }
        private void Update()
        {
            if (_terminal) return;
            try
            {
                if (_errors.Count > 0) throw new InvalidOperationException(_errors[0]);
                if (Time.realtimeSinceStartup > _deadline)
                    throw new TimeoutException($"Step={_step}; connection={_session?.State}; Match={_binding?.Matches?.Phase}; detail={_binding?.LastFailure}");
                if (_step == "ClientClosing")
                {
                    _probe.Pump();
                    if (Time.realtimeSinceStartup >= _finishAt) Finish(true, null);
                    return;
                }
                if (_step == "ClientDisconnecting")
                {
                    if (Time.realtimeSinceStartup < _finishAt) return;
                    _binding.ShutdownConnection(); Move("ClientAwaitDisconnected"); return;
                }
                if (_step == "ClientAwaitDisconnected" || _step == "WaitDisconnected")
                {
                    // Host의 마지막 진단 지시도 큐에서 실제 전송해야 Client가 연결을 닫습니다.
                    _probe.Pump();
                    if (IsMatchClean())
                    {
                        _observations.Add(new Probe { Phase = "Disconnected", Match = _lastMatch, Run = _run });
                        Finish(true, null);
                    }
                    return;
                }
                if (_binding == null) _binding = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
                if (_binding == null || _binding.Connection == null) return;
                if (_session == null)
                {
                    _session = (NgoNetworkSession)_binding.Connection;
                    var transport = _session.Manager.GetComponent<UnityTransport>();
                    if (transport == null) throw new InvalidOperationException("The validation build requires UTP.");
                    transport.SetConnectionData("127.0.0.1", _port, "127.0.0.1");
                    if (!(_role == "host" ? _binding.TryStartHost() : _binding.TryStartClient()))
                        throw new InvalidOperationException("The explicit connection API rejected startup.");
                    _probe = new NgoMessageChannel(_session.Manager, "teamhjd.validation.core.v1");
                    _probe.MessageReceived += OnProbe;
                    _turretProbe = new CoreNetworkTurretValidation(_binding, _session, _run);
                    Move(_role == "host" ? "WaitPeers" : "ClientMonitor");
                    if (_role == "host") Debug.Log("CORE_NETWORK_PROBE_HOST_LISTENING");
                }
                if (_role == "host") RememberEnvironment();
                _probe.Pump();
                _turretProbe.Pump();
                if (_turretProbe.Failure != null) throw new InvalidOperationException(_turretProbe.Failure);
                if (_role == "client") { ObserveClient(); return; }
                if (_step == "WaitPeers" && _session.ConnectedPeerIds.Count == 2)
                {
                    if (!_binding.TryPrepareMatch()) throw new InvalidOperationException("Host preparation rejected after Scene-ready cohort.");
                    Move("WaitReady");
                }
                else if ((_step == "WaitReady" || _step == "WaitRestartReady" || _step == "WaitDisconnectReady") && _binding.Matches.CanStart)
                {
                    bool restart = _step == "WaitRestartReady";
                    bool disconnect = _step == "WaitDisconnectReady";
                    if (!_binding.TryStartMatch()) throw new InvalidOperationException("Host start rejected despite readiness.");
                    Move(disconnect ? "WaitDisconnectAck" : restart ? "WaitRestartAck" : "WaitStaticAck");
                }
                else if (_step == "WaitTurretCommands" && _turretProbe.Completed)
                {
                    if (!_binding.GetComponent<RegionSceneBinding>().ResetPolicy(RegionPolicyMode.Dynamic, true))
                        throw new InvalidOperationException("The authoritative region policy reset was rejected.");
                    ActivateTriangle(); Move("WaitDynamicAck");
                    var dayNight = SceneDayNight();
                    if (dayNight == null || !dayNight.TryAdvancePhase())
                        throw new InvalidOperationException("Host could not advance the actual day/night source.");
                }
            }
            catch (Exception error) { Fail(error.ToString()); }
        }

        private bool IsMatchClean()
        {
            var app = Object.FindAnyObjectByType<AppRoot>();
            var spawnManager = _session.Manager.SpawnManager;
            // NGO는 연결 종료 시 SpawnManager 자체를 null로 정리합니다.
            // Host 연결 유지 중 null인 경우와 정상 Disconnected 상태를 구분합니다.
            bool objectsClean = spawnManager == null ? _session.State == NetworkConnectionState.Disconnected :
                !spawnManager.SpawnedObjectsList.Any(item => item != null && item.IsSceneObject != true);
            return app != null && _binding.Matches?.Phase == NetworkMatchPhase.Ended && app.CurrentMatchId.IsEmpty &&
                _binding.TurretCommands == null && _binding.TurretPresentation == null && app.NetworkRegions.Source == null &&
                app.NetworkEnvironments.Source == null && app.CurrentSpawnSources == null &&
                _binding.GetComponent<TurretMatchBinding>().Count == 0 &&
                Object.FindObjectsByType<NetworkMatchAnchor>(FindObjectsSortMode.None).Length == 0 &&
                Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length == 0 &&
                (CameraManager.Current == null || (CameraManager.Current.ActiveTarget == null && !CameraManager.Current.HasOverride)) &&
                objectsClean;
        }

        private void ObserveClient()
        {
            var app = Object.FindAnyObjectByType<AppRoot>();
            if (app == null || _binding.Matches == null) return;
            var descriptor = _binding.Matches.Descriptor;
            if (descriptor == null)
            {
                if (_binding.Matches.Phase != NetworkMatchPhase.Ended || string.IsNullOrEmpty(_lastMatch)) return;
                var spawnManager = _session.Manager.SpawnManager;
                if (spawnManager == null && _session.State != NetworkConnectionState.Disconnected) return;
                if (Object.FindObjectsByType<NetworkMatchAnchor>(FindObjectsSortMode.None).Length != 0 ||
                    Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None).Length != 0 ||
                    app.NetworkRegions.Source != null || !app.CurrentMatchId.IsEmpty ||
                    app.NetworkEnvironments.Source != null ||
                    _binding.TurretCommands != null || _binding.TurretPresentation != null ||
                    _binding.GetComponent<TurretMatchBinding>().Count != 0 ||
                    (spawnManager != null && spawnManager.SpawnedObjectsList.Any(item => item != null && item.IsSceneObject != true))) return;
                string phase = _lastMatch == _firstMatch ? "Ended" : "FinalEnded";
                SendOnce(new Probe { Phase = phase, Match = _lastMatch, Descriptor = _lastDescriptor });
                return;
            }
            if (_binding.Matches.Phase != NetworkMatchPhase.Running) return;
            if (!app.CurrentMatchId.IsEmpty) throw new InvalidOperationException("Client created an authority MatchSession.");
            if (_binding.Matches.Completion != null)
            {
                if (app.Screens.Top?.Kind != ScreenKind.MatchResult || app.Screens.Top.Completion == null) return;
                if (_binding.Matches.IsGameplayRunning) throw new InvalidOperationException("Client gameplay remained open after the authoritative result.");
                SendOnce(new Probe { Phase = "Completed", Match = descriptor.Config.MatchId.Value,
                    Descriptor = descriptor.DescriptorId.ToString("N"), ResultScreen = app.Screens.Top.Kind.ToString(),
                    CompletionHash = Hash(MatchLifecycleCodec.Encode(MatchLifecycleSignalKind.Completed,
                        new MatchLifecycleSignal(descriptor.Config.MatchId, descriptor.DescriptorId, app.Screens.Top.Completion))) });
                return;
            }
            var environment = app.CurrentEnvironment;
            if (environment == null || environment.MatchId != descriptor.Config.MatchId) return;
            var cycle = SceneDayNight();
            var matchTime = _binding.GetComponent<InitialStageCompositionRoot>().MatchTime;
            if (cycle == null || matchTime == null || !matchTime.IsAvailable || !double.IsFinite(cycle.RemainingSeconds)) return;
            var weatherSources = _binding.gameObject.scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<MonoBehaviour>())
                .OfType<IMatchWeatherSource>().ToArray();
            if (weatherSources.Length != 1 || weatherSources[0].WeatherId != environment.WeatherId ||
                !weatherSources[0].Forecast.SequenceEqual(environment.Forecast)) return;
            RegionGridSnapshot snapshot = app.NetworkRegions?.Source?.Snapshot;
            if (snapshot == null || snapshot.MatchId != descriptor.Config.MatchId) return;
            RegionSceneBinding regionBinding = _binding.GetComponent<RegionSceneBinding>();
            if (!ReferenceEquals(regionBinding.Source, app.NetworkRegions.Source)) return;
            if (regionBinding.HasWriteAuthority || regionBinding.LastEvaluation != null)
                throw new InvalidOperationException("Client computed an authoritative region policy.");
            var anchors = Object.FindObjectsByType<NetworkMatchAnchor>(FindObjectsSortMode.None);
            if (anchors.Length != 1 || !anchors[0].IsSpawned) return;
            var anchor = anchors[0]; var state = anchor.State;
            if (state.MatchId.ToString() != snapshot.MatchId.Value ||
                state.DescriptorId.ToString() != descriptor.DescriptorId.ToString("N") ||
                state.RegionGridId.ToString() != snapshot.GridId.ToString("N") || state.RegionRevision != snapshot.Revision ||
                state.CapturedCells != snapshot.CapturedCellCount || state.RegionMode != (int)snapshot.Mode) return;
            if (state.StageId.ToString() != descriptor.Config.StageId.Value || state.Seed != descriptor.Config.RandomSeed)
                throw new InvalidOperationException("Anchor metadata differs from the authoritative descriptor.");
            var presentation = _binding.TurretPresentation?.Snapshot;
            if (presentation == null || presentation.MatchId != snapshot.MatchId || presentation.DescriptorId != descriptor.DescriptorId ||
                presentation.Controller.RegionGridId != snapshot.GridId || presentation.Controller.RegionRevision != snapshot.Revision ||
                presentation.Controller.RegionMode != snapshot.Mode) return;
            var actors = Object.FindObjectsByType<PlayerActorIdentity>(FindObjectsSortMode.None);
            if (!_binding.Matches.TryResolveParticipant(_session.LocalConnectionId, out PlayerId player) ||
                actors.Length != 1 || actors[0].PlayerId != player) return;
            var localCamera = CameraManager.Current;
            if (localCamera == null || localCamera.ActiveTarget != actors[0].transform ||
                localCamera.OutputCamera != Camera.main)
                throw new InvalidOperationException("Client camera does not belong to its local player/output.");
            _firstMatch ??= snapshot.MatchId.Value; _lastMatch = snapshot.MatchId.Value;
            _seenMatches.Add(_lastMatch);
            _lastDescriptor = descriptor.DescriptorId.ToString("N");
            string phaseName = _seenMatches.Count >= 3 ? "Disconnect" : _firstMatch != _lastMatch ? "Restart" : snapshot.Mode == RegionPolicyMode.Static ? "Static" :
                _dynamicCount == 0 ? (snapshot.CapturedCellCount > 0 ? "Dynamic" : null) :
                snapshot.Revision > _dynamicRevision && snapshot.CapturedCellCount < _dynamicCount ? "Release" : null;
            if (phaseName == null) return;
            if (_firstMatch == _lastMatch && snapshot.Mode == RegionPolicyMode.Static)
            {
                if (_sent.Contains(_lastMatch + "/Static") && matchTime.Snapshot.Rate == 0) phaseName = "ClockPaused";
                else if (_sent.Contains(_lastMatch + "/ClockPaused") && matchTime.Snapshot.Rate == 2) phaseName = "ClockRate";
                else if (_sent.Contains(_lastMatch + "/ClockRate") && matchTime.Snapshot.Rate == 1 &&
                    environment.Revision > _rateEnvironmentRevision) phaseName = "ClockReset";
            }
            if (phaseName == "ClockRate") _rateEnvironmentRevision = environment.Revision;
            if ((phaseName == "Dynamic" || phaseName == "Release") && cycle.Phase != DayNightPhase.Night) return;
            if (phaseName == "Dynamic") { _dynamicCount = snapshot.CapturedCellCount; _dynamicRevision = snapshot.Revision; }
            SendOnce(new Probe
            {
                Phase = phaseName, Match = snapshot.MatchId.Value, Descriptor = _lastDescriptor,
                DescriptorHash = Hash(MatchLifecycleCodec.Encode(descriptor)),
                Grid = snapshot.GridId.ToString("N"), RegionHash = RegionHash(descriptor, snapshot),
                Seed = descriptor.Config.RandomSeed, Captured = snapshot.CapturedCellCount,
                Mode = (int)snapshot.Mode, Revision = snapshot.Revision, Anchor = anchor.NetworkObjectId,
                Player = player.Value, ActorCount = actors.Length,
                PresentationHash = CoreNetworkTurretValidation.Hash(presentation), PresentationRevision = presentation.Revision,
                TurretIdentityHash = TurretIdentityHash(_binding.GetComponent<TurretMatchBinding>()),
                EnvironmentRevision = environment.Revision,Weather = environment.WeatherId.Value,TimeOfDay = environment.TimeOfDayId.Value,
                DayNightRemaining = cycle.RemainingSeconds, MatchGameSeconds = matchTime.ElapsedSeconds, GameRate = matchTime.Snapshot.Rate,
                LocalCameraVerified = true,
                EnvironmentHash = Hash(EnvironmentReplicationCodec.Encode(new EnvironmentReplicationFrame(descriptor.DescriptorId, environment)))
            });
        }
        private void SendOnce(Probe value)
        {
            string key = value.Match + "/" + value.Phase;
            if (_sent.Contains(key)) return;
            value.Run = _run;
            if (!_probe.TrySend(0, Encoding.UTF8.GetBytes(JsonUtility.ToJson(value)))) return;
            _sent.Add(key); _observations.Add(value);
            Debug.Log("CORE_NETWORK_PROBE_CLIENT " + value.Phase + " " + value.Match);
        }
        private void OnProbe(ulong sender, byte[] bytes)
        {
            try
            {
                var packet = JsonUtility.FromJson<Probe>(Encoding.UTF8.GetString(bytes));
                if (packet == null || packet.Run != _run) throw new InvalidOperationException("Wrong probe run.");
                if (_role == "client")
                {
                    if (sender == 0 && packet.Phase == "DisconnectNow" && _sent.Contains(_lastMatch + "/Disconnect"))
                    { _finishAt = Time.realtimeSinceStartup + .2f; Move("ClientDisconnecting"); return; }
                    if (sender != 0 || packet.Phase != "Complete" || !_sent.Contains(_lastMatch + "/FinalEnded"))
                        throw new InvalidOperationException("Unexpected server completion.");
                    SendOnce(new Probe { Phase = "Closing", Match = _lastMatch });
                    _finishAt = Time.realtimeSinceStartup + .5f; Move("ClientClosing"); return;
                }
                if (sender == 0 || !_session.ConnectedPeerIds.Contains(sender))
                    throw new InvalidOperationException("Probe sender is not the connected Client.");
                _observations.Add(packet);
                if (_step == "WaitClosing" && packet.Phase == "Closing") { Finish(true, null); return; }
                if (_step == "WaitCompletionAck" && packet.Phase == "Completed")
                {
                    var app = Object.FindAnyObjectByType<AppRoot>();
                    var completion = _binding.Matches.Completion;
                    if (completion == null || completion.Result.Outcome != MatchOutcome.Defeat || app.Screens.Top?.Kind != ScreenKind.MatchResult ||
                        packet.ResultScreen != ScreenKind.MatchResult.ToString() || _binding.Matches.IsGameplayRunning ||
                        packet.Match != completion.Result.MatchId.Value ||
                        packet.CompletionHash != Hash(MatchLifecycleCodec.Encode(MatchLifecycleSignalKind.Completed,
                            new MatchLifecycleSignal(completion.Result.MatchId, _binding.Matches.Descriptor.DescriptorId, completion))))
                        throw new InvalidOperationException("The actual CU completion/result screen differed across processes.");
                    if (!_probe.TrySend(sender, Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Probe { Run = _run, Phase = "DisconnectNow" }))))
                        throw new InvalidOperationException("Disconnect instruction could not be queued.");
                    Move("WaitDisconnected"); return;
                }
                if (_step == "WaitEndedAck" || _step == "WaitFinalEndedAck")
                {
                    string expected = _step == "WaitEndedAck" ? "Ended" : "FinalEnded";
                    if (packet.Phase != expected || packet.Match != _lastMatch)
                        throw new InvalidOperationException("Unexpected Match end acknowledgement.");
                    if (expected == "Ended")
                    {
                        if (!_binding.TryPrepareMatch()) throw new InvalidOperationException("Match restart preparation rejected.");
                        Move("WaitRestartReady");
                    }
                    else
                    {
                        if (!_binding.TryPrepareMatch()) throw new InvalidOperationException("Disconnect verification preparation rejected.");
                        Move("WaitDisconnectReady");
                    }
                    return;
                }
                ValidateRunning(packet, sender);
                if (_step == "WaitStaticAck" && packet.Phase == "Static")
                {
                    _firstAnchor = packet.Anchor;
                    Time.timeScale = 0;
                    Move("WaitClockPauseAck");
                }
                else if (_step == "WaitClockPauseAck" && packet.Phase == "ClockPaused")
                {
                    if (packet.GameRate != 0) throw new InvalidOperationException("Client did not observe the paused game rate.");
                    Time.timeScale = 2;
                    Move("WaitClockRateAck");
                }
                else if (_step == "WaitClockRateAck" && packet.Phase == "ClockRate")
                {
                    if (packet.GameRate != 2) throw new InvalidOperationException("Client did not observe the doubled game rate.");
                    Time.timeScale = 1;
                    if (!SceneDayNight().TryRestartCurrentPhase())
                        throw new InvalidOperationException("Host could not restart the current day/night phase.");
                    Move("WaitClockResetAck");
                }
                else if (_step == "WaitClockResetAck" && packet.Phase == "ClockReset")
                {
                    if (packet.TimeOfDay != "day" || packet.DayNightRemaining < SceneDayNight().DaySeconds - 1)
                        throw new InvalidOperationException("Client did not observe the same-phase day/night reset.");
                    _turretProbe.Begin(sender); Move("WaitTurretCommands");
                }
                else if (_step == "WaitDynamicAck" && packet.Phase == "Dynamic")
                {
                    var controller = FieldTurretController.FindForScene(_binding.gameObject.scene);
                    if (controller.RequestActivation(_selected[0].InstanceId, false) != FieldTurretActivationResult.Deactivated)
                        throw new InvalidOperationException("Turret OFF rejected.");
                    if (!_selected[1].IsActivated || !_selected[2].IsActivated)
                        throw new InvalidOperationException("Capture release disabled remaining turrets.");
                    Move("WaitReleaseAck");
                }
                else if ((_step == "WaitReleaseAck" && packet.Phase == "Release") ||
                    (_step == "WaitRestartAck" && packet.Phase == "Restart"))
                {
                    bool restart = packet.Phase == "Restart";
                    if (restart && packet.Anchor == _firstAnchor) throw new InvalidOperationException("Restart reused the old live anchor.");
                    _lastMatch = packet.Match;
                    if (!_binding.TryEndMatch()) throw new InvalidOperationException("Host end rejected.");
                    Move(restart ? "WaitFinalEndedAck" : "WaitEndedAck");
                }
                else if (_step == "WaitDisconnectAck" && packet.Phase == "Disconnect")
                {
                    _lastMatch = packet.Match;
                    var target = _binding.GetComponent<RegionSceneBinding>().ControlUnit.GetComponents<MonoBehaviour>()
                        .OfType<IDamageableTarget>().Single(item => item is IControlUnitVitals);
                    if (!target.ApplyDamage(target.CurrentHealth))
                        throw new InvalidOperationException("The authored CU rejected the validation damage.");
                    // 기존 CU는 읽기 계약을 제공하고 Scene Update가 완료를 판정합니다.
                    // 여기서 동기 완료를 강제하지 않고 Client 결과 응답에서 Host 결과와 함께 검증합니다.
                    Move("WaitCompletionAck");
                }
                else throw new InvalidOperationException("Unexpected phase " + packet.Phase + " in " + _step);
            }
            catch (Exception error) { Fail(error.ToString()); }
        }
        private void ValidateRunning(Probe packet, ulong sender)
        {
            var descriptor = _binding.Matches.Descriptor;
            var region = _binding.GetComponent<RegionSceneBinding>().Source.Snapshot;
            var anchor = Object.FindAnyObjectByType<NetworkMatchAnchor>();
            if (!packet.LocalCameraVerified || packet.Match != descriptor.Config.MatchId.Value || packet.Descriptor != descriptor.DescriptorId.ToString("N") ||
                packet.DescriptorHash != Hash(MatchLifecycleCodec.Encode(descriptor)) ||
                packet.RegionHash != RegionHash(descriptor, region) || packet.Anchor != anchor.NetworkObjectId ||
                packet.TurretIdentityHash != TurretIdentityHash(_binding.GetComponent<TurretMatchBinding>()) ||
                !_turretProbe.MatchesAuthoritativeFrame(descriptor.DescriptorId, packet.PresentationRevision, packet.PresentationHash) ||
                !_binding.Matches.TryResolveParticipant(sender, out PlayerId player) || packet.Player != player.Value || packet.ActorCount != 1)
                throw new InvalidOperationException("Client settings/region/anchor/presentation/local participant differ from Host.");
            if (_environmentDescriptor != descriptor.DescriptorId.ToString("N") ||
                !_environmentHashes.TryGetValue(packet.EnvironmentRevision, out string environmentHash) || packet.EnvironmentHash != environmentHash)
                throw new InvalidOperationException("Client confirmed an unknown/different authoritative environment frame.");
            var currentTime = _binding.GetComponent<InitialStageCompositionRoot>().MatchTime;
            if (!double.IsFinite(packet.DayNightRemaining) || packet.DayNightRemaining < 0 ||
                !double.IsFinite(packet.MatchGameSeconds) || Math.Abs(packet.MatchGameSeconds - currentTime.ElapsedSeconds) > 1)
                throw new InvalidOperationException("Client day/night is unavailable or differs from the authority game clock by over one second.");
        }
        private void RememberEnvironment()
        {
            var descriptor = _binding.Matches?.Descriptor;
            var snapshot = Object.FindAnyObjectByType<AppRoot>()?.CurrentEnvironment;
            if (descriptor == null || snapshot == null || descriptor.Config.MatchId != snapshot.MatchId) return;
            string id = descriptor.DescriptorId.ToString("N");
            if (_environmentDescriptor != id) { _environmentDescriptor = id; _environmentHashes.Clear(); }
            _environmentHashes[snapshot.Revision] = Hash(EnvironmentReplicationCodec.Encode(new EnvironmentReplicationFrame(descriptor.DescriptorId, snapshot)));
            if (_environmentHashes.Count > 64) _environmentHashes.Remove(_environmentHashes.Keys.Min());
        }
        private MatchDayNightCycle SceneDayNight() => _binding.gameObject.scene.GetRootGameObjects()
            .SelectMany(item => item.GetComponentsInChildren<MatchDayNightCycle>()).SingleOrDefault();

        private void ActivateTriangle()
        {
            var controller = FieldTurretController.FindForScene(_binding.gameObject.scene);
            var turrets = Object.FindObjectsByType<FieldTurretBase>(FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene == _binding.gameObject.scene && !t.IsDestroyed && t.isActiveAndEnabled &&
                    t.TryGetEntityId(out var id) && id != _turretProbe.TargetId)
                .OrderBy(t => t.EffectivePower).ToArray();
            for (int a = 0; a < turrets.Length && _selected == null; a++)
                for (int b = a + 1; b < turrets.Length && _selected == null; b++)
                    for (int c = b + 1; c < turrets.Length && _selected == null; c++)
                    {
                        Vector2 ab = turrets[b].transform.position - turrets[a].transform.position;
                        Vector2 ac = turrets[c].transform.position - turrets[a].transform.position;
                        if (Mathf.Abs(ab.x * ac.y - ab.y * ac.x) > 1 &&
                            turrets[a].EffectivePower + turrets[b].EffectivePower + turrets[c].EffectivePower <= controller.Budget.MaximumPower)
                            _selected = new[] { turrets[a], turrets[b], turrets[c] };
                    }
            if (_selected == null) throw new InvalidOperationException("The authored Scene has no affordable non-collinear turret triangle.");
            foreach (var turret in _selected)
                if (controller.RequestActivation(turret.InstanceId, true) != FieldTurretActivationResult.Activated)
                    throw new InvalidOperationException("Authored turret activation rejected.");
        }
        private static string RegionHash(NetworkMatchDescriptor descriptor, RegionGridSnapshot snapshot)
            => Hash(RegionReplicationCodec.Encode(new RegionReplicationFrame(descriptor.DescriptorId, 1, snapshot)));
        private static string TurretIdentityHash(TurretMatchBinding binding)
            => Hash(Encoding.UTF8.GetBytes(string.Join("\n", binding.Registrations.Select(item => item.EntityId.Value).OrderBy(value => value, StringComparer.Ordinal))));
        private static string Hash(byte[] bytes)
        { using var hash = SHA256.Create(); return Convert.ToBase64String(hash.ComputeHash(bytes)); }
        private void OnLog(string message, string stack, LogType type)
        {
            if (!_terminal && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && _errors.Count < 32)
                _errors.Add(message + "\n" + stack);
        }
        private void Fail(string error) => Finish(false, error);
        private void Finish(bool success, string error)
        {
            if (_terminal) return; _terminal = true;
            UnityApplication.logMessageReceived -= OnLog;
            try
            {
                if (!string.IsNullOrEmpty(_path))
                {
                    string absolute = Path.GetFullPath(_path); Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                    File.WriteAllText(absolute, JsonUtility.ToJson(new Result
                    {
                        Success = success, Role = _role, Run = _run, Step = _step, Error = error,
                        Unity = UnityApplication.unityVersion, Build = UnityApplication.buildGUID, ProcessId = Process.GetCurrentProcess().Id,
                        Observations = _observations.ToArray(), Errors = _errors.ToArray(),
                        TurretChecks = _turretProbe?.Checks ?? Array.Empty<string>(), TurretFixtureScope = CoreNetworkTurretValidation.FixtureScope
                    }, true));
                }
            }
            finally
            {
                if (success) Debug.Log("CORE_NETWORK_PROBE_SUCCESS " + _role);
                else Debug.LogError("CORE_NETWORK_PROBE_FAILED " + _role + ": " + error);
                UnityApplication.Quit(success ? 0 : 1);
            }
        }
        private void OnDestroy()
        {
            UnityApplication.logMessageReceived -= OnLog;
            _turretProbe?.Dispose();
            _probe?.Dispose();
        }
    }
}
#endif
