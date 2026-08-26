using System;
using NUnit.Framework;

namespace BiomeRivals.Core.Tests
{
    public sealed class MatchStateStoreTests
    {
        [Test]
        public void Replace_UsesAuthoritativeSnapshot()
        {
            var store = new MatchStateStore();
            var snapshot = new MatchStateDto
            {
                matchId = "match-1", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                revision = 3, players = new[] { new PlayerStateDto(), new PlayerStateDto() }
            };

            store.Replace(snapshot);

            Assert.That(store.Current, Is.SameAs(snapshot));
            Assert.That(store.Current.revision, Is.EqualTo(3));
        }

        [Test]
        public void Apply_ReplaysDeploymentAndTurnState()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1",
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 0,
                turn = 1,
                activePlayerIndex = 0,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", redstone = 6, redstoneCapacity = 6,
                        hand = new[] { "pf_001" }, unitSlots = new string[4], buildingSlots = new string[3]
                    },
                    new PlayerStateDto
                    {
                        playerId = "bob", redstone = 6, redstoneCapacity = 6,
                        hand = new[] { "nt_001" }, unitSlots = new string[4], buildingSlots = new string[3]
                    }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1,
                        type = MatchEventTypes.CardDeployed,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", cardId = "pf_001", slotKind = "UNIT", slotIndex = 2,
                            occupiedSlots = 1, redstone = 5
                        }
                    }
                }
            });

            Assert.That(store.Current.revision, Is.EqualTo(1));
            Assert.That(store.Current.players[0].hand, Is.Empty);
            Assert.That(store.Current.players[0].unitSlots[2], Is.EqualTo("pf_001"));
            Assert.That(store.Current.players[0].redstone, Is.EqualTo(5));

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 2,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 2,
                        type = MatchEventTypes.TurnStarted,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", turn = 1, activePlayerIndex = 1, redstone = 6, redstoneCapacity = 6
                        }
                    }
                }
            });

            Assert.That(store.Current.activePlayerIndex, Is.EqualTo(1));
            Assert.That(store.Current.revision, Is.EqualTo(2));
        }

        [Test]
        public void Apply_RejectsRevisionGapsBeforeMutation()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                revision = 3, players = new[] { new PlayerStateDto(), new PlayerStateDto() }
            });
            var batch = new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 5
            };

            Assert.Throws<InvalidOperationException>(() => store.Apply(batch));
            Assert.That(store.Current.revision, Is.EqualTo(3));
        }
    }
}
