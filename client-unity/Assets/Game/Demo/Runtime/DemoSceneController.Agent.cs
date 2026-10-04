using System;
using BiomeRivals.Networking;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
        private IMatchAgentPolicy _agentPolicy;
        private MatchAgentRunner _agentRunner;
        private float _nextAgentTick;

        // A second client/account can use this entry without owning the human's session.
        public void SetAgentPolicy(IMatchAgentPolicy policy)
        {
            _agentRunner?.Dispose();
            _agentPolicy = policy;
            _agentRunner = policy != null && _onlineSession != null
                ? new MatchAgentRunner(_onlineSession, policy) : null;
        }

        private async void TickAgent()
        {
            if (_agentRunner == null || Time.unscaledTime < _nextAgentTick) return;
            var runner = _agentRunner;
            _nextAgentTick = Time.unscaledTime + 0.4f;
            try
            {
                var result = await runner.TickAsync();
                if (result.HasValue && result.Value.Outcome == MatchCommandOutcome.Accepted)
                    Debug.Log($"Agent accepted: {runner.LastActionType}; revision={result.Value.Revision}.", this);
                if (result.HasValue && result.Value.Outcome != MatchCommandOutcome.Accepted)
                    Debug.LogWarning($"Agent action stopped at revision {result.Value.Revision}: {result.Value.Code}", this);
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(runner, _agentRunner)) SetAgentPolicy(null);
                Debug.LogWarning("Agent stopped: " + exception.Message, this);
            }
        }
    }
}
