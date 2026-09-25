// 씬 오브젝트의 Awake 전에 앱 범위를 한 번 만들고 런타임 세션마다 중복 방지 상태를 초기화합니다.

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
        //  Game에서 제공하는 모든 콘텐츠 정보에 대한 목록 파일 위치
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

            //  일단 PoC 단계에서는 해당 구문을 타게 됩니다.
            if (content == null)
            {
                Debug.LogWarning($"No ContentCatalog at Resources/{ContentCatalogResourceKey}; using an empty fake catalog.");
                content = new FakeContentCatalog();
            }

            var localUser = new PlatformUser(new PlayerId("local-player"), "Local Player");

            //  FakeProfileService()도 마찬가지로 PoC 단계에서 사용되는 임시 클래스입니다.
            return new AppServices(
                new FakeProfileService(),
                content,
                new FakePlatformService(localUser),
                new FakeSceneFlow(),
                new LocalAuthority());
        }
    }
}
