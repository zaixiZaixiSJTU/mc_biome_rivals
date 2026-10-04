using System.Reflection;
using BiomeRivals.Bootstrap;
using BiomeRivals.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineStatusLayoutTests
    {
        [TestCase(39, "prototype-0.64", 42, 48)]
        [TestCase(40, "prototype-0.64", 42, 48)]
        [TestCase(40, "prototype-0.65", 41, 48)]
        [TestCase(40, "prototype-0.65", 42, 47)]
        public void TypedCompatibilityFailureShowsTargetAndActionableThreeLineHint(int protocol, string ruleset, int cards, int effects)
        {
            var root = new GameObject("CompatibilityHintLayoutTest");
            var instance = typeof(GameCompositionRoot).GetProperty("Instance");
            var previous = instance.GetValue(null);
            try
            {
                instance.GetSetMethod(true).Invoke(null, new object[] { null });
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var failure = ServerCompatibilityFailure.Create(
                    new NakamaConnectionSettings { host = "localhost", port = 17350, serverKey = "test-key" },
                    new MatchmakingPreferences(BiomeRivals.Core.FactionIds.PlainsForest, 42, 48), protocol, ruleset, cards, effects);
                var status = new MatchConnectionStatus(MatchConnectionPhase.Failed, failure.DiagnosticText, compatibilityFailure: failure);
                typeof(DemoSceneController).GetMethod("HandleOnlineConnectionState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { status });
                Canvas.ForceUpdateCanvases();
                var hint = root.transform.Find("DemoCanvas/StatusPlate/Status").GetComponent<Text>();
                Assert.That(hint.text, Is.EqualTo(failure.UserSummary));
                Assert.That(hint.text, Does.Contain("localhost:17350"));
                Assert.That(hint.text, Does.Contain("更新两端"));
                Assert.That(hint.cachedTextGenerator.lineCount, Is.EqualTo(3), "No accidental fourth wrapped line may hide the remedy.");
                Assert.That(hint.cachedTextGenerator.vertexCount, Is.GreaterThan(0));
                Assert.That(hint.preferredHeight, Is.LessThanOrEqualTo(hint.rectTransform.rect.height));
                Assert.That((bool)typeof(DemoSceneController).GetMethod("VerifyOnlineStatusTextLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null), Is.True);
                Assert.That(root.transform.Find("DemoCanvas/OnlineStatusPanel/OnlineAction").GetComponentInChildren<Text>().text, Is.EqualTo("匹配"));
                Assert.That(status.CanSendCommands, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                instance.GetSetMethod(true).Invoke(null, new[] { previous });
            }
        }

        [TestCase("权威对局已连接")]
        [TestCase("权威对局\n已连接\n多余\n一行")]
        public void LayoutVerifierRejectsOriginalOrVerticallyTruncatedText(string overflowingText)
        {
            var root = new GameObject("OnlineStatusOverflowControl");
            var instance = typeof(GameCompositionRoot).GetProperty("Instance");
            var previous = instance.GetValue(null);
            try
            {
                instance.GetSetMethod(true).Invoke(null, new object[] { null });
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                root.transform.Find("DemoCanvas/OnlineStatusPanel/Status").GetComponent<Text>().text = overflowingText;
                Assert.That((bool)typeof(DemoSceneController).GetMethod("VerifyOnlineStatusTextLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null), Is.False, "The gate must reject original automatic wrap and vertical truncation, not merely nonempty text.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                instance.GetSetMethod(true).Invoke(null, new[] { previous });
            }
        }

        [TestCase(MatchConnectionPhase.Offline, "", 0, "本地模式", "匹配")]
        [TestCase(MatchConnectionPhase.Authenticating, "", 0, "身份认证中", "取消")]
        [TestCase(MatchConnectionPhase.Authenticating, "Checking server gameplay", 0, "校验客户端\n版本", "取消")]
        [TestCase(MatchConnectionPhase.Connecting, "", 0, "连接服务器", "取消")]
        [TestCase(MatchConnectionPhase.Matchmaking, "", 0, "寻找对手中\n平原", "取消")]
        [TestCase(MatchConnectionPhase.Joining, "", 0, "进入\n权威对局", "取消")]
        [TestCase(MatchConnectionPhase.Ready, "", 0, "权威对局\n已连接", "断开")]
        [TestCase(MatchConnectionPhase.Reconnecting, "", 1, "正在重连\n第 1 次", "取消")]
        [TestCase(MatchConnectionPhase.Reconnecting, "", 999, "正在重连\n第 999 次", "取消")]
        [TestCase(MatchConnectionPhase.Failed, "", 0, "连接失败", "匹配")]
        [TestCase(MatchConnectionPhase.Failed, "version mismatch", 0, "版本不兼容\n检查两端", "匹配")]
        [TestCase(MatchConnectionPhase.Disconnecting, "", 0, "正在断开", "取消")]
        public void StatusUsesIntentionalLinesWithoutReducingFontOrOverlappingOtherColumns(
            MatchConnectionPhase phase, string detail, int attempt, string expected, string action)
        {
            var root = new GameObject("OnlineStatusLayoutTest");
            try
            {
                var controller = root.AddComponent<DemoSceneController>();
                var instance = typeof(GameCompositionRoot).GetProperty("Instance");
                var previous = instance.GetValue(null);
                instance.GetSetMethod(true).Invoke(null, new object[] { null });
                try { controller.BuildNow(); }
                finally { instance.GetSetMethod(true).Invoke(null, new[] { previous }); }
                typeof(DemoSceneController).GetMethod("HandleOnlineConnectionState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { new MatchConnectionStatus(phase, detail, "match-test", attempt) });
                var panel = root.transform.Find("DemoCanvas/OnlineStatusPanel");
                var status = panel.Find("Status").GetComponent<Text>();
                var button = panel.Find("OnlineAction").GetComponent<Button>();
                Assert.That(status.text, Is.EqualTo(expected));
                Assert.That(button.GetComponentInChildren<Text>().text, Is.EqualTo(action));
                Assert.That((bool)typeof(DemoSceneController).GetMethod("VerifyOnlineStatusTextLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null), Is.True, "Every explicit line must fit with no automatic extra line or hidden text.");
                Assert.That(status.fontSize, Is.EqualTo(15));
                Assert.That(status.resizeTextForBestFit, Is.False);
                var statusRect = status.rectTransform;
                var accountRect = panel.Find("Account").GetComponent<RectTransform>();
                var actionRect = button.GetComponent<RectTransform>();
                Assert.That(accountRect.anchoredPosition.x + accountRect.rect.width / 2,
                    Is.LessThan(statusRect.anchoredPosition.x - statusRect.rect.width / 2));
                Assert.That(statusRect.anchoredPosition.x + statusRect.rect.width / 2,
                    Is.LessThan(actionRect.anchoredPosition.x - actionRect.rect.width / 2));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
