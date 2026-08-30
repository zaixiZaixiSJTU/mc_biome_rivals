using System;
using System.Linq;
using NUnit.Framework;

namespace BiomeRivals.Core.Tests
{
    public sealed class MatchStateStoreTests
    {
        [Test]
        public void Replace_UsesAuthoritativeSnapshot()
        {
            var store = new MatchStateStore();
            MatchStateDto changed = null;
            store.Changed += state => changed = state;
            var snapshot = new MatchStateDto
            {
                matchId = "match-1", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                revision = 3, players = new[] { new PlayerStateDto(), new PlayerStateDto() }
            };

            store.Replace(snapshot);

            Assert.That(store.Current, Is.SameAs(snapshot));
            Assert.That(store.Current.revision, Is.EqualTo(3));
            Assert.That(changed, Is.SameAs(snapshot));
        }

        [Test]
        public void Replace_RejectsUnsupportedAuthoritativeFaction()
        {
            var store = new MatchStateStore();
            var snapshot = new MatchStateDto
            {
                matchId = "match-1",
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { factionId = FactionIds.PlainsForest },
                    new PlayerStateDto { factionId = "unsupported" }
                }
            };

            Assert.Throws<InvalidOperationException>(() => store.Replace(snapshot));
            Assert.That(store.Current, Is.Null);
        }

