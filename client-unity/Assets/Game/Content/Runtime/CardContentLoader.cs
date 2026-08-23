using System;
using UnityEngine;

namespace BiomeRivals.Content
{
    public static class CardContentLoader
    {
        private const string NamesResource = "CardContent/card-name-registry.zh-CN.v1";
        private const string ThemesResource = "CardContent/card-theme-registry.v1";
        private const string DefinitionsResource = "CardContent/card-definition-registry.v1";
        private const string TextsResource = "CardContent/card-text-registry.zh-CN.v1";
        private static CardContentRegistry _current;

        public static CardContentRegistry Current => _current ??= Load();

        public static CardContentRegistry Load()
        {
            var names = Resources.Load<TextAsset>(NamesResource);
            var themes = Resources.Load<TextAsset>(ThemesResource);
            var definitions = Resources.Load<TextAsset>(DefinitionsResource);
            var texts = Resources.Load<TextAsset>(TextsResource);
            if (names == null) throw new InvalidOperationException($"Missing Resources/{NamesResource}.json");
            if (themes == null) throw new InvalidOperationException($"Missing Resources/{ThemesResource}.json");
            if (definitions == null) throw new InvalidOperationException($"Missing Resources/{DefinitionsResource}.json");
            if (texts == null) throw new InvalidOperationException($"Missing Resources/{TextsResource}.json");
            return new CardContentRegistry(names.text, themes.text, definitions.text, texts.text);
        }

        public static void ResetForTests() => _current = null;
    }
}
