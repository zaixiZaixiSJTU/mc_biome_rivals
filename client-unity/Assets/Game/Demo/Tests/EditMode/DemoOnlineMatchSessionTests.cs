using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using BiomeRivals.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineMatchSessionTests
    {
        [Test]
        public void AuthoritativeViewAlwaysOrientsViewerOnPlayerSide()
        {
            var store = CreateStore(viewerIndex: 1);
            var view = new DemoAuthoritativeMatchView(store);

            Assert.That(view.IsAuthoritative, Is.True);
            Assert.That(view.ViewerIndex, Is.EqualTo(1));
            Assert.That(view.Hand, Is.EqualTo(new[] { "nt_001" }));
            Assert.That(view.PlayerLife, Is.EqualTo(27));
            Assert.That(view.OpponentLife, Is.EqualTo(30));
            Assert.That(view.Energy, Is.EqualTo(2));
            Assert.That(view.IsPlayerTurn, Is.True);
            Assert.That(view.PlayerFactionId, Is.EqualTo(FactionIds.End));
            Assert.That(view.OpponentFactionId, Is.EqualTo(FactionIds.OceanRiver));
        }

        [Test]
        public async Task OnlineSessionWaitsForAuthoritativeDeployAcknowledgement()
        {
            var store = CreateStore(viewerIndex: 0);
            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                var pending = session.DeployAsync("pf_001", DemoSlotKind.Unit, 2);

                Assert.That(session.HasPendingCommand, Is.True);
                Assert.That(gateway.LastCommand, Is.Not.Null);
                Assert.That(gateway.LastCommand.expectedRevision, Is.Zero);
                Assert.That(gateway.LastCommand.payload.slotIndex, Is.EqualTo(2));

                var batch = new MatchEventBatchDto
                {
                    protocolVersion = GameVersions.Protocol,
                    rulesetVersion = GameVersions.Ruleset,
                    revision = 1,
                    acknowledgedCommandId = gateway.LastCommand.commandId,
                    events = new[]
                    {
                        new MatchEventDto
                        {
                            eventId = 1,
                            type = MatchEventTypes.CardDeployed,
                            payload = new MatchEventPayloadDto
                            {
                                playerId = "alice", instanceId = "object-1", cardId = "pf_001", cardType = "UNIT",
                                slotKind = "UNIT", slotIndex = 2, occupiedSlots = 1, redstone = 0,
                                attack = 1, health = 2, maxHealth = 2, summonedTurn = 1, nextInstanceId = 2
                            }
                        }
                    }
                };
                store.Apply(batch);
                gateway.Emit(batch);

                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.Accepted));
                Assert.That(session.View.UnitSlots[2], Is.EqualTo("pf_001"));
                Assert.That(session.View.GetObject(true, DemoSlotKind.Unit, 2).InstanceId, Is.EqualTo("object-1"));
                Assert.That(session.View.Hand, Is.Empty);
                Assert.That(session.HasPendingCommand, Is.False);
            }
        }

        [Test]
        public void OnlineSessionRejectsSecondCommandWhileFirstIsPending()
        {
            var store = CreateStore(viewerIndex: 0);
            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                _ = session.EnterCombatAsync();

                Assert.That(session.CanIssueCommand, Is.False);
                Assert.ThrowsAsync<InvalidOperationException>(async () => await session.EndTurnAsync());
            }
        }

        [Test]
        public void PresentationQueueCanRebaseForANewOrRejoinedMatch()
        {
            var root = new GameObject("PresentationQueueTest");
            try
            {
                var queue = root.AddComponent<PresentationQueue>();
                queue.Reset(37);

                Assert.That(queue.LastQueuedEventId, Is.EqualTo(37));
                Assert.That(queue.PendingCount, Is.Zero);
                Assert.That(queue.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static MatchStateStore CreateStore(int viewerIndex)
        {
            var players = new[]
            {
                new PlayerStateDto
                {
                    playerId = "alice", factionId = FactionIds.OceanRiver, life = 30, redstone = 1, redstoneCapacity = 1,
                    hand = new[] { "pf_001" }, unitSlots = new string[4], buildingSlots = new string[3]
                },
                new PlayerStateDto
                {
                    playerId = "bob", factionId = FactionIds.End, life = 27, redstone = 2, redstoneCapacity = 2,
                    hand = new[] { "nt_001" }, unitSlots = new string[4], buildingSlots = new string[3]
                }
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1",
                viewerPlayerId = players[viewerIndex].playerId,
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 0,
                lastEventId = 0,
                status = "ACTIVE",
                turn = 1,
                phase = "MAIN",
                activePlayerIndex = viewerIndex,
                players = players
            });
            return store;
        }

        private sealed class FakeGateway : IMatchGateway
        {
            public event Action<MatchEventBatchDto> EventBatchReceived;
            public event Action<MatchStateDto> SnapshotReceived;
            public event Action<CommandRejectionDto> CommandRejected;
            public event Action<Exception> Faulted;
            public event Action<MatchConnectionStatus> ConnectionStateChanged;

            public MatchConnectionStatus CurrentStatus { get; private set; } =
                new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1");
            public MatchCommandDto LastCommand { get; private set; }

            public Task ConnectAsync() => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public Task SendCommandAsync(MatchCommandDto command)
            {
                LastCommand = command;
                return Task.CompletedTask;
            }
            public void Emit(MatchEventBatchDto batch) => EventBatchReceived?.Invoke(batch);
            public void Dispose() { }
        }
    }
}
