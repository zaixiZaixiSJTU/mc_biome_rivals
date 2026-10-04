using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BiomeRivals.Content;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // A labelled local fixture, not a server projection or an alternate rules path.
        private void SetupHudResourceScenario()
        {
            SelectFaction("nether");
            SelectOpponentFaction("ocean_river");
            _match.ResetOpponent(Array.Empty<CardDefinitionEntry>());
            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetDeckAndHand(new[] { "nt_007", "nt_007", "or_006" }, Array.Empty<string>());
            if (!_registry.TryGetDefinition("nt_007", out var anchor) ||
                !_registry.TryGetDefinition("or_006", out var trident))
                throw new InvalidOperationException("Registered HUD fixture cards are missing.");
            var anchorInstances = _match.HandCards.Take(2).Select(card => card.handCardInstanceId).ToArray();
            for (var slot = 0; slot < 2; slot++)
            {
                var result = _match.ApplyDeploy(anchor, _match.CreateDeployCommand(anchor.id, DemoSlotKind.Building, slot,
                    handCardInstanceId: anchorInstances[slot]));
                if (!result.Accepted) throw new InvalidOperationException("HUD anchor deployment failed: " + result.Message);
            }
            var equip = _match.ApplyPlayCard(trident, _match.CreatePlayCardCommand(trident.id));
            if (!equip.Accepted) throw new InvalidOperationException("HUD equipment command failed: " + equip.Message);
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn(); // Empty-deck fatigue legally triggers both anchors' temporary energy.
            if (_match.TemporaryEnergy != 2 || _match.Energy != 12 || _match.MaxEnergy != 10 || _match.PlayerEquipment?.CardId != trident.id)
                throw new InvalidOperationException("HUD fixture did not reach equipped / temporary-resource state.");
            var buried = Enumerable.Repeat("tk_007", 10).ToArray();
            _match.ResetDeckAndHand(new[] { "nt_004", "nt_005", "nt_006", "nt_001", "nt_002" }, buried, buried);
            SelectCard("nt_004");
            RefreshAll();
            ShowStatus("资源与阅读验收示例：已装备激流三叉戟；两座重生锚提供临时红石 +2；牌库包含 10 张掩埋牌。", false);
        }

        private void AuditHudResourceReading()
        {
            if (MatchView.PlayerEquipment?.CardId != "or_006" || MatchView.TemporaryEnergy != 2 ||
                MatchView.BuriedCount != 10 || !_energyText.text.Contains("12/10") ||
                !_energyText.text.Contains("临时 +2") || !_playerEquipmentText.text.Contains("耐久 3/3") ||
                !_handLabel.text.Contains("掩埋 10"))
                throw new InvalidOperationException("Actual equipped / temporary / buried HUD does not match the local rules state.");
            AuditResponsiveCanvasBounds();
        }

        private IEnumerator PrepareHudResourceCapture(bool showNotes)
        {
            SetupHudResourceScenario();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            AuditHudResourceReading();
            var revision = MatchView.Revision;
            var instances = MatchView.HandCards.Select(card => card.handCardInstanceId).ToArray();
            var expectedNotes = GetCardNotesText();
            if (string.IsNullOrWhiteSpace(expectedNotes) || !ClickButtonThroughEventSystem(_cardNotesButton))
                throw new InvalidOperationException("Actual card-notes entry click failed.");
            yield return null;
            if (!_statusInspectionOpen || !_readingCardNotes || _statusInspectionBody.text != expectedNotes ||
                _handCanvasGroup.interactable || GetComponent<DemoBattlefieldPointerController>().InputEnabled)
                throw new InvalidOperationException("Card-notes full text or modal input locks failed.");
            if (!ClickButtonThroughEventSystem(_statusInspectionClose)) throw new InvalidOperationException("Actual card-notes return click failed.");
            yield return null;
            if (_statusInspectionOpen || !_handCanvasGroup.interactable || !GetComponent<DemoBattlefieldPointerController>().InputEnabled)
                throw new InvalidOperationException("Card-notes return did not restore legal input.");
            if (MatchView.Revision != revision || !MatchView.HandCards.Select(card => card.handCardInstanceId).SequenceEqual(instances))
                throw new InvalidOperationException("Reading changed gameplay state or private hand identities.");
            if (showNotes)
            {
                if (!ClickButtonThroughEventSystem(_cardNotesButton)) throw new InvalidOperationException("Actual card-notes reopen click failed.");
                yield return null;
            }
            AuditHudResourceReading();
            Debug.Log("HUD resource reading settled: True; equipped trident; temporary +2; buried 10; actual notes open/return; unchanged revision/hand; input restored.");
        }
#else
        private IEnumerator PrepareHudResourceCapture(bool showNotes)
        {
            throw new NotSupportedException("HUD reading capture diagnostics are unavailable in release builds.");
        }
#endif
    }
}
