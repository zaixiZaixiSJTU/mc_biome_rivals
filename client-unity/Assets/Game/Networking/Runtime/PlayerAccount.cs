using System;
using System.Threading;
using System.Threading.Tasks;

namespace BiomeRivals.Networking
{
    public enum PlayerAccountPhase
    {
        SignedOut,
        Authenticating,
        Ready,
        Updating,
        SigningOut,
        Failed
    }

    public sealed class PlayerAccountProfile
    {
        public PlayerAccountProfile(string userId, string username, string displayName, bool isGuest)
        {
            UserId = userId ?? string.Empty;
            Username = username ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Username : displayName.Trim();
            IsGuest = isGuest;
        }

        public string UserId { get; }
        public string Username { get; }
        public string DisplayName { get; }
        public bool IsGuest { get; }
    }

    public readonly struct PlayerAccountStatus
    {
        public PlayerAccountStatus(PlayerAccountPhase phase, PlayerAccountProfile profile = null, string detail = "")
        {
            Phase = phase;
            Profile = profile;
            Detail = detail ?? string.Empty;
        }

        public PlayerAccountPhase Phase { get; }
        public PlayerAccountProfile Profile { get; }
        public string Detail { get; }
        public bool CanMatch => Phase == PlayerAccountPhase.Ready && Profile != null;
    }

    public sealed class PlayerAccountBackendSession
    {
        public PlayerAccountBackendSession(object nativeSession, PlayerAccountProfile profile)
        {
            NativeSession = nativeSession ?? throw new ArgumentNullException(nameof(nativeSession));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public object NativeSession { get; }
        public PlayerAccountProfile Profile { get; }
    }

    public interface IPlayerAccountBackend
    {
        Task<PlayerAccountBackendSession> AuthenticateGuestAsync(CancellationToken cancellationToken);
        Task<PlayerAccountProfile> UpdateDisplayNameAsync(
            PlayerAccountBackendSession session,
            string displayName,
            CancellationToken cancellationToken);
        Task SignOutAsync(PlayerAccountBackendSession session, CancellationToken cancellationToken);
    }

    public interface IPlayerAccountService : IDisposable
    {
        event Action<PlayerAccountStatus> StateChanged;
        PlayerAccountStatus CurrentStatus { get; }
        Task<PlayerAccountProfile> AuthenticateGuestAsync(CancellationToken cancellationToken = default);
        Task<PlayerAccountProfile> UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken = default);
        Task SignOutAsync(CancellationToken cancellationToken = default);
    }

    public interface IPlayerAccountSessionProvider
    {
        Task<object> GetOrAuthenticateSessionAsync(CancellationToken cancellationToken);
    }

    public sealed class PlayerAccountService : IPlayerAccountService, IPlayerAccountSessionProvider
    {
        private readonly IPlayerAccountBackend _backend;
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private PlayerAccountBackendSession _session;
        private bool _disposed;

        public PlayerAccountService(IPlayerAccountBackend backend) =>
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));

        public event Action<PlayerAccountStatus> StateChanged;
        public PlayerAccountStatus CurrentStatus { get; private set; } =
            new PlayerAccountStatus(PlayerAccountPhase.SignedOut);

        public async Task<PlayerAccountProfile> AuthenticateGuestAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _lifecycle.WaitAsync(cancellationToken);
            try
            {
                if (_session != null)
                {
                    Publish(new PlayerAccountStatus(PlayerAccountPhase.Ready, _session.Profile));
                    return _session.Profile;
                }
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Authenticating, detail: "Authenticating guest device."));
                _session = await _backend.AuthenticateGuestAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Ready, _session.Profile));
                return _session.Profile;
            }
            catch (OperationCanceledException)
            {
                _session = null;
                Publish(new PlayerAccountStatus(PlayerAccountPhase.SignedOut, detail: "Authentication cancelled."));
                throw;
            }
            catch (Exception exception)
            {
                _session = null;
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Failed, detail: exception.Message));
                throw;
            }
            finally
            {
                _lifecycle.Release();
            }
        }

        public async Task<PlayerAccountProfile> UpdateDisplayNameAsync(
            string displayName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            displayName = displayName?.Trim();
            if (string.IsNullOrEmpty(displayName) || displayName.Length > 24)
                throw new ArgumentOutOfRangeException(nameof(displayName), "Display name must contain 1 to 24 characters.");
            await _lifecycle.WaitAsync(cancellationToken);
            try
            {
                if (_session == null) throw new InvalidOperationException("Authenticate before updating the display name.");
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Updating, _session.Profile, "Updating display name."));
                var profile = await _backend.UpdateDisplayNameAsync(_session, displayName, cancellationToken);
                _session = new PlayerAccountBackendSession(_session.NativeSession, profile);
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Ready, profile));
                return profile;
            }
            catch (OperationCanceledException)
            {
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Ready, _session?.Profile, "Display-name update cancelled."));
                throw;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Failed, _session?.Profile, exception.Message));
                throw;
            }
            finally
            {
                _lifecycle.Release();
            }
        }

        public async Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _lifecycle.WaitAsync(cancellationToken);
            try
            {
                if (_session == null)
                {
                    Publish(new PlayerAccountStatus(PlayerAccountPhase.SignedOut));
                    return;
                }
                Publish(new PlayerAccountStatus(PlayerAccountPhase.SigningOut, _session.Profile));
                await _backend.SignOutAsync(_session, cancellationToken);
                _session = null;
                Publish(new PlayerAccountStatus(PlayerAccountPhase.SignedOut));
            }
            catch (OperationCanceledException)
            {
                _session = null;
                Publish(new PlayerAccountStatus(PlayerAccountPhase.SignedOut, detail: "Sign-out cancelled locally."));
                throw;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                _session = null;
                Publish(new PlayerAccountStatus(PlayerAccountPhase.Failed, detail: exception.Message));
                throw;
            }
            finally
            {
                _lifecycle.Release();
            }
        }

        async Task<object> IPlayerAccountSessionProvider.GetOrAuthenticateSessionAsync(CancellationToken cancellationToken)
        {
            await AuthenticateGuestAsync(cancellationToken);
            return _session.NativeSession;
        }

        private void Publish(PlayerAccountStatus status)
        {
            CurrentStatus = status;
            StateChanged?.Invoke(status);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PlayerAccountService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // An in-flight backend operation may still release this semaphore after
            // the composition root is destroyed, so it intentionally stays alive.
        }
    }
}
