using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BiomeRivals.Core;
using NUnit.Framework;

namespace BiomeRivals.Networking.Tests
{
    public sealed class AuthoritativeMatchGatewayTests
    {
        [Test]
        public void NetworkSnapshotPreservesPrivateHandAndOptionalWireNullsInStateStore()
        {
            var transport = new FakeTransport();
            var store = new MatchStateStore();
            Exception fault = null;
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                gateway.SnapshotReceived += store.Replace;
                gateway.Faulted += exception => fault = exception;
                transport.Emit(MatchOpcodes.Snapshot, CreatePrivateSnapshotWire());
                Assert.That(fault, Is.Null, "Nakama's wire-null hand placeholders must survive JSON decoding.");
                Assert.That(store.Current, Is.Not.Null);
                Assert.That(store.Current.players[1].hand, Is.EqualTo(new string[] { null, null }));
                Assert.That(store.Current.players[1].handCards, Is.EqualTo(new HandCardStateDto[] { null, null }));
                Assert.That(store.Current.players[0].handCards[0].handCardInstanceId, Is.EqualTo("hand-1"));
                Assert.That(store.Current.players[0].handCards[0].expiresAtEndOfTurnPlayerId, Is.Null);
                Assert.That(store.Current.players[0].equipment, Is.Null);
                Assert.That(store.Current.players[1].equipment, Is.Null);
                Assert.That(store.Current.pendingChoice, Is.Null);
                Assert.That(store.Current.winnerPlayerId, Is.Null);
            }
        }

