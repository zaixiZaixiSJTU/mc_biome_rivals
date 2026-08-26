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
                    "{\"matchId\":\"match-1\",\"protocolVersion\":1,\"rulesetVersion\":\"prototype-0.1\",\"revision\":0," +
                    "\"lastEventId\":0,\"status\":\"ACTIVE\",\"turn\":1,\"activePlayerIndex\":0," +
                    "\"players\":[{\"playerId\":\"alice\",\"life\":30,\"armor\":0,\"redstone\":6," +
                    "\"redstoneCapacity\":6,\"hand\":[\"pf_001\"],\"unitSlots\":[null,null,null,null]," +
                    "\"buildingSlots\":[null,null,null]},{\"playerId\":\"bob\",\"life\":30,\"armor\":0," +
                    "\"redstone\":6,\"redstoneCapacity\":6,\"hand\":[\"nt_001\"]," +
                    "\"unitSlots\":[null,null,null,null],\"buildingSlots\":[null,null,null]}]," +
                    "\"winnerPlayerId\":null,\"processedCommandIds\":[]}");

                Assert.That(received, Is.Not.Null);
                Assert.That(received.matchId, Is.EqualTo("match-1"));
                Assert.That(received.players[0].hand[0], Is.EqualTo("pf_001"));
                Assert.That(received.players[0].unitSlots, Has.Length.EqualTo(4));
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
            }
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
