using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using NUnit.Framework;

namespace BiomeRivals.Demo.Tests
{
    public sealed class PlayerOperationsTests
    {
        [TestCase(MatchCommandTypes.Mulligan)]
        [TestCase(MatchCommandTypes.DeployCard)]
        [TestCase(MatchCommandTypes.PlayCard)]
        [TestCase(MatchCommandTypes.ResolveChoice)]
        [TestCase(MatchCommandTypes.EnterCombat)]
        [TestCase(MatchCommandTypes.Attack)]
        [TestCase(MatchCommandTypes.EndTurn)]
        [TestCase(MatchCommandTypes.Concede)]
        public async Task AllPlayerOperationsUseTheExistingAcknowledgedGateway(string type)
        {
            var gateway = new Gateway();
            using (var session = new DemoOnlineMatchSession(gateway, Store()))
            {
                IPlayerOperations api = session;
                var request = PlayerActionRequest.Create(api.Observe(), type,
                    new MatchCommandPayloadDto { cardId = "pf_001", handCardInstanceId = "hand-1",
                        slotKind = "UNIT", slotIndex = 3, paymentMethod = MatchPaymentMethods.Crafting,
                        targetType = "UNIT", targetInstanceId = "object-7", targetInstanceIds = new[] { "object-7", "object-8" },
                        attackerInstanceId = MatchAttackerIds.Hero, cardIndices = new[] { 1, 2 },
                        choiceId = "choice-1", selectedOptionIndex = 1 });
                var task = api.ExecuteAsync(request);
                Assert.That(session.HasPendingCommand, Is.True);
                Assert.That(gateway.Last.type, Is.EqualTo(type));
                Assert.That(gateway.Last.protocolVersion, Is.EqualTo(GameVersions.Protocol));
                Assert.That(gateway.Last.expectedRevision, Is.EqualTo(0));
                Assert.That(gateway.Last.payload.handCardInstanceId, Is.EqualTo("hand-1"));
                Assert.That(gateway.Last.payload.paymentMethod, Is.EqualTo(MatchPaymentMethods.Crafting));
                Assert.That(gateway.Last.payload.targetInstanceIds, Is.EqualTo(new[] { "object-7", "object-8" }));
                Assert.That(gateway.Last.payload.selectedOptionIndex, Is.EqualTo(1));
                request.payload.targetInstanceIds[0] = "caller-mutated";
                request.payload.cardIndices[0] = 5;
                Assert.That(gateway.Last.payload.targetInstanceIds[0], Is.EqualTo("object-7"));
                Assert.That(gateway.Last.payload.cardIndices[0], Is.EqualTo(1));
                gateway.Accept();
                Assert.That((await task).Outcome, Is.EqualTo(MatchCommandOutcome.Accepted));
                Assert.That(session.HasPendingCommand, Is.False);
            }
        }

        [TestCase("STALE_OBSERVATION")]
        [TestCase("MATCH_MISMATCH")]
        [TestCase("UNKNOWN_ACTION")]
        [TestCase("INVALID_PAYLOAD")]
        public async Task InvalidAgentRequestsNeverReachTheTransport(string code)
        {
            var gateway = new Gateway();
            using (var session = new DemoOnlineMatchSession(gateway, Store()))
            {
                var request = PlayerActionRequest.Create(session.Observe(), MatchCommandTypes.EndTurn);
                if (code == "STALE_OBSERVATION") request.expectedRevision--;
                if (code == "MATCH_MISMATCH") request.matchId = "previous-match";
                if (code == "UNKNOWN_ACTION") request.type = "SET_HERO_LIFE";
                if (code == "INVALID_PAYLOAD") request.payload = null;
                var result = await session.ExecuteAsync(request);
                Assert.That(result.Code, Is.EqualTo(code));
                Assert.That(gateway.Last, Is.Null);
            }
        }

        [Test]
        public async Task PendingAndDisconnectedSessionsDoNotSendAnotherAction()
        {
            var gateway = new Gateway();
            using (var session = new DemoOnlineMatchSession(gateway, Store()))
            {
                var action = PlayerActionRequest.Create(session.Observe(), MatchCommandTypes.EndTurn);
                var pending = session.ExecuteAsync(action);
                Assert.That((await session.ExecuteAsync(action)).Code, Is.EqualTo("NOT_READY"));
                gateway.Accept();
                await pending;
                gateway.CurrentStatus = new MatchConnectionStatus(MatchConnectionPhase.Reconnecting);
                Assert.That((await session.ExecuteAsync(action)).Code, Is.EqualTo("NOT_READY"));
                Assert.That(session.Observe().CanIssueCommand, Is.False);
            }
        }