        [Test]
        public void NetworkSnapshotDoesNotSilentlyEraseLeakedOpponentHandInstances()
        {
            var transport = new FakeTransport();
            var store = new MatchStateStore();
            Exception fault = null;
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                gateway.SnapshotReceived += store.Replace;
                gateway.Faulted += exception => fault = exception;
                var leakedWire = CreatePrivateSnapshotWire().Replace("\"handCards\":[null,null]",
                    "\"handCards\":[{\"handCardInstanceId\":\"hand-2\",\"cardId\":\"db_001\",\"costModifier\":0},null]");
                transport.Emit(MatchOpcodes.Snapshot, leakedWire);
                Assert.That(fault, Is.TypeOf<InvalidOperationException>());
                Assert.That(fault.Message, Does.Contain("invalid hand card instances"));
                Assert.That(store.Current, Is.Null, "Reject privacy-invalid snapshots; do not sanitize them into accepted ones.");
            }
        }

        [Test]
        public void NetworkEventBatchPreservesOptionalReferenceAndRedactedStringNulls()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchEventBatchDto received = null;
                gateway.EventBatchReceived += batch => received = batch;
                transport.Emit(MatchOpcodes.EventBatch, "{\"protocolVersion\":" + GameVersions.Protocol +
                    ",\"rulesetVersion\":\"" + GameVersions.Ruleset + "\",\"revision\":1,\"handProjection\":null," +
                    "\"events\":[{\"eventId\":1,\"type\":\"MATCH_ENDED\",\"payload\":{\"winnerPlayerId\":null}}]}");
                Assert.That(received, Is.Not.Null);
                Assert.That(received.handProjection, Is.Null);
                Assert.That(received.events[0].payload.winnerPlayerId, Is.Null);
            }
        }

        [TestCase(31, false)]
        [TestCase(32, true)]
        public void NetworkDecoderBoundsNestingBeforeCallingTheSdkParser(int arrayDepth, bool shouldFail)
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchStateDto received = null;
                Exception fault = null;
                gateway.SnapshotReceived += snapshot => received = snapshot;
                gateway.Faulted += exception => fault = exception;
                var wire = "{\"protocolVersion\":" + GameVersions.Protocol + ",\"rulesetVersion\":\"" +
                    GameVersions.Ruleset + "\",\"ignored\":" + new string('[', arrayDepth) +
                    "null" + new string(']', arrayDepth) + "}";
                transport.Emit(MatchOpcodes.Snapshot, wire);
                Assert.That(fault != null, Is.EqualTo(shouldFail));
                Assert.That(received == null, Is.EqualTo(shouldFail));
                if (shouldFail) Assert.That(fault.Message, Does.Contain("nesting limit"));
            }
        }

        [TestCase("{]")]
        [TestCase("{\"broken\":\"unfinished}")]
        public void NetworkDecoderRejectsIncompleteOrMismatchedJson(string wire)
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                Exception fault = null;
                MatchStateDto received = null;
                gateway.Faulted += exception => fault = exception;
                gateway.SnapshotReceived += snapshot => received = snapshot;
                transport.Emit(MatchOpcodes.Snapshot, wire);
                Assert.That(fault, Is.TypeOf<FormatException>());
                Assert.That(received, Is.Null);
            }
        }

        [Test]
        public void NetworkDecoderRejectsOversizedMessagesBeforeParsing()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                Exception fault = null;
                gateway.Faulted += exception => fault = exception;
                transport.Emit(MatchOpcodes.Snapshot, "{\"ignored\":\"" + new string('x', 262144) + "\"}");
                Assert.That(fault, Is.TypeOf<FormatException>());
                Assert.That(fault.Message, Does.Contain("message size limit"));
            }
        }

        [Test]
        public void NetworkDecoderDoesNotConfuseQuotedBracketsWithNestingOrEraseRealChoices()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                Exception fault = null;
                MatchStateDto received = null;
                gateway.Faulted += exception => fault = exception;
                gateway.SnapshotReceived += snapshot => received = snapshot;
                var wire = CreatePrivateSnapshotWire().Replace("\"pendingChoice\":null",
                    "\"pendingChoice\":{\"choiceId\":\"choice-1\",\"kind\":\"MOVE\",\"options\":[{\"optionIndex\":0,\"selectable\":true}]}")
                    .Replace("\"matchId\":\"wire-match\"", "\"ignored\":\"" + new string('[', 64) +
                    "\\\"pendingChoice\\\":null\\\\tail\",\"matchId\":\"wire-match\"");
                transport.Emit(MatchOpcodes.Snapshot, wire);
                Assert.That(fault, Is.Null);
                Assert.That(received.pendingChoice.choiceId, Is.EqualTo("choice-1"));
                Assert.That(received.pendingChoice.options[0].selectable, Is.True);
            }
        }

        private static string CreatePrivateSnapshotWire() =>
            "{\"matchId\":\"wire-match\",\"viewerPlayerId\":\"alice\",\"arenaId\":\"standard_meadow\",\"protocolVersion\":" +
            GameVersions.Protocol + ",\"rulesetVersion\":\"" + GameVersions.Ruleset + "\",\"status\":\"ACTIVE\"," +
            "\"turn\":1,\"phase\":\"MAIN\",\"activePlayerIndex\":0,\"nextInstanceId\":1,\"pendingChoice\":null,\"winnerPlayerId\":null," +
            "\"players\":[{\"playerId\":\"alice\",\"factionId\":\"plains_forest\",\"life\":30,\"redstone\":1," +
            "\"redstoneCapacity\":1,\"totalRedstone\":1,\"hand\":[\"pf_001\"],\"handCards\":[{\"handCardInstanceId\":\"hand-1\"," +
            "\"cardId\":\"pf_001\",\"costModifier\":0,\"expiresAtEndOfTurnPlayerId\":null}],\"equipment\":null," +
            "\"unitSlots\":[null,null,null,null],\"buildingSlots\":[null,null,null],\"battlefield\":[]}," +
            "{\"playerId\":\"bob\",\"factionId\":\"desert_badlands\",\"life\":30,\"redstone\":1,\"redstoneCapacity\":1," +
            "\"totalRedstone\":1,\"hand\":[null,null],\"handCards\":[null,null],\"equipment\":null," +
            "\"unitSlots\":[null,null,null,null],\"buildingSlots\":[null,null,null],\"battlefield\":[]}]}";

        [Test]
        public void SnapshotOpcodePublishesAuthoritativeState()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchStateDto received = null;
                gateway.SnapshotReceived += snapshot => received = snapshot;
                transport.Emit(MatchOpcodes.Snapshot,
                    "{\"matchId\":\"match-1\",\"viewerPlayerId\":\"alice\",\"arenaId\":\"standard_meadow\",\"protocolVersion\":40,\"rulesetVersion\":\"prototype-0.65\",\"revision\":0," +
                    "\"lastEventId\":0,\"status\":\"ACTIVE\",\"turn\":1,\"phase\":\"MAIN\",\"activePlayerIndex\":0,\"nextInstanceId\":1," +
                    "\"players\":[{\"playerId\":\"alice\",\"factionId\":\"ocean_river\",\"mulliganCompleted\":true,\"life\":30,\"armor\":0,\"redstone\":6," +
                    "\"temporaryRedstone\":0,\"totalRedstone\":6,\"redstoneCapacity\":6,\"hand\":[\"pf_001\"],\"deckCount\":26,\"buriedCount\":0,\"excavatedThisTurn\":false,\"discardPile\":[],\"fatigueCount\":0,\"unitSlots\":[null,null,null,null]," +
                    "\"buildingSlots\":[null,null,null],\"battlefield\":[]},{\"playerId\":\"bob\",\"factionId\":\"end\",\"mulliganCompleted\":true,\"life\":30,\"armor\":0," +
                    "\"redstone\":6,\"temporaryRedstone\":0,\"totalRedstone\":6,\"redstoneCapacity\":6,\"hand\":[null],\"deckCount\":26,\"buriedCount\":0,\"excavatedThisTurn\":false,\"discardPile\":[],\"fatigueCount\":0," +
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
                Assert.That(received.players[1].hand[0], Is.Null);
                Assert.That(received.players[1].deckCount, Is.EqualTo(26));
                Assert.That(received.pendingChoice, Is.Null);
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
                    "{\"protocolVersion\":40,\"rulesetVersion\":\"prototype-0.65\",\"arenaId\":\"standard_meadow\",\"revision\":1," +
                    "\"acknowledgedCommandId\":\"turn-1\",\"events\":[{\"eventId\":1,\"type\":\"CARD_DRAWN\"," +
                    "\"payload\":{\"playerId\":\"bob\",\"cardId\":null,\"handCount\":5,\"deckCount\":25}}]}");

                Assert.That(received, Is.Not.Null);
                Assert.That(received.events[0].type, Is.EqualTo(MatchEventTypes.CardDrawn));
                Assert.That(received.events[0].payload.cardId, Is.Null);
                Assert.That(received.events[0].payload.handCount, Is.EqualTo(5));
                Assert.That(received.events[0].payload.deckCount, Is.EqualTo(25));
            }
        }

        [Test]
        public void IncompatibleEventRulesetFailsAndLocksTheGateway()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchEventBatchDto received = null;
                Exception fault = null;
                gateway.EventBatchReceived += batch => received = batch;
                gateway.Faulted += exception => fault = exception;
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));

                transport.Emit(MatchOpcodes.EventBatch,
                    "{\"protocolVersion\":32,\"rulesetVersion\":\"future-rules\",\"revision\":1," +
                    "\"acknowledgedCommandId\":\"turn-1\",\"events\":[]}");

                Assert.That(received, Is.Null);
                Assert.That(fault, Is.TypeOf<InvalidOperationException>());
                Assert.That(gateway.CurrentStatus.Phase, Is.EqualTo(MatchConnectionPhase.Failed));
                Assert.That(gateway.CurrentStatus.MatchId, Is.EqualTo("match-1"));
                Assert.That(gateway.CurrentStatus.CanSendCommands, Is.False);

                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "reconnected", "match-1", 1));
                Assert.That(gateway.CurrentStatus.Phase, Is.EqualTo(MatchConnectionPhase.Failed),
                    "A transport reconnect cannot make an incompatible client safe to use.");
            }
        }

        [Test]
        public void IncompatibleSnapshotProtocolFailsInsteadOfPublishingState()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchStateDto received = null;
                gateway.SnapshotReceived += snapshot => received = snapshot;
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));

                transport.Emit(MatchOpcodes.Snapshot,
                    "{\"matchId\":\"match-1\",\"protocolVersion\":999,\"rulesetVersion\":\"prototype-0.61\"}");

                Assert.That(received, Is.Null);
                Assert.That(gateway.CurrentStatus.Phase, Is.EqualTo(MatchConnectionPhase.Failed));
            }
        }

        [Test]
        public async Task DeployCommandIsSentOnAuthoritativeCommandOpcode()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                var command = MatchCommandFactory.DeployCard(
                    "cmd-1", 3, "si_003", "UNIT", 2, "hand-7", "REDSTONE", "UNIT", "object-7");
                await gateway.SendCommandAsync(command);

                Assert.That(transport.LastOpcode, Is.EqualTo(MatchOpcodes.Command));
                Assert.That(transport.LastJson, Does.Contain("\"type\":\"DEPLOY_CARD\""));
                Assert.That(transport.LastJson, Does.Contain("\"paymentMethod\":\"REDSTONE\""));
                Assert.That(transport.LastJson, Does.Contain("\"payload\":{"));
                Assert.That(transport.LastJson, Does.Contain("\"slotKind\":\"UNIT\""));
                Assert.That(transport.LastJson, Does.Contain("\"handCardInstanceId\":\"hand-7\""));
                Assert.That(transport.LastJson, Does.Contain("\"targetType\":\"UNIT\""));
                Assert.That(transport.LastJson, Does.Contain("\"targetInstanceId\":\"object-7\""));
                Assert.That(transport.LastJson, Does.Not.Contain("attackerInstanceId"));
            }
        }

        [Test]
        public void UntargetedDeployWireCarriesTheSelectedHandInstance()
        {
            var json = AuthoritativeMatchGateway.SerializeCommand(
                MatchCommandFactory.DeployCard(
                    "deploy-untargeted", 4, "pf_001", "UNIT", 0, "hand-44"));

            Assert.That(json, Does.Contain("\"cardId\":\"pf_001\""));
            Assert.That(json, Does.Contain("\"handCardInstanceId\":\"hand-44\""));
            Assert.That(json, Does.Not.Contain("targetType"));
        }

        [Test]
        public void CommandWithIncompatibleRulesetIsRejectedBeforeTransport()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                var command = MatchCommandFactory.EndTurn("future-command", 1);
                command.rulesetVersion = "future-rules";

                var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await gateway.SendCommandAsync(command));

                Assert.That(exception.Message, Does.Contain("ruleset version"));
                Assert.That(transport.LastOpcode, Is.Zero);
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
        public async Task PlayCardCommandWirePayloadContainsCardAndStableHandInstanceId()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                await gateway.SendCommandAsync(MatchCommandFactory.PlayCard("play-1", 2, "tk_016", "hand-16"));

                Assert.That(transport.LastJson, Does.Contain("\"type\":\"PLAY_CARD\""));
                Assert.That(transport.LastJson, Does.Contain("\"cardId\":\"tk_016\""));
                Assert.That(transport.LastJson, Does.Contain("\"handCardInstanceId\":\"hand-16\""));
                Assert.That(transport.LastJson, Does.Not.Contain("slotKind"));
                Assert.That(transport.LastJson, Does.Not.Contain("targetType"));
            }
        }

        [TestCase("hand-invalid")]
        [TestCase("local-hand-3")]
        public void CardCommandSerializationRejectsMissingOrNonAuthoritativeHandIds(string handCardInstanceId)
        {
            var command = MatchCommandFactory.PlayCard(
                "play-invalid-hand-id", 2, "tk_016", handCardInstanceId);

            Assert.Throws<InvalidOperationException>(() => AuthoritativeMatchGateway.SerializeCommand(command));
        }

        [Test]
        public void CardCommandFactoryRejectsMissingHandInstances()
        {
            Assert.Throws<ArgumentException>(() =>
                MatchCommandFactory.DeployCard("deploy-missing-hand", 0, "pf_001", "UNIT", 0, ""));
            Assert.Throws<ArgumentException>(() =>
                MatchCommandFactory.PlayCard("play-missing-hand", 0, "tk_016", ""));
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
                MatchCommandFactory.PlayCard("play-targeted", 2, "si_001", "hand-21", "UNIT", "object-7"));

            Assert.That(json, Does.Contain("\"cardId\":\"si_001\""));
            Assert.That(json, Does.Contain("\"handCardInstanceId\":\"hand-21\""));
            Assert.That(json, Does.Contain("\"targetType\":\"UNIT\""));
            Assert.That(json, Does.Contain("\"targetInstanceId\":\"object-7\""));
            Assert.That(json, Does.Not.Contain("slotKind"));
        }

        [Test]
        public void MultiTargetedPlayCardWireContainsOnlyTheStableTargetArray()
        {
            var json = AuthoritativeMatchGateway.SerializeCommand(
                MatchCommandFactory.PlayCard("play-multi-targeted", 3, "pf_006", "hand-22", "UNIT", "", new[] { "object-2", "object-7" }));

            Assert.That(json, Does.Contain("\"cardId\":\"pf_006\""));
            Assert.That(json, Does.Contain("\"handCardInstanceId\":\"hand-22\""));
            Assert.That(json, Does.Contain("\"targetType\":\"UNIT\""));
            Assert.That(json, Does.Contain("\"targetInstanceIds\":[\"object-2\",\"object-7\"]"));
            Assert.That(json, Does.Not.Contain("\"targetInstanceId\":"));
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
                    "{\"protocolVersion\":40,\"rulesetVersion\":\"prototype-0.65\",\"arenaId\":\"standard_meadow\",\"revision\":5," +
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

        [Test]
        public async Task CommandDispatcherFailsPendingCommandAsSoonAsReconnectStarts()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            using (var dispatcher = new MatchCommandDispatcher(gateway))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var pending = dispatcher.SendAndWaitAsync(
                    MatchCommandFactory.EndTurn("reconnect-1", 2),
                    TimeSpan.FromSeconds(5));
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Reconnecting, "socket closed", "match-1", 1));

                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.TransportFailed));
                Assert.That(result.Code, Is.EqualTo("TRANSPORT_FAULT"));
                Assert.That(dispatcher.PendingCount, Is.Zero);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string HeldSnapshotWire(int revision) =>
            "{\"protocolVersion\":" + GameVersions.Protocol + ",\"rulesetVersion\":\"" + GameVersions.Ruleset + "\",\"revision\":" + revision + "}";

        [Test]
        public async Task InboundHoldLeavesDispatcherPendingUntilBufferedServerAcknowledgementIsDelivered()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            using (var dispatcher = new MatchCommandDispatcher(gateway))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var scope = gateway.HoldIncomingMatchMessages();
                var pending = dispatcher.SendAndWaitAsync(MatchCommandFactory.EndTurn("hold-ack", 4), TimeSpan.FromSeconds(5));
                transport.Emit(MatchOpcodes.EventBatch,
                    "{\"protocolVersion\":" + GameVersions.Protocol + ",\"rulesetVersion\":\"" + GameVersions.Ruleset +
                    "\",\"revision\":5,\"acknowledgedCommandId\":\"hold-ack\",\"events\":[]}");
                Assert.That(dispatcher.PendingCount, Is.EqualTo(1));
                Assert.That(pending.IsCompleted, Is.False);
                Assert.That(gateway.HeldIncomingMessageCount, Is.EqualTo(1));
                scope.Dispose();
                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.Accepted));
                Assert.That(result.Revision, Is.EqualTo(5));
                Assert.That(dispatcher.PendingCount, Is.Zero);
            }
        }

        [Test]
        public void InboundHoldOldDrainFinallyCannotCancelNewScopeAcquiredByFaultCallback()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                IDisposable replacement = null;
                var delivered = 0;
                gateway.SnapshotReceived += snapshot => delivered++;
                gateway.Faulted += error =>
                {
                    replacement = gateway.HoldIncomingMatchMessages();
                    transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(7));
                };
                var scope = gateway.HoldIncomingMatchMessages();
                transport.Emit(5, "null");
                scope.Dispose();
                Assert.That(delivered, Is.Zero);
                Assert.That(gateway.HeldIncomingMessageCount, Is.EqualTo(1));
                replacement.Dispose();
                Assert.That(delivered, Is.EqualTo(1));
            }
        }

        [Test]
        public void InboundHoldCompatibilityFailureDiscardsRemainingFramesAndDisposalDiscardsQueuedFrames()
        {
            var transport = new FakeTransport();
            var gateway = new AuthoritativeMatchGateway(transport);
            transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
            var delivered = 0;
            gateway.SnapshotReceived += snapshot => delivered++;
            var scope = gateway.HoldIncomingMatchMessages();
            transport.Emit(MatchOpcodes.Snapshot, "{\"protocolVersion\":-1,\"rulesetVersion\":\"wrong\"}");
            transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(2));
            scope.Dispose();
            Assert.That(delivered, Is.Zero);
            Assert.That(gateway.HeldIncomingMessageCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => gateway.HoldIncomingMatchMessages());
            gateway.Dispose();
            var other = new AuthoritativeMatchGateway(transport);
            other.SnapshotReceived += snapshot => delivered++;
            var otherScope = other.HoldIncomingMatchMessages();
            transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(3));
            other.Dispose();
            otherScope.Dispose();
            Assert.That(delivered, Is.Zero);
            Assert.That(other.HeldIncomingMessageCount, Is.Zero);
        }

        [Test]
        public async Task InboundHoldPreservesOutgoingCommandsAndReplaysInWireOrderExactlyOnce()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var delivered = new List<string>();
                gateway.SnapshotReceived += snapshot => delivered.Add("snapshot:" + snapshot.revision);
                gateway.CommandRejected += rejection => delivered.Add("rejection:" + rejection.commandId);
                var scope = gateway.HoldIncomingMatchMessages();
                var command = MatchCommandFactory.EndTurn("held-1", 1);
                await gateway.SendCommandAsync(command);
                Assert.That(transport.LastOpcode, Is.EqualTo(MatchOpcodes.Command));
                Assert.That(transport.LastJson, Is.EqualTo(AuthoritativeMatchGateway.SerializeCommand(command)));
                transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(1));
                transport.Emit(MatchOpcodes.Rejection, "{\"commandId\":\"held-1\",\"code\":\"NOT_YOUR_TURN\"}");
                transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(2));
                Assert.That(delivered, Is.Empty);
                Assert.That(gateway.HeldIncomingMessageCount, Is.EqualTo(3));
                scope.Dispose();
                scope.Dispose();
                Assert.That(delivered, Is.EqualTo(new[] { "snapshot:1", "rejection:held-1", "snapshot:2" }));
                Assert.That(gateway.HeldIncomingMessageCount, Is.Zero);
                transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(3));
                Assert.That(delivered.Count, Is.EqualTo(4));
            }
        }

        [Test]
        public void InboundHoldRejectsNestingAndAppendsReentrantFramesBehindBufferedFrames()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var delivered = new List<int>();
                gateway.SnapshotReceived += snapshot =>
                {
                    delivered.Add(snapshot.revision);
                    if (snapshot.revision != 1) return;
                    Assert.Throws<InvalidOperationException>(() => gateway.HoldIncomingMatchMessages());
                    transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(3));
                };
                var scope = gateway.HoldIncomingMatchMessages();
                Assert.Throws<InvalidOperationException>(() => gateway.HoldIncomingMatchMessages());
                transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(1));
                transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(2));
                scope.Dispose();
                Assert.That(delivered, Is.EqualTo(new[] { 1, 2, 3 }));
            }
        }

        [TestCase("fault")]
        [TestCase("disconnect")]
        [TestCase("new-match")]
        public void InboundHoldInvalidationPreventsOldScopeFromReleasingNewMatchFrames(string cause)
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var delivered = new List<int>();
                gateway.SnapshotReceived += snapshot => delivered.Add(snapshot.revision);
                var old = gateway.HoldIncomingMatchMessages();
                transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(1));
                if (cause == "fault") transport.EmitFault(new Exception("socket failed"));
                else transport.EmitStatus(new MatchConnectionStatus(cause == "disconnect" ? MatchConnectionPhase.Reconnecting : MatchConnectionPhase.Ready, "changed", "match-2"));
                Assert.That(gateway.HeldIncomingMessageCount, Is.Zero);
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-2"));
                var current = gateway.HoldIncomingMatchMessages();
                transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(2));
                old.Dispose();
                Assert.That(delivered, Is.Empty);
                Assert.That(gateway.HeldIncomingMessageCount, Is.EqualTo(1));
                current.Dispose();
                Assert.That(delivered, Is.EqualTo(new[] { 2 }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InboundHoldBufferOverflowFaultsAndNeverReplaysPartialQueue(bool oversizedFrame)
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                var faults = 0;
                var delivered = 0;
                gateway.Faulted += error => faults++;
                gateway.SnapshotReceived += snapshot => delivered++;
                var scope = gateway.HoldIncomingMatchMessages();
                if (oversizedFrame) transport.Emit(MatchOpcodes.Snapshot, new string('x', 2 * 1024 * 1024 + 1));
                else for (var index = 0; index < 65; index++) transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(index));
                Assert.That(faults, Is.EqualTo(1));
                Assert.That(gateway.HeldIncomingMessageCount, Is.Zero);
                scope.Dispose();
                Assert.That(delivered, Is.Zero);
            }
        }

        [Test]
        public void InboundHoldFaultDuringReplayStopsRemainingFramesAndDisposedGatewayCannotHold()
        {
            var transport = new FakeTransport();
            var gateway = new AuthoritativeMatchGateway(transport);
            Assert.Throws<InvalidOperationException>(() => gateway.HoldIncomingMatchMessages());
            transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
            var delivered = 0;
            var faults = 0;
            gateway.SnapshotReceived += snapshot => delivered++;
            gateway.Faulted += error => faults++;
            var scope = gateway.HoldIncomingMatchMessages();
            transport.Emit(5, "null");
            transport.Emit(MatchOpcodes.Snapshot, HeldSnapshotWire(2));
            scope.Dispose();
            Assert.That(faults, Is.EqualTo(1));
            Assert.That(delivered, Is.Zero);
            Assert.That(gateway.HeldIncomingMessageCount, Is.Zero);
            gateway.Dispose();
            Assert.Throws<InvalidOperationException>(() => gateway.HoldIncomingMatchMessages());
        }

        [Test]
        public async Task DrawFixtureDiagnosticsSendOnlyWhenReadyAndStopAfterDisposal()
        {
            var transport = new FakeTransport();
            var gateway = new AuthoritativeMatchGateway(transport);
            Assert.Throws<InvalidOperationException>(() => gateway.RequestSimultaneousDefeatFixtureAsync());
            Assert.That(transport.LastOpcode, Is.Zero);
            transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
            await gateway.RequestSimultaneousDefeatFixtureAsync();
            Assert.That(transport.LastOpcode, Is.EqualTo(255));
            Assert.That(transport.LastJson, Is.EqualTo("{\"fixture\":\"simultaneous-defeat\"}"));
            await gateway.RequestSimultaneousDefeatFixtureAsync(true);
            Assert.That(transport.LastJson, Is.EqualTo("{\"fixture\":\"simultaneous-defeat-readable\"}"));
            gateway.Dispose();
            Assert.Throws<InvalidOperationException>(() => gateway.RequestSimultaneousDefeatFixtureAsync());
        }

        [Test]
        public void DrawFixtureDiagnosticsCannotBypassCompatibilityFailure()
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                transport.Emit(MatchOpcodes.EventBatch, "{\"protocolVersion\":-1,\"rulesetVersion\":\"wrong\",\"events\":[]}");
                Assert.Throws<InvalidOperationException>(() => gateway.RequestSimultaneousDefeatFixtureAsync());
                Assert.That(transport.LastOpcode, Is.Zero);
            }
        }

        [Test]
        public void DrawFixtureAckPreservesWinnerlessWireAndUnsubscribesOnDisposal()
        {
            var transport = new FakeTransport();
            var gateway = new AuthoritativeMatchGateway(transport);
            MatchTestFixtureResult received = null;
            Exception fault = null;
            gateway.TestFixtureResultReceived += result => received = result;
            gateway.Faulted += error => fault = error;
            const string wire = "{\"ok\":true,\"revision\":3,\"winnerPlayerId\":null,\"reason\":\"SIMULTANEOUS_DEFEAT\"}";
            transport.Emit(5, wire);
            Assert.That(fault, Is.Null);
            Assert.That(received.revision, Is.EqualTo(3));
            Assert.That(received.winnerPlayerId, Is.Null);
            received = null;
            gateway.Dispose();
            transport.Emit(5, wire);
            Assert.That(received, Is.Null);
        }

        [TestCase("{\"ok\":false,\"revision\":3,\"reason\":\"SIMULTANEOUS_DEFEAT\"}")]
        [TestCase("{\"ok\":true,\"revision\":0,\"reason\":\"SIMULTANEOUS_DEFEAT\"}")]
        [TestCase("{\"ok\":true,\"revision\":3,\"reason\":\"CONCEDED\"}")]
        [TestCase("{\"ok\":true,\"revision\":3,\"reason\":\"SIMULTANEOUS_DEFEAT\",\"winnerPlayerId\":\"winner\"}")]
        [TestCase("null")]
        public void InvalidDrawFixtureAckIsFaultedWithoutPublishingSuccess(string wire)
        {
            var transport = new FakeTransport();
            using (var gateway = new AuthoritativeMatchGateway(transport))
            {
                MatchTestFixtureResult received = null;
                Exception fault = null;
                gateway.TestFixtureResultReceived += result => received = result;
                gateway.Faulted += error => fault = error;
                transport.Emit(5, wire);
                Assert.That(fault, Is.Not.Null);
                Assert.That(received, Is.Null);
            }
        }
#endif

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
            public void EmitFault(Exception error) => Faulted?.Invoke(error);

            public void EmitStatus(MatchConnectionStatus status)
            {
                CurrentStatus = status;
                ConnectionStateChanged?.Invoke(status);
            }
        }
    }
}
