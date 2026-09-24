// 구성부와 Adapter 테스트를 위해 요청된 Scene 전환을 메모리에 기록합니다.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Infrastructure.Fakes
{
    public sealed class FakeSceneFlow : ISceneFlow
    {
        private readonly HashSet<string> _loadedScenes = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> LoadedScenes => new List<string>(_loadedScenes).AsReadOnly();

        public Task LoadAsync(string sceneKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sceneKey)) throw new ArgumentException("A scene key is required.", nameof(sceneKey));
            _loadedScenes.Add(sceneKey);
            return Task.CompletedTask;
        }

        public Task UnloadAsync(string sceneKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sceneKey)) throw new ArgumentException("A scene key is required.", nameof(sceneKey));
            _loadedScenes.Remove(sceneKey);
            return Task.CompletedTask;
        }
    }
}
