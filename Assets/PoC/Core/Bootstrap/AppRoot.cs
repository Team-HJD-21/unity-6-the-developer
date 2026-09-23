// Persistent Unity owner for app-scoped service composition and shutdown.

using System;
using TeamHJD.Game.Application;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        public AppServices Services { get; private set; }

        public void Initialize(AppServices services)
        {
            if (Services != null) throw new InvalidOperationException("AppRoot has already been initialized.");
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        private void OnDestroy()
        {
            Services?.Dispose();
            Services = null;
        }
    }
}
