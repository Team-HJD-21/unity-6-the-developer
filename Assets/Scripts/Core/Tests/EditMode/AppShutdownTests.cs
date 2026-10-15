// App 정리 중 서비스 실패가 있어도 Match와 나머지 서비스가 정리되는지 검증합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class AppShutdownTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Dispose_CleansRemainingServicesAndIsIdempotent(bool failProfile)
        {
            var owner = new GameObject("Shutdown Test");
            var root = owner.AddComponent<AppRoot>();
            var profiles = new ProbeProfiles { Fail = failProfile };
            var scenes = new ProbeScenes();
            root.Initialize(new AppServices(profiles, new FakeContentCatalog(),
                new FakePlatformService(new PlatformUser(new PlayerId("test"), "Test")),
                scenes, new LocalAuthority()));
            try
            {
                if (failProfile) Assert.Throws<AggregateException>(() => root.Dispose());
                else Assert.DoesNotThrow(() => root.Dispose());
                Assert.That(profiles.DisposedCount, Is.EqualTo(1));
                Assert.That(scenes.DisposedCount, Is.EqualTo(1));
                Assert.That(root.CurrentMatchId.IsEmpty, Is.True);
                Assert.DoesNotThrow(() => root.Dispose());
                Assert.That(scenes.DisposedCount, Is.EqualTo(1));
                Assert.Throws<ObjectDisposedException>(() => root.ReconfigureBattlefieldGrid(BattlefieldGridConfiguration.Default));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        private sealed class ProbeProfiles : IProfileService, IDisposable
        {
            public bool Fail;
            public int DisposedCount;
            public Task<PlayerProfileSnapshot> LoadAsync(PlayerId id, CancellationToken cancellationToken)
                => Task.FromResult<PlayerProfileSnapshot>(null);
            public Task SaveAsync(PlayerProfileSnapshot profile, CancellationToken cancellationToken)
                => Task.CompletedTask;
            public void Dispose()
            {
                DisposedCount++;
                if (Fail) throw new InvalidOperationException("profile cleanup");
            }
        }

        private sealed class ProbeScenes : ISceneFlow, IDisposable
        {
            public int DisposedCount;
            public Task LoadAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task UnloadAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
            public void Dispose() => DisposedCount++;
        }
    }
}
