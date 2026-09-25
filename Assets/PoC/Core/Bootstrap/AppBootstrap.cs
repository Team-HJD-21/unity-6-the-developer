// 씬 오브젝트의 Awake 전에 앱 범위를 한 번 만들고 런타임 세션마다 중복 방지 상태를 초기화합니다.

using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public static class AppBootstrap
    {
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
                var localUser = new PlatformUser(new PlayerId("local-player"), "Local Player");
                root.Initialize(new AppServices(
                    new FakePlatformService(localUser),
                    new FakeSceneFlow()));
                Object.DontDestroyOnLoad(rootObject);
            }
            catch
            {
                _hasBootstrapped = false;
                Object.Destroy(rootObject);
                throw;
            }
        }

    }
}
