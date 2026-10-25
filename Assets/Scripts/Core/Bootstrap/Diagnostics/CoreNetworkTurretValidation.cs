// 명시적 개발 빌드 검사에서 실제 Client 요청·Host D 실행·표시 전송의 연결을 검증합니다.
// 검사 지시 채널과 생산용 명령 채널을 분리하며 개발 actor fixture의 한계를 결과에 남깁니다.
// 전력·상태·권한 규칙을 우회하지 않으며 실제 원격 Player 복제는 검증하지 않습니다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class CoreNetworkTurretValidation : IDisposable
    {
        [Serializable] private sealed class Packet
        {
            public string Run, Tag, Target, Request, Match, Descriptor, Status, Code, Hash;
            public bool Receipt, Replay; public long Sequence, Revision;
        }
        private static readonly string[] Steps = { "On", "Replay", "Off", "Far", "NoActor", "ForeignMatch", "ForgedIssuer" };
        private readonly InitialStageNetworkBinding _binding;
        private readonly NgoNetworkSession _network;
        private readonly AppRoot _app;
        private readonly ITurretCommandTransport _commands;
        private readonly string _run;
        private readonly NgoMessageChannel _channel;
        private readonly Dictionary<string, string> _frameHashes = new Dictionary<string, string>();
        private readonly Queue<string> _frameKeys = new Queue<string>();
        private readonly List<string> _checks = new List<string>();
        private ITurretPresentationSource _source;
        private ITurretCommandRequester _requester;
        private GameObject _fixture;
        private IDisposable _actorLease;
        private TurretBase _target;
        private Packet _challenge;
        private TurretCommandResult _result;
        private Guid _activationId;
        private long _activationSequence;
        private int _activationReserved, _index = -1;
        private ulong _peer;
        private bool _disposed;
        public bool Completed { get; private set; }
        public string Failure { get; private set; }
        public EntityId TargetId { get; private set; }
        public string[] Checks => _checks.ToArray();
        public const string FixtureScope = "Development-only Host interaction actor fixture; production Client gateway/Host authorization/D executor/HUD transport. Not B remote actor replication.";

        public CoreNetworkTurretValidation(InitialStageNetworkBinding binding, NgoNetworkSession network, string run)
        {
            _binding = binding; _network = network; _run = run;
            _app = Object.FindAnyObjectByType<AppRoot>();
            if (_app == null || _app.NetworkTurretCommands == null) throw new InvalidOperationException("The production turret transport is required.");
            _commands = _app.NetworkTurretCommands;
            _channel = new NgoMessageChannel(network.Manager, "teamhjd.validation.turrets.v1");
            _channel.MessageReceived += OnPacket;
            _commands.ResultReceived += OnRawResult;
        }
        public void Pump()
        {
            if (_disposed || Failure != null) return;
            try
            {
                if (!ReferenceEquals(_source, _binding.TurretPresentation))
                {
                    if (_source != null) _source.Changed -= CaptureFrame;
                    _source = _binding.TurretPresentation;
                    if (_source != null) { _source.Changed += CaptureFrame; CaptureFrame(_source.Snapshot); }
                }
                if (!ReferenceEquals(_requester, _binding.TurretCommands))
                {
                    if (_requester != null) _requester.ResultReceived -= OnResult;
                    _requester = _binding.TurretCommands;
                    if (_requester != null) _requester.ResultReceived += OnResult;
                }
                _channel.Pump();
                if (!_network.IsServer) SendReceiptWhenReady();
            }
            catch (Exception error) { Failure = error.ToString(); }
        }
        private static string Key(Guid descriptor, long revision) => descriptor.ToString("N") + "/" + revision;
        public static string Hash(TurretPresentationFrame frame)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(TurretPresentationCodec.Encode(frame)));
        }
        private void CaptureFrame(TurretPresentationFrame frame)
        {
            if (!_network.IsServer || frame == null) return;
            string key = Key(frame.DescriptorId, frame.Revision);
            if (_frameHashes.ContainsKey(key)) return;
            _frameHashes.Add(key, Hash(frame)); _frameKeys.Enqueue(key);
            while (_frameKeys.Count > 512) _frameHashes.Remove(_frameKeys.Dequeue());
        }
        public bool MatchesAuthoritativeFrame(Guid descriptor, long revision, string hash)
        {
            CaptureFrame(_binding.TurretPresentation?.Snapshot);
            return _frameHashes.TryGetValue(Key(descriptor, revision), out string expected) && expected == hash;
        }
        public void Begin(ulong peer)
        {
            if (!_network.IsServer || _index != -1) throw new InvalidOperationException("Begin the turret probe once on Host.");
            _peer = peer;
            var entities = _binding.GetComponent<TurretMatchBinding>();
            _target = TurretInstanceRegistry.GetAll().Where(t => entities.Controller.OwnsTurret(t) && t.IsInitialized &&
                !t.IsDestroyed && !t.IsLocked && !t.IsActivated && !t.IsCoolingDown && !t.IsManualOperationBlocked &&
                t.EffectivePower <= entities.Controller.Budget.CurrentPower).OrderBy(t => t.EffectivePower).FirstOrDefault();
            if (_target == null || !entities.TryResolveEntityId(_target.InstanceId, out var id))
                throw new InvalidOperationException("No legal authored turret for the real D command probe.");
            TargetId = id;
            if (!_binding.Matches.TryResolveParticipant(peer, out PlayerId player)) throw new InvalidOperationException("Missing actual Client participant.");
            _fixture = new GameObject("Development verification interaction actor (not B replication)");
            SceneManager.MoveGameObjectToScene(_fixture, _binding.gameObject.scene);
            _fixture.transform.position = _target.transform.position;
            _fixture.AddComponent<PlayerActorIdentity>().Bind(player);
            _fixture.AddComponent<CoreNetworkValidationActor>();
            _actorLease = _binding.RegisterAuthorityPlayerActor(entities.BoundMatchId, player, _fixture);
            _activationId = Guid.NewGuid(); _index = 0; SendChallenge();
        }
        private void SendChallenge()
        {
            string tag = Steps[_index];
            if (tag == "Far") _fixture.transform.position = _target.transform.position + new Vector3(10000, 10000, 0);
            if (tag == "NoActor") { _actorLease.Dispose(); _actorLease = null; }
            _challenge = new Packet { Run = _run, Tag = tag, Target = TargetId.Value,
                Request = (tag == "On" || tag == "Replay" ? _activationId : Guid.NewGuid()).ToString("N"),
                Match = _binding.Matches.Descriptor.Config.MatchId.Value,
                Descriptor = _binding.Matches.Descriptor.DescriptorId.ToString("N") };
            Send(_peer, _challenge);
        }
        private void Send(ulong peer, Packet packet)
        {
            if (!_channel.TrySend(peer, Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet))))
                throw new InvalidOperationException("The diagnostic command could not be queued.");
        }
        private void OnPacket(ulong sender, byte[] bytes)
        {
            try
            {
                if (_disposed || bytes.Length > 4096) throw new InvalidOperationException("Invalid diagnostic packet.");
                var packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
                if (packet == null || packet.Run != _run || _binding.Matches.Descriptor == null ||
                    packet.Descriptor != _binding.Matches.Descriptor.DescriptorId.ToString("N"))
                    throw new InvalidOperationException("Wrong command verification generation.");
                if (_network.IsServer)
                {
                    if (sender != _peer || !packet.Receipt || _challenge == null || packet.Tag != _challenge.Tag ||
                        packet.Request != _challenge.Request || packet.Target != _challenge.Target)
                        throw new InvalidOperationException("Wrong command verification receipt.");
                    VerifyReceipt(packet); _checks.Add(packet.Tag + ":" + packet.Status + ":" + packet.Code);
                    _index++;
                    if (_index == Steps.Length) { Completed = true; ReleaseFixture(); _challenge = null; }
                    else SendChallenge();
                }
                else
                {
                    if (sender != 0 || packet.Receipt || _challenge != null) throw new InvalidOperationException("Unexpected command challenge.");
                    _challenge = packet; _result = null;
                    var descriptor = _binding.Matches.Descriptor;
                    if (!_binding.Matches.TryResolveParticipant(_network.LocalConnectionId, out PlayerId player))
                        throw new InvalidOperationException("Missing Client identity.");
                    MatchId match = packet.Tag == "ForeignMatch" ? new MatchId("verification-foreign-match") : descriptor.Config.MatchId;
                    if (packet.Tag == "ForgedIssuer" && !_binding.Matches.TryResolveParticipant(0, out player))
                        throw new InvalidOperationException("Missing Host identity.");
                    var request = new TurretCommandRequest(new MatchRequestContext(match, Guid.ParseExact(packet.Request, "N"), player),
                        TurretCommandKind.SetActivation, new EntityId(packet.Target), packet.Tag != "Off");
                    bool raw = packet.Tag == "ForeignMatch" || packet.Tag == "ForgedIssuer";
                    bool queued = raw ? _commands.TrySendRequest(0, descriptor.DescriptorId, request) :
                        _binding.TurretCommands != null && _binding.TurretCommands.TrySubmit(request);
                    if (!queued) throw new InvalidOperationException("Production Client command path rejected the verification intent.");
                }
            }
            catch (Exception error) { Failure = error.ToString(); }
        }
        private void OnRawResult(ulong sender, Guid descriptor, TurretCommandResult result)
        {
            if (_challenge == null || (_challenge.Tag != "ForeignMatch" && _challenge.Tag != "ForgedIssuer")) return;
            if (sender == 0 && descriptor.ToString("N") == _challenge.Descriptor) OnResult(result);
        }
        private void OnResult(TurretCommandResult result)
        {
            if (_network.IsServer || _challenge == null || result == null || result.RequestId.ToString("N") != _challenge.Request ||
                result.Status == TurretCommandStatus.InProgress) return;
            _result = result;
        }
        private void SendReceiptWhenReady()
        {
            if (_challenge == null || _result == null) return;
            var frame = _binding.TurretPresentation?.Snapshot;
            if (frame == null || frame.DescriptorId.ToString("N") != _challenge.Descriptor) return;
            if (_result.WasAccepted && frame.ConfirmedCommandSequence < _result.ExecutionSequence) return;
            if (!frame.TryGet(new EntityId(_challenge.Target), out var row)) throw new InvalidOperationException("Authoritative HUD omitted the target.");
            if (_result.WasAccepted && row.IsActivated != (_challenge.Tag != "Off"))
                throw new InvalidOperationException("Client HUD did not reflect its confirmed activation intent.");
            Packet packet = _challenge;
            packet.Receipt = true; packet.Status = _result.Status.ToString(); packet.Code = _result.FeatureCode;
            packet.Match = _result.MatchId.Value; packet.Replay = _result.IsReplay; packet.Sequence = _result.ExecutionSequence;
            packet.Revision = frame.Revision; packet.Hash = Hash(frame);
            Send(0, packet); _checks.Add(packet.Tag + ":" + packet.Status + ":" + packet.Code);
            _challenge = null; _result = null;
        }
        private void VerifyReceipt(Packet packet)
        {
            if (!MatchesAuthoritativeFrame(Guid.ParseExact(packet.Descriptor, "N"), packet.Revision, packet.Hash))
                throw new InvalidOperationException("The complete Client HUD frame differs from the Host source.");
            string expected = packet.Tag == "ForeignMatch" ? "DifferentMatch" :
                packet.Tag == "Far" || packet.Tag == "NoActor" || packet.Tag == "ForgedIssuer" ? "Unauthorized" : "Applied";
            if (packet.Status != expected || packet.Replay != (packet.Tag == "Replay"))
                throw new InvalidOperationException("Unexpected command outcome: " + packet.Tag + "/" + packet.Status + "/" + packet.Code);
            bool foreign = packet.Tag == "ForeignMatch";
            if (packet.Match != (foreign ? "verification-foreign-match" : _binding.Matches.Descriptor.Config.MatchId.Value))
                throw new InvalidOperationException("The result did not echo the request's Match.");
            if ((packet.Tag == "Far" && packet.Code != "OutOfInteractionRange") ||
                (packet.Tag == "NoActor" && packet.Code != "AuthorityActorUnavailable"))
                throw new InvalidOperationException("Host interaction validation was not applied.");
            if (packet.Tag == "On") { _activationSequence = packet.Sequence; _activationReserved = _binding.GetComponent<TurretMatchBinding>().Controller.Budget.ReservedPower; }
            if (packet.Tag == "Replay" && (packet.Sequence != _activationSequence ||
                _activationReserved != _binding.GetComponent<TurretMatchBinding>().Controller.Budget.ReservedPower))
                throw new InvalidOperationException("Retry re-executed or charged power twice.");
            if (_target.IsActivated != (packet.Tag == "On" || packet.Tag == "Replay"))
                throw new InvalidOperationException("Unexpected real D turret state after command.");
        }
        private void ReleaseFixture()
        {
            _actorLease?.Dispose(); _actorLease = null;
            if (_fixture != null) Object.Destroy(_fixture);
            _fixture = null;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _channel.MessageReceived -= OnPacket; _channel.Dispose();
            _commands.ResultReceived -= OnRawResult;
            if (_source != null) _source.Changed -= CaptureFrame;
            if (_requester != null) _requester.ResultReceived -= OnResult;
            ReleaseFixture(); _frameHashes.Clear(); _frameKeys.Clear();
        }
    }
}
#endif
