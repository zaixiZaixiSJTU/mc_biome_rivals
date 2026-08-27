using System;
using BiomeRivals.Core;
using NUnit.Framework;

namespace BiomeRivals.Networking.Tests
{
    public sealed class MatchmakingPreferencesTests
    {
        [Test]
        public void SerializesSupportedFactionAsNakamaStringProperty()
        {
            var preferences = new MatchmakingPreferences(FactionIds.OceanRiver);
            var properties = preferences.ToStringProperties();
            Assert.That(preferences.FactionId, Is.EqualTo(FactionIds.OceanRiver));
            Assert.That(properties, Has.Count.EqualTo(1));
            Assert.That(properties[MatchmakingPreferences.FactionProperty], Is.EqualTo(FactionIds.OceanRiver));
        }

        [Test]
        public void RejectsUnsupportedFactionBeforeOpeningSocket()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchmakingPreferences("unknown"));
        }
    }
}
