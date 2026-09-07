using NUnit.Framework;

namespace BiomeRivals.Content.Tests
{
    public sealed class CardContentRegistryTests
    {
        [Test]
        public void ShippedResourcesContainEveryRegisteredCardAndTheme()
        {
            CardContentLoader.ResetForTests();
            var registry = CardContentLoader.Load();

            Assert.That(registry.NameCount, Is.EqualTo(74));
            Assert.That(registry.ThemeCount, Is.EqualTo(7));
            Assert.That(registry.DefinitionCount, Is.EqualTo(74));
            Assert.That(registry.TextCount, Is.EqualTo(74));
            Assert.That(registry.TryGetName("pf_001", out var name), Is.True);
            Assert.That(name, Is.EqualTo("蜜蜂"));
            Assert.That(registry.TryGetTheme("nether", out _), Is.True);
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);
            Assert.That(temple.cardType, Is.EqualTo("STRUCTURE"));
            Assert.That(temple.health, Is.EqualTo(8));
            Assert.That(temple.buildingSlots, Is.EqualTo(2));
            Assert.That(temple.hasCraftingRecipe, Is.True);
            Assert.That(temple.recipeId, Is.EqualTo("recipe.db_007.01"));
            Assert.That(temple.craftingRecipe.Length, Is.EqualTo(2));
            Assert.That(temple.craftingRecipe[0].cardId, Is.EqualTo("db_002"));
            Assert.That(temple.craftingRecipe[1].cardId, Is.EqualTo("tk_006"));
            Assert.That(temple.craftedHealthBonus, Is.EqualTo(2));
            Assert.That(temple.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(temple.effectIds, Is.EqualTo(new[] { "effect.db_007.01" }));
            foreach (var tokenId in new[] { "tk_006", "tk_007", "tk_008" })
            {
                Assert.That(registry.TryGetDefinition(tokenId, out var excavationToken), Is.True);
                Assert.That(excavationToken.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"), tokenId);
                Assert.That(excavationToken.manualPlayAllowed, Is.False, tokenId);
            }
            Assert.That(registry.TryGetDefinition("db_002", out var suspiciousSand), Is.True);
            Assert.That(suspiciousSand.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(sacrifice.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(sacrifice.effectIds, Is.EqualTo(new[] { "effect.nt_006.01" }));
            Assert.That(registry.TryGetDefinition("si_001", out var snowball), Is.True);
            Assert.That(snowball.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(snowball.effectIds, Is.EqualTo(new[] { "effect.si_001.01" }));
            Assert.That(registry.TryGetDefinition("si_002", out var snowGolem), Is.True);
            Assert.That(snowGolem.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(snowGolem.effectIds, Is.EqualTo(new[] { "effect.si_002.01" }));
            Assert.That(registry.TryGetDefinition("tk_002", out var wheat), Is.True);
            Assert.That(wheat.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(wheat.effectIds, Is.EqualTo(new[] { "effect.tk_002.01" }));
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(ironGolem.keywords, Is.EqualTo(new[] { "TAUNT" }));
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(bee.keywords, Is.Empty);
            Assert.That(registry.TryGetText("db_007", out var templeText), Is.True);
            Assert.That(templeText.rulesText, Does.Contain("藏宝图"));
        }
    }
}
