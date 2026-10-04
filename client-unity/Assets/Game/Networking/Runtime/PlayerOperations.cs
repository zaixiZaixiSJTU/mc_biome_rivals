using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BiomeRivals.Core;
using Nakama.TinyJson;

namespace BiomeRivals.Networking
{
    // UI, scripted agents and external decision adapters share the same command boundary.
    // Calls belong on the Unity main thread. Server acknowledgements remain authoritative.
    public interface IPlayerOperations
    {
        event Action StateChanged;
        bool CanIssueCommand { get; }
        PlayerObservation Observe();
        Task<MatchCommandDispatchResult> ExecuteAsync(PlayerActionRequest action);
    }

    [Serializable]
    public sealed class PlayerActionRequest
    {
        public string matchId = string.Empty;
        public int expectedRevision;
        public string type = string.Empty;
        public MatchCommandPayloadDto payload = new MatchCommandPayloadDto();

        public static PlayerActionRequest Create(PlayerObservation observation, string type,
            MatchCommandPayloadDto payload = null)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            return new PlayerActionRequest { matchId = observation.MatchId,
                expectedRevision = observation.Revision, type = type,
                payload = payload ?? new MatchCommandPayloadDto() };
        }
    }

    public sealed class PlayerObservation
    {
        public string MatchId { get; }
        public int Revision { get; }
        public bool CanIssueCommand { get; }
        public string StateJson { get; }

        private PlayerObservation(MatchStateDto state, bool canIssueCommand)
        {
            MatchId = state?.matchId ?? string.Empty;
            Revision = state?.revision ?? -1;
            CanIssueCommand = state != null && canIssueCommand;
            StateJson = state == null ? string.Empty : state.ToJson();
        }

        // Returning a new DTO on every read prevents agents from mutating the live store.
        public MatchStateDto ReadState() => string.IsNullOrEmpty(StateJson)
            ? null : DeserializeProjection(StateJson);

        private static MatchStateDto DeserializeProjection(string json)
        {
            var state = MatchWireJson.Deserialize<MatchStateDto>(json);
            var wire = json.FromJson<Dictionary<string, object>>();
            // TinyJson omits null object fields; Unity otherwise creates empty objects for them.
            if (!wire.ContainsKey("pendingChoice") || wire["pendingChoice"] == null) state.pendingChoice = null;
            if (wire.TryGetValue("players", out var players) && players is System.Collections.IList entries)
                for (var i = 0; i < state.players.Length; i++)
                    if (entries[i] is Dictionary<string, object> player &&
                        (!player.ContainsKey("equipment") || player["equipment"] == null))
                        state.players[i].equipment = null;
            return state;
        }

        public static PlayerObservation FromSnapshot(MatchStateDto snapshot, bool canIssueCommand)
        {
            if (snapshot == null) return new PlayerObservation(null, false);
            var copy = DeserializeProjection(snapshot.ToJson());
            if (copy.players == null || copy.players.Length != 2 ||
                Array.Find(copy.players, player => player != null && player.playerId == copy.viewerPlayerId) == null)
                throw new InvalidOperationException("A player observation requires a valid viewer projection.");
            foreach (var player in copy.players)
            {
                if (player == null) throw new InvalidOperationException("Missing projected player.");
                if (player.playerId == copy.viewerPlayerId) continue;
                var count = Math.Max(player.hand?.Length ?? 0, player.handCards?.Length ?? 0);
                player.hand = new string[count];
                player.handCards = new HandCardStateDto[count];
                for (var i = 0; i < count; i++) player.hand[i] = string.Empty;
            }
            if (copy.pendingChoice != null && copy.pendingChoice.playerId != copy.viewerPlayerId)
            {
                var options = copy.pendingChoice.options ?? Array.Empty<PendingChoiceOptionDto>();
                copy.pendingChoice.options = new PendingChoiceOptionDto[options.Length];
                for (var i = 0; i < options.Length; i++)
                    copy.pendingChoice.options[i] = new PendingChoiceOptionDto { optionIndex = i, selectable = false };
            }
            return new PlayerObservation(copy, canIssueCommand);
        }
    }

    public interface IMatchAgentPolicy
    {
        // Return null to wait. Never submit commands or touch scene objects in a policy.
        PlayerActionRequest Decide(PlayerObservation observation);
    }

    public sealed class MatchAgentRunner : IDisposable
    {
        private readonly IPlayerOperations _operations;
        private readonly IMatchAgentPolicy _policy;
        private bool _running;
        private bool _disposed;
        private string _attemptedMatch = string.Empty;
        private int _attemptedRevision = -1;
        public MatchCommandDispatchResult? LastResult { get; private set; }
        public string LastActionType { get; private set; } = string.Empty;

        public MatchAgentRunner(IPlayerOperations operations, IMatchAgentPolicy policy)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public async Task<MatchCommandDispatchResult?> TickAsync()
        {
            if (_disposed || _running || !_operations.CanIssueCommand) return null;
            _running = true;
            try
            {
                var observation = _operations.Observe();
                if (!observation.CanIssueCommand || (observation.MatchId == _attemptedMatch &&
                    observation.Revision == _attemptedRevision)) return null;
                var action = _policy.Decide(observation);
                if (action == null) return null;
                _attemptedMatch = observation.MatchId;
                _attemptedRevision = observation.Revision;
                LastActionType = action.type;
                // Rejections/timeouts do not trigger a blind retry at the same revision.
                LastResult = await _operations.ExecuteAsync(action);
                return LastResult;
            }
            finally { _running = false; }
        }

        public void Dispose() => _disposed = true; // Does not own the player session.
    }
}
