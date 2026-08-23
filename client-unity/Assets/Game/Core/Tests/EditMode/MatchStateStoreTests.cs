using NUnit.Framework;

namespace BiomeRivals.Core.Tests
{
    public sealed class MatchStateStoreTests
    {
        [Test]
        public void Replace_UsesAuthoritativeSnapshot()
        {
            var store = new MatchStateStore();
            var snapshot = new MatchStateDto { matchId = "match-1", revision = 3 };

            store.Replace(snapshot);

            Assert.That(store.Current, Is.SameAs(snapshot));
            Assert.That(store.Current.revision, Is.EqualTo(3));
        }
    }
}
