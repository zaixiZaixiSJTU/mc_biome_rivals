using System.Collections.Generic;
using BiomeRivals.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoCardPaperTests
    {
        // Independent measurements of the approved bitmap, not values read back from the provider.
        private static readonly Dictionary<string, Vector2> MeasuredWidths = new Dictionary<string, Vector2>
        {
            { "plains_forest", new Vector2(158, 218) }, { "desert_badlands", new Vector2(162, 220) },
            { "snow_ice", new Vector2(156, 220) }, { "cave_dark_forest", new Vector2(155, 219) },
            { "ocean_river", new Vector2(157, 221) }, { "nether", new Vector2(157, 221) },
            { "end", new Vector2(160, 222) }
        };

        [TestCase(166, 216, true, 2f / 3f)]
        [TestCase(250, 430, false, 2f / 3f)]
        [TestCase(360, 480, false, 2f / 3f)]
        [TestCase(166, 216, true, 1f)]
        [TestCase(250, 430, false, 1f)]
        [TestCase(360, 480, false, 1f)]
        [TestCase(166, 216, true, 1024f / 1920f)]
        [TestCase(250, 430, false, 1024f / 1920f)]
        [TestCase(360, 480, false, 1024f / 1920f)]
        public void All74CardsUseMeasuredPaperAndKeepFullRules(int width, int height, bool compact, float scale)
        {
            var registry = CardContentLoader.Load();
            var document = JsonUtility.FromJson<CardDefinitionRegistryDocument>(
                Resources.Load<TextAsset>("CardContent/card-definition-registry.v1").text);
            Assert.That(document.entries, Has.Length.EqualTo(74));
            var canvasRoot = new GameObject("PaperContract", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRoot.GetComponent<Canvas>().scaleFactor = scale;
            var cardRoot = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardUI));
            cardRoot.transform.SetParent(canvasRoot.transform, false);
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 20);
            var themes = new HashSet<string>();
            try
            {
                var card = cardRoot.GetComponent<CardUI>();
                foreach (var definition in document.entries)
                {
                    card.Bind(registry, definition.id, new Vector2(width, height), compact, font, null,
                        definition.cost, "paper-instance");
                    registry.TryGetText(definition.id, out var registered);
                    themes.Add(definition.themeId);
                    var text = cardRoot.transform.Find("Rules").GetComponent<Text>();
                    var rect = text.rectTransform;
                    var measured = MeasuredWidths[definition.themeId];
                    Assert.That(rect.sizeDelta.x, Is.EqualTo(width * measured.x / measured.y).Within(0.01f), definition.id);
                    Assert.That(rect.sizeDelta.y, Is.EqualTo(height * 104f / 520f).Within(0.01f), definition.id);
                    Assert.That(rect.anchoredPosition.x, Is.Zero.Within(0.01f), definition.id);
                    Assert.That(rect.anchoredPosition.y - rect.rect.height / 2f,
                        Is.EqualTo(height * (93f / 520f - 0.5f)).Within(0.01f), definition.id);
                    Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                    Assert.That(text.supportRichText, Is.False);
                    Assert.That(text.resizeTextMinSize * scale, Is.GreaterThanOrEqualTo(12f));
                    var settings = text.GetGenerationSettings(rect.rect.size);
                    settings.resizeTextForBestFit = false;
                    settings.fontSize = text.resizeTextMinSize;
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    Assert.That(new TextGenerator().GetPreferredHeight(text.text, settings) / text.pixelsPerUnit,
                        Is.LessThanOrEqualTo(rect.rect.height + 0.5f), definition.id);
                    Assert.That(card.FullRulesText, Is.EqualTo(registered.rulesText), definition.id);
                    if (card.HasRulesPreview) Assert.That(text.text, Does.EndWith("…"), definition.id);
                    else Assert.That(text.text, Is.EqualTo(registered.rulesText), definition.id);
                    card.SetRulesSummary(false);
                    Assert.That(rect.sizeDelta.y, Is.EqualTo(height * 0.2f).Within(0.01f), "Disabling a summary must not enlarge the physical paper.");
                    Assert.That(card.HandCardInstanceId, Is.EqualTo("paper-instance"));
                    Assert.That(card.DisplayedCost, Is.EqualTo(definition.cost));
                    Assert.That(cardRoot.GetComponent<Image>().sprite.name, Is.EqualTo("CardFrame_" + definition.themeId));
                }
                Assert.That(themes.Count, Is.EqualTo(7));
            }
            finally
            {
                Object.DestroyImmediate(canvasRoot);
                Object.DestroyImmediate(font);
            }
        }
    }
}
