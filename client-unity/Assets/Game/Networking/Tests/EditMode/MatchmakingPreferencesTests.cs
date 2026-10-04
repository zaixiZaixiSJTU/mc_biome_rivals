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
            var preferences = new MatchmakingPreferences(FactionIds.OceanRiver, 42, 48);
            var properties = preferences.ToStringProperties();
            Assert.That(preferences.FactionId, Is.EqualTo(FactionIds.OceanRiver));
            Assert.That(preferences.CardContentVersion, Is.EqualTo(42));
            Assert.That(preferences.ImplementedEffectRegistryVersion, Is.EqualTo(48));
            Assert.That(properties, Has.Count.EqualTo(1));
            Assert.That(properties[MatchmakingPreferences.FactionProperty], Is.EqualTo(FactionIds.OceanRiver));
        }

        [Test]
        public void RejectsUnsupportedFactionBeforeOpeningSocket()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchmakingPreferences("unknown", 42, 48));
        }

        [TestCase(0, 48)]
        [TestCase(-1, 48)]
        [TestCase(42, 0)]
        [TestCase(42, -1)]
        public void RejectsInvalidContentVersionsBeforeOpeningSocket(int cardVersion, int effectVersion)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchmakingPreferences(FactionIds.OceanRiver, cardVersion, effectVersion));
        }

        [Test]
        public void AcceptsServerWithMatchingGameplayAndContentVersions()
        {
            var preferences = new MatchmakingPreferences(FactionIds.OceanRiver, 42, 48);
            Assert.That(preferences.GetIncompatibilityMessage(GameVersions.Protocol, GameVersions.Ruleset, 42, 48), Is.Empty);
        }

        [TestCase(999, "prototype-0.65", 42, 48, "protocol version mismatch")]
        [TestCase(40, "future-rules", 42, 48, "ruleset version mismatch")]
        [TestCase(40, "prototype-0.65", 41, 48, "card data version mismatch")]
        [TestCase(40, "prototype-0.65", 42, 47, "implemented effect registry version mismatch")]
        public void ReportsEveryServerVersionMismatchBeforeOpeningSocket(
            int protocolVersion,
            string rulesetVersion,
            int cardVersion,
            int effectVersion,
            string expectedMessage)
        {
            var preferences = new MatchmakingPreferences(FactionIds.OceanRiver, 42, 48);
            Assert.That(
                preferences.GetIncompatibilityMessage(protocolVersion, rulesetVersion, cardVersion, effectVersion),
                Does.StartWith(expectedMessage));
        }
    }
}
