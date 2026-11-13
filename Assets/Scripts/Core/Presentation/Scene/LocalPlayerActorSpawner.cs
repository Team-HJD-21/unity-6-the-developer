// 로컬 PoC용 Player prefab을 생성하고 Match 종료 시 생성 인스턴스를 정리합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class LocalPlayerActorSpawner : IPlayerActorSpawner
    {
        private readonly GameObject _playerPrefab;
        private readonly Transform _actorParent;
        private readonly IRandomStreams _presentationRandom;
        private readonly Dictionary<PlayerId, GameObject> _actors = new Dictionary<PlayerId, GameObject>();
        private bool _isDisposed;

        public LocalPlayerActorSpawner(GameObject playerPrefab, Transform actorParent = null,
            IRandomStreams presentationRandom = null)
        {
            _playerPrefab = playerPrefab != null
                ? playerPrefab
                : throw new ArgumentNullException(nameof(playerPrefab));
            _actorParent = actorParent;
            _presentationRandom = presentationRandom;
        }

        public GameObject Spawn(PlayerId playerId, PlayerStart playerStart)
        {
            ThrowIfDisposed();
            if (playerId.IsEmpty) throw new ArgumentException("A spawned player requires a valid ID.", nameof(playerId));
            if (playerStart == null) throw new ArgumentNullException(nameof(playerStart));
            if (!playerStart.isActiveAndEnabled) throw new InvalidOperationException("PlayerStart must be active.");
            if (_actorParent != null && _actorParent.gameObject.scene != playerStart.gameObject.scene)
                throw new InvalidOperationException("The actor parent and PlayerStart must belong to the same Match scene.");
            if (_actors.ContainsKey(playerId)) throw new InvalidOperationException($"Player '{playerId}' is already spawned.");

            GameObject actor = UnityEngine.Object.Instantiate(
                _playerPrefab,
                playerStart.Position,
                playerStart.Rotation,
                _actorParent);

            try
            {
                // Additive 출격에서도 활성 Hub Scene이 아닌 PlayerStart의 Match Scene에 귀속시킵니다.
                if (actor.scene != playerStart.gameObject.scene)
                    SceneManager.MoveGameObjectToScene(actor, playerStart.gameObject.scene);
                PlayerActorIdentity identity = actor.GetComponent<PlayerActorIdentity>();
                if (identity == null) identity = actor.AddComponent<PlayerActorIdentity>();
                identity.Bind(playerId, playerStart.SlotIndex);
                if (_presentationRandom != null)
                    foreach (var component in actor.GetComponents<MonoBehaviour>())
                        if (component is IPlayerPresentationRandomBinding binding)
                            binding.BindPresentationRandom(_presentationRandom);
                _actors.Add(playerId, actor);
                return actor;
            }
            catch
            {
                UnityEngine.Object.Destroy(actor);
                throw;
            }
        }

        public bool Despawn(PlayerId playerId)
        {
            ThrowIfDisposed();
            if (!_actors.TryGetValue(playerId, out GameObject actor)) return false;

            _actors.Remove(playerId);
            if (actor != null) UnityEngine.Object.Destroy(actor);
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (GameObject actor in _actors.Values)
            {
                if (actor != null) UnityEngine.Object.Destroy(actor);
            }

            _actors.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(LocalPlayerActorSpawner));
        }
    }
}
