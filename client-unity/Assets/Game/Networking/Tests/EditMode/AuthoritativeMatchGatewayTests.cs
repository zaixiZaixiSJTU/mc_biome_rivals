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
                    "{\"matchId\":\"match-1\",\"viewerPlayerId\":\"alice\",\"protocolVersion\":2,\"rulesetVersion\":\"prototype-0.2\",\"revision\":0," +
                    "\"lastEventId\":0,\"status\":\"ACTIVE\",\"turn\":1,\"phase\":\"MAIN\",\"activePlayerIndex\":0,\"nextInstanceId\":1," +
                    "\"players\":[{\"playerId\":\"alice\",\"life\":30,\"armor\":0,\"redstone\":6," +
                    "\"redstoneCapacity\":6,\"hand\":[\"pf_001\"],\"unitSlots\":[null,null,null,null]," +
                    "\"buildingSlots\":[null,null,null],\"battlefield\":[]},{\"playerId\":\"bob\",\"life\":30,\"armor\":0," +
                    "\"redstone\":6,\"redstoneCapacity\":6,\"hand\":[null]," +
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