        [Test]
        public void Clear_NotifiesSubscribersThatAuthoritativeStateEnded()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[] { new PlayerStateDto(), new PlayerStateDto() }
            });
            var notifications = 0;
            MatchStateDto changed = store.Current;
            store.Changed += state => { notifications++; changed = state; };

            store.Clear();

            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(changed, Is.Null);
        }

        [Test]
        public void Apply_ReplaysPrivateMulliganThenStartsFirstTurn()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "opening-1",
                viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                status = "MULLIGAN",
                turn = 1,
                phase = "MAIN",
                activePlayerIndex = 0,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", hand = new[] { "pf_001", "pf_002", "pf_003" }, deckCount = 27 },
                    new PlayerStateDto { playerId = "bob", hand = new[] { string.Empty, string.Empty, string.Empty, string.Empty }, deckCount = 26 }
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
                        type = MatchEventTypes.MulliganCompleted,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", hand = new[] { "pf_002", "pf_004", "pf_005" },
                            handCount = 3, deckCount = 27, replacedCount = 2
                        }
                    }
                }
            });

            Assert.That(store.Current.status, Is.EqualTo("MULLIGAN"));
            Assert.That(store.Current.players[0].mulliganCompleted, Is.True);
            Assert.That(store.Current.players[0].hand, Is.EqualTo(new[] { "pf_002", "pf_004", "pf_005" }));

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
                        type = MatchEventTypes.MulliganCompleted,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", hand = new[] { string.Empty, string.Empty, string.Empty, string.Empty },
                            handCount = 4, deckCount = 26, replacedCount = 1
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 3,
                        type = MatchEventTypes.MatchStarted,
                        payload = new MatchEventPayloadDto { playerId = "alice", turn = 1, activePlayerIndex = 0, phase = "MAIN" }
                    },
                    new MatchEventDto
                    {
                        eventId = 4,
                        type = MatchEventTypes.CardDrawn,
                        payload = new MatchEventPayloadDto { playerId = "alice", cardId = "pf_006", handCount = 4, deckCount = 26 }
                    }
                }
            });

            Assert.That(store.Current.status, Is.EqualTo("ACTIVE"));
            Assert.That(store.Current.players[1].mulliganCompleted, Is.True);
            Assert.That(store.Current.players[0].hand, Is.EqualTo(new[] { "pf_002", "pf_004", "pf_005", "pf_006" }));
            Assert.That(store.Current.revision, Is.EqualTo(2));
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
                            occupiedSlots = 1, redstone = 5, attack = 1, health = 2, maxHealth = 2, summonedTurn = 1,
                            keywords = new[] { "CHARGE" }, nextInstanceId = 2
                        }
                    }
                }
            });

            Assert.That(store.Current.revision, Is.EqualTo(1));
            Assert.That(store.Current.players[0].hand, Is.Empty);
            Assert.That(store.Current.players[0].unitSlots[2], Is.EqualTo("object-1"));
            Assert.That(store.Current.players[0].battlefield[0].cardId, Is.EqualTo("pf_001"));
            Assert.That(store.Current.players[0].battlefield[0].keywords, Is.EqualTo(new[] { "CHARGE" }));
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
        public void Apply_ReplaysThreeSlotStructureDamageAndReleasesItsWholeRange()
        {
            var attacker = new BattlefieldObjectStateDto
            {
                instanceId = "object-1", cardId = "pf_003", cardType = "UNIT", attack = 4,
                health = 2, maxHealth = 2, slotKind = "UNIT", slotIndex = 0, occupiedSlots = 1, summonedTurn = 1
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-structure", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                revision = 0, lastEventId = 0, turn = 2, phase = "COMBAT", activePlayerIndex = 0,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", hand = Array.Empty<string>(), unitSlots = new[] { "object-1", null, null, null },
                        buildingSlots = new string[3], battlefield = new[] { attacker }
                    },
                    new PlayerStateDto
                    {
                        playerId = "bob", hand = new[] { "ed_008" }, unitSlots = new string[4],
                        buildingSlots = new string[3], battlefield = Array.Empty<BattlefieldObjectStateDto>(), discardPile = Array.Empty<string>()
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
                        eventId = 1, type = MatchEventTypes.CardDeployed,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-2", cardId = "ed_008", cardType = "STRUCTURE",
                            slotKind = "BUILDING", slotIndex = 0, occupiedSlots = 3, redstone = 0,
                            health = 12, maxHealth = 12, summonedTurn = 2, nextInstanceId = 3
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.AttackResolved,
                        payload = new MatchEventPayloadDto
                        {
                            attackerPlayerId = "alice", attackerInstanceId = "object-1", attackerHealth = 2,
                            targetPlayerId = "bob", targetType = "BUILDING", targetInstanceId = "object-2", targetHealth = 0
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 3, type = MatchEventTypes.ObjectDied,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-2", cardId = "ed_008",
                            slotKind = "BUILDING", slotIndex = 0, occupiedSlots = 3, discardCount = 1
                        }
                    }
                }
            });

            Assert.That(store.Current.players[0].battlefield[0].hasAttacked, Is.True);
            Assert.That(store.Current.players[0].battlefield[0].health, Is.EqualTo(2));
            Assert.That(store.Current.players[1].battlefield, Is.Empty);
            Assert.That(store.Current.players[1].buildingSlots, Is.EqualTo(new string[3]));
            Assert.That(store.Current.players[1].discardPile, Is.EqualTo(new[] { "ed_008" }));
            Assert.That(store.Current.lastEventId, Is.EqualTo(3));
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
        public void Apply_ReplaysMaterialConsumptionBeforeCraftedDeployment()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-crafting", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                revision = 0, lastEventId = 0, turn = 1, phase = "MAIN", activePlayerIndex = 0, nextInstanceId = 1,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", redstone = 0, hand = new[] { "db_002", "db_007", "tk_006", "db_002" },
                        discardPile = Array.Empty<string>(), unitSlots = new string[4], buildingSlots = new string[3]
                    },
                    new PlayerStateDto { playerId = "bob", hand = Array.Empty<string>(), unitSlots = new string[4], buildingSlots = new string[3] }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.MaterialsConsumed,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", craftedCardId = "db_007", recipeId = "recipe.db_007.01",
                            materials = new[]
                            {
                                new CraftingMaterialDto { cardId = "db_002", count = 1 },
                                new CraftingMaterialDto { cardId = "tk_006", count = 1 }
                            },
                            handCount = 2, discardCount = 2
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.CardDeployed,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", instanceId = "object-1", cardId = "db_007", cardType = "STRUCTURE",
                            slotKind = "BUILDING", slotIndex = 1, occupiedSlots = 2, paymentMethod = MatchPaymentMethods.Crafting,
                            redstone = 0, health = 10, maxHealth = 10, summonedTurn = 1, nextInstanceId = 2
                        }
                    }
                }
            });

            Assert.That(store.Current.players[0].hand, Is.EqualTo(new[] { "db_002" }));
            Assert.That(store.Current.players[0].discardPile, Is.EqualTo(new[] { "db_002", "tk_006" }));
            Assert.That(store.Current.players[0].buildingSlots[1], Is.EqualTo("object-1"));
            Assert.That(store.Current.players[0].buildingSlots[2], Is.EqualTo("object-1"));
            Assert.That(store.Current.players[0].battlefield[0].maxHealth, Is.EqualTo(10));
            Assert.That(store.Current.lastEventId, Is.EqualTo(2));
        }

        [Test]
        public void Apply_ReplaysPublicCraftingAgainstHiddenOpponentHandSlots()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-hidden-crafting", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", hand = Array.Empty<string>(), unitSlots = new string[4], buildingSlots = new string[3] },
                    new PlayerStateDto
                    {
                        playerId = "bob", hand = new string[3], discardPile = Array.Empty<string>(),
                        unitSlots = new string[4], buildingSlots = new string[3]
                    }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.MaterialsConsumed,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", craftedCardId = "db_007", recipeId = "recipe.db_007.01",
                            materials = new[]
                            {
                                new CraftingMaterialDto { cardId = "db_002", count = 1 },
                                new CraftingMaterialDto { cardId = "tk_006", count = 1 }
                            },
                            handCount = 1, discardCount = 2
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.CardDeployed,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-1", cardId = "db_007", cardType = "STRUCTURE",
                            slotKind = "BUILDING", slotIndex = 0, occupiedSlots = 2, paymentMethod = MatchPaymentMethods.Crafting,
                            health = 10, maxHealth = 10, nextInstanceId = 2
                        }
                    }
                }
            });

            Assert.That(store.Current.players[1].hand, Is.Empty);
            Assert.That(store.Current.players[1].discardPile, Is.EqualTo(new[] { "db_002", "tk_006" }));
            Assert.That(store.Current.players[1].battlefield[0].cardId, Is.EqualTo("db_007"));
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
                        payload = new MatchEventPayloadDto { playerId = "bob", instanceId = "object-2", discardCount = 1 }
                    }
                }
            });

            Assert.That(store.Current.players[0].battlefield[0].health, Is.EqualTo(1));
            Assert.That(store.Current.players[0].battlefield[0].hasAttacked, Is.True);
            Assert.That(store.Current.players[1].battlefield, Is.Empty);
            Assert.That(store.Current.players[1].unitSlots[1], Is.Null);
            Assert.That(store.Current.players[1].discardPile, Is.EqualTo(new[] { "pf_001" }));
            Assert.That(store.Current.revision, Is.EqualTo(3));
        }

        [Test]
        public void Apply_ReplaysDeathThenSummonIntoTheReleasedUnitSlot()
        {
            var magmaCube = new BattlefieldObjectStateDto
            {
                instanceId = "object-1", cardId = "nt_001", cardType = "UNIT", attack = 2,
                health = 0, maxHealth = 1, slotKind = "UNIT", slotIndex = 2, occupiedSlots = 1, summonedTurn = 1
            };
            var unitSlots = new string[4];
            unitSlots[2] = magmaCube.instanceId;
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                revision = 2, lastEventId = 2, turn = 2, phase = "COMBAT", activePlayerIndex = 0, nextInstanceId = 2,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", unitSlots = unitSlots, buildingSlots = new string[3],
                        battlefield = new[] { magmaCube }, discardPile = Array.Empty<string>()
                    },
                    new PlayerStateDto { playerId = "bob", unitSlots = new string[4], buildingSlots = new string[3] }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 3,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 3, type = MatchEventTypes.ObjectDied,
                        payload = new MatchEventPayloadDto { playerId = "alice", instanceId = "object-1", discardCount = 1 }
                    },
                    new MatchEventDto
                    {
                        eventId = 4, type = MatchEventTypes.ObjectSummoned,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", sourceCardId = "nt_001", sourceInstanceId = "object-1",
                            effectId = "effect.nt_001.01", cardId = "tk_014", instanceId = "object-2",
                            cardType = "UNIT", slotKind = "UNIT", slotIndex = 2, occupiedSlots = 1,
                            attack = 1, health = 1, maxHealth = 1, summonedTurn = 2,
                            keywords = Array.Empty<string>(), nextInstanceId = 3
                        }
                    }
                }
            });

            Assert.That(store.Current.players[0].discardPile, Is.EqualTo(new[] { "nt_001" }));
            Assert.That(store.Current.players[0].unitSlots[2], Is.EqualTo("object-2"));
            Assert.That(store.Current.players[0].battlefield, Has.Length.EqualTo(1));
            Assert.That(store.Current.players[0].battlefield[0].cardId, Is.EqualTo("tk_014"));
            Assert.That(store.Current.players[0].battlefield[0].summonedTurn, Is.EqualTo(2));
            Assert.That(store.Current.nextInstanceId, Is.EqualTo(3));
        }

        [Test]
        public void Apply_ReplaysPrivateDrawAndPublicBurnWithoutLeakingOpponentCard()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", hand = new[] { "pf_001" }, deckCount = 2 },
                    new PlayerStateDto { playerId = "bob", hand = new[] { string.Empty }, deckCount = 2 }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.CardDrawn,
                        payload = new MatchEventPayloadDto { playerId = "bob", cardId = string.Empty, handCount = 2, deckCount = 1 }
                    },
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.CardBurned,
                        payload = new MatchEventPayloadDto { playerId = "alice", cardId = "pf_008", handCount = 1, deckCount = 1, discardCount = 1 }
                    }
                }
            });

            Assert.That(store.Current.players[1].hand, Has.Length.EqualTo(2));
            Assert.That(store.Current.players[1].hand[1], Is.Empty);
            Assert.That(store.Current.players[1].deckCount, Is.EqualTo(1));
            Assert.That(store.Current.players[0].discardPile, Is.EqualTo(new[] { "pf_008" }));
        }

        [Test]
        public void Apply_ReplaysPrivateGeneratedHandCardsAndPublicGeneratedDiscards()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", hand = new[] { "pf_001" }, discardPile = Array.Empty<string>() },
                    new PlayerStateDto { playerId = "bob", hand = new[] { string.Empty }, discardPile = Array.Empty<string>() }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.CardGenerated,
                        payload = new MatchEventPayloadDto { playerId = "alice", cardId = "tk_016", destination = "HAND", handCount = 2, discardCount = 0 }
                    },
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.CardGenerated,
                        payload = new MatchEventPayloadDto { playerId = "bob", cardId = null, destination = "HAND", handCount = 2, discardCount = 0 }
                    },
                    new MatchEventDto
                    {
                        eventId = 3, type = MatchEventTypes.CardGenerated,
                        payload = new MatchEventPayloadDto { playerId = "alice", cardId = "tk_016", destination = "DISCARD", handCount = 2, discardCount = 1 }
                    }
                }
            });

            Assert.That(store.Current.players[0].hand, Is.EqualTo(new[] { "pf_001", "tk_016" }));
            Assert.That(store.Current.players[0].discardPile, Is.EqualTo(new[] { "tk_016" }));
            Assert.That(store.Current.players[1].hand, Has.Length.EqualTo(2));
            Assert.That(store.Current.players[1].hand[1], Is.Null);
        }

        [Test]
        public void Apply_ReplaysBurialAndPublicExcavationWithoutRevealingOpponentHandSlots()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice" },
                    new PlayerStateDto { playerId = "bob", hand = new[] { string.Empty }, deckCount = 2 }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.CardBuried,
                        payload = new MatchEventPayloadDto { playerId = "bob", cardId = "tk_006", deckCount = 3, buriedCount = 1 }
                    },
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.CardExcavated,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", cardId = "tk_006", destination = "HAND",
                            handCount = 2, deckCount = 2, discardCount = 0, buriedCount = 0
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 3, type = MatchEventTypes.ArmorGained,
                        payload = new MatchEventPayloadDto { playerId = "bob", amount = 1, armor = 1 }
                    },
                    new MatchEventDto
                    {
                        eventId = 4, type = MatchEventTypes.CardDrawn,
                        payload = new MatchEventPayloadDto { playerId = "bob", cardId = string.Empty, handCount = 3, deckCount = 1 }
                    }
                }
            });

            Assert.That(store.Current.players[1].buriedCount, Is.Zero);
            Assert.That(store.Current.players[1].deckCount, Is.EqualTo(1));
            Assert.That(store.Current.players[1].armor, Is.EqualTo(1));
            Assert.That(store.Current.players[1].excavatedThisTurn, Is.True);
            Assert.That(store.Current.players[1].hand, Has.Length.EqualTo(3));
            Assert.That(store.Current.players[1].hand[1], Is.Empty);
            Assert.That(store.Current.players[1].hand[2], Is.Empty);

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 5, type = MatchEventTypes.TurnEnded,
                        payload = new MatchEventPayloadDto { playerId = "bob" }
                    }
                }
            });
            Assert.That(store.Current.players[1].excavatedThisTurn, Is.False);
        }

        [Test]
        public void Apply_ReplaysFatigueAsTrueHeroDamage()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", life = 30 },
                    new PlayerStateDto { playerId = "bob", life = 3, armor = 4, deckCount = 0 }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.FatigueDamage,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", damage = 2, fatigueCount = 2, life = 1, armor = 4, handCount = 0, deckCount = 0
                        }
                    }
                }
            });

            Assert.That(store.Current.players[1].life, Is.EqualTo(1));
            Assert.That(store.Current.players[1].armor, Is.EqualTo(4));
            Assert.That(store.Current.players[1].fatigueCount, Is.EqualTo(2));
        }

        [Test]
        public void Apply_ReplaysPlayedCardSelfDamagePrivateDrawAndArmor()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", life = 30, redstone = 2, hand = new[] { "nt_006", "tk_016" }, deckCount = 1 },
                    new PlayerStateDto { playerId = "bob", life = 30, hand = new string[0] }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto { eventId = 1, type = MatchEventTypes.CardPlayed, payload = new MatchEventPayloadDto { playerId = "alice", cardId = "nt_006", redstone = 1, handCount = 1, discardCount = 1 } },
                    new MatchEventDto { eventId = 2, type = MatchEventTypes.HeroDamaged, payload = new MatchEventPayloadDto { playerId = "alice", damage = 2, damageType = "TRUE", life = 28, armor = 0 } },
                    new MatchEventDto { eventId = 3, type = MatchEventTypes.CardDrawn, payload = new MatchEventPayloadDto { playerId = "alice", cardId = "nt_001", handCount = 2, deckCount = 0 } }
                }
            });
            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto { eventId = 4, type = MatchEventTypes.CardPlayed, payload = new MatchEventPayloadDto { playerId = "alice", cardId = "tk_016", redstone = 0, handCount = 1, discardCount = 2 } },
                    new MatchEventDto { eventId = 5, type = MatchEventTypes.ArmorGained, payload = new MatchEventPayloadDto { playerId = "alice", amount = 2, armor = 2 } }
                }
            });

            Assert.That(store.Current.players[0].life, Is.EqualTo(28));
            Assert.That(store.Current.players[0].armor, Is.EqualTo(2));
            Assert.That(store.Current.players[0].hand, Is.EqualTo(new[] { "nt_001" }));
            Assert.That(store.Current.players[0].discardPile, Is.EqualTo(new[] { "nt_006", "tk_016" }));
        }

        [Test]
        public void Apply_ReplaysHealingBeforeDamageInEventOrder()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[] { new PlayerStateDto { playerId = "alice", life = 25 }, new PlayerStateDto { playerId = "bob", life = 30 } }
            });
            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto { eventId = 1, type = MatchEventTypes.HeroHealed, payload = new MatchEventPayloadDto { playerId = "alice", healing = 2, life = 27 } },
                    new MatchEventDto { eventId = 2, type = MatchEventTypes.HeroDamaged, payload = new MatchEventPayloadDto { playerId = "alice", damage = 1, damageType = "TRUE", life = 26, armor = 0 } }
                }
            });
            Assert.That(store.Current.players[0].life, Is.EqualTo(26));
        }

        [Test]
        public void Apply_ReplaysTemporaryAttackModifierAndExpiry()
        {
            var target = new BattlefieldObjectStateDto
            {
                instanceId = "object-7", cardId = "nt_003", cardType = "UNIT", attack = 3,
                health = 3, maxHealth = 3, slotKind = "UNIT", slotIndex = 1, occupiedSlots = 1, summonedTurn = 1
            };
            var slots = new string[4];
            slots[1] = target.instanceId;
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice" },
                    new PlayerStateDto { playerId = "bob", unitSlots = slots, battlefield = new[] { target } }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.ObjectStatsChanged,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-7", attack = 2, health = 3,
                            temporaryAttackModifier = -1, temporaryAttackModifierExpiresOnTurn = 1
                        }
                    }
                }
            });
            Assert.That(target.attack, Is.EqualTo(2));
            Assert.That(target.temporaryAttackModifier, Is.EqualTo(-1));

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.ObjectStatsChanged,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-7", attack = 3, health = 3,
                            temporaryAttackModifier = 0, temporaryAttackModifierExpiresOnTurn = 0
                        }
                    }
                }
            });
            Assert.That(target.attack, Is.EqualTo(3));
            Assert.That(target.temporaryAttackModifier, Is.Zero);
        }

        [Test]
        public void Apply_ReplaysFriendlyAttackBuffAndBuildingHeal()
        {
            var unit = new BattlefieldObjectStateDto
            {
                instanceId = "object-1", cardId = "pf_001", cardType = "UNIT", attack = 1,
                health = 2, maxHealth = 2, slotKind = "UNIT", slotIndex = 0, occupiedSlots = 1, summonedTurn = 1
            };
            var building = new BattlefieldObjectStateDto
            {
                instanceId = "object-2", cardId = "db_004", cardType = "BUILDING", attack = 0,
                health = 1, maxHealth = 5, slotKind = "BUILDING", slotIndex = 0, occupiedSlots = 1, summonedTurn = 1
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", unitSlots = new[] { "object-1", null, null, null },
                        buildingSlots = new[] { "object-2", null, null }, battlefield = new[] { unit, building }
                    },
                    new PlayerStateDto { playerId = "bob" }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.ObjectStatsChanged,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", instanceId = "object-1", attack = 2, health = 2,
                            temporaryAttackModifier = 1, temporaryAttackModifierExpiresOnTurn = 1
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.ObjectStatsChanged,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", instanceId = "object-2", attack = 0, health = 3,
                            temporaryAttackModifier = 0, temporaryAttackModifierExpiresOnTurn = 0
                        }
                    }
                }
            });

            Assert.That(unit.attack, Is.EqualTo(2));
            Assert.That(unit.temporaryAttackModifier, Is.EqualTo(1));
            Assert.That(building.health, Is.EqualTo(3));
        }

        [Test]
        public void Apply_ReplaysPrivateArchaeologyChoiceAndItsResolution()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice", protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, status = "ACTIVE", phase = "MAIN",
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", hand = new string[0], deckCount = 3, buriedCount = 1,
                        battlefield = new[]
                        {
                            new BattlefieldObjectStateDto
                            {
                                instanceId = "object-1", cardId = "db_003", cardType = "UNIT", slotKind = "UNIT",
                                slotIndex = 0, occupiedSlots = 1, attack = 2, health = 3, maxHealth = 3
                            }
                        }
                    },
                    new PlayerStateDto { playerId = "bob" }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.ChoiceOffered,
                        payload = new MatchEventPayloadDto
                        {
                            choiceId = "choice-1", playerId = "alice", sourceCardId = "db_003",
                            sourceInstanceId = "object-1", effectId = "effect.db_003.01", kind = "ARCHAEOLOGY_TOP_3",
                            options = new[]
                            {
                                new PendingChoiceOptionDto { optionIndex = 0, cardId = "db_001", selectable = false },
                                new PendingChoiceOptionDto { optionIndex = 1, cardId = "tk_006", selectable = true },
                                new PendingChoiceOptionDto { optionIndex = 2, cardId = "db_004", selectable = false }
                            }
                        }
                    }
                }
            });

            Assert.That(store.Current.pendingChoice, Is.Not.Null);
            Assert.That(store.Current.pendingChoice.options[1].cardId, Is.EqualTo("tk_006"));
            Assert.That(store.Current.pendingChoice.options[1].selectable, Is.True);

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.ChoiceResolved,
                        payload = new MatchEventPayloadDto
                        {
                            choiceId = "choice-1", playerId = "alice", selectedOptionIndex = 1,
                            selectedCardId = "tk_006", sourceCardId = "db_003", sourceInstanceId = "object-1",
                            effectId = "effect.db_003.01", kind = "ARCHAEOLOGY_TOP_3"
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 3, type = MatchEventTypes.CardExcavated,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "alice", cardId = "tk_006", destination = "HAND",
                            handCount = 1, deckCount = 2, buriedCount = 0, discardCount = 0
                        }
                    },
                    new MatchEventDto
                    {
                        eventId = 4, type = MatchEventTypes.ArmorGained,
                        payload = new MatchEventPayloadDto { playerId = "alice", amount = 1, armor = 1 }
                    },
                    new MatchEventDto
                    {
                        eventId = 5, type = MatchEventTypes.CardDrawn,
                        payload = new MatchEventPayloadDto { playerId = "alice", cardId = "db_001", handCount = 2, deckCount = 1 }
                    }
                }
            });

            Assert.That(store.Current.pendingChoice, Is.Null);
            Assert.That(store.Current.players[0].hand, Is.EqualTo(new[] { "tk_006", "db_001" }));
            Assert.That(store.Current.players[0].deckCount, Is.EqualTo(1));
            Assert.That(store.Current.players[0].buriedCount, Is.Zero);
            Assert.That(store.Current.players[0].armor, Is.EqualTo(1));
            Assert.That(store.Current.players[0].excavatedThisTurn, Is.True);
        }

        [Test]
        public void Replace_AcceptsRedactedOpponentChoiceWithoutLeakingCardIds()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "bob", protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, status = "ACTIVE", phase = "MAIN",
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice",
                        battlefield = new[]
                        {
                            new BattlefieldObjectStateDto
                            {
                                instanceId = "object-1", cardId = "db_003", cardType = "UNIT",
                                slotKind = "UNIT", slotIndex = 0, occupiedSlots = 1
                            }
                        }
                    },
                    new PlayerStateDto { playerId = "bob" }
                },
                pendingChoice = new PendingChoiceDto
                {
                    choiceId = "choice-1", playerId = "alice", sourceCardId = "db_003", sourceInstanceId = "object-1",
                    effectId = "effect.db_003.01", kind = "ARCHAEOLOGY_TOP_3",
                    options = new[]
                    {
                        new PendingChoiceOptionDto { optionIndex = 0, cardId = string.Empty, selectable = false },
                        new PendingChoiceOptionDto { optionIndex = 1, cardId = string.Empty, selectable = false },
                        new PendingChoiceOptionDto { optionIndex = 2, cardId = string.Empty, selectable = false }
                    }
                }
            });

            Assert.That(store.Current.pendingChoice.playerId, Is.EqualTo("alice"));
            Assert.That(store.Current.pendingChoice.options.All(option => option.cardId == string.Empty), Is.True);
            Assert.That(store.Current.pendingChoice.options.All(option => !option.selectable), Is.True);
            store.Current.pendingChoice.options[0].cardId = "db_001";
            Assert.Throws<InvalidOperationException>(() => store.Replace(store.Current),
                "A non-owner snapshot must be rejected if it leaks a real option card id.");
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

        [Test]
        public void Apply_ReplaysSlowApplicationAndRemoval()
        {
            var target = new BattlefieldObjectStateDto
            {
                instanceId = "object-7", cardId = "nt_003", cardType = "UNIT", attack = 3,
                health = 3, maxHealth = 3, slotKind = "UNIT", slotIndex = 1, occupiedSlots = 1, summonedTurn = 1
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "slow-replay", protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice" },
                    new PlayerStateDto
                    {
                        playerId = "bob", unitSlots = new[] { null, "object-7", null, null },
                        battlefield = new[] { target }
                    }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 1, type = MatchEventTypes.ObjectStatusApplied,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-7", statusId = "SLOW", remainingDuration = 1,
                            sourcePlayerId = "alice", sourceCardId = "si_006", effectId = "effect.si_006.01",
                            statusAttackModifier = -2, boundAttackModifier = -2, attack = 1, health = 3
                        }
                    }
                }
            });
            Assert.That(target.attack, Is.EqualTo(1));
            Assert.That(target.statuses.Single().statusId, Is.EqualTo("SLOW"));
            Assert.That(target.statuses.Single().sourcePlayerId, Is.EqualTo("alice"));
            Assert.That(target.statuses.Single().boundAttackModifier, Is.EqualTo(-2));

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto
                    {
                        eventId = 2, type = MatchEventTypes.ObjectStatusRemoved,
                        payload = new MatchEventPayloadDto
                        {
                            playerId = "bob", instanceId = "object-7", statusId = "SLOW",
                            sourcePlayerId = "alice", sourceCardId = "si_006", effectId = "effect.si_006.01",
                            reason = "DURATION_EXPIRED", attack = 3, health = 3
                        }
                    }
                }
            });
            Assert.That(target.attack, Is.EqualTo(3));
            Assert.That(target.statuses, Is.Empty);
        }

        [Test]
        public void Apply_ReplaysHeroEquipmentAttackAndTridentMovementChoice()
        {
            var target = new BattlefieldObjectStateDto
            {
                instanceId = "object-7", cardId = "si_005", cardType = "UNIT", attack = 3,
                health = 6, maxHealth = 6, slotKind = "UNIT", slotIndex = 2, occupiedSlots = 1, summonedTurn = 1
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "equipment-replay", viewerPlayerId = "alice", protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, status = "ACTIVE", phase = "MAIN", turn = 1, activePlayerIndex = 0,
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", life = 30, redstone = 6, redstoneCapacity = 6, hand = new[] { "or_006" } },
                    new PlayerStateDto
                    {
                        playerId = "bob", life = 30, unitSlots = new[] { null, null, "object-7", null },
                        battlefield = new[] { target }
                    }
                }
            });
            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto { eventId = 1, type = MatchEventTypes.CardEquipped, payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", instanceId = "equipment-1", cardId = "or_006", attack = 2,
                        durability = 3, maxDurability = 3, redstone = 3, handCount = 0, nextInstanceId = 2
                    }},
                    new MatchEventDto { eventId = 2, type = MatchEventTypes.PhaseChanged, payload = new MatchEventPayloadDto { phase = "COMBAT" } },
                    new MatchEventDto { eventId = 3, type = MatchEventTypes.AttackResolved, payload = new MatchEventPayloadDto
                    {
                        attackerPlayerId = "alice", attackerInstanceId = MatchAttackerIds.Hero,
                        targetPlayerId = "bob", targetType = "UNIT", targetInstanceId = "object-7",
                        attackerHealth = 27, attackerArmor = 0, targetHealth = 4
                    }},
                    new MatchEventDto { eventId = 4, type = MatchEventTypes.EquipmentDurabilityChanged, payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", instanceId = "equipment-1", cardId = "or_006", durability = 2, maxDurability = 3
                    }},
                    new MatchEventDto { eventId = 5, type = MatchEventTypes.ChoiceOffered, payload = new MatchEventPayloadDto
                    {
                        choiceId = "choice-5", playerId = "alice", sourceCardId = "or_006", sourceInstanceId = "equipment-1",
                        effectId = "effect.or_006.01", kind = "MOVE_UNIT", targetPlayerId = "bob", targetInstanceId = "object-7",
                        options = new[]
                        {
                            new PendingChoiceOptionDto { optionIndex = 0, cardId = "si_005", slotIndex = 1, selectable = true },
                            new PendingChoiceOptionDto { optionIndex = 1, cardId = "si_005", slotIndex = 3, selectable = true }
                        }
                    }}
                }
            });
            Assert.That(store.Current.players[0].equipment.durability, Is.EqualTo(2));
            Assert.That(store.Current.players[0].heroHasAttacked, Is.True);
            Assert.That(store.Current.players[0].life, Is.EqualTo(27));
            Assert.That(store.Current.pendingChoice.kind, Is.EqualTo("MOVE_UNIT"));

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto { eventId = 6, type = MatchEventTypes.ChoiceResolved, payload = new MatchEventPayloadDto
                    {
                        choiceId = "choice-5", playerId = "alice"
                    }},
                    new MatchEventDto { eventId = 7, type = MatchEventTypes.ObjectMoved, payload = new MatchEventPayloadDto
                    {
                        playerId = "bob", instanceId = "object-7", fromSlotIndex = 2, toSlotIndex = 1
                    }}
                }
            });
            Assert.That(target.slotIndex, Is.EqualTo(1));
            Assert.That(store.Current.players[1].unitSlots[1], Is.EqualTo("object-7"));
            Assert.That(store.Current.pendingChoice, Is.Null);
        }

        [Test]
        public void Apply_ReplaysPrismarineShardEffectChoiceMovementAndHealing()
        {
            var salmon = new BattlefieldObjectStateDto
            {
                instanceId = "object-1", cardId = "or_001", cardType = "UNIT", attack = 1,
                health = 1, maxHealth = 2, slotKind = "UNIT", slotIndex = 1, occupiedSlots = 1, summonedTurn = 1
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "prismarine-replay", viewerPlayerId = "alice", protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, status = "ACTIVE", phase = "MAIN", turn = 1, activePlayerIndex = 0,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", life = 30, redstone = 0, hand = new[] { "tk_012" },
                        discardPile = Array.Empty<string>(), unitSlots = new[] { null, "object-1", null, null },
                        battlefield = new[] { salmon }
                    },
                    new PlayerStateDto { playerId = "bob", life = 30 }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto { eventId = 1, type = MatchEventTypes.CardPlayed, payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", cardId = "tk_012", effectId = "effect.tk_012.01",
                        redstone = 0, handCount = 0, discardCount = 1
                    }},
                    new MatchEventDto { eventId = 2, type = MatchEventTypes.ChoiceOffered, payload = new MatchEventPayloadDto
                    {
                        choiceId = "choice-2", playerId = "alice", sourceCardId = "tk_012", sourceInstanceId = "effect-1",
                        effectId = "effect.tk_012.01", kind = "MOVE_UNIT", targetPlayerId = "alice", targetInstanceId = "object-1",
                        options = new[]
                        {
                            new PendingChoiceOptionDto { optionIndex = 0, cardId = "or_001", slotIndex = 0, selectable = true },
                            new PendingChoiceOptionDto { optionIndex = 1, cardId = "or_001", slotIndex = 2, selectable = true }
                        }
                    }}
                }
            });
            Assert.That(store.Current.pendingChoice.sourceInstanceId, Is.EqualTo("effect-1"));
            Assert.That(store.Current.players[0].discardPile, Is.EqualTo(new[] { "tk_012" }));

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto { eventId = 3, type = MatchEventTypes.ChoiceResolved,
                        payload = new MatchEventPayloadDto { choiceId = "choice-2", playerId = "alice" } },
                    new MatchEventDto { eventId = 4, type = MatchEventTypes.ObjectMoved, payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", instanceId = "object-1", cardId = "or_001",
                        sourceCardId = "tk_012", sourceInstanceId = "effect-1", effectId = "effect.tk_012.01",
                        fromSlotIndex = 1, toSlotIndex = 2
                    }},
                    new MatchEventDto { eventId = 5, type = MatchEventTypes.ObjectStatsChanged, payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", instanceId = "object-1", sourceCardId = "tk_012",
                        sourceInstanceId = "effect-1", effectId = "effect.tk_012.01",
                        attack = 1, health = 2, temporaryAttackModifier = 0, temporaryAttackModifierExpiresOnTurn = 0
                    }}
                }
            });
            Assert.That(store.Current.pendingChoice, Is.Null);
            Assert.That(salmon.slotIndex, Is.EqualTo(2));
            Assert.That(salmon.health, Is.EqualTo(2));
        }

        [Test]
        public void Apply_TracksAndClearsGuardianOncePerTurnReactionMarker()
        {
            var guardian = new BattlefieldObjectStateDto
            {
                instanceId = "object-10", cardId = "or_004", cardType = "UNIT", attack = 3,
                health = 4, maxHealth = 4, slotKind = "UNIT", slotIndex = 0, occupiedSlots = 1, summonedTurn = 1
            };
            var movedUnit = new BattlefieldObjectStateDto
            {
                instanceId = "object-20", cardId = "or_001", cardType = "UNIT", attack = 2,
                health = 2, maxHealth = 2, slotKind = "UNIT", slotIndex = 1, occupiedSlots = 1, summonedTurn = 1
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "guardian-reaction-replay", viewerPlayerId = "alice", protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, status = "ACTIVE", phase = "MAIN", turn = 1, activePlayerIndex = 1,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", life = 30, unitSlots = new[] { "object-10", null, null, null },
                        battlefield = new[] { guardian }
                    },
                    new PlayerStateDto
                    {
                        playerId = "bob", life = 30, unitSlots = new[] { null, "object-20", null, null },
                        battlefield = new[] { movedUnit }
                    }
                }
            });

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 1,
                events = new[]
                {
                    new MatchEventDto { eventId = 1, type = MatchEventTypes.ObjectStatsChanged, payload = new MatchEventPayloadDto
                    {
                        playerId = "bob", instanceId = "object-20", sourceCardId = "or_004",
                        sourceInstanceId = "object-10", effectId = "effect.or_004.01",
                        attack = 2, health = 1, temporaryAttackModifier = 1, temporaryAttackModifierExpiresOnTurn = 1
                    }}
                }
            });
            Assert.That(movedUnit.health, Is.EqualTo(1));
            Assert.That(store.Current.players[0].triggeredEffectKeysThisTurn,
                Is.EqualTo(new[] { "object-10:effect.or_004.01" }));

            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset, revision = 2,
                events = new[]
                {
                    new MatchEventDto { eventId = 2, type = MatchEventTypes.TurnEnded,
                        payload = new MatchEventPayloadDto { playerId = "bob" } }
                }
            });
            Assert.That(store.Current.players[0].triggeredEffectKeysThisTurn, Is.Empty);
            Assert.That(store.Current.players[1].triggeredEffectKeysThisTurn, Is.Empty);
        }
    }
}
