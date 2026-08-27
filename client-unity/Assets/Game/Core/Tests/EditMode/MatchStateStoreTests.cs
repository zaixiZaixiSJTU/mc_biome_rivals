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
