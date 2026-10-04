using System;
using BiomeRivals.Core;
using NUnit.Framework;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineGoatProbeTests
    {
        [Test]
        public void MulliganKeepsOneOfEachRequiredCardWithoutMutatingTheHand()
        {
            Assert.That(DemoOnlineGoatProbe.SelectMulligan(null), Is.Empty);
            var hand = new[] { "si_002", "si_004", "si_002", "si_007", "si_004" };
            var original = (string[])hand.Clone();
            Assert.That(DemoOnlineGoatProbe.SelectMulligan(hand), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(hand, Is.EqualTo(original));
            Assert.That(DemoOnlineGoatProbe.SelectMulligan(new[] { "si_006", "si_007" }), Is.EqualTo(new[] { 0, 1 }));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void MovementProofRequiresExactFootprintIdentityAndBattlecryState(int count)
        {
            var slots = new string[count];
            slots[count - 2] = "object-2"; slots[count - 1] = "object-1";
            var target = new BattlefieldObjectStateDto { instanceId = "object-1", ownerPlayerId = "alice", cardId = "si_002", slotKind = "UNIT",
                slotIndex = count - 1, occupiedSlots = 1, attack = 1, health = 4, maxHealth = 4 };
            var goat = new BattlefieldObjectStateDto { instanceId = "object-2", ownerPlayerId = "alice", cardId = "si_004", slotKind = "UNIT",
                slotIndex = count - 2, occupiedSlots = 1, health = 2, attack = 4, temporaryAttackModifier = 1, temporaryAttackModifierExpiresOnTurn = 3 };
            var state = new MatchStateDto { turn = 3, players = new[] { new PlayerStateDto { playerId = "alice", unitSlots = slots,
                battlefield = new[] { target, goat } }, new PlayerStateDto { playerId = "bob" } } };
            DemoOnlineGoatProbe.ValidateMovement(state, target.instanceId, goat.instanceId);
            slots[count - 3] = target.instanceId;
            Assert.Throws<InvalidOperationException>(() => DemoOnlineGoatProbe.ValidateMovement(state, target.instanceId, goat.instanceId));
            slots[count - 3] = null; target.slotIndex = count - 3;
            Assert.Throws<InvalidOperationException>(() => DemoOnlineGoatProbe.ValidateMovement(state, target.instanceId, goat.instanceId));
            target.slotIndex = count - 1; target.ownerPlayerId = "bob";
            Assert.Throws<InvalidOperationException>(() => DemoOnlineGoatProbe.ValidateMovement(state, target.instanceId, goat.instanceId));
            target.ownerPlayerId = "alice"; goat.attack = 3;
            Assert.Throws<InvalidOperationException>(() => DemoOnlineGoatProbe.ValidateMovement(state, target.instanceId, goat.instanceId));
            goat.attack = 4; goat.temporaryAttackModifierExpiresOnTurn = 2;
            Assert.Throws<InvalidOperationException>(() => DemoOnlineGoatProbe.ValidateMovement(state, target.instanceId, goat.instanceId));
            goat.temporaryAttackModifierExpiresOnTurn = 3; target.health = 3;
            Assert.Throws<InvalidOperationException>(() => DemoOnlineGoatProbe.ValidateMovement(state, target.instanceId, goat.instanceId));
        }
    }
}
