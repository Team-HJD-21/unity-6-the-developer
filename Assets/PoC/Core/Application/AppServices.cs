// 앱 범위의 플랫폼 및 Scene 전환 서비스를 소유하고 종료 시 해제합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Application
{
    public sealed class AppServices : IDisposable
    {
        private bool _isDisposed;

        public IPlatformService Platform { get; }
        public ISceneFlow SceneFlow { get; }

        public AppServices(IPlatformService platform, ISceneFlow sceneFlow)
        {
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            SceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            List<Exception> errors = null;
            DisposeIfNeeded(Platform, ref errors);
            DisposeIfNeeded(SceneFlow, ref errors);
            if (errors != null) throw new AggregateException("One or more app services failed to dispose.", errors);
        }

        private static void DisposeIfNeeded(object service, ref List<Exception> errors)
        {
            if (!(service is IDisposable disposable)) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                if (errors == null) errors = new List<Exception>();
                errors.Add(exception);
            }
        }
    }
}
