using System;
using System.Reflection;
using BiomeRivals.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoCardIdentityTests
    {
        [TestCase(166, 216, true, 2f / 3f)]
        [TestCase(250, 430, false, 2f / 3f)]
        [TestCase(360, 480, false, 2f / 3f)]
        [TestCase(166, 216, true, 1f)]
        [TestCase(250, 430, false, 1f)]
        [TestCase(360, 480, false, 1f)]
        [TestCase(166, 216, true, 1024f / 1920f)]
        [TestCase(250, 430, false, 1024f / 1920f)]
        [TestCase(360, 480, false, 1024f / 1920f)]
        public void All74CardNamesAndStatsAreCompleteReadableAndSeparated(int width, int height, bool compact, float scale)
        {
            WithCard(scale, (card, registry, font) =>
            {
                var definitions = JsonUtility.FromJson<CardDefinitionRegistryDocument>(
                    Resources.Load<TextAsset>("CardContent/card-definition-registry.v1").text).entries;
                Assert.That(definitions, Has.Length.EqualTo(74));
                foreach (var definition in definitions)
                {
                    card.Bind(registry, definition.id, new Vector2(width, height), compact, font, null,
                        definition.cost, "identity-instance");
                    registry.TryGetText(definition.id, out var registered);
                    var name = Label(card, "Name");
                    var cost = Label(card, "Cost");
                    var type = Label(card, "Type");
                    Assert.That(name.text, Is.EqualTo(registered.name), definition.id);
                    Assert.That(type.text, Is.EqualTo(registered.typeLabel), definition.id);
                    Assert.That(cost.text, Is.EqualTo(definition.cost.ToString()), definition.id);
                    AssertFits(name, scale, 12f, false, definition.id);
                    AssertFits(type, scale, 12f, true, definition.id);
                    AssertFits(cost, scale, 14f, true, definition.id);
                    var footer = card.transform.Find("TypeSurface");
                    if (!definition.hasAttack && !definition.hasHealth && !definition.hasDurability)
                    {
                        Assert.That(footer, Is.Not.Null, definition.id);
                        var image = footer.GetComponent<Image>();
                        Assert.That(image.sprite.name, Is.EqualTo("CardTitleSurface_" + definition.themeId));
                        Assert.That(image.raycastTarget, Is.False);
                        Assert.That(image.rectTransform.rect.width, Is.GreaterThan(type.rectTransform.rect.width));
                        Assert.That(image.rectTransform.rect.height, Is.GreaterThan(type.rectTransform.rect.height));
                    }
                    else Assert.That(footer, Is.Null, "A material footer must never cover real attack/health/durability.");
                    var socket = card.transform.Find("CostSocketFrame").GetComponent<RectTransform>();
                    Assert.That(name.rectTransform.anchoredPosition.x + name.rectTransform.rect.xMin,
                        Is.GreaterThan(socket.anchoredPosition.x + socket.rect.xMax), definition.id);
                    if (compact)
                    {
                        var surface = card.transform.Find("NameSurface").GetComponent<Image>();
                        Assert.That(surface.sprite.name, Is.EqualTo("CardTitleSurface_" + definition.themeId));
                        Assert.That(surface.raycastTarget, Is.False);
                        var art = card.transform.Find("ArtSurface").GetComponent<RectTransform>();
                        Assert.That(art.anchoredPosition.y + art.rect.yMax,
                            Is.LessThan(surface.rectTransform.anchoredPosition.y + surface.rectTransform.rect.yMin));
                        var rules = Label(card, "Rules");
                        Assert.That(art.anchoredPosition.y + art.rect.yMin,
                            Is.GreaterThan(rules.rectTransform.anchoredPosition.y + rules.rectTransform.rect.yMax));
                    }
                    foreach (var stat in new[] { "Attack", "Health", "Durability" })
                    {
                        var child = card.transform.Find(stat);
                        if (child == null) continue;
                        var label = child.GetComponent<Text>();
                        AssertFits(label, scale, 14f, true, definition.id);
                        Assert.That(label.text, Is.EqualTo((stat == "Attack" ? definition.attack :
                            stat == "Health" ? definition.health : definition.durability).ToString()), definition.id);
                        Assert.That(label.GetComponent<Outline>(), Is.Not.Null);
                        var bounds = LocalBounds(label);
                        Assert.That(bounds.Overlaps(LocalBounds(type)), Is.False, definition.id + " stat/type must not overlap");
                    }
                    Assert.That(card.HandCardInstanceId, Is.EqualTo("identity-instance"));
                    Assert.That(card.FullRulesText, Is.EqualTo(registered.rulesText));
                }
            });
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        [TestCase(1024f / 1920f)]
        public void DiscountBadgeRetainsLiteralReductionAndDoesNotCoverName(float scale)
        {
            WithCard(scale, (card, registry, font) =>
            {
                card.Bind(registry, "tk_017", new Vector2(166, 216), true, font, null, 0, "discount-instance");
                var modifier = Label(card, "CostModifier");
                Assert.That(modifier.text, Is.EqualTo("-" + card.BaseCost));
                AssertFits(modifier, scale, 12f, true, "discount");
                AssertFits(Label(card, "Cost"), scale, 14f, true, "discount");
                Assert.That(LocalBounds(modifier).Overlaps(LocalBounds(Label(card, "Name"))), Is.False);
                card.Bind(registry, "pf_001", new Vector2(166, 216), true, font, null);
                Assert.That(card.transform.Find("CostModifier"), Is.Null, "Rebinding must clear a stale discount.");
            });
        }

        [Test]
        public void ResizingRaisesAndRestoresIdentityFontsWithoutCumulativeGrowth()
        {
            WithCard(1f, (card, registry, font) =>
            {
                card.Bind(registry, "pf_001", new Vector2(166, 216), true, font, null);
                var canvas = card.GetComponentInParent<Canvas>();
                foreach (var scale in new[] { 2f / 3f, 1024f / 1920f, 1f, 2f / 3f, 1f })
                {
                    canvas.scaleFactor = scale;
                    typeof(CardUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, null);
                    AssertFits(Label(card, "Name"), scale, 12f, false, "resize");
                    foreach (var key in new[] { "Cost", "Attack", "Health" }) AssertFits(Label(card, key), scale, 14f, true, "resize");
                    AssertFits(Label(card, "Type"), scale, 12f, true, "resize");
                    if (Mathf.Approximately(scale, 1f))
                    {
                        Assert.That(Label(card, "Cost").fontSize, Is.EqualTo(18));
                        Assert.That(Label(card, "Type").fontSize, Is.EqualTo(14));
                        Assert.That(Label(card, "Attack").fontSize, Is.EqualTo(18));
                    }
                }
            });
        }

        private static Text Label(CardUI card, string key) => card.transform.Find(key).GetComponent<Text>();
        private static Rect LocalBounds(Text text) => new Rect(text.rectTransform.anchoredPosition + text.rectTransform.rect.min, text.rectTransform.rect.size);

        private static void AssertFits(Text text, float scale, float minimumPixels, bool singleLine, string context)
        {
            var size = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
            Assert.That(size * scale, Is.GreaterThanOrEqualTo(minimumPixels), context + ": " + text.name);
            Assert.That(text.supportRichText, Is.False);
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = size;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            if (singleLine) settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            var generator = new TextGenerator();
            Assert.That(generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit,
                Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f), context + ": " + text.name + " height");
            if (singleLine) Assert.That(generator.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit,
                Is.LessThanOrEqualTo(text.rectTransform.rect.width + 0.5f), context + ": " + text.name + " width");
        }

        private static void WithCard(float scale, Action<CardUI, CardContentRegistry, Font> run)
        {
            var canvasRoot = new GameObject("IdentityContract", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRoot.GetComponent<Canvas>().scaleFactor = scale;
            var cardRoot = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardUI));
            cardRoot.transform.SetParent(canvasRoot.transform, false);
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 20);
            try { run(cardRoot.GetComponent<CardUI>(), CardContentLoader.Load(), font); }
            finally { UnityEngine.Object.DestroyImmediate(canvasRoot); UnityEngine.Object.DestroyImmediate(font); }
        }
    }
}
