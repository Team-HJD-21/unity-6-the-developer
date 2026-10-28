// Unity의 실제 Scene 비동기 작업을 좁은 Scene Port로 제공합니다.
// Additive 취소는 새로 소유한 Scene을 정리한 뒤 끝나며 Single/Unload의 시작 이후는 commit입니다.
// 이미 로드된 Scene을 다른 요청의 소유물로 취급해 취소 시 제거하지 않습니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class UnitySceneFlow : ISceneFlow
    {
        private AsyncOperation _operation;
        public float Progress => _operation == null ? 1f : Mathf.Clamp01(_operation.progress / .9f);
        public bool CanLoad(string sceneKey) => !string.IsNullOrWhiteSpace(sceneKey) && SceneUtility.GetBuildIndexByScenePath(sceneKey) >= 0;
        public bool IsLoaded(string sceneKey) => !string.IsNullOrWhiteSpace(sceneKey) && SceneManager.GetSceneByPath(sceneKey).isLoaded;
        public async Task LoadAsync(string sceneKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Validate(sceneKey);
            if (IsLoaded(sceneKey)) return;
            try
            {
                await WaitAsync(SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Additive));
                if (cancellationToken.IsCancellationRequested)
                {
                    await WaitAsync(SceneManager.UnloadSceneAsync(sceneKey));
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            finally { _operation = null; }
        }
        public async Task ReplaceAsync(string sceneKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Validate(sceneKey);
            // Unity Single 교체는 시작 이후 취소할 수 없습니다. 완료를 기다려 실제 commit을 성공으로 보고합니다.
            try { await WaitAsync(SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Single)); }
            finally { _operation = null; }
        }
        public async Task UnloadAsync(string sceneKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsLoaded(sceneKey)) return;
            try { await WaitAsync(SceneManager.UnloadSceneAsync(sceneKey)); }
            finally { _operation = null; }
        }
        private void Validate(string sceneKey)
        { if (!CanLoad(sceneKey)) throw new ArgumentException("Enable the Scene path in Build Settings.", nameof(sceneKey)); }
        private Task WaitAsync(AsyncOperation operation)
        {
            if (operation == null) throw new InvalidOperationException("Unity could not start the Scene operation.");
            _operation = operation;
            if (operation.isDone) return Task.CompletedTask;
            var completion = new TaskCompletionSource<bool>();
            void OnCompleted(AsyncOperation done) { done.completed -= OnCompleted; completion.TrySetResult(true); }
            operation.completed += OnCompleted;
            return completion.Task;
        }
    }
}
