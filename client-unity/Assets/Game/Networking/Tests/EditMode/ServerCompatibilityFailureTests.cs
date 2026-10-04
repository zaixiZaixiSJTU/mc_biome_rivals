using System;
using BiomeRivals.Core;
using NUnit.Framework;

namespace BiomeRivals.Networking.Tests
{
    public sealed class ServerCompatibilityFailureTests
    {
        private static NakamaConnectionSettings Settings => new NakamaConnectionSettings { host = "localhost", port = 17350, serverKey = "secret-not-in-diagnostics" };
        private static MatchmakingPreferences Preferences => new MatchmakingPreferences(FactionIds.PlainsForest, 42, 48);

        [Test]
        public void MatchingVersionsDoNotProduceFailureMetadata()
        { Assert.That(ServerCompatibilityFailure.Create(Settings, Preferences, GameVersions.Protocol, GameVersions.Ruleset, 42, 48), Is.Null); }

        [TestCase(39, "prototype-0.64", 41, 47, ServerCompatibilityFailureKind.Protocol)]
        [TestCase(40, "prototype-0.64", 42, 48, ServerCompatibilityFailureKind.Ruleset)]
        [TestCase(40, "prototype-0.65", 41, 48, ServerCompatibilityFailureKind.CardData)]
        [TestCase(40, "prototype-0.65", 42, 47, ServerCompatibilityFailureKind.Effects)]
        public void FailurePreservesEndpointAndBothCompleteVersionTuplesWithoutSecrets(int protocol, string ruleset, int cards, int effects, ServerCompatibilityFailureKind kind)
        {
            var failure = ServerCompatibilityFailure.Create(Settings, Preferences, protocol, ruleset, cards, effects);
            Assert.That(failure.Kind, Is.EqualTo(kind));
            Assert.That(failure.Endpoint, Is.EqualTo("http://localhost:17350"));
            Assert.That(failure.ServerProtocol, Is.EqualTo(protocol)); Assert.That(failure.ServerRuleset, Is.EqualTo(ruleset));
            Assert.That(failure.ServerCardVersion, Is.EqualTo(cards)); Assert.That(failure.ServerEffectVersion, Is.EqualTo(effects));
            Assert.That(failure.ClientProtocol, Is.EqualTo(GameVersions.Protocol)); Assert.That(failure.ClientRuleset, Is.EqualTo(GameVersions.Ruleset));
            Assert.That(failure.DiagnosticText, Does.Contain("Client: protocol=40, ruleset=prototype-0.65, cards=42, effects=48"));
            Assert.That(failure.DiagnosticText, Does.Contain($"Server: protocol={protocol}, ruleset={ruleset}, cards={cards}, effects={effects}"));
            Assert.That(failure.DiagnosticText, Does.Not.Contain(Settings.serverKey));
            Assert.That(failure.UserSummary, Does.Contain("localhost:17350"));
            var exception = new ServerCompatibilityException(failure);
            var status = new MatchConnectionStatus(MatchConnectionPhase.Failed, exception.Message, compatibilityFailure: exception.Failure);
            Assert.That(status.CompatibilityFailure, Is.SameAs(failure));
            Assert.That(status.CanSendCommands, Is.False);
        }

        [TestCase(0, "prototype-0.65", 42, 48)]
        [TestCase(40, "", 42, 48)]
        [TestCase(40, "prototype-0.65", 0, 48)]
        [TestCase(40, "prototype-0.65", 42, 0)]
        public void IncompleteHealthIsNotPresentedAsKnownServerVersions(int protocol, string ruleset, int cards, int effects)
        { Assert.Throws<ArgumentException>(() => ServerCompatibilityFailure.Create(Settings, Preferences, protocol, ruleset, cards, effects)); }
    }
}
