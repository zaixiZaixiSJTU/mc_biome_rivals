using System;
using System.Text.RegularExpressions;
using BiomeRivals.Core;

namespace BiomeRivals.Networking
{
    public enum ServerCompatibilityFailureKind { Protocol, Ruleset, CardData, Effects }

    // Diagnostic metadata only: never changes negotiated versions or permits an incompatible socket.
    public sealed class ServerCompatibilityFailure
    {
        public readonly ServerCompatibilityFailureKind Kind;
        public readonly string Endpoint, ClientRuleset, ServerRuleset;
        public readonly int ClientProtocol, ServerProtocol, ClientCardVersion, ServerCardVersion, ClientEffectVersion, ServerEffectVersion;
        public readonly string DiagnosticText, UserSummary, UserDetails;

        private ServerCompatibilityFailure(NakamaConnectionSettings settings, MatchmakingPreferences preferences,
            int protocol, string ruleset, int cards, int effects, string mismatch)
        {
            var uri = new UriBuilder(settings.scheme, settings.host, settings.port).Uri;
            Endpoint = uri.GetLeftPart(UriPartial.Authority);
            ClientProtocol = GameVersions.Protocol; ServerProtocol = protocol;
            ClientRuleset = GameVersions.Ruleset; ServerRuleset = ruleset;
            ClientCardVersion = preferences.CardContentVersion; ServerCardVersion = cards;
            ClientEffectVersion = preferences.ImplementedEffectRegistryVersion; ServerEffectVersion = effects;
            Kind = protocol != ClientProtocol ? ServerCompatibilityFailureKind.Protocol :
                ruleset != ClientRuleset ? ServerCompatibilityFailureKind.Ruleset :
                cards != ClientCardVersion ? ServerCompatibilityFailureKind.CardData : ServerCompatibilityFailureKind.Effects;
            DiagnosticText = $"{mismatch}; endpoint {Endpoint}. " +
                $"Client: protocol={ClientProtocol}, ruleset={ClientRuleset}, cards={ClientCardVersion}, effects={ClientEffectVersion}. " +
                $"Server: protocol={ServerProtocol}, ruleset={ServerRuleset}, cards={ServerCardVersion}, effects={ServerEffectVersion}. " +
                "Update the server or client to matching versions; matchmaking was not attempted.";
            var host = uri.IdnHost;
            if (host.Length > 12) host = host.Substring(0, 11) + "…";
            var target = host + ":" + settings.port;
            var reason = Kind == ServerCompatibilityFailureKind.Protocol ? $"协议：服务{protocol} / 客户端{ClientProtocol}" :
                Kind == ServerCompatibilityFailureKind.Ruleset ? "规则版本不匹配" :
                Kind == ServerCompatibilityFailureKind.CardData ? $"卡牌：服务{cards} / 客户端{ClientCardVersion}" :
                $"效果：服务{effects} / 客户端{ClientEffectVersion}";
            UserSummary = $"{target}\n{reason}\n更新两端后重试";
            UserDetails = $"无法连接：客户端与服务器版本不兼容。\n\n服务器地址：{Endpoint}\n\n" +
                $"客户端：通信 {ClientProtocol} · 规则 {ReadableRuleset(ClientRuleset)}\n卡牌 {ClientCardVersion} · 效果 {ClientEffectVersion}\n\n" +
                $"服务器：通信 {ServerProtocol} · 规则 {ReadableRuleset(ServerRuleset)}\n卡牌 {ServerCardVersion} · 效果 {ServerEffectVersion}\n\n" +
                "请更新客户端与服务器，使通信、规则、卡牌和效果版本一致，再点击匹配重试。\n" +
                "版本校验未通过，当前连接不能发送战场操作。\n" +
                "若正在恢复已有对局，请先完成更新，再重新连接查看对局状态。";
        }

        // Health metadata is untrusted; arbitrary labels are diagnostic data, not player copy.
        private static string ReadableRuleset(string value) => Regex.IsMatch(value ?? string.Empty,
            @"\A(?:prototype|release)-[0-9]{1,6}(?:\.[0-9]{1,6}){0,3}\z", RegexOptions.CultureInvariant)
            ? value : "未识别的版本标签";

        public static ServerCompatibilityFailure Create(NakamaConnectionSettings settings, MatchmakingPreferences preferences,
            int protocol, string ruleset, int cards, int effects)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            settings.Validate();
            if (protocol <= 0 || string.IsNullOrWhiteSpace(ruleset) || cards <= 0 || effects <= 0)
                throw new ArgumentException("Compatibility diagnostics require a complete, valid server version tuple.");
            var mismatch = preferences.GetIncompatibilityMessage(protocol, ruleset, cards, effects);
            return string.IsNullOrEmpty(mismatch) ? null : new ServerCompatibilityFailure(settings, preferences, protocol, ruleset, cards, effects, mismatch);
        }
    }

    public sealed class ServerCompatibilityException : InvalidOperationException
    {
        public ServerCompatibilityFailure Failure { get; }
        public ServerCompatibilityException(ServerCompatibilityFailure failure) : base(failure?.DiagnosticText)
        { Failure = failure ?? throw new ArgumentNullException(nameof(failure)); }
    }
}
