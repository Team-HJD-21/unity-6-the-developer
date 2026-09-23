// Creates the app scope once before scenes load and resets its guard per runtime session.

using TeamHJD.Game.Application;
using TeamHJD.Game.Content.Runtime;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public static class AppBootstrap
    {
        private const string ContentCatalogResourceKey = "Core/ContentCatalog";
        private static bool _hasBootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRuntimeSession()
        {
            _hasBootstrapped = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateAppRoot()
        {
            if (_hasBootstrapped) return;
            _hasBootstrapped = true;

            var rootObject = new GameObject(nameof(AppRoot));
            try
            {
                var root = rootObject.AddComponent<AppRoot>();
                root.Initialize(CreateServices());
                Object.DontDestroyOnLoad(rootObject);
            }
            catch
            {
                _hasBootstrapped = false;
                Object.Destroy(rootObject);
                throw;
            }
        }

        private static AppServices CreateServices()
        {
            var contentCatalog = Resources.Load<ContentCatalog>(ContentCatalogResourceKey);
            IContentCatalog content = contentCatalog;
            if (content == null)
            {
                Debug.LogWarning($"No ContentCatalog at Resources/{ContentCatalogResourceKey}; using an empty fake catalog.");
                content = new FakeContentCatalog();
            }

            var localUser = new PlatformUser(new PlayerId("local-player"), "Local Player");
            return new AppServices(
                new FakeProfileService(),
                content,
                new FakePlatformService(localUser),
                new FakeSceneFlow(),
                new LocalAuthority());
        }
    }
}
