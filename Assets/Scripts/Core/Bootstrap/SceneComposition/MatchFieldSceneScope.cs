// 한 권위 Match의 참가자 위치 등록과 필드 Feature 시간 바인딩을 소유합니다.
// Scene 조립부의 생성·해제 순서만 담당하며 출입 규칙과 Feature 동작은 위임합니다.
using System;
using System.Threading;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    internal sealed class MatchFieldSceneScope : IDisposable
    {
        private readonly FieldSimulationSceneScope _features;
        private readonly List<ActorLease> _actors = new();
        private bool _disposed;
        private readonly IMatchTime _time;
        private readonly CancellationToken _gameplay;
        public MatchPresenceService Presence { get; }
        public MatchFieldSceneScope(MatchConfig config, MatchTimeService time, CancellationToken gameplay, Func<bool> authority,
            IEnumerable<IFieldSimulationConsumer> features, IReadOnlyDictionary<PlayerId, GameObject> actors)
        {
            _time = time.Clock; _gameplay = gameplay;
            Presence = new MatchPresenceService(config, authority, time.SetPaused);
            try
            {
                _features = new FieldSimulationSceneScope(features, time.Clock, gameplay);
                foreach (var actor in actors) RegisterActor(actor.Key, actor.Value);
            }
            catch (Exception error)
            {
                try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }
        public IDisposable RegisterActor(PlayerId playerId, GameObject actor)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchFieldSceneScope));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            var component = actor.GetComponent<PlayerMatchPresence>() ?? actor.AddComponent<PlayerMatchPresence>();
            var registration = Presence.RegisterActor(playerId);
            try { component.Bind(registration, _time, _gameplay); }
            catch { registration.Dispose(); throw; }
            var lease = new ActorLease(this, component, registration); _actors.Add(lease); return lease;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            Presence.Dispose();
            var errors = new List<Exception>();
            foreach (var actor in new List<ActorLease>(_actors)) try { actor.Dispose(); } catch (Exception e) { errors.Add(e); }
            try { _features?.Dispose(); } catch (Exception e) { errors.Add(e); }
            _actors.Clear();
            if (errors.Count > 0) throw new AggregateException("Field bindings were released with errors.", errors);
        }
        private sealed class ActorLease : IDisposable
        {
            private PlayerMatchPresence _actor;
            private MatchFieldSceneScope _owner;
            private readonly MatchPresenceService.Registration _registration;
            public ActorLease(MatchFieldSceneScope owner, PlayerMatchPresence actor, MatchPresenceService.Registration registration)
            { _owner = owner; _actor = actor; _registration = registration; }
            public void Dispose()
            {
                var actor = _actor; _actor = null; var owner = _owner; _owner = null;
                owner?._actors.Remove(this); if (actor != null) actor.Unbind(_registration);
            }
        }
    }
}
