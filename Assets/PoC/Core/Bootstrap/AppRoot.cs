// 앱 범위 서비스 구성과 종료를 맡으며 Scene 전환 후에도 유지되는 Unity 객체입니다.

using System;
using TeamHJD.Game.Application;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        private bool _isDisposed;

        internal AppServices Services { get; private set; }

        public void Initialize(AppServices services)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppRoot));
            if (Services != null) throw new InvalidOperationException("AppRoot has already been initialized.");

            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        private void OnDestroy()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Services?.Dispose();
            Services = null;
        }
    }
}
