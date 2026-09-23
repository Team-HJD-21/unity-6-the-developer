// Creates one persistent AppRoot from the first scene's composition settings.

using TeamHJD.Game.Application;
using TeamHJD.Game.Content.Runtime;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private ContentCatalog _contentCatalog;
        [SerializeField] private string _localPlayerKey = "local-player";
        [SerializeField] private string _localDisplayName = "Local Player";

        private void Awake()
        {
            var existingRoot = FindFirstObjectByType<AppRoot>();
            if (existingRoot != null)
            {
                Destroy(this);
                return;
            }

            if (_contentCatalog == null)
            {
                Debug.LogError("AppBootstrap requires a ContentCatalog asset.", this);
                enabled = false;
                return;
            }

            var rootObject = new GameObject("AppRoot");
            DontDestroyOnLoad(rootObject);
            var root = rootObject.AddComponent<AppRoot>();
            var localUser = new PlatformUser(new PlayerId(_localPlayerKey), _localDisplayName);
            var services = new AppServices(
                new FakeProfileService(),
                _contentCatalog,
                new FakePlatformService(localUser),
                new FakeSceneFlow(),
                new LocalAuthority());
            root.Initialize(services);
        }
    }
}
