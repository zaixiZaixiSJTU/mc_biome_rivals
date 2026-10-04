using BiomeRivals.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoResponsiveLayoutTests
    {
        [TestCase(1424, 714)]
        [TestCase(1024, 768)]
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1080)]
        [TestCase(1080, 1920)]
        public void AuthoredUiBoundsFitTheSameCenteredViewportAsWorldCamera(int width, int height)
        {
            var root = new GameObject("ResponsiveLayoutContract");
            var property = typeof(GameCompositionRoot).GetProperty("Instance");
            var previous = property.GetValue(null);
            try
            {
                property.GetSetMethod(true).Invoke(null, new object[] { null });
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var canvas = root.transform.Find("DemoCanvas");
                var scaler = canvas.GetComponent<CanvasScaler>();
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
                var reference = scaler.referenceResolution;
                var scale = Mathf.Min(width / reference.x, height / reference.y);
                var normalized = DemoBattlefield3D.CalculateAspectViewport(width / (float)height);
                var viewport = new Rect(normalized.x * width, normalized.y * height, normalized.width * width, normalized.height * height);
                Assert.That(viewport.width, Is.EqualTo(reference.x * scale).Within(0.002f));
                Assert.That(viewport.height, Is.EqualTo(reference.y * scale).Within(0.002f));
                foreach (var name in new[] { "TitlePlate", "FactionRail", "HandPlate", "CardDetailsPanel", "EndTurnButton", "InspectHand", "OnlineStatusPanel" })
                {
                    var rect = canvas.Find(name).GetComponent<RectTransform>();
                    var min = new Vector2(width, height) / 2f + (rect.anchoredPosition - rect.rect.size / 2f) * scale;
                    var max = new Vector2(width, height) / 2f + (rect.anchoredPosition + rect.rect.size / 2f) * scale;
                    Assert.That(min.x, Is.GreaterThanOrEqualTo(viewport.xMin - 0.002f), name);
                    Assert.That(min.y, Is.GreaterThanOrEqualTo(viewport.yMin - 0.002f), name);
                    Assert.That(max.x, Is.LessThanOrEqualTo(viewport.xMax + 0.002f), name);
                    Assert.That(max.y, Is.LessThanOrEqualTo(viewport.yMax + 0.002f), name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                property.GetSetMethod(true).Invoke(null, new[] { previous });
            }
        }

        [Test]
        public void OriginalAverageScaleReproducesInspectionCenterBelowWindow()
        {
            var oldScale = Mathf.Sqrt((1424f / 1920f) * (714f / 1080f));
            Assert.That(714f / 2f - 510f * oldScale, Is.LessThan(0f));
            var fitScale = Mathf.Min(1424f / 1920f, 714f / 1080f);
            Assert.That(714f / 2f - (510f + 16f) * fitScale, Is.GreaterThan(8f));
        }
    }
}
