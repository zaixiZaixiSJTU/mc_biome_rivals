using System;
using BiomeRivals.Core;
using NUnit.Framework;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineEndReturnProbeTests
    {
        [Test]
        public void MulliganKeepsOnlyExactSourceAndOneSafeUnit()
        {
            Assert.That(DemoOnlineEndReturnProbe.SelectMulliganIndices(
                new[] { "ed_005", "ed_002", "ed_004", "ed_001", "ed_005" }, "ed_005"),
                Is.EqualTo(new[] { 1, 3, 4 }));
            Assert.That(DemoOnlineEndReturnProbe.SelectMulliganIndices(new[] { "ed_008", "ed_006" }, "ed_002"),
                Is.EqualTo(new[] { 0, 1 }));
        }

        [TestCase("ed_002", -1)]
        [TestCase("ed_005", -2)]
        public void OwnerEvidenceRequiresExactReturnedInstanceAndModifier(string source, int modifier)
        {
            var batch = ReturnBatch(source, modifier);
            Assert.DoesNotThrow(() => DemoOnlineEndReturnProbe.ValidateReturnBatch(batch, "owner", source));
            batch.handProjection.ownHandCards[0].handCardInstanceId = "other-instance";
            Assert.Throws<InvalidOperationException>(() => DemoOnlineEndReturnProbe.ValidateReturnBatch(batch, "owner", source));
        }

        [Test]
        public void ReturnWithoutCausallyAdjacentPlayIsRejected()
        {
            var batch = ReturnBatch("ed_002", -1);
            batch.events[0].type = MatchEventTypes.PhaseChanged;
            Assert.Throws<InvalidOperationException>(() => DemoOnlineEndReturnProbe.ValidateReturnBatch(batch, "owner", "ed_002"));
        }

        [Test]
        public void ReturnWithWrongEffectSourceIsRejected()
        {
            var batch = ReturnBatch("ed_002", -1);
            batch.events[1].payload.sourceInstanceId = "effect-999";
            Assert.Throws<InvalidOperationException>(() => DemoOnlineEndReturnProbe.ValidateReturnBatch(batch, "owner", "ed_002"));
        }

        [Test]
        public void ObserverRejectsEvenAnEmptyInsteadOfNullPrivateField()
        {
            var batch = ReturnBatch("ed_002", -1);
            var payload = batch.events[1].payload;
            payload.returnedHandCardInstanceId = null;
            payload.expiresAtEndOfTurnPlayerId = null;
            payload.costModifier = 0;
            batch.handProjection = new HandProjectionDto { ownPlayerId = "observer",
                opponentPlayerId = "owner", opponentHandCount = 1 };
            Assert.DoesNotThrow(() => DemoOnlineEndReturnProbe.ValidateReturnBatch(batch, "observer", "ed_002"));
            payload.returnedHandCardInstanceId = "";
            Assert.Throws<InvalidOperationException>(() => DemoOnlineEndReturnProbe.ValidateReturnBatch(batch, "observer", "ed_002"));
        }

        [TestCase("owner")]
        [TestCase("observer")]
        public void ExpiryRejectsWrongPrivateInstanceOrLeak(string viewer)
        {
            var returned = ReturnBatch("ed_005", -2).events[1].payload;
            var expiry = new MatchEventPayloadDto { playerId = "owner", expiredAtEndOfTurnPlayerId = "owner",
                handCardInstanceId = viewer == "owner" ? "returned-1" : null,
                cardId = viewer == "owner" ? "ed_004" : null, expiredCostModifier = viewer == "owner" ? -2 : 0 };
            Assert.DoesNotThrow(() => DemoOnlineEndReturnProbe.ValidateExpiry(expiry, returned, viewer, -2));
            expiry.handCardInstanceId = "wrong-or-leaked";
            Assert.Throws<InvalidOperationException>(() => DemoOnlineEndReturnProbe.ValidateExpiry(expiry, returned, viewer, -2));
        }

        [Test]
        public void RecoveryRejectsInstantiatedOpponentHandPlaceholders()
        {
            var state = new MatchStateDto { viewerPlayerId = "observer", players = new[]
            {
                new PlayerStateDto { playerId = "observer" },
                new PlayerStateDto { playerId = "owner", hand = new string[] { null },
                    handCards = new HandCardStateDto[] { null } }
            } };
            Assert.DoesNotThrow(() => DemoOnlineEndReturnProbe.ValidateHiddenHands(state));
            state.players[1].handCards[0] = new HandCardStateDto();
            Assert.Throws<InvalidOperationException>(() => DemoOnlineEndReturnProbe.ValidateHiddenHands(state));
        }

        private static MatchEventBatchDto ReturnBatch(string source, int modifier) => new MatchEventBatchDto
        {
            revision = 10,
            events = new[]
            {
                new MatchEventDto { eventId = 20, type = MatchEventTypes.CardPlayed, payload = new MatchEventPayloadDto
                    { cardId = source, playerId = "owner", targetInstanceId = "unit-1" } },
                new MatchEventDto { eventId = 21, type = MatchEventTypes.ObjectReturned, payload = new MatchEventPayloadDto
                    { sourceCardId = source, ownerPlayerId = "owner", controllerPlayerId = "owner",
                        sourcePlayerId = "owner", instanceId = "unit-1", cardId = "ed_004", destination = "HAND",
                        sourceInstanceId = "effect-20", effectId = "effect." + source + ".01",
                        fromSlotKind = "UNIT", fromSlotIndex = 0, returnedHandCardInstanceId = "returned-1",
                        costModifier = modifier, expiresAtEndOfTurnPlayerId = "owner", ownerHandCount = 1 } }
            },
            handProjection = new HandProjectionDto { ownPlayerId = "owner", ownHand = new[] { "ed_004" },
                ownHandCards = new[] { new HandCardStateDto { handCardInstanceId = "returned-1", cardId = "ed_004",
                    costModifier = modifier, expiresAtEndOfTurnPlayerId = "owner" } } }
        };
    }
}
