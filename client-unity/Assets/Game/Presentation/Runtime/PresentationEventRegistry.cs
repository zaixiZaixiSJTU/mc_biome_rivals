using System;
using System.Collections.Generic;

namespace BiomeRivals.Presentation
{
    public sealed class PresentationEventRegistry
    {
        private readonly Dictionary<string, IMatchEventPresenter> _presenters =
            new Dictionary<string, IMatchEventPresenter>(StringComparer.Ordinal);

        public int Count => _presenters.Count;

        public void Register(IMatchEventPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (string.IsNullOrWhiteSpace(presenter.EventType))
                throw new ArgumentException("Presenter event type is required.", nameof(presenter));
            if (_presenters.ContainsKey(presenter.EventType))
                throw new InvalidOperationException($"A presenter is already registered for {presenter.EventType}.");
            _presenters.Add(presenter.EventType, presenter);
        }

        public bool Unregister(IMatchEventPresenter presenter)
        {
            if (presenter == null) return false;
            return _presenters.Remove(presenter.EventType);
        }

        public bool TryResolve(string eventType, out IMatchEventPresenter presenter)
        {
            return _presenters.TryGetValue(eventType, out presenter);
        }
    }
}
