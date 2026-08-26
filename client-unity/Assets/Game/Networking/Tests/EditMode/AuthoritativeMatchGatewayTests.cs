using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using NUnit.Framework;

namespace BiomeRivals.Networking.Tests
{
    public sealed class AuthoritativeMatchGatewayTests
    {
        [Test]
        public void SnapshotOpcodePublishesAuthoritativeState()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchStateDto received = null;
                gateway.SnapshotReceived += snapshot => received = snapshot;
                transport.Emit(MatchOpcodes.Snapshot,
                    "{\"matchId\":\"match-1\",\"viewerPlayerId\":\"alice\",\"protocolVersion\":4,\"rulesetVersion\":\"prototype-0.4\",\"revision\":0," +
                    "\"lastEventId\":0,\"status\":\"ACTIVE\",\"turn\":1,\"phase\":\"MAIN\",\"activePlayerIndex\":0,\"nextInstanceId\":1," +
                    "\"players\":[{\"playerId\":\"alice\",\"life\":30,\"armor\":0,\"redstone\":6," +
                    "\"redstoneCapacity\":6,\"hand\":[\"pf_001\"],\"deckCount\":26,\"discardPile\":[],\"fatigueCount\":0,\"unitSlots\":[null,null,null,null]," +
                    "\"buildingSlots\":[null,null,null],\"battlefield\":[]},{\"playerId\":\"bob\",\"life\":30,\"armor\":0," +
                    "\"redstone\":6,\"redstoneCapacity\":6,\"hand\":[null],\"deckCount\":26,\"discardPile\":[],\"fatigueCount\":0," +
                    "\"unitSlots\":[null,null,null,null],\"buildingSlots\":[null,null,null],\"battlefield\":[]}]," +
                    "\"winnerPlayerId\":null}");

                Assert.That(received, Is.Not.Null);
                Assert.That(received.matchId, Is.EqualTo("match-1"));
                Assert.That(received.players[0].hand[0], Is.EqualTo("pf_001"));
                Assert.That(received.players[0].unitSlots, Has.Length.EqualTo(4));
                Assert.That(received.phase, Is.EqualTo("MAIN"));
                Assert.That(received.viewerPlayerId, Is.EqualTo("alice"));
                Assert.That(received.players[1].hand[0], Is.Empty,
                    "Unity JsonUtility represents a null string array entry as an empty string.");
                Assert.That(received.players[1].deckCount, Is.EqualTo(26));
            }
        }

        [Test]
        public void EventOpcodePublishesRedactedOpponentDraw()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchEventBatchDto received = null;
                gateway.EventBatchReceived += batch => received = batch;
                transport.Emit(MatchOpcodes.EventBatch,
                    "{\"protocolVersion\":4,\"rulesetVersion\":\"prototype-0.4\",\"revision\":1," +
                    "\"acknowledgedCommandId\":\"turn-1\",\"events\":[{\"eventId\":1,\"type\":\"CARD_DRAWN\"," +
                    "\"payload\":{\"playerId\":\"bob\",\"cardId\":null,\"handCount\":5,\"deckCount\":25}}]}");

                Assert.That(received, Is.Not.Null);
                Assert.That(received.events[0].type, Is.EqualTo(MatchEventTypes.CardDrawn));
                Assert.That(received.events[0].payload.cardId, Is.Empty);
                Assert.That(received.events[0].payload.handCount, Is.EqualTo(5));
                Assert.That(received.events[0].payload.deckCount, Is.EqualTo(25));
            }
        }

        [Test]
        public async Task DeployCommandIsSentOnAuthoritativeCommandOpcode()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                var command = MatchCommandFactory.DeployCard("cmd-1", 3, "pf_001", "UNIT", 2);
                await gateway.SendCommandAsync(command);

                Assert.That(transport.LastOpcode, Is.EqualTo(MatchOpcodes.Command));
                Assert.That(transport.LastJson, Does.Contain("\"type\":\"DEPLOY_CARD\""));
                Assert.That(transport.LastJson, Does.Contain("\"payload\":{"));
                Assert.That(transport.LastJson, Does.Contain("\"slotKind\":\"UNIT\""));
                Assert.That(transport.LastJson, Does.Not.Contain("attackerInstanceId"));
            }
        }

        [Test]
        public async Task PlayCardCommandWirePayloadContainsOnlyCardId()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                await gateway.SendCommandAsync(MatchCommandFactory.PlayCard("play-1", 2, "tk_016"));

                Assert.That(transport.LastJson, Does.Contain("\"type\":\"PLAY_CARD\""));
                Assert.That(transport.LastJson, Does.Contain("\"cardId\":\"tk_016\""));
                Assert.That(transport.LastJson, Does.Not.Contain("slotKind"));
                Assert.That(transport.LastJson, Does.Not.Contain("targetType"));
            }
        }

        [Test]
        public async Task AttackCommandWirePayloadContainsNoDeployFields()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                var command = MatchCommandFactory.Attack("cmd-2", 4, "object-1", "UNIT", "object-2");
                await gateway.SendCommandAsync(command);

                Assert.That(transport.LastJson, Does.Contain("\"type\":\"ATTACK\""));
                Assert.That(transport.LastJson, Does.Contain("\"attackerInstanceId\":\"object-1\""));
                Assert.That(transport.LastJson, Does.Contain("\"targetInstanceId\":\"object-2\""));
                Assert.That(transport.LastJson, Does.Not.Contain("cardId"));
                Assert.That(transport.LastJson, Does.Not.Contain("slotIndex"));
            }
        }

        [Test]
        public void HeroAttackWireOmitsUnusedTargetInstance()
        {
            var json = AuthoritativeMatchGateway.SerializeCommand(
                MatchCommandFactory.Attack("cmd-3", 5, "object-1", "HERO"));

            Assert.That(json, Does.Contain("\"targetType\":\"HERO\""));
            Assert.That(json, Does.Not.Contain("targetInstanceId"));
        }

        private sealed class FakeTransport : IMatchTransport
        {
            public event Action<int, string> MessageReceived;
            public event Action<Exception> Faulted;
            public int LastOpcode { get; private set; }
            public string LastJson { get; private set; }

            public Task ConnectAsync() => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public void Dispose() { }

            public Task SendAsync(int opcode, string json)
            {
                LastOpcode = opcode;
                LastJson = json;
                return Task.CompletedTask;
            }

            public void Emit(int opcode, string json) => MessageReceived?.Invoke(opcode, json);
        }
    }
}
