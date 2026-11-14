// Match의 Player actor를 생성·연결하고 Match 종료 때 Scene 객체를 정리합니다.
// Match 규칙 상태는 MatchSession, Unity 객체 수명은 이 Scene 범위 객체가 맡습니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class MatchPlayerActorScope : IDisposable
    {
        private readonly IPlayerActorSpawner _spawner;
        private readonly Dictionary<PlayerId, GameObject> _actors = new Dictionary<PlayerId, GameObject>();
        private bool _isDisposed;

        public IReadOnlyDictionary<PlayerId, GameObject> Actors => _actors;

        private MatchPlayerActorScope(IPlayerActorSpawner spawner)
        {
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        }

        public static MatchPlayerActorScope SpawnPlayers(
            MatchSession session,
            IPlayerActorSpawner spawner,
            IEnumerable<PlayerStart> playerStarts)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (playerStarts == null) throw new ArgumentNullException(nameof(playerStarts));

            var scope = new MatchPlayerActorScope(spawner);
            try
            {
                var startsBySlot = new SortedDictionary<int, PlayerStart>();
                foreach (PlayerStart start in playerStarts)
                {
                    if (start == null) throw new ArgumentException("Player starts cannot contain null entries.", nameof(playerStarts));
                    if (!start.isActiveAndEnabled) continue;
                    if (!startsBySlot.TryAdd(start.SlotIndex, start))
                        throw new ArgumentException($"Duplicate PlayerStart slot '{start.SlotIndex}'.", nameof(playerStarts));
                }

                if (session.Config.Players.Count > startsBySlot.Count)
                    throw new InvalidOperationException(
                        $"Match has {session.Config.Players.Count} players but only {startsBySlot.Count} active PlayerStart slots.");

                int slot = 0;
                foreach (PlayerLoadoutSnapshot player in session.Config.Players)
                {
                    if (!startsBySlot.TryGetValue(slot, out PlayerStart playerStart))
                        throw new InvalidOperationException(
                            $"PlayerStart slots must be contiguous from zero; missing slot '{slot}'.");

                    GameObject actor = spawner.Spawn(player.PlayerId, playerStart);
                    if (actor == null) throw new InvalidOperationException($"Spawner returned no actor for player '{player.PlayerId}'.");

                    scope._actors.Add(player.PlayerId, actor);
                    PlayerSceneAdapter adapter = actor.GetComponent<PlayerSceneAdapter>();
                    if (adapter == null) adapter = actor.AddComponent<PlayerSceneAdapter>();
                    adapter.Bind(session);
                    slot++;
                }

                return scope;
            }
            catch (Exception error)
            {
                try { scope.Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException("Player scope setup and cleanup failed.", error, cleanupError); }
                throw;
            }
        }

        /// <summary>해당 연결이 소유한 actor만 생성합니다. Client에는 권한 MatchSession을 만들지 않습니다.</summary>
        public static MatchPlayerActorScope SpawnLocalPlayer(
            NetworkMatchCoordinator match, ulong localConnectionId, IPlayerActorSpawner spawner,
            IEnumerable<PlayerStart> playerStarts, MatchSession authoritySession = null)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (match.Phase != NetworkMatchPhase.Running || match.Descriptor == null)
                throw new InvalidOperationException("A started network Match is required.");
            if (!match.Descriptor.TryGetSlot(localConnectionId, out int slot) ||
                !match.TryResolveParticipant(localConnectionId, out PlayerId playerId))
                throw new InvalidOperationException("The local connection is not a Match participant.");
            if (authoritySession != null && (authoritySession.IsDisposed ||
                authoritySession.Config.MatchId != match.Descriptor.Config.MatchId))
                throw new ArgumentException("The authority session belongs to another Match.", nameof(authoritySession));
            if (playerStarts == null) throw new ArgumentNullException(nameof(playerStarts));
            var scope = new MatchPlayerActorScope(spawner);
            try
            {
                var starts = new Dictionary<int, PlayerStart>();
                foreach (PlayerStart start in playerStarts)
                {
                    if (start == null) throw new ArgumentException("Player starts cannot contain null entries.", nameof(playerStarts));
                    if (start.isActiveAndEnabled && !starts.TryAdd(start.SlotIndex, start))
                        throw new ArgumentException($"Duplicate PlayerStart slot '{start.SlotIndex}'.", nameof(playerStarts));
                }
                if (!starts.TryGetValue(slot, out PlayerStart localStart))
                    throw new InvalidOperationException($"Missing local PlayerStart slot '{slot}'.");
                GameObject actor = spawner.Spawn(playerId, localStart);
                if (actor == null) throw new InvalidOperationException("Spawner returned no local actor.");
                // Bind 실패도 이 scope의 정리 대상이어야 합니다.
                scope._actors.Add(playerId, actor);
                PlayerSceneAdapter adapter = actor.GetComponent<PlayerSceneAdapter>();
                if (adapter == null) adapter = actor.AddComponent<PlayerSceneAdapter>();
                if (authoritySession != null) adapter.Bind(authoritySession);
                else adapter.BindNetworkMatch(match, playerId);
                return scope;
            }
            catch (Exception error)
            {
                try { scope.Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException("Player scope setup and cleanup failed.", error, cleanupError); }
                throw;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            var actors = new List<GameObject>(_actors.Values);
            _actors.Clear();
            List<Exception> errors = null;
            foreach (GameObject actor in actors)
            {
                if (actor == null) continue;
                try
                {
                    PlayerSceneAdapter adapter = actor.GetComponent<PlayerSceneAdapter>();
                    if (adapter != null) adapter.Unbind();
                }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            }
            try { _spawner.Dispose(); }
            catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            if (errors != null) throw new AggregateException("Player scope cleanup failed after all owned actors were released.", errors);
        }
    }
}
