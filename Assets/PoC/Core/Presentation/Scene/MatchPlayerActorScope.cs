// Match의 Player actor를 생성·연결하고 Match 종료 때 Scene 객체를 정리합니다.
// Match 규칙 상태는 MatchSession, Unity 객체 수명은 이 Scene 범위 객체가 맡습니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
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

                if (session.State.Players.Count > startsBySlot.Count)
                    throw new InvalidOperationException(
                        $"Match has {session.State.Players.Count} players but only {startsBySlot.Count} active PlayerStart slots.");

                int slot = 0;
                foreach (PlayerState player in session.State.Players)
                {
                    if (!startsBySlot.TryGetValue(slot, out PlayerStart playerStart))
                        throw new InvalidOperationException(
                            $"PlayerStart slots must be contiguous from zero; missing slot '{slot}'.");

                    GameObject actor = spawner.Spawn(player.PlayerId, playerStart);
                    if (actor == null) throw new InvalidOperationException($"Spawner returned no actor for player '{player.PlayerId}'.");

                    PlayerSceneAdapter adapter = actor.GetComponent<PlayerSceneAdapter>();
                    if (adapter == null) adapter = actor.AddComponent<PlayerSceneAdapter>();
                    adapter.Bind(session);
                    scope._actors.Add(player.PlayerId, actor);
                    slot++;
                }

                return scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (GameObject actor in _actors.Values)
            {
                if (actor == null) continue;
                PlayerSceneAdapter adapter = actor.GetComponent<PlayerSceneAdapter>();
                if (adapter != null) adapter.Unbind();
            }

            _actors.Clear();
            _spawner.Dispose();
        }
    }
}
