using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomeRivals.Content
{
    [Serializable]
    public sealed class CardNameRegistryDocument
    {
        public int schemaVersion;
        public string locale = string.Empty;
        public CardNameEntry[] entries = Array.Empty<CardNameEntry>();
    }

    [Serializable]
    public sealed class CardNameEntry
    {
        public string id = string.Empty;
        public string designId = string.Empty;
        public string nameKey = string.Empty;
        public string name = string.Empty;
        public bool collectible;
    }

    [Serializable]
    public sealed class CardThemeRegistryDocument
    {
        public int schemaVersion;
        public string layoutId = string.Empty;
        public CardThemeDefinition[] themes = Array.Empty<CardThemeDefinition>();
    }

    [Serializable]
    public sealed class CardThemeDefinition
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string frameBase = string.Empty;
        public string frameDark = string.Empty;
        public string accent = string.Empty;
        public string rulesSurface = string.Empty;
        public string titleText = string.Empty;
        public string bodyText = string.Empty;
        public string motif = string.Empty;
    }

    [Serializable]
    public sealed class CardDefinitionRegistryDocument
    {
        public int schemaVersion;
        public int contentVersion;
        public CardDefinitionEntry[] entries = Array.Empty<CardDefinitionEntry>();
    }

    [Serializable]
    public sealed class CraftingIngredientEntry
    {
        public string cardId = string.Empty;
        public int count;
    }

    [Serializable]
    public sealed class CardDefinitionEntry
    {
        public string id = string.Empty;
        public string designId = string.Empty;
        public int contentVersion;
        public bool collectible;
        public string nameKey = string.Empty;
        public string rulesTextKey = string.Empty;
        public string factionId = string.Empty;
        public string themeId = string.Empty;
        public string rarity = string.Empty;
        public string cardType = string.Empty;
        public int cost;
        public bool hasAttack;
        public int attack;
        public bool hasHealth;
        public int health;
        public bool hasDurability;
        public int durability;
        public int buildingSlots;
        public string artKey = string.Empty;
        public string[] tags = Array.Empty<string>();
        public string[] keywords = Array.Empty<string>();
        public bool hasCraftingRecipe;
        public string recipeId = string.Empty;
        public CraftingIngredientEntry[] craftingRecipe = Array.Empty<CraftingIngredientEntry>();
        public int craftedAttackBonus;
        public int craftedHealthBonus;
        public int craftedDurabilityBonus;
        public string effectImplementationStatus = string.Empty;
        public string[] effectIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CardTextRegistryDocument
    {
        public int schemaVersion;
        public string locale = string.Empty;
        public CardTextEntry[] entries = Array.Empty<CardTextEntry>();
    }

    [Serializable]
    public sealed class CardTextEntry
    {
        public string id = string.Empty;
        public string nameKey = string.Empty;
        public string name = string.Empty;
        public string rulesTextKey = string.Empty;
        public string rulesText = string.Empty;
        public string typeLabel = string.Empty;
        public string rarityLabel = string.Empty;
        public string[] tagLabels = Array.Empty<string>();
        public string designNotes = string.Empty;
    }

    public readonly struct CardThemePalette
    {
        public readonly string Id;
        public readonly Color FrameBase;
        public readonly Color FrameDark;
        public readonly Color Accent;
        public readonly Color RulesSurface;
        public readonly Color TitleText;
        public readonly Color BodyText;

        internal CardThemePalette(CardThemeDefinition definition)
        {
            Id = definition.id;
            FrameBase = ParseColor(definition.frameBase, definition.id, nameof(definition.frameBase));
            FrameDark = ParseColor(definition.frameDark, definition.id, nameof(definition.frameDark));
            Accent = ParseColor(definition.accent, definition.id, nameof(definition.accent));
            RulesSurface = ParseColor(definition.rulesSurface, definition.id, nameof(definition.rulesSurface));
            TitleText = ParseColor(definition.titleText, definition.id, nameof(definition.titleText));
            BodyText = ParseColor(definition.bodyText, definition.id, nameof(definition.bodyText));
        }

        private static Color ParseColor(string value, string themeId, string field)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color)) return color;
            throw new FormatException($"Theme '{themeId}' contains invalid color '{value}' in {field}.");
        }
    }

    public sealed class CardContentRegistry
    {
        private readonly Dictionary<string, CardNameEntry> _names =
            new Dictionary<string, CardNameEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, CardThemePalette> _themes =
            new Dictionary<string, CardThemePalette>(StringComparer.Ordinal);
        private readonly Dictionary<string, CardDefinitionEntry> _definitions =
            new Dictionary<string, CardDefinitionEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, CardTextEntry> _texts =
            new Dictionary<string, CardTextEntry>(StringComparer.Ordinal);

        public int NameCount => _names.Count;
        public int ThemeCount => _themes.Count;
        public int DefinitionCount => _definitions.Count;
        public int TextCount => _texts.Count;

        public CardContentRegistry(
            string nameRegistryJson,
            string themeRegistryJson,
            string definitionRegistryJson,
            string textRegistryJson)
        {
            if (string.IsNullOrWhiteSpace(nameRegistryJson)) throw new ArgumentException("Name registry JSON is required.", nameof(nameRegistryJson));
            if (string.IsNullOrWhiteSpace(themeRegistryJson)) throw new ArgumentException("Theme registry JSON is required.", nameof(themeRegistryJson));
            if (string.IsNullOrWhiteSpace(definitionRegistryJson)) throw new ArgumentException("Definition registry JSON is required.", nameof(definitionRegistryJson));
            if (string.IsNullOrWhiteSpace(textRegistryJson)) throw new ArgumentException("Text registry JSON is required.", nameof(textRegistryJson));

            var names = JsonUtility.FromJson<CardNameRegistryDocument>(nameRegistryJson);
            var themes = JsonUtility.FromJson<CardThemeRegistryDocument>(themeRegistryJson);
            var definitions = JsonUtility.FromJson<CardDefinitionRegistryDocument>(definitionRegistryJson);
            var texts = JsonUtility.FromJson<CardTextRegistryDocument>(textRegistryJson);
            if (names?.entries == null) throw new FormatException("Card name registry has no entries.");
            if (themes?.themes == null) throw new FormatException("Card theme registry has no themes.");
            if (definitions?.entries == null) throw new FormatException("Card definition registry has no entries.");
            if (texts?.entries == null) throw new FormatException("Card text registry has no entries.");
            if (definitions.schemaVersion != 3) throw new FormatException($"Unsupported card definition schema version: {definitions.schemaVersion}.");

            foreach (var entry in names.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.name))
                    throw new FormatException("Card name entry is incomplete.");
                if (!_names.TryAdd(entry.id, entry)) throw new FormatException($"Duplicate card id: {entry.id}");
            }

            foreach (var definition in themes.themes)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                    throw new FormatException("Card theme entry is incomplete.");
                if (!_themes.TryAdd(definition.id, new CardThemePalette(definition)))
                    throw new FormatException($"Duplicate card theme id: {definition.id}");
            }

            foreach (var definition in definitions.entries)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id) ||
                    string.IsNullOrWhiteSpace(definition.nameKey) || string.IsNullOrWhiteSpace(definition.themeId))
                    throw new FormatException("Card definition entry is incomplete.");
                definition.keywords ??= Array.Empty<string>();
                var registeredKeywords = new HashSet<string>(StringComparer.Ordinal);
                foreach (var keyword in definition.keywords)
                {
                    if (keyword != "TAUNT" && keyword != "CHARGE")
                        throw new FormatException($"Card '{definition.id}' has unsupported keyword '{keyword}'.");
                    if (!registeredKeywords.Add(keyword))
                        throw new FormatException($"Card '{definition.id}' repeats keyword '{keyword}'.");
                }
                if (!_definitions.TryAdd(definition.id, definition))
                    throw new FormatException($"Duplicate card definition id: {definition.id}");
            }

            foreach (var text in texts.entries)
            {
                if (text == null || string.IsNullOrWhiteSpace(text.id) || string.IsNullOrWhiteSpace(text.name))
                    throw new FormatException("Card text entry is incomplete.");
                if (!_texts.TryAdd(text.id, text)) throw new FormatException($"Duplicate card text id: {text.id}");
            }

            ValidateMatchingCardSets();
        }

        public bool TryGetName(string cardId, out string name)
        {
            if (_names.TryGetValue(cardId, out var entry))
            {
                name = entry.name;
                return true;
            }
            name = string.Empty;
            return false;
        }

        public bool TryGetTheme(string themeId, out CardThemePalette theme) =>
            _themes.TryGetValue(themeId, out theme);

        public bool TryGetDefinition(string cardId, out CardDefinitionEntry definition) =>
            _definitions.TryGetValue(cardId, out definition);

        public bool TryGetText(string cardId, out CardTextEntry text) =>
            _texts.TryGetValue(cardId, out text);

        private void ValidateMatchingCardSets()
        {
            if (_names.Count != _definitions.Count || _names.Count != _texts.Count)
                throw new FormatException("Card name, definition and text registries have different counts.");

            foreach (var cardId in _names.Keys)
            {
                if (!_definitions.ContainsKey(cardId) || !_texts.ContainsKey(cardId))
                    throw new FormatException($"Card registries are inconsistent at id: {cardId}");
            }
        }
    }
}
