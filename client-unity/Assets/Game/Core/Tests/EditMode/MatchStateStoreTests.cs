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
                            playerId = "alice", instanceId = "object-1", cardId = "pf_001", cardType = "UNIT", slotKind = "UNIT", slotIndex = 2,
                            occupiedSlots = 1, redstone = 5, attack = 1, health = 2, maxHealth = 2, summonedTurn = 1, nextInstanceId = 2
                        }
                    }
                }
            });

            Assert.That(store.Current.revision, Is.EqualTo(1));
            Assert.That(store.Current.players[0].hand, Is.Empty);
            Assert.That(store.Current.players[0].unitSlots[2], Is.EqualTo("object-1"));
            Assert.That(store.Current.players[0].battlefield[0].cardId, Is.EqualTo("pf_001"));
            Assert.That(store.Current.players[0].redstone, Is.EqualTo(5));
            Assert.That(store.Current.nextInstanceId, Is.EqualTo(2));

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
                            playerId = "bob", turn = 1, phase = "MAIN", activePlayerIndex = 1, redstone = 6, redstoneCapacity = 6
                        }
                    }
                }
            });

            Assert.That(store.Current.activePlayerIndex, Is.EqualTo(1));
            Assert.That(store.Current.revision, Is.EqualTo(2));
        }

        [Test]
        public void Apply_RemovesOneHiddenOpponentHandSlotOnDeployment()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", hand = new[] { "pf_001" }, unitSlots = new string[4], buildingSlots = new string[3] },
                    new PlayerStateDto { playerId = "bob", hand = new[] { string.Empty, string.Empty }, unitSlots = new string[4], buildingSlots = new string[3] }
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
                        eventId = 1, type = MatchEventTypes.CardDeployed,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-1", cardId = "nt_001", cardType = "UNIT",
                            slotKind = "UNIT", occupiedSlots = 1, health = 2, maxHealth = 2, nextInstanceId = 2
                        }
                    }
                }
            });

            Assert.That(store.Current.players[1].hand, Has.Length.EqualTo(1));
            Assert.That(store.Current.players[1].hand[0], Is.Empty);
            Assert.That(store.Current.players[1].battlefield[0].cardId, Is.EqualTo("nt_001"));
        }

        [Test]
        public void Apply_ReplaysCombatDamageAndDeathInOrder()
        {
            var attacker = new BattlefieldObjectStateDto
            {
                instanceId = "object-1", cardId = "pf_003", cardType = "UNIT", attack = 3,
                health = 2, maxHealth = 2, slotKind = "UNIT", slotIndex = 0, occupiedSlots = 1, summonedTurn = 1
            };
            var target = new BattlefieldObjectStateDto
            {
                instanceId = "object-2", cardId = "pf_001", cardType = "UNIT", attack = 1,
                health = 2, maxHealth = 2, slotKind = "UNIT", slotIndex = 1, occupiedSlots = 1, summonedTurn = 1
            };
            var aliceSlots = new string[4];
            var bobSlots = new string[4];
            aliceSlots[0] = attacker.instanceId;
            bobSlots[1] = target.instanceId;
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                revision = 2, lastEventId = 2, turn = 2, phase = "COMBAT", activePlayerIndex = 0,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", unitSlots = aliceSlots, buildingSlots = new string[3], battlefield = new[] { attacker } },
                    new PlayerStateDto { playerId = "bob", unitSlots = bobSlots, buildingSlots = new string[3], battlefield = new[] { target } }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 3,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 3, type = MatchEventTypes.AttackResolved,
                        payload = new MatchEventPayloadDto
                        {
                            attackerPlayerId = "alice", attackerInstanceId = "object-1", attackerHealth = 1,
                            targetPlayerId = "bob", targetType = "UNIT", targetInstanceId = "object-2", targetHealth = 0
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 4, type = MatchEventTypes.ObjectDied,
                        payload = new MatchEventPayloadDto { playerId = "bob", instanceId = "object-2" }
                    }
                }
            });

            Assert.That(store.Current.players[0].battlefield[0].health, Is.EqualTo(1));
            Assert.That(store.Current.players[0].battlefield[0].hasAttacked, Is.True);
            Assert.That(store.Current.players[1].battlefield, Is.Empty);
            Assert.That(store.Current.players[1].unitSlots[1], Is.Null);
            Assert.That(store.Current.revision, Is.EqualTo(3));
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
