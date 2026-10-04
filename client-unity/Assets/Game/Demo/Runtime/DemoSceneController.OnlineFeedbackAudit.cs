using System;
using System.Collections;
using System.Linq;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IEnumerator PrepareOnlineFeedbackCapture(string sample)
        {
            if (IsOnlineBoard) throw new InvalidOperationException("Reading samples must not alter a live online match.");
            var revision = MatchView.Revision;
            var energy = MatchView.Energy;
            var identities = MatchView.HandCards.Select(card => card.handCardInstanceId).ToArray();
            if (sample == "Compatibility")
            {
                var failure = ServerCompatibilityFailure.Create(new NakamaConnectionSettings
                    { host = "biome-rivals.preview.example.test", port = 17350, serverKey = "sample-private-key" },
                    new MatchmakingPreferences(FactionIds.PlainsForest, _registry.ContentVersion, _registry.ImplementedEffectRegistryVersion),
                    39, "prototype-0.64", 41, 47);
                HandleOnlineConnectionState(new MatchConnectionStatus(MatchConnectionPhase.Failed,
                    "Bearer sample-private-token", compatibilityFailure: failure));
            }
            else if (sample == "Deployment")
                HandleOnlineCommandCompleted(new MatchCommandDispatchResult("sample-private-command", MatchCommandOutcome.Rejected,
                    "INVALID_TARGET", "goat battlecry requires the opposite adjacent unit slot to be empty", 999999));
            else if (sample == "Timeout")
                HandleOnlineCommandCompleted(new MatchCommandDispatchResult("sample-private-command", MatchCommandOutcome.TimedOut,
                    "ACK_TIMEOUT", "Bearer sample-private-token", 999999));
            else throw new InvalidOperationException("Unknown online feedback reading sample.");
            var expected = _statusSummary.FullText;
            if (expected.Contains("sample-private") || expected.Contains("999999"))
                throw new InvalidOperationException("Private sample metadata leaked into player reading text.");
            _titleText.text = "联机提示阅读 · 本地验收示例";
            yield return null;
            yield return null;
            if (!ClickButtonThroughEventSystem(_statusInspectionButton)) throw new InvalidOperationException("Feedback reading entry click failed.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null; // Let UGUI LateUpdate settle its play-mode scrollbar visibility.
            if (!_statusInspectionOpen || _statusInspectionBody.text != expected || _handCanvasGroup.interactable ||
                GetComponent<DemoBattlefieldPointerController>().InputEnabled)
                throw new InvalidOperationException("Feedback full text or gameplay input locks failed.");
            var needsScroll = _statusInspectionScroll.content.rect.height > _statusInspectionScroll.viewport.rect.height + 0.01f;
            if (_statusInspectionScroll.verticalScrollbar.gameObject.activeSelf != needsScroll)
                throw new InvalidOperationException("Actual Player scrollbar visibility does not match its reading content.");
            if (_statusInspectionScroll.content.rect.height > _statusInspectionScroll.viewport.rect.height)
            {
                var before = _statusInspectionScroll.verticalNormalizedPosition;
                ExecuteEvents.Execute(_statusInspectionScroll.gameObject, new PointerEventData(EventSystem.current)
                    { scrollDelta = new Vector2(0f, -5f) }, ExecuteEvents.scrollHandler);
                yield return null;
                if (_statusInspectionScroll.verticalNormalizedPosition >= before) throw new InvalidOperationException("Feedback reading wheel failed.");
            }
            if (!ClickButtonThroughEventSystem(_statusInspectionClose)) throw new InvalidOperationException("Feedback reading return click failed.");
            yield return null;
            if (_statusInspectionOpen || !_handCanvasGroup.interactable || !GetComponent<DemoBattlefieldPointerController>().InputEnabled ||
                MatchView.Revision != revision || MatchView.Energy != energy ||
                !MatchView.HandCards.Select(card => card.handCardInstanceId).SequenceEqual(identities))
                throw new InvalidOperationException("Feedback return did not restore legal input or changed game state.");
            if (!ClickButtonThroughEventSystem(_statusInspectionButton)) throw new InvalidOperationException("Feedback reading reopen click failed.");
            yield return null;
            AuditResponsiveCanvasBounds();
            _titleText.text = "联机提示阅读 · 本地验收示例";
            _statusInspectionTitle.text = "操作提示 · 本地验收示例";
            Debug.Log($"Online feedback reading settled: True; sample {sample}; actual entry/return/reopen; localized full text; unchanged revision/energy/hand; private metadata hidden.");
        }
#else
        private IEnumerator PrepareOnlineFeedbackCapture(string sample)
        { throw new NotSupportedException("Online feedback reading diagnostics are unavailable in release builds."); }
#endif
    }
}
