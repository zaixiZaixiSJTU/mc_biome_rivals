using UnityEngine;

namespace BiomeRivals.Demo
{
    public sealed class DemoBattlefieldSlotTarget : MonoBehaviour
    {
        public bool Player { get; private set; }
        public DemoSlotKind Kind { get; private set; }
        public int Index { get; private set; }

        public void Configure(bool player, DemoSlotKind kind, int index)
        {
            Player = player;
            Kind = kind;
            Index = index;
        }
    }
}
