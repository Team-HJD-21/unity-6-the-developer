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
