// 로컬 Scene 전환 하나를 직렬화하고 진행·취소·실패와 입력 잠금을 관리합니다.
// 취소는 소유한 additive 작업의 복구에 사용하고 Single 교체 commit은 되돌리지 않습니다.
// 네트워크 다중 참가자의 합의·동기 Scene 이동은 별도 Host 경계가 담당합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Application
{
    public sealed class SceneFlowController : IDisposable
    {
        private ISceneFlow _scenes;
        private ISceneFlow _pendingScenes;
        private readonly IInputBlocker _input;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CancellationTokenSource _current;
        private bool _disposed;
        public bool IsTransitioning => _current != null;
        public bool CanCancel { get; private set; }
        public string TargetScene { get; private set; }
        public Exception LastFailure { get; private set; }
        public float Progress => IsTransitioning ? _scenes.Progress : 1f;
        public SceneFlowController(ISceneFlow scenes, IInputBlocker input)
        { _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes)); _input = input ?? throw new ArgumentNullException(nameof(input)); }
        /// <summary>조립부가 연결 adapter를 교체합니다. 진행 중 작업의 driver는 바꾸지 않습니다.</summary>
        public void SetSceneFlow(ISceneFlow scenes)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SceneFlowController));
            if (scenes == null) throw new ArgumentNullException(nameof(scenes));
            if (IsTransitioning) _pendingScenes = scenes;
            else _scenes = scenes;
        }
        public Task LoadAsync(string sceneKey, CancellationToken token) => RunAsync(sceneKey, false, false, token);
        public Task ReplaceAsync(string sceneKey, CancellationToken token) => RunAsync(sceneKey, true, false, token);
        public Task UnloadAsync(string sceneKey, CancellationToken token) => RunAsync(sceneKey, false, true, token);
        public bool CancelCurrent()
        {
            if (_disposed || !IsTransitioning || !CanCancel) return false;
            _current.Cancel(); return true;
        }
        private async Task RunAsync(string sceneKey, bool replace, bool unload, CancellationToken token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SceneFlowController));
            if (IsTransitioning) throw new InvalidOperationException("A Scene transition is already in progress.");
            if (string.IsNullOrWhiteSpace(sceneKey)) throw new ArgumentException("A Scene key is required.", nameof(sceneKey));
            token.ThrowIfCancellationRequested();
            if (!unload && !_scenes.CanLoad(sceneKey)) throw new ArgumentException("The Scene is not available in this build.", nameof(sceneKey));
            bool loaded = _scenes.IsLoaded(sceneKey);
            if (unload && !loaded) return;
            if (!unload && !replace && loaded) return;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
            using var input = _input.Acquire("SceneFlow:" + sceneKey);
            _current = linked; CanCancel = !replace && !unload; TargetScene = sceneKey; LastFailure = null;
            try
            {
                if (unload) await _scenes.UnloadAsync(sceneKey, linked.Token);
                else if (replace) await _scenes.ReplaceAsync(sceneKey, linked.Token);
                else await _scenes.LoadAsync(sceneKey, linked.Token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { LastFailure = error; throw; }
            finally
            {
                _current = null; CanCancel = false;
                if (_pendingScenes != null) { _scenes = _pendingScenes; _pendingScenes = null; }
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try { _lifetime.Cancel(); } finally { _lifetime.Dispose(); }
        }
    }
}
