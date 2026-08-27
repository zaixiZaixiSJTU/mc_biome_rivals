using System.Collections;
using System.Collections.Generic;
using BiomeRivals.Core;
using UnityEngine;

namespace BiomeRivals.Presentation
{
    public sealed class PresentationQueue : MonoBehaviour
    {
        private readonly Queue<MatchEventDto> _pending = new Queue<MatchEventDto>();
        private readonly PresentationEventRegistry _registry = new PresentationEventRegistry();
        private Coroutine _runner;
        private long _lastQueuedEventId;

        public int PendingCount => _pending.Count;
        public bool IsPlaying => _runner != null;
        public long LastQueuedEventId => _lastQueuedEventId;
        public PresentationEventRegistry Registry => _registry;

        public void Reset(long lastPresentedEventId = 0)
        {
            if (_runner != null) StopCoroutine(_runner);
            _runner = null;
            _pending.Clear();
            _lastQueuedEventId = lastPresentedEventId;
        }

        public void Enqueue(MatchEventBatchDto batch)
        {
            if (batch == null || batch.events == null) return;
            foreach (var matchEvent in batch.events)
            {
                if (matchEvent == null) continue;
                if (matchEvent.eventId <= _lastQueuedEventId)
                {
                    Debug.LogWarning($"Ignored duplicate/out-of-order event {matchEvent.eventId}.", this);
                    continue;
                }
                _lastQueuedEventId = matchEvent.eventId;
                _pending.Enqueue(matchEvent);
            }
            if (_runner == null && isActiveAndEnabled) _runner = StartCoroutine(Drain());
        }

        private IEnumerator Drain()
        {
            while (_pending.Count > 0)
            {
                var matchEvent = _pending.Dequeue();
                if (_registry.TryResolve(matchEvent.type, out var presenter))
                {
                    yield return presenter.Play(matchEvent);
                }
                else
                {
                    Debug.LogWarning($"No presenter registered for event {matchEvent.type}.", this);
                }
            }
            _runner = null;
        }
    }
}
