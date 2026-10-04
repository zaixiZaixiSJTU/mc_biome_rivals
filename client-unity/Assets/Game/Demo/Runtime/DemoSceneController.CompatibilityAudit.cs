using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BiomeRivals.Bootstrap;
using BiomeRivals.Networking;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable]
        private sealed class CompatibilityAuditReport
        {
            public bool success, authoritativeStateAbsent, realtimeSocketAbsent, matchmakerAbsent, matchAbsent;
            public bool canSendCommands, canIssueCommand, localStateUnchanged, headerLayoutValid, hintLayoutValid;
            public string phase, kind, endpoint, clientRuleset, serverRuleset, header, hint;
            public int clientProtocol, serverProtocol, clientCards, serverCards, clientEffects, serverEffects;
            public int localRevisionBefore, localRevisionAfter, screenWidth, screenHeight;
        }

        // Explicit expected-negative audit: never replaces the ordinary successful-match probe.
        private async Task AuditExpectedCompatibilityFailure(string reportPath, string capturePath)
        {
            if (string.IsNullOrWhiteSpace(reportPath) || string.IsNullOrWhiteSpace(capturePath))
                throw new ArgumentException("Compatibility audit requires both report and screenshot paths.");
            if (File.Exists(reportPath) || File.Exists(capturePath))
                throw new IOException("Refusing to overwrite compatibility audit evidence.");
            var revision = _match.Revision;
            var deadline = Time.realtimeSinceStartup + 30f;
            while (_onlineGateway?.CurrentStatus.Phase != MatchConnectionPhase.Failed && Time.realtimeSinceStartup < deadline)
                await Task.Yield();
            if (_onlineGateway?.CurrentStatus.Phase != MatchConnectionPhase.Failed)
                throw new TimeoutException("Expected compatibility failure was not observed within 30 seconds.");
            var status = _onlineGateway.CurrentStatus;
            var failure = status.CompatibilityFailure;
            if (failure == null) throw new InvalidOperationException("A generic network/authentication failure is not compatibility evidence.");
            // The coroutine/UI callback and generated glyphs must settle before inspecting or capturing them.
            var frame = Time.frameCount;
            while (Time.frameCount < frame + 2) await Task.Yield();
            Canvas.ForceUpdateCanvases();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var gateway = _onlineGateway as AuthoritativeMatchGateway ?? throw new InvalidOperationException("Expected the real authoritative gateway.");
            var transportField = typeof(AuthoritativeMatchGateway).GetField("_transport", flags)
                ?? throw new MissingFieldException("Authoritative gateway transport diagnostic field is missing.");
            var transport = transportField.GetValue(gateway) as NakamaMatchTransport
                ?? throw new InvalidOperationException("Expected the real Nakama transport, not a fixture.");
            bool IsAbsent(string field)
            {
                var info = typeof(NakamaMatchTransport).GetField(field, flags)
                    ?? throw new MissingFieldException("Transport diagnostic field is missing: " + field);
                return info.GetValue(transport) == null;
            }
            var report = new CompatibilityAuditReport
            {
                phase = status.Phase.ToString(), kind = failure.Kind.ToString(), endpoint = failure.Endpoint,
                clientProtocol = failure.ClientProtocol, serverProtocol = failure.ServerProtocol,
                clientRuleset = failure.ClientRuleset, serverRuleset = failure.ServerRuleset,
                clientCards = failure.ClientCardVersion, serverCards = failure.ServerCardVersion,
                clientEffects = failure.ClientEffectVersion, serverEffects = failure.ServerEffectVersion,
                authoritativeStateAbsent = GameCompositionRoot.Instance.MatchStateStore.Current == null && !_onlineSession.HasAuthoritativeState,
                realtimeSocketAbsent = IsAbsent("_socket"), matchmakerAbsent = IsAbsent("_ticket") && IsAbsent("_matchedSource"),
                matchAbsent = IsAbsent("_match"), canSendCommands = status.CanSendCommands, canIssueCommand = _onlineSession.CanIssueCommand,
                localRevisionBefore = revision, localRevisionAfter = _match.Revision, localStateUnchanged = revision == _match.Revision,
                header = _onlineStatusText.text, hint = _statusText.text,
                headerLayoutValid = VerifyOnlineStatusTextLayout(),
                hintLayoutValid = _statusText.text == failure.UserSummary && _statusText.cachedTextGenerator.lineCount == 3 &&
                    _statusText.preferredHeight <= _statusText.rectTransform.rect.height,
                screenWidth = Screen.width, screenHeight = Screen.height
            };
            report.success = report.authoritativeStateAbsent && report.realtimeSocketAbsent && report.matchmakerAbsent && report.matchAbsent &&
                !report.canSendCommands && !report.canIssueCommand && report.localStateUnchanged && report.headerLayoutValid && report.hintLayoutValid;
            if (!report.success) throw new InvalidOperationException("Compatibility preflight/UI invariants failed: " + JsonUtility.ToJson(report));
            WriteDemoScreenshot(capturePath);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("Unity expected compatibility failure audit passed: " + failure.Endpoint);
        }
#else
        private Task AuditExpectedCompatibilityFailure(string reportPath, string capturePath) =>
            throw new NotSupportedException("Compatibility audit is unavailable in release builds.");
#endif
    }
}
