using System.Collections;
using BiomeRivals.Core;

namespace BiomeRivals.Presentation
{
    public interface IMatchEventPresenter
    {
        string EventType { get; }
        IEnumerator Play(MatchEventDto matchEvent);
    }
}
