using System;
using UnityEngine;

namespace BiomeRivals.Networking
{
    [Serializable]
    public sealed class NakamaConnectionSettings
    {
        private const string ResourcePath = "Networking/nakama-connection.v1";

        public string scheme = "http";
        public string host = "127.0.0.1";
        public int port = 7350;
        public string serverKey = "local_only_change_me";
        public int requestTimeoutSeconds = 10;
        public int matchmakingTimeoutSeconds = 30;
        public int maxReconnectAttempts = 3;

        public static NakamaConnectionSettings Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            var settings = asset == null
                ? new NakamaConnectionSettings()
                : JsonUtility.FromJson<NakamaConnectionSettings>(asset.text);
            if (settings == null) throw new FormatException("Nakama connection settings JSON is invalid.");
            settings.ApplyEnvironmentOverrides();
            settings.Validate();
            return settings;
        }

        public void Validate()
        {
            if (scheme != "http" && scheme != "https") throw new FormatException("Nakama scheme must be http or https.");
            if (string.IsNullOrWhiteSpace(host)) throw new FormatException("Nakama host is required.");
            if (port < 1 || port > 65535) throw new FormatException("Nakama port is outside the valid range.");
            if (string.IsNullOrWhiteSpace(serverKey)) throw new FormatException("Nakama server key is required.");
            if (requestTimeoutSeconds < 1) throw new FormatException("Nakama request timeout must be positive.");
            if (matchmakingTimeoutSeconds < 1) throw new FormatException("Nakama matchmaking timeout must be positive.");
            if (maxReconnectAttempts < 0) throw new FormatException("Nakama reconnect attempts cannot be negative.");
        }

        private void ApplyEnvironmentOverrides()
        {
            scheme = Read("BIOME_RIVALS_NAKAMA_SCHEME", scheme);
            host = Read("BIOME_RIVALS_NAKAMA_HOST", host);
            serverKey = Read("BIOME_RIVALS_NAKAMA_SERVER_KEY", serverKey);
            port = ReadInt("BIOME_RIVALS_NAKAMA_PORT", port);
        }

        private static string Read(string key, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int ReadInt(string key, int fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }
}
