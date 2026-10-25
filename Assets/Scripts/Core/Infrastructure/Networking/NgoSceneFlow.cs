// 연결 중 Scene 교체는 NGO 로더를 사용해 Scene NetworkObject가 이전 Scene에서 남지 않게 합니다.
// 현재 로컬 Host 수직 경로만 허용하며 다중 참가자 전환 합의는 별도 계약으로 남깁니다.
// 연결이 없으면 기본 Unity 로더에 위임하고, 교체 전 Match 종료는 호출자가 명시합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using UnityEngine.SceneManagement;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoSceneFlow : ISceneFlow
    {
        private readonly NetworkManager _manager;
        private readonly ISceneFlow _local;
        private readonly Func<bool> _canReplace;
        private bool _busy;
        private bool IsConnected => _manager != null && _manager.IsListening;
        public float Progress => _busy ? float.NaN : _local.Progress;
        public NgoSceneFlow(NetworkManager manager, ISceneFlow local, Func<bool> canReplace)
        {
            _manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            _local = local ?? throw new ArgumentNullException(nameof(local));
            _canReplace = canReplace ?? throw new ArgumentNullException(nameof(canReplace));
        }
        public bool CanLoad(string key) => _local.CanLoad(key);
        public bool IsLoaded(string key) => _local.IsLoaded(key);
        public async Task LoadAsync(string key, CancellationToken token)
        {
            if (!IsConnected) { await _local.LoadAsync(key, token); return; }
            token.ThrowIfCancellationRequested();
            if (IsLoaded(key)) return;
            await RunAsync(key, LoadSceneMode.Additive, false);
            if (token.IsCancellationRequested)
            {
                await RunAsync(key, LoadSceneMode.Additive, true);
                token.ThrowIfCancellationRequested();
            }
        }
        public async Task ReplaceAsync(string key, CancellationToken token)
        {
            if (!IsConnected) { await _local.ReplaceAsync(key, token); return; }
            token.ThrowIfCancellationRequested();
            if (!_canReplace()) throw new InvalidOperationException("End the authoritative Match before replacing its Scene.");
            await RunAsync(key, LoadSceneMode.Single, false);
        }
        public async Task UnloadAsync(string key, CancellationToken token)
        {
            if (!IsConnected) { await _local.UnloadAsync(key, token); return; }
            token.ThrowIfCancellationRequested();
            if (IsLoaded(key)) await RunAsync(key, LoadSceneMode.Additive, true);
        }
        private async Task RunAsync(string key, LoadSceneMode mode, bool unload)
        {
            if (_busy) throw new InvalidOperationException("A network Scene operation is already running.");
            if (!_manager.IsServer || _manager.ConnectedClientsIds.Count != 1)
                throw new NotSupportedException("Only a solo Host may use this Scene route; multiplayer travel needs its own readiness contract.");
            if (!_manager.NetworkConfig.EnableSceneManagement || _manager.SceneManager == null)
                throw new InvalidOperationException("Enable NGO Scene management before network Scene travel.");
            string name = Path.GetFileNameWithoutExtension(key);
            var completion = new TaskCompletionSource<bool>();
            var scenes = _manager.SceneManager;
            void Finished(string sceneName, LoadSceneMode completedMode, List<ulong> ready, List<ulong> timedOut)
            {
                if (sceneName != name || (!unload && completedMode != mode)) return;
                if (timedOut.Count > 0) completion.TrySetException(new TimeoutException("Network Scene completion timed out."));
                else completion.TrySetResult(true);
            }
            void Stopped(bool wasHost) => completion.TrySetException(new InvalidOperationException("The Host stopped during Scene travel."));
            if (unload) scenes.OnUnloadEventCompleted += Finished;
            else scenes.OnLoadEventCompleted += Finished;
            _manager.OnServerStopped += Stopped;
            _busy = true;
            try
            {
                SceneEventProgressStatus status = unload ? scenes.UnloadScene(SceneManager.GetSceneByPath(key)) : scenes.LoadScene(name, mode);
                if (status != SceneEventProgressStatus.Started) throw new InvalidOperationException("NGO rejected Scene travel: " + status);
                await completion.Task;
            }
            finally
            {
                _busy = false;
                if (unload) scenes.OnUnloadEventCompleted -= Finished;
                else scenes.OnLoadEventCompleted -= Finished;
                _manager.OnServerStopped -= Stopped;
            }
        }
    }
}
