// Bridges scene-owned player input to the current match command boundary.

using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class PlayerSceneAdapter : MonoBehaviour
    {
        private MatchSession _session;

        public void Bind(MatchSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            Unbind();
            _session = session;
        }

        public CommandSubmissionResult Submit(GameCommand command)
        {
            if (_session == null) throw new InvalidOperationException("PlayerSceneAdapter is not bound to a match.");
            return _session.Submit(command);
        }

        public void Unbind()
        {
            _session = null;
        }

        private void OnDestroy() => Unbind();
    }
}
