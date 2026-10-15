// Scene이 소유한 플레이어 입력을 현재 Match 명령 경계로 전달합니다.

using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class PlayerSceneAdapter : MonoBehaviour
    {
        private MatchSession _session;
        private NetworkMatchCoordinator _networkMatch;

        public void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (localPlayerId.IsEmpty) throw new ArgumentException("A local participant is required.", nameof(localPlayerId));
            Unbind(); _networkMatch = match;
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IPlayerNetworkMatchBinding binding) binding.BindNetworkMatch(match, localPlayerId);
        }

        public void Bind(MatchSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            Unbind();
            _session = session;
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IPlayerMatchBinding binding) binding.BindMatch(session);
        }

        public CommandSubmissionResult Submit(GameCommand command)
        {
            if (_session == null) throw new InvalidOperationException("PlayerSceneAdapter is not bound to a match.");
            return _session.Submit(command);
        }

        public void Unbind()
        {
            if (_networkMatch != null)
                foreach (var component in GetComponents<MonoBehaviour>())
                    if (component is IPlayerNetworkMatchBinding binding) binding.UnbindNetworkMatch();
            if (_session != null)
                foreach (var component in GetComponents<MonoBehaviour>())
                    if (component is IPlayerMatchBinding binding) binding.UnbindMatch();
            _session = null;
            _networkMatch = null;
        }

        private void OnDestroy() => Unbind();
    }
}
