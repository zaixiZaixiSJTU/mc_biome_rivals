using System;
using BiomeRivals.Networking;
using NUnit.Framework;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineFeedbackTests
    {
        private const string PrivateData = "Bearer secret-token; {\"deviceId\":\"private-device\"}; D:\\private\\credentials.json";

        [TestCase("INVALID_TARGET", "goat battlecry requires the opposite adjacent unit slot to be empty", "另一侧")]
        [TestCase("INVALID_TARGET", "ender dragon avatar cannot be returned by its controller's spell", "材料不受")]
        [TestCase("INVALID_TARGET", "darkness restricts the first enemy battlefield target to a legal row edge", "首次")]
        [TestCase("INVALID_TARGET", "strider requires a burning friendly character while one is available", "着火友军")]
        [TestCase("INVALID_TARGET", "building cards require enough consecutive building slots", "连续建筑格")]
        [TestCase("INVALID_TARGET", "material requires a friendly unit target", "己方生物")]
        [TestCase("INVALID_TARGET", "blaze rod requires an enemy unit target", "敌方生物")]
        [TestCase("INVALID_TARGET", "snow spell requires an enemy unit target", "粉雪桶")]
        [TestCase("INVALID_CHOICE", "the selected movement destination is not available", "目的地")]
        [TestCase("CARD_NOT_IN_HAND", "card is not in the actor hand", "重新选择")]
        [TestCase("MISSING_MATERIALS", "crafting recipe materials are missing from hand", "配方材料")]
        [TestCase("INSUFFICIENT_REDSTONE", "not enough redstone", "红石不足")]
        public void RegisteredGameplayReasonsRetainTheirSpecificMeaning(string code, string source, string meaning)
        {
            Assert.That(DemoOnlineFeedback.TryGetRegisteredReason(code, source, out var detail), Is.True);
            Assert.That(detail, Does.Contain(meaning));
            var text = DemoOnlineFeedback.FormatCommand(new MatchCommandDispatchResult("private-command", MatchCommandOutcome.Rejected, code, source, 123));
            Assert.That(text, Does.Contain(detail));
            Assert.That(text, Does.Not.Contain(code).And.Not.Contain(source).And.Not.Contain("private-command"));
        }

        [TestCase(MatchCommandOutcome.Accepted)]
        [TestCase(MatchCommandOutcome.TimedOut)]
        [TestCase(MatchCommandOutcome.TransportFailed)]
        [TestCase(MatchCommandOutcome.Rejected)]
        public void UntrustedRawMetadataNeverBecomesPlayerCopy(MatchCommandOutcome outcome)
        {
            var text = DemoOnlineFeedback.FormatCommand(new MatchCommandDispatchResult(PrivateData, outcome, PrivateData, PrivateData, 12345));
            Assert.That(text, Does.Not.Contain("secret-token").And.Not.Contain("private-device")
                .And.Not.Contain("credentials.json").And.Not.Contain("12345"));
            if (outcome == MatchCommandOutcome.Accepted) Assert.That(text, Is.EqualTo("操作已确认。"));
            else if (outcome == MatchCommandOutcome.Rejected) Assert.That(text, Does.Contain("尚未识别"));
            else Assert.That(text, Does.Contain("结果待确认").And.Contain("同步").And.Not.Contain("未生效"));
        }

        [Test]
        public void DuplicateFallbackDoesNotFalselyClaimTheOperationDidNotExecute()
        {
            var text = DemoOnlineFeedback.FormatCommand(new MatchCommandDispatchResult("id", MatchCommandOutcome.Rejected,
                "DUPLICATE_COMMAND", PrivateData, 2));
            Assert.That(text, Does.Contain("已经处理").And.Not.Contain("未生效").And.Not.Contain("secret-token"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExceptionsAreLocalizedWithoutLeakingTheirMessages(bool connecting)
        {
            var text = DemoOnlineFeedback.FormatException(new InvalidOperationException(PrivateData), connecting);
            Assert.That(text, Does.Not.Contain("secret-token").And.Not.Contain("private-device").And.Not.Contain("credentials.json"));
            Assert.That(text, Does.Contain(connecting ? "检查网络" : "结果待确认"));
        }

        [Test]
        public void CancelledSendStillRequiresReconciliationBecauseItMayHaveExecuted()
        {
            var cancelled = new OperationCanceledException(PrivateData);
            Assert.That(DemoOnlineFeedback.FormatException(cancelled, false), Does.Contain("结果待确认").And.Contain("同步"));
            Assert.That(DemoOnlineFeedback.FormatException(cancelled, true), Does.Contain("连接已取消"));
        }
    }
}
