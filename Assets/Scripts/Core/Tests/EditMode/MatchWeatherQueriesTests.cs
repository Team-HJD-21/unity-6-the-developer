using System;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchWeatherQueriesTests
    {
        private static readonly MatchId Match = new MatchId("match");

        private sealed class EnvironmentSource : IMatchEnvironmentSource
        {
            public MatchEnvironmentSnapshot Snapshot { get; set; }
            public event Action<MatchEnvironmentSnapshot> Changed
            {
                add { }
                remove { }
            }
        }

        [Test]
        public void ReturnsConfirmedWeatherForMatchingMatch()
        {
            var source = new EnvironmentSource
            {
                Snapshot = new MatchEnvironmentSnapshot(Match, 1, 0,
                    new DefinitionId("rain"), default)
            };

            Assert.That(source.TryGetCurrentWeather(Match, out DefinitionId weatherId), Is.True);
            Assert.That(weatherId, Is.EqualTo(new DefinitionId("rain")));
        }

        [Test]
        public void ReturnsFalseWhenWeatherIsUnavailableOrMatchDiffers()
        {
            var source = new EnvironmentSource
            {
                Snapshot = new MatchEnvironmentSnapshot(Match, 1, 0, default, default)
            };

            Assert.That(source.TryGetCurrentWeather(Match, out DefinitionId missing), Is.False);
            Assert.That(missing.IsEmpty, Is.True);

            source.Snapshot = new MatchEnvironmentSnapshot(Match, 2, 1,
                new DefinitionId("heatwave"), default);
            Assert.That(source.TryGetCurrentWeather(new MatchId("other"), out DefinitionId other), Is.False);
            Assert.That(other.IsEmpty, Is.True);
        }

        [Test]
        public void ReturnsFalseForMissingSourceOrSnapshot()
        {
            IMatchEnvironmentSource source = null;
            Assert.That(source.TryGetCurrentWeather(Match, out _), Is.False);

            source = new EnvironmentSource();
            Assert.That(source.TryGetCurrentWeather(Match, out _), Is.False);
            Assert.That(source.TryGetCurrentWeather(default, out _), Is.False);
        }
    }
}
