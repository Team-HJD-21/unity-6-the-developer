// HUD 요청 추적에서 응답/표시 상태 도착 순서·동기 응답·재전송·구독 해제를 검사합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
public sealed class StageTurretCommandBindingTests
{
    private static readonly MatchId Match = new MatchId("match");
    private static readonly PlayerId Player = new PlayerId("player");
    private static readonly EntityId Target = new EntityId("turret/key");
    private sealed class Requests : ITurretCommandRequester
    {
        public bool Accept = true; public TurretCommandRequest Last; public Guid Retried; public Action<TurretCommandRequest> DuringSubmit;
        private Action<TurretCommandResult> _received;
        public int Subscribers => _received?.GetInvocationList().Length ?? 0;
        public event Action<TurretCommandResult> ResultReceived { add => _received += value; remove => _received -= value; }
        public int PendingCount => 0;
        public bool TrySubmit(TurretCommandRequest request) { Last = request; DuringSubmit?.Invoke(request); return Accept; }
        public bool TryRetry(Guid requestId) { Retried = requestId; return true; }
        public void Respond(TurretCommandResult result) => _received?.Invoke(result);
    }
    private sealed class Source : ITurretPresentationSource
    {
        private readonly Guid _descriptor = Guid.NewGuid();
        public TurretPresentationFrame Snapshot { get; private set; }
        private Action<TurretPresentationFrame> _changed;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event Action<TurretPresentationFrame> Changed { add => _changed += value; remove => _changed -= value; }
        public void Set(long confirmed)
        {
            Snapshot = new TurretPresentationFrame(Match, _descriptor, confirmed + 1,
                new TurretControllerPresentationSnapshot(100, 100, 0, 0, 100, 100, default, false, 0, RegionPolicyMode.Dynamic),
                Array.Empty<TurretPresentationSnapshot>(), confirmed); _changed?.Invoke(Snapshot);
        }
    }
    private static TurretCommandResult Result(TurretCommandRequest request, long sequence, TurretCommandStatus status = TurretCommandStatus.Applied)
        => new TurretCommandResult(request.Context.MatchId, request.Context.RequestId, request.Kind, request.TargetId, sequence, status);
    [TestCase(TurretCommandStatus.Applied)]
    [TestCase(TurretCommandStatus.ExecutionFailed)]
    public void AckBeforeStateKeepsIntentPendingAndRetriesExactGuid(TurretCommandStatus status)
    {
        var requests = new Requests(); var source = new Source(); source.Set(0);
        using var binding = new StageTurretCommandBinding(Match, Player, requests, source); int results = 0;
        Assert.That(binding.Matches(Match, Player, requests, source), Is.True);
        Assert.That(binding.Matches(Match, Player, new Requests(), source), Is.False);
        binding.ResultReceived += result => results++;
        Assert.That(binding.TrySubmit(TurretCommandKind.SetActivation, Target, true), Is.True); var intent = requests.Last;
        requests.Respond(Result(intent, 5, status)); Assert.That(binding.HasPending(Target), Is.True); Assert.That(results, Is.Zero);
        Assert.That(binding.TrySubmit(TurretCommandKind.SetActivation, Target, false), Is.False);
        Assert.That(binding.TryRetry(Target), Is.True); Assert.That(requests.Retried, Is.EqualTo(intent.Context.RequestId));
        source.Set(4); Assert.That(binding.HasPending(Target), Is.True);
        source.Set(5); Assert.That(binding.HasPending(Target), Is.False); Assert.That(results, Is.EqualTo(1));
        requests.Respond(Result(intent, 5, status)); Assert.That(results, Is.EqualTo(1));
    }
    [Test]
    public void StateBeforeSynchronousAckIsNotLost()
    {
        var requests = new Requests(); var source = new Source();
        using var binding = new StageTurretCommandBinding(Match, Player, requests, source); int results = 0;
        binding.ResultReceived += result => results++;
        requests.DuringSubmit = request => { source.Set(2); requests.Respond(Result(request, 2)); };
        Assert.That(binding.TrySubmit(TurretCommandKind.StartAutoActivation), Is.True);
        Assert.That(binding.HasPending(default), Is.False); Assert.That(results, Is.EqualTo(1));
    }
    [Test]
    public void RejectionClearsImmediatelyAndDisposeReleasesSubscriptions()
    {
        var requests = new Requests { Accept = false }; var source = new Source();
        using var binding = new StageTurretCommandBinding(Match, Player, requests, source);
        Assert.That(binding.TrySubmit(TurretCommandKind.SetActivation, Target, true), Is.False); Assert.That(binding.HasPending(Target), Is.False);
        requests.Accept = true; binding.TrySubmit(TurretCommandKind.SetActivation, Target, true);
        requests.Respond(Result(requests.Last, 0, TurretCommandStatus.Unauthorized)); Assert.That(binding.HasPending(Target), Is.False);
        binding.Dispose(); Assert.That(requests.Subscribers, Is.Zero); Assert.That(source.Subscribers, Is.Zero);
        Assert.That(binding.TrySubmit(TurretCommandKind.StartAutoActivation), Is.False);
    }
}
