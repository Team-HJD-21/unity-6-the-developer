// Client의 공유 Match 바인딩이 권한 세션 없이 로컬 슬롯 하나만 생성하는지 검사합니다.
// 실제 패킷/이동 복제 대신 Scene actor 소유권과 종료 후 입력 잠금만 검증합니다.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class NetworkLocalActorTests
{
    private sealed class ClientSession : INetworkSession
    {
        public NetworkConnectionState State => NetworkConnectionState.ConnectedClient;
        public bool IsServer => false;
        public ulong LocalConnectionId => 42;
        public event Action<NetworkConnectionState> StateChanged { add { } remove { } }
        public event Action<ulong> PeerConnected { add { } remove { } }
        public event Action<ulong> PeerDisconnected { add { } remove { } }
        public bool TryStartHost() => false;
        public bool TryStartClient() => false;
        public void Shutdown() { }
        public void Dispose() { }
    }
    private sealed class Wire : IMatchLifecycleTransport
    {
        public event Action<ulong, NetworkMatchDescriptor> DescriptorReceived;
        public event Action<ulong, MatchLifecycleSignalKind, MatchLifecycleSignal> SignalReceived;
        public bool TrySendDescriptor(ulong target, NetworkMatchDescriptor descriptor) => false;
        public bool TrySendSignal(ulong target, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal) => true;
        public void Prepare(NetworkMatchDescriptor descriptor) => DescriptorReceived?.Invoke(0, descriptor);
        public void Signal(MatchLifecycleSignalKind kind, NetworkMatchDescriptor descriptor)
            => SignalReceived?.Invoke(0, kind, new MatchLifecycleSignal(descriptor.Config.MatchId, descriptor.DescriptorId));
    }
    private sealed class Spawner : IPlayerActorSpawner
    {
        public GameObject Actor; public int Count; public bool Disposed;
        public GameObject Spawn(PlayerId id, PlayerStart start)
        {
            Count++; Actor = new GameObject("Test local actor", typeof(PlayerControlState));
            Actor.transform.position = start.Position; return Actor;
        }
        public bool Despawn(PlayerId id) => false;
        public void Dispose() { Disposed = true; if (Actor != null) Object.DestroyImmediate(Actor); }
    }
    private readonly List<GameObject> _starts = new List<GameObject>();
    [TearDown]
    public void Cleanup() { foreach (GameObject start in _starts) Object.DestroyImmediate(start); _starts.Clear(); }
    private PlayerStart Start(int slot)
    {
        var item = new GameObject("Start " + slot, typeof(PlayerStart)); _starts.Add(item);
        item.transform.position = Vector3.right * slot * 10;
        var serialized = new SerializedObject(item.GetComponent<PlayerStart>());
        serialized.FindProperty("slotIndex").intValue = slot; serialized.ApplyModifiedPropertiesWithoutUndo();
        return item.GetComponent<PlayerStart>();
    }
    private static NetworkMatchDescriptor Descriptor()
    {
        var host = new PlayerId("host"); var client = new PlayerId("client");
        var config = new MatchConfig(new MatchId("client-actor-test"), MatchMode.Cooperative,
            new StageId("initial-stage"), new DefinitionId("difficulty"), "poc", 1,
            new[] { new PlayerLoadoutSnapshot(host, null), new PlayerLoadoutSnapshot(client, null) });
        return new NetworkMatchDescriptor(Guid.NewGuid(), 1, config, RegionPolicyMode.Dynamic,
            new RegionGridDefinition(BattlefieldGridConfiguration.Default), new[]
            { new MatchParticipantBinding(0, host, true), new MatchParticipantBinding(42, client, false) });
    }
    [Test]
    public void ClientSpawnsOnlyItsSlot_AndEndsInputWithoutAnAuthoritySession()
    {
        var wire = new Wire(); using var match = new NetworkMatchCoordinator(new ClientSession(), wire);
        NetworkMatchDescriptor descriptor = Descriptor(); wire.Prepare(descriptor); match.MarkLocalReady();
        wire.Signal(MatchLifecycleSignalKind.Started, descriptor);
        var spawner = new Spawner();
        using var scope = MatchPlayerActorScope.SpawnLocalPlayer(match, 42, spawner, new[] { Start(0), Start(1) });
        Assert.That(scope.Actors.Count, Is.EqualTo(1));
        Assert.That(scope.Actors.ContainsKey(new PlayerId("client")), Is.True);
        Assert.That(spawner.Count, Is.EqualTo(1));
        Assert.That(spawner.Actor.transform.position.x, Is.EqualTo(10));
        PlayerControlState control = spawner.Actor.GetComponent<PlayerControlState>();
        Assert.That(control.HasMatchBinding, Is.True); Assert.That(control.CanAct, Is.True);
        wire.Signal(MatchLifecycleSignalKind.Ended, descriptor);
        Assert.That(control.CanAct, Is.False); Assert.That(control.CanPrepare, Is.False);
        scope.Dispose(); Assert.That(spawner.Disposed, Is.True);
    }
    [Test]
    public void MissingOrDuplicateLocalStartIsRejectedBeforeActorCreation()
    {
        var wire = new Wire(); using var match = new NetworkMatchCoordinator(new ClientSession(), wire);
        var descriptor = Descriptor(); wire.Prepare(descriptor); match.MarkLocalReady(); wire.Signal(MatchLifecycleSignalKind.Started, descriptor);
        var spawner = new Spawner();
        Assert.Throws<InvalidOperationException>(() => MatchPlayerActorScope.SpawnLocalPlayer(match, 42, spawner, new[] { Start(0) }));
        Assert.That(spawner.Count, Is.Zero); Assert.That(spawner.Disposed, Is.True);
        var other = new Spawner();
        Assert.Throws<ArgumentException>(() => MatchPlayerActorScope.SpawnLocalPlayer(match, 42, other, new[] { Start(1), Start(1) }));
        Assert.That(other.Count, Is.Zero); Assert.That(other.Disposed, Is.True);
    }
}
