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
                    "{\"matchId\":\"match-1\",\"viewerPlayerId\":\"alice\",\"protocolVersion\":18,\"rulesetVersion\":\"prototype-0.22\",\"revision\":0," +
                    "\"lastEventId\":0,\"status\":\"ACTIVE\",\"turn\":1,\"phase\":\"MAIN\",\"activePlayerIndex\":0,\"nextInstanceId\":1," +
                    "\"players\":[{\"playerId\":\"alice\",\"factionId\":\"ocean_river\",\"mulliganCompleted\":true,\"life\":30,\"armor\":0,\"redstone\":6," +
                    "\"redstoneCapacity\":6,\"hand\":[\"pf_001\"],\"deckCount\":26,\"buriedCount\":0,\"excavatedThisTurn\":false,\"discardPile\":[],\"fatigueCount\":0,\"unitSlots\":[null,null,null,null]," +
                    "\"buildingSlots\":[null,null,null],\"battlefield\":[]},{\"playerId\":\"bob\",\"factionId\":\"end\",\"mulliganCompleted\":true,\"life\":30,\"armor\":0," +
                    "\"redstone\":6,\"redstoneCapacity\":6,\"hand\":[null],\"deckCount\":26,\"buriedCount\":0,\"excavatedThisTurn\":false,\"discardPile\":[],\"fatigueCount\":0," +
                    "\"unitSlots\":[null,null,null,null],\"buildingSlots\":[null,null,null],\"battlefield\":[]}]," +
                    "\"pendingChoice\":null,\"winnerPlayerId\":null}");

                Assert.That(received, Is.Not.Null);
                Assert.That(received.matchId, Is.EqualTo("match-1"));
                Assert.That(received.players[0].hand[0], Is.EqualTo("pf_001"));
                Assert.That(received.players[0].unitSlots, Has.Length.EqualTo(4));
                Assert.That(received.phase, Is.EqualTo("MAIN"));
                Assert.That(received.viewerPlayerId, Is.EqualTo("alice"));
                Assert.That(received.players[0].factionId, Is.EqualTo(FactionIds.OceanRiver));
                Assert.That(received.players[1].factionId, Is.EqualTo(FactionIds.End));
                Assert.That(received.players[1].hand[0], Is.Empty,
                    "Unity JsonUtility represents a null string array entry as an empty string.");
                Assert.That(received.players[1].deckCount, Is.EqualTo(26));
                Assert.That(received.pendingChoice, Is.Null,
                    "Unity JsonUtility requires explicit wire-null normalization for serializable reference fields.");
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
                    "{\"protocolVersion\":18,\"rulesetVersion\":\"prototype-0.22\",\"revision\":1," +
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
                var command = MatchCommandFactory.DeployCard(
                    "cmd-1", 3, "si_003", "UNIT", 2, "REDSTONE", "UNIT", "object-7");
                await gateway.SendCommandAsync(command);

                Assert.That(transport.LastOpcode, Is.EqualTo(MatchOpcodes.Command));
                Assert.That(transport.LastJson, Does.Contain("\"type\":\"DEPLOY_CARD\""));
                Assert.That(transport.LastJson, Does.Contain("\"paymentMethod\":\"REDSTONE\""));
                Assert.That(transport.LastJson, Does.Contain("\"payload\":{"));
                Assert.That(transport.LastJson, Does.Contain("\"slotKind\":\"UNIT\""));
                Assert.That(transport.LastJson, Does.Contain("\"targetType\":\"UNIT\""));
                Assert.That(transport.LastJson, Does.Contain("\"targetInstanceId\":\"object-7\""));
                Assert.That(transport.LastJson, Does.Not.Contain("attackerInstanceId"));
            }
        }

        [Test]
        public void MulliganWirePayloadUsesStableOpeningHandIndices()
        {
            var json = AuthoritativeMatchGateway.SerializeCommand(
                MatchCommandFactory.Mulligan("mulligan-1", 0, new[] { 0, 2 }));

            Assert.That(json, Does.Contain("\"type\":\"MULLIGAN\""));
            Assert.That(json, Does.Contain("\"cardIndices\":[0,2]"));
            Assert.That(json, Does.Not.Contain("cardId"));
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
        public void ResolveChoiceWirePayloadContainsOnlyTheStableChoiceAndOption()
        {
            var json = AuthoritativeMatchGateway.SerializeCommand(
                MatchCommandFactory.ResolveChoice("choice-command-1", 4, "choice-7", 1));

            Assert.That(json, Does.Contain("\"type\":\"RESOLVE_CHOICE\""));
            Assert.That(json, Does.Contain("\"choiceId\":\"choice-7\""));
            Assert.That(json, Does.Contain("\"selectedOptionIndex\":1"));
            Assert.That(json, Does.Not.Contain("cardId"));
        }

        [Test]
        public void TargetedPlayCardWireContainsStableTargetInstance()
        {
            var json = AuthoritativeMatchGateway.SerializeCommand(
                MatchCommandFactory.PlayCard("play-targeted", 2, "si_001", "UNIT", "object-7"));

            Assert.That(json, Does.Contain("\"cardId\":\"si_001\""));
            Assert.That(json, Does.Contain("\"targetType\":\"UNIT\""));
            Assert.That(json, Does.Contain("\"targetInstanceId\":\"object-7\""));
            Assert.That(json, Does.Not.Contain("slotKind"));
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

        [Test]
        public void ConnectionLifecycleIsForwardedWithoutSdkTypes()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchConnectionStatus received = default;
                gateway.ConnectionStateChanged += status => received = status;
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-7"));

                Assert.That(received.Phase, Is.EqualTo(MatchConnectionPhase.Ready));
                Assert.That(received.MatchId, Is.EqualTo("match-7"));
                Assert.That(received.CanSendCommands, Is.True);
                Assert.That(gateway.CurrentStatus.MatchId, Is.EqualTo("match-7"));
            }
        }

        [Test]
        public async Task CommandDispatcherWaitsForAuthoritativeAcknowledgement()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            using (var dispatcher = new MatchCommandDispatcher(gateway))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var pending = dispatcher.SendAndWaitAsync(
                    MatchCommandFactory.EndTurn("ack-1", 4),
                    TimeSpan.FromSeconds(1));
                Assert.That(dispatcher.PendingCount, Is.EqualTo(1));
                transport.Emit(MatchOpcodes.EventBatch,
                    "{\"protocolVersion\":18,\"rulesetVersion\":\"prototype-0.22\",\"revision\":5," +
                    "\"acknowledgedCommandId\":\"ack-1\",\"events\":[]}");

                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.Accepted));
                Assert.That(result.Revision, Is.EqualTo(5));
                Assert.That(dispatcher.PendingCount, Is.Zero);
            }
        }

        [Test]
        public async Task CommandDispatcherSurfacesServerRejection()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            using (var dispatcher = new MatchCommandDispatcher(gateway))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var pending = dispatcher.SendAndWaitAsync(
                    MatchCommandFactory.EndTurn("reject-1", 7),
                    TimeSpan.FromSeconds(1));
                transport.Emit(MatchOpcodes.Rejection,
                    "{\"commandId\":\"reject-1\",\"code\":\"REVISION_MISMATCH\",\"message\":\"stale\",\"revision\":8}");

                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.Rejected));
                Assert.That(result.Code, Is.EqualTo("REVISION_MISMATCH"));
                Assert.That(result.Revision, Is.EqualTo(8));
            }
        }

        [Test]
        public async Task CommandDispatcherRefusesCommandsBeforeMatchReady()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            using (var dispatcher = new MatchCommandDispatcher(gateway))
            {
                var result = await dispatcher.SendAndWaitAsync(
                    MatchCommandFactory.EndTurn("offline-1", 0),
                    TimeSpan.FromSeconds(1));

                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.TransportFailed));
                Assert.That(result.Code, Is.EqualTo("NOT_CONNECTED"));
                Assert.That(transport.LastOpcode, Is.Zero);
            }
        }

        [Test]
        public async Task CommandDispatcherFailsPendingCommandWhenConnectionStops()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            using (var dispatcher = new MatchCommandDispatcher(gateway))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var pending = dispatcher.SendAndWaitAsync(
                    MatchCommandFactory.EndTurn("disconnect-1", 2),
                    TimeSpan.FromSeconds(5));
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Disconnecting));

                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.TransportFailed));
                Assert.That(result.Code, Is.EqualTo("TRANSPORT_FAULT"));
                Assert.That(dispatcher.PendingCount, Is.Zero);
            }
        }

        private sealed class FakeTransport : IMatchTransport
        {
            public event Action<int, string> MessageReceived;
            public event Action<Exception> Faulted;
            public event Action<MatchConnectionStatus> ConnectionStateChanged;
            public int LastOpcode { get; private set; }
            public string LastJson { get; private set; }
            public MatchConnectionStatus CurrentStatus { get; private set; } =
                new MatchConnectionStatus(MatchConnectionPhase.Offline);

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

            public void EmitStatus(MatchConnectionStatus status)
            {
                CurrentStatus = status;
                ConnectionStateChanged?.Invoke(status);
            }
        }
    }
}
