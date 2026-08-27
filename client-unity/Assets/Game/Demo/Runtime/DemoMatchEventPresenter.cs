using System;
using System.Collections;
using BiomeRivals.Core;
using BiomeRivals.Presentation;

namespace BiomeRivals.Demo
{
    public sealed class DemoMatchEventPresenter : IMatchEventPresenter
    {
        private readonly Func<MatchEventDto, IEnumerator> _play;

        public DemoMatchEventPresenter(string eventType, Func<MatchEventDto, IEnumerator> play)
        {
            EventType = string.IsNullOrWhiteSpace(eventType) ? throw new ArgumentException("Event type is required.", nameof(eventType)) : eventType;
            _play = play ?? throw new ArgumentNullException(nameof(play));
        }

        public string EventType { get; }
        public IEnumerator Play(MatchEventDto matchEvent) => _play(matchEvent);
    }
}
