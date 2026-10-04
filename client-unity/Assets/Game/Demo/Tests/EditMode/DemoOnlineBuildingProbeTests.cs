using System;
using BiomeRivals.Core;
using NUnit.Framework;
using UnityEngine;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineBuildingProbeTests
    {
        [TestCase("cd_004")]
        [TestCase("cd_007")]
        public void BuildingMulliganKeepsExactlyOneCriticalCardAndDoesNotMutateTheHand(string cardId)
        {
            Assert.That(DemoOnlineBuildingProbe.SelectMulligan(null, cardId), Is.Empty);
            Assert.That(DemoOnlineBuildingProbe.SelectMulligan(Array.Empty<string>(), cardId), Is.Empty);
            Assert.That(DemoOnlineBuildingProbe.SelectMulligan(new[] { "cd_001", "cd_008" }, cardId), Is.EqualTo(new[] { 0, 1 }));
            var hand = new[] { "cd_002", cardId, cardId, "cd_006" };
            var copy = (string[])hand.Clone();
            Assert.That(DemoOnlineBuildingProbe.SelectMulligan(hand, cardId), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(hand, Is.EqualTo(copy));
        }

        [Test]
        public void BuildingBarrierHashIgnoresViewerAndPrivateHandButIncludesCompleteBoardFootprints()
        {
            var state = new MatchStateDto
            {
                arenaId = "nether_lava_sea", viewerPlayerId = "alice",
                players = new[]
                {
                    new PlayerStateDto { playerId = "alice", unitSlots = new string[3], buildingSlots = new[] { null, null, "object-1", "object-1" },
                        hand = new[] { "cd_001" }, handCards = new[] { new HandCardStateDto { cardId = "cd_001", handCardInstanceId = "hand-1" } },
                        battlefield = new[] { new BattlefieldObjectStateDto { instanceId = "object-1", ownerPlayerId = "alice", cardId = "cd_007", slotIndex = 2, occupiedSlots = 2, health = 8 } } },
                    new PlayerStateDto { playerId = "bob", unitSlots = new string[3], buildingSlots = new string[4] }
                }
            };
            var initial = DemoOnlineBuildingProbe.PublicBoardHash(state);
            state.viewerPlayerId = "bob";
            state.players[0].hand = new string[1];
            state.players[0].handCards = new HandCardStateDto[1];
            state.players[1].hand = new[] { "cd_007" };
            state.players[1].handCards = new[] { new HandCardStateDto { cardId = "cd_007", handCardInstanceId = "hand-5" } };
            Assert.That(DemoOnlineBuildingProbe.PublicBoardHash(state), Is.EqualTo(initial), "barriers may not depend on private projection");
            state.players[0].buildingSlots[3] = null;
            Assert.That(DemoOnlineBuildingProbe.PublicBoardHash(state), Is.Not.EqualTo(initial), "a missing second occupied cell must change the hash");
            state.players[0].buildingSlots[3] = "object-1";
            state.players[0].battlefield[0].slotIndex = 1;
            Assert.That(DemoOnlineBuildingProbe.PublicBoardHash(state), Is.Not.EqualTo(initial), "an incorrect model anchor must change the hash");
            state.players[0].battlefield[0].slotIndex = 2;
            state.players[0].battlefield[0].occupiedSlots = 1;
            Assert.That(DemoOnlineBuildingProbe.PublicBoardHash(state), Is.Not.EqualTo(initial), "a stale object width must change the hash");
        }
    }
}
