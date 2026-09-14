using System;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace BiomeRivals.Networking
{
    public sealed class NakamaPlayerAccountBackend : IPlayerAccountBackend
    {
        private const string DeviceIdPreference = "biome_rivals.nakama.device_id";
        private const string AuthTokenPreference = "biome_rivals.nakama.auth_token";
        private const string RefreshTokenPreference = "biome_rivals.nakama.refresh_token";

        private readonly IClient _client;
        private readonly string _deviceId;
        private readonly string _sessionKeySuffix;

        public NakamaPlayerAccountBackend(NakamaConnectionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            _client = new Client(settings.scheme, settings.host, settings.port, settings.serverKey, UnityWebRequestAdapter.Instance)
            {
                Timeout = settings.requestTimeoutSeconds,
                Logger = new UnityLogger()
            };
            _deviceId = ResolveDeviceId(out var overridden);
            _sessionKeySuffix = overridden ? "." + _deviceId : string.Empty;
        }

        public async Task<PlayerAccountBackendSession> AuthenticateGuestAsync(CancellationToken cancellationToken)
        {
            var session = RestoreSession();
            if (session == null || session.IsExpired)
            {
                session = await _client.AuthenticateDeviceAsync(_deviceId, canceller: cancellationToken);
                SaveSession(session);
            }
            var account = await _client.GetAccountAsync(session, canceller: cancellationToken);
            return new PlayerAccountBackendSession(session, ToProfile(account));
        }

        public async Task<PlayerAccountProfile> UpdateDisplayNameAsync(
            PlayerAccountBackendSession backendSession,
            string displayName,
            CancellationToken cancellationToken)
        {
            var session = RequireSession(backendSession);
            await _client.UpdateAccountAsync(session, null, displayName, canceller: cancellationToken);
            return ToProfile(await _client.GetAccountAsync(session, canceller: cancellationToken));
        }

        public async Task SignOutAsync(PlayerAccountBackendSession backendSession, CancellationToken cancellationToken)
        {
            try
            {
                await _client.SessionLogoutAsync(RequireSession(backendSession), canceller: cancellationToken);
            }
            finally
            {
                PlayerPrefs.DeleteKey(AuthTokenPreference + _sessionKeySuffix);
                PlayerPrefs.DeleteKey(RefreshTokenPreference + _sessionKeySuffix);
                PlayerPrefs.Save();
            }
        }

        private static PlayerAccountProfile ToProfile(IApiAccount account)
        {
            if (account?.User == null) throw new InvalidOperationException("Nakama returned an account without a user profile.");
            return new PlayerAccountProfile(
                account.User.Id,
                account.User.Username,
                account.User.DisplayName,
                true);
        }

        private static ISession RequireSession(PlayerAccountBackendSession session)
        {
            if (!(session?.NativeSession is ISession nativeSession))
                throw new InvalidOperationException("The account session is not compatible with Nakama.");
            return nativeSession;
        }

        private static string ResolveDeviceId(out bool overridden)
        {
            var overrideId = Environment.GetEnvironmentVariable("BIOME_RIVALS_NAKAMA_DEVICE_ID");
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], "-nakamaDeviceId", StringComparison.Ordinal)) overrideId = arguments[index + 1];
            if (!string.IsNullOrWhiteSpace(overrideId))
            {
                overrideId = overrideId.Trim();
                if (overrideId.Length < 10 || overrideId.Length > 128)
                    throw new FormatException("Nakama device ID override must contain 10 to 128 characters.");
                overridden = true;
                return overrideId;
            }

            overridden = false;
            var deviceId = PlayerPrefs.GetString(DeviceIdPreference, string.Empty);
            if (string.IsNullOrWhiteSpace(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
            {
                deviceId = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrWhiteSpace(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
                    deviceId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceIdPreference, deviceId);
                PlayerPrefs.Save();
            }
            return deviceId;
        }

        private ISession RestoreSession()
        {
            try
            {
                return Session.Restore(
                    PlayerPrefs.GetString(AuthTokenPreference + _sessionKeySuffix, string.Empty),
                    PlayerPrefs.GetString(RefreshTokenPreference + _sessionKeySuffix, string.Empty));
            }
            catch (Exception)
            {
                PlayerPrefs.DeleteKey(AuthTokenPreference + _sessionKeySuffix);
                PlayerPrefs.DeleteKey(RefreshTokenPreference + _sessionKeySuffix);
                return null;
            }
        }

        private void SaveSession(ISession session)
        {
            PlayerPrefs.SetString(AuthTokenPreference + _sessionKeySuffix, session.AuthToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenPreference + _sessionKeySuffix, session.RefreshToken ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
