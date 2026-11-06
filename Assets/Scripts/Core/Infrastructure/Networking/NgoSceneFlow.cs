// 연결 중 Scene 교체는 NGO 로더를 사용해 Scene NetworkObject가 이전 Scene에서 남지 않게 합니다.
// Host가 고정한 1~2인 전체의 NGO 로드 완료를 기다리며 이탈·새 참가·timeout은 성공으로 처리하지 않습니다.
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
            if (!IsConnected)
            {
                await _local.LoadAsync(key, token);
                return;
            }
            token.ThrowIfCancellationRequested();
            if (IsLoaded(key))
                return;
            await RunAsync(key, LoadSceneMode.Additive, false);
            if (token.IsCancellationRequested)
            {
                await RunAsync(key, LoadSceneMode.Additive, true);
                token.ThrowIfCancellationRequested();
            }
        }
        public async Task ReplaceAsync(string key, CancellationToken token)
        {
            if (!IsConnected)
            {
                await _local.ReplaceAsync(key, token);
                return;
            }
            token.ThrowIfCancellationRequested();
            if (!_canReplace())
                throw new InvalidOperationException("End the authoritative Match before replacing its Scene.");
            await RunAsync(key, LoadSceneMode.Single, false);
        }
        public async Task UnloadAsync(string key, CancellationToken token)
        {
            if (!IsConnected)
            {
                await _local.UnloadAsync(key, token);
                return;
            }
            token.ThrowIfCancellationRequested();
            if (IsLoaded(key))
                await RunAsync(key, LoadSceneMode.Additive, true);
        }
        private async Task RunAsync(string key, LoadSceneMode mode, bool unload)
        {
            if (_busy)
                throw new InvalidOperationException("A network Scene operation is already running.");
            if (!_manager.IsHost || _manager.ConnectedClientsIds.Count < 1 || _manager.ConnectedClientsIds.Count > 2)
                throw new NotSupportedException("Only the authoritative one/two-player Host chooses the shared Scene.");
            if (!_manager.NetworkConfig.EnableSceneManagement || _manager.SceneManager == null)
                throw new InvalidOperationException("Enable NGO Scene management before network Scene travel.");
            string name = Path.GetFileNameWithoutExtension(key);
            var completion = new TaskCompletionSource<bool>();
            var cohort = new HashSet<ulong>(_manager.ConnectedClientsIds);
            bool cohortChanged = false;
            var scenes = _manager.SceneManager;
            void Finished(string sceneName, LoadSceneMode completedMode, List<ulong> ready, List<ulong> timedOut)
            {
                if (sceneName != name || (!unload && completedMode != mode))
                    return;
                if (cohortChanged || !cohort.SetEquals(_manager.ConnectedClientsIds))
                    completion.TrySetException(new InvalidOperationException("Participants changed during shared Scene travel."));
                else if (timedOut.Count > 0 || !cohort.SetEquals(ready))
                    completion.TrySetException(new TimeoutException("Not all prepared participants completed shared Scene travel."));
                else
                    completion.TrySetResult(true);
            }
            void Connected(ulong id)
            {
                if (!cohort.Contains(id))
                    cohortChanged = true;
            }
            void Disconnected(ulong id)
            {
                if (cohort.Contains(id))
                    cohortChanged = true;
            }
            void Stopped(bool wasHost) => completion.TrySetException(new InvalidOperationException("The Host stopped during Scene travel."));
            if (unload)
                scenes.OnUnloadEventCompleted += Finished;
            else
                scenes.OnLoadEventCompleted += Finished;
            _manager.OnServerStopped += Stopped;
            _manager.OnClientConnectedCallback += Connected;
            _manager.OnClientDisconnectCallback += Disconnected;
            using var timeout = new CancellationTokenSource();
            _busy = true;
            try
            {
                SceneEventProgressStatus status = unload ? scenes.UnloadScene(SceneManager.GetSceneByPath(key)) : scenes.LoadScene(name, mode);
                if (status != SceneEventProgressStatus.Started)
                    throw new InvalidOperationException("NGO rejected Scene travel: " + status);
                // SDK의 timeout coroutine이 Unity 배속/정지에 묶여도 호출자를 무기한 대기시키지 않습니다.
                var deadline = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _manager.NetworkConfig.LoadSceneTimeOut) + 5), timeout.Token);
                if (await Task.WhenAny(completion.Task, deadline) != completion.Task)
                    throw new TimeoutException("The NGO shared Scene operation did not finish before its real-time deadline.");
                await completion.Task;
                await Task.Yield(); // NGO가 완료 콜백의 내부 정리를 마친 뒤 다음 Scene/Match 작업을 허용합니다.
            }
            finally
            {
                timeout.Cancel();
                _busy = false;
                if (unload)
                    scenes.OnUnloadEventCompleted -= Finished;
                else
                    scenes.OnLoadEventCompleted -= Finished;
                _manager.OnServerStopped -= Stopped;
                _manager.OnClientConnectedCallback -= Connected;
                _manager.OnClientDisconnectCallback -= Disconnected;
            }
        }
    }
}
