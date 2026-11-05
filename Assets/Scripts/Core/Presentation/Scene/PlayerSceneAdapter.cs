// Scene의 Player Feature에 실제 Match/네트워크 소유권을 연결하고 해제합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class PlayerSceneAdapter : MonoBehaviour
    {
        private MatchSession _session;
        private NetworkMatchCoordinator _networkMatch;
        private bool _isBinding;
        private bool _isUnbinding;

        public void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (localPlayerId.IsEmpty) throw new ArgumentException("A local participant is required.", nameof(localPlayerId));
            if (_isBinding || _isUnbinding) throw new InvalidOperationException("Player binding cannot reenter setup or cleanup.");
            Unbind(); _networkMatch = match;
            _isBinding = true;
            try
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (component is IPlayerNetworkMatchBinding binding) binding.BindNetworkMatch(match, localPlayerId);
                    if (!ReferenceEquals(_networkMatch, match)) throw new InvalidOperationException("The Player binding was released during setup.");
                }
            }
            catch (Exception error) { RollbackBinding(error); throw; }
            finally { _isBinding = false; }
        }

        public void Bind(MatchSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (_isBinding || _isUnbinding) throw new InvalidOperationException("Player binding cannot reenter setup or cleanup.");
            Unbind();
            _session = session;
            _isBinding = true;
            try
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (component is IPlayerMatchBinding binding) binding.BindMatch(session);
                    if (!ReferenceEquals(_session, session)) throw new InvalidOperationException("The Player binding was released during setup.");
                }
            }
            catch (Exception error) { RollbackBinding(error); throw; }
            finally { _isBinding = false; }
        }

        public void Unbind()
        {
            if (_isUnbinding) return;
            bool hadNetwork = _networkMatch != null, hadSession = _session != null;
            // 콜백보다 먼저 권한 참조를 닫아 재진입/부분 해제 후 입력이 이전 Match로 가지 않게 합니다.
            _session = null;
            _networkMatch = null;
            List<Exception> errors = null;
            _isUnbinding = true;
            try
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (hadNetwork && component is IPlayerNetworkMatchBinding networkBinding)
                        try { networkBinding.UnbindNetworkMatch(); }
                        catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                    if (hadSession && component is IPlayerMatchBinding binding)
                        try { binding.UnbindMatch(); }
                        catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                }
            }
            finally { _isUnbinding = false; }
            if (errors != null) throw new AggregateException("Player feature cleanup failed after all bindings were released.", errors);
        }

        private void RollbackBinding(Exception bindingError)
        {
            try { Unbind(); }
            catch (Exception cleanupError) { throw new AggregateException("Player setup and rollback failed.", bindingError, cleanupError); }
        }

        private void OnDestroy()
        {
            try { Unbind(); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
    }
}