        [Test]
        public void ObservationsAreDetachedAndHideOpponentCardsAndPrivateChoice()
        {
            var snapshot = Store().Current;
            snapshot.players[1].hand = new[] { "secret-card" };
            snapshot.players[1].handCards = new[] { new HandCardStateDto { cardId = "secret-card", handCardInstanceId = "secret-id" } };
            snapshot.pendingChoice = new PendingChoiceDto { playerId = "bob", options = new[] {
                new PendingChoiceOptionDto { optionIndex = 0, cardId = "secret-top", selectable = true, slotIndex = 2 } } };
            var observation = PlayerObservation.FromSnapshot(snapshot, true);
            Assert.That(observation.StateJson, Does.Not.Contain("secret"));
            var state = observation.ReadState();
            Assert.That(state.players[1].hand.Length, Is.EqualTo(1));
            Assert.That(state.players[1].handCards[0], Is.Null);
            Assert.That(state.pendingChoice.options[0].selectable, Is.False);
            state.players[0].life = 0;
            state.players[0].handCards[0].cardId = "tampered";
            Assert.That(snapshot.players[0].life, Is.EqualTo(30));
            Assert.That(observation.ReadState().players[0].handCards[0].cardId, Is.EqualTo("pf_001"));
        }

        [Test]
        public void AbsentNullableFieldsStayNullAcrossObservationRoundTrips()
        {
            var observation = PlayerObservation.FromSnapshot(Store().Current, true);
            Assert.That(observation.ReadState().pendingChoice, Is.Null);
            Assert.That(observation.ReadState().players[0].equipment, Is.Null);
            Assert.That(observation.ReadState().players[1].equipment, Is.Null);
        }

        [Test]
        public void ObservationsRejectAMissingViewerRatherThanExposeBothHands()
        {
            var snapshot = Store().Current;
            snapshot.viewerPlayerId = "not-a-participant";
            Assert.Throws<InvalidOperationException>(() => PlayerObservation.FromSnapshot(snapshot, true));
        }

        [Test]
        public async Task RunnerWaitsForAcknowledgementAndDoesNotRetryARejectedRevision()
        {
            var gateway = new Gateway();
            var store = Store();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            using (var runner = new MatchAgentRunner(session, new EndTurnPolicy()))
            {
                var pending = runner.TickAsync();
                Assert.That(await runner.TickAsync(), Is.Null);
                gateway.Reject();
                Assert.That((await pending).Value.Outcome, Is.EqualTo(MatchCommandOutcome.Rejected));
                Assert.That(await runner.TickAsync(), Is.Null);
                var next = PlayerObservation.FromSnapshot(store.Current, true).ReadState();
                next.revision = 1;
                store.Replace(next);
                pending = runner.TickAsync();
                Assert.That(gateway.Last.expectedRevision, Is.EqualTo(1));
                gateway.Accept();
                Assert.That((await pending).Value.Outcome, Is.EqualTo(MatchCommandOutcome.Accepted));
                runner.Dispose();
                Assert.That(await runner.TickAsync(), Is.Null);
            }
        }

        [Test]
        public void BasicAgentDeploysRegisteredCardsThenWaitsOnTheOtherPlayersTurn()
        {
            var registry = BiomeRivals.Content.CardContentLoader.Load();
            var policy = new BasicMatchAgentPolicy(registry);
            var snapshot = Store().Current;
            var decision = policy.Decide(PlayerObservation.FromSnapshot(snapshot, true));
            Assert.That(decision.type, Is.EqualTo(MatchCommandTypes.DeployCard));
            Assert.That(decision.payload.handCardInstanceId, Is.EqualTo("hand-1"));
            snapshot.activePlayerIndex = 1;
            Assert.That(policy.Decide(PlayerObservation.FromSnapshot(snapshot, true)), Is.Null);
        }

        private static MatchStateStore Store()
        {
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto { matchId = "agent-match", viewerPlayerId = "alice",
                protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                status = "ACTIVE", phase = "MAIN", turn = 1, players = new[] {
                    new PlayerStateDto { playerId = "alice", life = 30, redstone = 1, totalRedstone = 1, redstoneCapacity = 1,
                        hand = new[] { "pf_001" }, handCards = new[] { new HandCardStateDto {
                            cardId = "pf_001", handCardInstanceId = "hand-1" } } },
                    new PlayerStateDto { playerId = "bob", life = 30 } } });
            return store;
        }
        private sealed class EndTurnPolicy : IMatchAgentPolicy
        {
            public PlayerActionRequest Decide(PlayerObservation observation) =>
                PlayerActionRequest.Create(observation, MatchCommandTypes.EndTurn);
        }
        private sealed class Gateway : IMatchGateway
        {
            public event Action<MatchEventBatchDto> EventBatchReceived;
            public event Action<MatchStateDto> SnapshotReceived;
            public event Action<CommandRejectionDto> CommandRejected;
            public event Action<Exception> Faulted;
            public event Action<MatchConnectionStatus> ConnectionStateChanged;
            public MatchConnectionStatus CurrentStatus { get; set; } =
                new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "agent-match");
            public MatchCommandDto Last;
            public Task SendCommandAsync(MatchCommandDto command) { Last = command; return Task.CompletedTask; }
            public void Accept() => EventBatchReceived?.Invoke(new MatchEventBatchDto {
                acknowledgedCommandId = Last.commandId, revision = Last.expectedRevision + 1 });
            public void Reject() => CommandRejected?.Invoke(new CommandRejectionDto {
                commandId = Last.commandId, code = "WRONG_PHASE", revision = Last.expectedRevision });
            public Task ConnectAsync() => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }
}
