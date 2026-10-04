using System;
using BiomeRivals.Core;
using NUnit.Framework;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineDeploymentAuditTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void MulliganReservesOrdinaryCardsForPositiveAndOccupiedControls(int seat)
        {
            Assert.That(DemoOnlineDeploymentAudit.SelectMulligan(null, seat), Is.Empty);
            var hand = new[] { "cd_004", "cd_007", "cd_007", "cd_001", "cd_002", "cd_005", "cd_008" };
            var original = (string[])hand.Clone();
            Assert.That(DemoOnlineDeploymentAudit.SelectMulligan(hand, seat), Is.EqualTo(new[] { 2, 5, 6 }));
            Assert.That(hand, Is.EqualTo(original));
            Assert.That(DemoOnlineDeploymentAudit.SelectMulligan(new[] { "cd_003", "cd_001", "cd_005" }, seat), Is.EqualTo(new[] { 2 }),
                "ordinary units can be retained before the protected unit-prefix completes");
            Assert.That(DemoOnlineDeploymentAudit.SelectMulligan(new[] { "cd_006", "cd_008" }, seat), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void UnitPrefixMarkerRequiresBothIdentityAndNetworkRevisionConvergence()
        {
            const string valid = "{\"matchId\":\"audit\",\"role\":\"structure\",\"caseCount\":4,\"revision\":12}";
            Assert.That(DemoOnlineDeploymentAudit.UnitPrefixMarkerHasConverged(valid, "audit", "structure", 11), Is.False);
            Assert.That(DemoOnlineDeploymentAudit.UnitPrefixMarkerHasConverged(valid, "audit", "structure", 12), Is.True);
            Assert.Throws<InvalidOperationException>(() => DemoOnlineDeploymentAudit.UnitPrefixMarkerHasConverged(valid, "other", "structure", 12));
            Assert.Throws<InvalidOperationException>(() => DemoOnlineDeploymentAudit.UnitPrefixMarkerHasConverged(valid, "audit", "single", 12));
            Assert.Throws<InvalidOperationException>(() => DemoOnlineDeploymentAudit.UnitPrefixMarkerHasConverged(valid.Replace("\"caseCount\":4", "\"caseCount\":3"), "audit", "structure", 12));
            Assert.Throws<InvalidOperationException>(() => DemoOnlineDeploymentAudit.UnitPrefixMarkerHasConverged(valid.Replace("\"revision\":12", "\"revision\":0"), "audit", "structure", 12));
        }

        [Test]
        public void FullProjectionHashDetectsPrivateHandPaymentMetadataAndRevisionMutations()
        {
            var state = new MatchStateDto { matchId = "audit", viewerPlayerId = "alice", revision = 10,
                players = new[] { new PlayerStateDto { playerId = "alice", redstone = 5, totalRedstone = 5,
                    hand = new[] { "cd_007" }, handCards = new[] { new HandCardStateDto { cardId = "cd_007", handCardInstanceId = "hand-1" } } },
                    new PlayerStateDto { playerId = "bob" } } };
            var before = DemoOnlineDeploymentAudit.ProjectionHash(state);
            Assert.That(DemoOnlineDeploymentAudit.ProjectionHash(state), Is.EqualTo(before));
            state.players[0].handCards[0].handCardInstanceId = "hand-2";
            Assert.That(DemoOnlineDeploymentAudit.ProjectionHash(state), Is.Not.EqualTo(before));
            state.players[0].handCards[0].handCardInstanceId = "hand-1";
            state.players[0].redstone--;
            Assert.That(DemoOnlineDeploymentAudit.ProjectionHash(state), Is.Not.EqualTo(before));
            state.players[0].redstone++; state.players[0].discardPile = new[] { "cd_007" };
            Assert.That(DemoOnlineDeploymentAudit.ProjectionHash(state), Is.Not.EqualTo(before));
            state.players[0].discardPile = Array.Empty<string>(); state.revision++;
            Assert.That(DemoOnlineDeploymentAudit.ProjectionHash(state), Is.Not.EqualTo(before));
            state.revision--; state.players[1].deckCount++;
            Assert.That(DemoOnlineDeploymentAudit.ProjectionHash(state), Is.Not.EqualTo(before));
        }
    }
}
