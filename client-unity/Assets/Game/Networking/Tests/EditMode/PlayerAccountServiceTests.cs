using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BiomeRivals.Networking.Tests
{
    public sealed class PlayerAccountServiceTests
    {
        [Test]
        public async Task GuestAuthenticationPublishesAReusableReadySession()
        {
            var backend = new FakeBackend();
            using (var service = new PlayerAccountService(backend))
            {
                var phases = new List<PlayerAccountPhase>();
                service.StateChanged += status => phases.Add(status.Phase);

                var profile = await service.AuthenticateGuestAsync();
                var nativeSession = await ((IPlayerAccountSessionProvider)service)
                    .GetOrAuthenticateSessionAsync(CancellationToken.None);

                Assert.That(profile.UserId, Is.EqualTo("player-1"));
                Assert.That(profile.DisplayName, Is.EqualTo("Guest Miner"));
                Assert.That(nativeSession, Is.SameAs(backend.NativeSession));
                Assert.That(backend.AuthenticationCount, Is.EqualTo(1));
                Assert.That(phases, Is.EqualTo(new[]
                {
                    PlayerAccountPhase.Authenticating,
                    PlayerAccountPhase.Ready,
                    PlayerAccountPhase.Ready
                }));
                Assert.That(service.CurrentStatus.CanMatch, Is.True);
            }
        }

        [Test]
        public async Task FailedAuthenticationCanBeRetried()
        {
            var backend = new FakeBackend { AuthenticationFailuresRemaining = 1 };
            using (var service = new PlayerAccountService(backend))
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AuthenticateGuestAsync());
                Assert.That(service.CurrentStatus.Phase, Is.EqualTo(PlayerAccountPhase.Failed));
                Assert.That(service.CurrentStatus.CanMatch, Is.False);

                var profile = await service.AuthenticateGuestAsync();

                Assert.That(profile.UserId, Is.EqualTo("player-1"));
                Assert.That(service.CurrentStatus.Phase, Is.EqualTo(PlayerAccountPhase.Ready));
                Assert.That(backend.AuthenticationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task CancelledAuthenticationReturnsToSignedOutAndCanRetry()
        {
            var backend = new FakeBackend { AuthenticationCancellationsRemaining = 1 };
            using (var service = new PlayerAccountService(backend))
            {
                Assert.ThrowsAsync<TaskCanceledException>(async () => await service.AuthenticateGuestAsync());
                Assert.That(service.CurrentStatus.Phase, Is.EqualTo(PlayerAccountPhase.SignedOut));

                var profile = await service.AuthenticateGuestAsync();

                Assert.That(profile.UserId, Is.EqualTo("player-1"));
                Assert.That(service.CurrentStatus.CanMatch, Is.True);
            }
        }

        [Test]
        public async Task DisplayNameAndSignOutUseTheAccountBoundary()
        {
            var backend = new FakeBackend();
            using (var service = new PlayerAccountService(backend))
            {
                await service.AuthenticateGuestAsync();
                var profile = await service.UpdateDisplayNameAsync("  Redstone Fox  ");

                Assert.That(profile.DisplayName, Is.EqualTo("Redstone Fox"));
                Assert.That(backend.LastDisplayName, Is.EqualTo("Redstone Fox"));
                Assert.That(service.CurrentStatus.CanMatch, Is.True);

                await service.SignOutAsync();

                Assert.That(backend.SignOutCount, Is.EqualTo(1));
                Assert.That(service.CurrentStatus.Phase, Is.EqualTo(PlayerAccountPhase.SignedOut));
                Assert.That(service.CurrentStatus.Profile, Is.Null);
            }
        }

        [Test]
        public async Task CancelledSignOutClearsTheAmbiguousLocalSession()
        {
            var backend = new FakeBackend { CancelSignOut = true };
            using (var service = new PlayerAccountService(backend))
            {
                await service.AuthenticateGuestAsync();

                Assert.ThrowsAsync<TaskCanceledException>(async () => await service.SignOutAsync());

                Assert.That(service.CurrentStatus.Phase, Is.EqualTo(PlayerAccountPhase.SignedOut));
                backend.CancelSignOut = false;
                await service.AuthenticateGuestAsync();
                Assert.That(backend.AuthenticationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void InvalidDisplayNameIsRejectedBeforeTheBackend()
        {
            var backend = new FakeBackend();
            using (var service = new PlayerAccountService(backend))
            {
                Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                    await service.UpdateDisplayNameAsync(new string('x', 25)));
                Assert.That(backend.LastDisplayName, Is.Null);
            }
        }

        private sealed class FakeBackend : IPlayerAccountBackend
        {
            public readonly object NativeSession = new object();
            public int AuthenticationFailuresRemaining;
            public int AuthenticationCancellationsRemaining;
            public int AuthenticationCount;
            public int SignOutCount;
            public bool CancelSignOut;
            public string LastDisplayName;

            public Task<PlayerAccountBackendSession> AuthenticateGuestAsync(CancellationToken cancellationToken)
            {
                AuthenticationCount++;
                if (AuthenticationCancellationsRemaining-- > 0)
                    return Task.FromException<PlayerAccountBackendSession>(new OperationCanceledException());
                if (AuthenticationFailuresRemaining-- > 0)
                    return Task.FromException<PlayerAccountBackendSession>(new InvalidOperationException("offline"));
                return Task.FromResult(new PlayerAccountBackendSession(
                    NativeSession,
                    new PlayerAccountProfile("player-1", "device-user", "Guest Miner", true)));
            }

            public Task<PlayerAccountProfile> UpdateDisplayNameAsync(
                PlayerAccountBackendSession session,
                string displayName,
                CancellationToken cancellationToken)
            {
                LastDisplayName = displayName;
                return Task.FromResult(new PlayerAccountProfile("player-1", "device-user", displayName, true));
            }

            public Task SignOutAsync(PlayerAccountBackendSession session, CancellationToken cancellationToken)
            {
                SignOutCount++;
                if (CancelSignOut) return Task.FromException(new OperationCanceledException());
                return Task.CompletedTask;
            }
        }
    }
}
