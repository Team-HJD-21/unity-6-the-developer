// Host가 명시적으로 주입한 실제 actor를 한 Match의 검증 제공자로 등록합니다.
// Feature가 생성/복제를 소유하며 이 바인딩은 객체를 찾거나 생성·파괴하지 않습니다.
using System;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class PlayerInteractionActorBinding : IDisposable
    {
        private readonly GameObject _actor;
        private readonly PlayerActorIdentity _identity;
        private readonly PlayerId _player;
        private readonly IPlayerInteractionActor _source;
        private readonly MonoBehaviour _behaviour;
        private readonly Func<bool> _hasAuthority;
        private IDisposable _registration;
        private bool _disposed;
        public PlayerInteractionActorBinding(PlayerInteractionRegistry registry, PlayerId playerId,
            GameObject authorityActor, Func<bool> hasAuthority)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            _hasAuthority = hasAuthority ?? throw new ArgumentNullException(nameof(hasAuthority));
            if (!hasAuthority()) throw new InvalidOperationException("Register an interaction actor only on the authority.");
            _actor = authorityActor != null ? authorityActor : throw new ArgumentNullException(nameof(authorityActor));
            if (!_actor.scene.IsValid()) throw new ArgumentException("Bind a spawned actor, not a prefab.", nameof(authorityActor));
            _identity = authorityActor.GetComponent<PlayerActorIdentity>();
            if (playerId.IsEmpty || _identity == null || _identity.PlayerId != playerId)
                throw new ArgumentException("Bind the verified participant's actor.", nameof(playerId));
            IPlayerInteractionActor[] sources = _actor.GetComponents<MonoBehaviour>().OfType<IPlayerInteractionActor>().ToArray();
            if (sources.Length != 1) throw new ArgumentException("Provide exactly one interaction-state source on the actor.", nameof(authorityActor));
            _player = playerId; _source = sources[0]; _behaviour = (MonoBehaviour)_source;
            _registration = registry.Register(playerId, Capture);
        }
        private PlayerInteractionSnapshot? Capture()
        {
            if (_disposed || !_hasAuthority() || _actor == null || !_actor.activeInHierarchy ||
                _identity == null || _identity.PlayerId != _player || _behaviour == null || !_behaviour.isActiveAndEnabled) return null;
            if (!_source.TryCaptureInteraction(out var snapshot) || _disposed || !_hasAuthority()) return null;
            return snapshot;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            IDisposable registration = _registration; _registration = null; registration?.Dispose();
        }
    }
}
