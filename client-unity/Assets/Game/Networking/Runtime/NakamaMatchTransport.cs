using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace BiomeRivals.Networking
{
    public sealed class NakamaMatchTransport : IMatchTransport
    {
        private const string DeviceIdPreference = "biome_rivals.nakama.device_id";
        private const string AuthTokenPreference = "biome_rivals.nakama.auth_token";
        private const string RefreshTokenPreference = "biome_rivals.nakama.refresh_token";

        private readonly NakamaConnectionSettings _settings;
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private readonly string _deviceId;
        private readonly string _sessionKeySuffix;
        private Client _client;
        private ISession _session;
        private ISocket _socket;
        private IMatchmakerTicket _ticket;
        private IMatch _match;
        private TaskCompletionSource<IMatchmakerMatched> _matchedSource;
        private CancellationTokenSource _connectCancellation;
        private bool _connecting;
        private bool _disconnecting;
        private bool _disposed;
        private string _resumeMatchId = string.Empty;
        private string _expectedMatchId = string.Empty;

        public event Action<int, string> MessageReceived;
        public event Action<Exception> Faulted;
        public event Action<MatchConnectionStatus> ConnectionStateChanged;

        public MatchConnectionStatus CurrentStatus { get; private set; } =
            new MatchConnectionStatus(MatchConnectionPhase.Offline);

        public NakamaMatchTransport(NakamaConnectionSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.Validate();
            _deviceId = ResolveDeviceId(out var overridden);
            _sessionKeySuffix = overridden ? "." + _deviceId : string.Empty;
        }

        public async Task ConnectAsync()
        {
            ThrowIfDisposed();
            await _lifecycle.WaitAsync();
            CancellationToken cancellationToken = default;
            try
            {
                if (CurrentStatus.Phase == MatchConnectionPhase.Ready || _connecting) return;
                _connecting = true;
                _disconnecting = false;
                ReplaceConnectionCancellation();
                cancellationToken = _connectCancellation.Token;
                await ConnectCoreAsync(0, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // DisconnectAsync owns the final transition back to Offline. A user
                // cancelling matchmaking is not a transport fault.
            }
            catch (Exception exception)
            {
                Publish(new MatchConnectionStatus(MatchConnectionPhase.Failed, exception.Message));
                Faulted?.Invoke(exception);
                throw;
            }
            finally
            {
                _connecting = false;
                ReleaseConnectionCancellation();
                _lifecycle.Release();
            }
        }

        public Task SendAsync(int opcode, string json)
        {
            ThrowIfDisposed();
            if (!CurrentStatus.CanSendCommands || _socket == null || _match == null)
                return Task.FromException(new InvalidOperationException("Cannot send a match command before the authoritative match is ready."));
            if (opcode < 0) return Task.FromException(new ArgumentOutOfRangeException(nameof(opcode)));
            if (json == null) return Task.FromException(new ArgumentNullException(nameof(json)));
            return _socket.SendMatchStateAsync(_match.Id, opcode, json);
        }

        public async Task DisconnectAsync()
        {
            if (_disposed) return;
            _disconnecting = true;
            _connectCancellation?.Cancel();
            await _lifecycle.WaitAsync();
            try
            {
                _resumeMatchId = string.Empty;
                _expectedMatchId = string.Empty;
                Publish(new MatchConnectionStatus(MatchConnectionPhase.Disconnecting));
                if (_ticket != null && _socket != null && _socket.IsConnected)
                    await _socket.RemoveMatchmakerAsync(_ticket);
                _ticket = null;
                if (_match != null && _socket != null && _socket.IsConnected)
                    await _socket.LeaveMatchAsync(_match);
                _match = null;
                if (_socket != null && (_socket.IsConnected || _socket.IsConnecting)) await _socket.CloseAsync();
                DetachSocket();
                Publish(new MatchConnectionStatus(MatchConnectionPhase.Offline));
            }
            finally
            {
                _disconnecting = false;
                _lifecycle.Release();
            }
        }

        private async Task ConnectCoreAsync(int reconnectAttempt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Publish(new MatchConnectionStatus(
                reconnectAttempt > 0 ? MatchConnectionPhase.Reconnecting : MatchConnectionPhase.Authenticating,
                reconnectAttempt > 0 ? "Restoring authoritative match session." : "Authenticating device.",
                _resumeMatchId,
                reconnectAttempt));
            EnsureClient();
            _session = RestoreSession();
            if (_session == null || _session.IsExpired)
            {
                _session = await _client.AuthenticateDeviceAsync(_deviceId);
                SaveSession(_session);
            }
            cancellationToken.ThrowIfCancellationRequested();

            ResetSocket();
            _socket = _client.NewSocket(useMainThread: true);
            AttachSocket();
            Publish(new MatchConnectionStatus(MatchConnectionPhase.Connecting, "Opening realtime socket.", _resumeMatchId, reconnectAttempt));
            await _socket.ConnectAsync(_session, true, _settings.requestTimeoutSeconds);
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(_resumeMatchId))
            {
                Publish(new MatchConnectionStatus(MatchConnectionPhase.Joining, "Rejoining authoritative match.", _resumeMatchId, reconnectAttempt));
                _match = await _socket.JoinMatchAsync(_resumeMatchId);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                Publish(new MatchConnectionStatus(MatchConnectionPhase.Matchmaking, "Waiting for one opponent."));
                _matchedSource = new TaskCompletionSource<IMatchmakerMatched>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ticket = await _socket.AddMatchmakerAsync("*", 2, 2);
                var timeout = Task.Delay(TimeSpan.FromSeconds(_settings.matchmakingTimeoutSeconds), cancellationToken);
                var completed = await Task.WhenAny(_matchedSource.Task, timeout);
                cancellationToken.ThrowIfCancellationRequested();
                if (completed != _matchedSource.Task)
                {
                    if (_ticket != null && _socket.IsConnected) await _socket.RemoveMatchmakerAsync(_ticket);
                    _ticket = null;
                    throw new TimeoutException("Nakama matchmaking timed out before a second player joined.");
                }
                var matched = await _matchedSource.Task;
                _ticket = null;
                _expectedMatchId = matched.MatchId;
                Publish(new MatchConnectionStatus(MatchConnectionPhase.Joining, "Joining authoritative match.", matched.MatchId));
                _match = await _socket.JoinMatchAsync(matched);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_match == null || !_match.Authoritative)
                throw new InvalidOperationException("Nakama returned a relayed match; Biome Rivals requires an authoritative handler.");
            _resumeMatchId = _match.Id;
            _expectedMatchId = _match.Id;
            Publish(new MatchConnectionStatus(MatchConnectionPhase.Ready, "Authoritative match ready.", _match.Id, reconnectAttempt));
        }

        private void EnsureClient()
        {
            if (_client != null) return;
            _client = new Client(
                _settings.scheme,
                _settings.host,
                _settings.port,
                _settings.serverKey,
                UnityWebRequestAdapter.Instance)
            {
                Timeout = _settings.requestTimeoutSeconds,
                Logger = new UnityLogger()
            };
        }

        private void AttachSocket()
        {
            _socket.Closed += HandleSocketClosed;
            _socket.ReceivedError += HandleSocketError;
            _socket.ReceivedMatchmakerMatched += HandleMatched;
            _socket.ReceivedMatchState += HandleMatchState;
        }

        private void DetachSocket()
        {
            if (_socket == null) return;
            _socket.Closed -= HandleSocketClosed;
            _socket.ReceivedError -= HandleSocketError;
            _socket.ReceivedMatchmakerMatched -= HandleMatched;
            _socket.ReceivedMatchState -= HandleMatchState;
            _socket = null;
        }

        private void ResetSocket()
        {
            DetachSocket();
            _matchedSource = null;
            _ticket = null;
            _match = null;
            _expectedMatchId = _resumeMatchId;
        }

        private void HandleMatched(IMatchmakerMatched matched) => _matchedSource?.TrySetResult(matched);

        private void HandleMatchState(IMatchState state)
        {
            if (state == null || state.MatchId != _expectedMatchId) return;
            if (state.OpCode < int.MinValue || state.OpCode > int.MaxValue)
            {
                HandleSocketError(new InvalidOperationException("Received a match opcode outside the client integer range."));
                return;
            }
            MessageReceived?.Invoke((int)state.OpCode, Encoding.UTF8.GetString(state.State ?? Array.Empty<byte>()));
        }

        private void HandleSocketError(Exception exception)
        {
            if (exception == null) return;
            Faulted?.Invoke(exception);
        }

        private void HandleSocketClosed(string reason)
        {
            if (_disconnecting || _disposed)
            {
                Publish(new MatchConnectionStatus(MatchConnectionPhase.Offline, reason));
                return;
            }
            if (_connecting) return;
            _ = ReconnectAsync(reason);
        }

        private async Task ReconnectAsync(string reason)
        {
            ReplaceConnectionCancellation();
            var cancellationToken = _connectCancellation.Token;
            try
            {
                for (var attempt = 1; attempt <= _settings.maxReconnectAttempts && !_disposed && !_disconnecting; attempt++)
                {
                    Publish(new MatchConnectionStatus(MatchConnectionPhase.Reconnecting, reason, _resumeMatchId, attempt));
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(4, attempt)), cancellationToken);
                    await _lifecycle.WaitAsync(cancellationToken);
                    try
                    {
                        _connecting = true;
                        await ConnectCoreAsync(attempt, cancellationToken);
                        return;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        Faulted?.Invoke(exception);
                        if (attempt == _settings.maxReconnectAttempts)
                            Publish(new MatchConnectionStatus(MatchConnectionPhase.Failed, exception.Message, _resumeMatchId, attempt));
                    }
                    finally
                    {
                        _connecting = false;
                        _lifecycle.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                ReleaseConnectionCancellation();
            }
        }

        private void ReplaceConnectionCancellation()
        {
            _connectCancellation?.Cancel();
            _connectCancellation?.Dispose();
            _connectCancellation = new CancellationTokenSource();
        }

        private void ReleaseConnectionCancellation()
        {
            _connectCancellation?.Dispose();
            _connectCancellation = null;
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

        private void Publish(MatchConnectionStatus status)
        {
            CurrentStatus = status;
            ConnectionStateChanged?.Invoke(status);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NakamaMatchTransport));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _disconnecting = true;
            _connectCancellation?.Cancel();
            if (_socket != null && (_socket.IsConnected || _socket.IsConnecting)) _ = _socket.CloseAsync();
            DetachSocket();
            // Do not dispose the semaphore: an in-flight connect/reconnect can still
            // execute its finally block and release it after cancellation.
        }
    }
}
