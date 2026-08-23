using System;

namespace BiomeRivals.Presentation
{
    [Serializable]
    public sealed class CardFaceViewModel
    {
        public string cardId = string.Empty;
        public string themeId = string.Empty;
        public string rulesText = string.Empty;
        public string typeText = string.Empty;
        public int cost;
        public bool showAttack;
        public int attack;
        public bool showHealth;
        public int health;
        public bool showDurability;
        public int durability;
    }
}
