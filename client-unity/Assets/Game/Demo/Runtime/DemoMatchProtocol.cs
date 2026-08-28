using System.Collections.Generic;

namespace BiomeRivals.Demo
{
    public enum DemoTurnPhase
    {
        Main,
        Combat
    }

    public enum DemoDrawOutcome
    {
        Drawn,
        Burned,
        Fatigue
    }

    public sealed class DemoDrawResult
    {
        public DemoDrawResult(DemoDrawOutcome outcome, string cardId, int fatigueDamage, string[] excavatedCardIds = null)
        {
            Outcome = outcome;
            CardId = cardId ?? string.Empty;
            FatigueDamage = fatigueDamage;
            ExcavatedCardIds = excavatedCardIds ?? System.Array.Empty<string>();
        }

        public DemoDrawOutcome Outcome { get; }
        public string CardId { get; }
        public int FatigueDamage { get; }
        public string[] ExcavatedCardIds { get; }
    }

    public sealed class DemoBattlefieldObject
    {
        public string InstanceId { get; set; }
        public string CardId { get; set; }
        public bool Player { get; set; }
        public DemoSlotKind SlotKind { get; set; }
        public int SlotIndex { get; set; }
        public int OccupiedSlots { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int SummonedRound { get; set; }
        public bool HasAttacked { get; set; }
        public string[] Keywords { get; set; } = System.Array.Empty<string>();
        public int TemporaryAttackModifier { get; set; }
        public int TemporaryAttackModifierExpiresOnRound { get; set; }

        public bool HasKeyword(string keyword) =>
            !string.IsNullOrEmpty(keyword) && System.Array.IndexOf(Keywords ?? System.Array.Empty<string>(), keyword) >= 0;
    }

    public interface IDemoMatchView
    {
        bool IsAuthoritative { get; }
        string PlayerFactionId { get; }
        string OpponentFactionId { get; }
        bool IsMulligan { get; }
        bool PlayerMulliganCompleted { get; }
        bool OpponentMulliganCompleted { get; }
        IReadOnlyList<string> Hand { get; }
        string[] UnitSlots { get; }
        string[] BuildingSlots { get; }
        string[] OpponentUnitSlots { get; }
        string[] OpponentBuildingSlots { get; }
        IReadOnlyList<DemoBattlefieldObject> PlayerBattlefield { get; }
        IReadOnlyList<DemoBattlefieldObject> OpponentBattlefield { get; }
        int ViewerIndex { get; }
        int DeckCount { get; }
        int BuriedCount { get; }
        int DiscardCount { get; }
        int OpponentHandCount { get; }
        int Round { get; }
        int MaxEnergy { get; }
        int Energy { get; }
        bool IsPlayerTurn { get; }
        DemoTurnPhase Phase { get; }
        int PlayerLife { get; }
        int PlayerArmor { get; }
        int OpponentLife { get; }
        bool IsFinished { get; }
        int Revision { get; }
        DemoBattlefieldObject GetObject(bool player, DemoSlotKind kind, int slotIndex);
        bool CanAttackWith(DemoBattlefieldObject attacker, out string message);
        bool CanAttackTarget(DemoBattlefieldObject target, string targetType, out string message);
    }

    public enum DemoCommandRejectionCode
    {
        None,
        InvalidCommand,
        RevisionMismatch,
        DuplicateCommand,
        NotActivePlayer,
        UnknownCard,
        CardNotInHand,
        InsufficientRedstone,
        InvalidPaymentMethod,
        MissingMaterials,
        InvalidTarget,
        SlotOccupied,
        WrongPhase,
        AttackerNotReady,
        TauntTargetRequired,
        EffectNotImplemented
    }

    public sealed class DemoCommandResult
    {
        private DemoCommandResult(bool accepted, DemoCommandRejectionCode code, string message, int revision)
        {
            Accepted = accepted;
            Code = code;
            Message = message;
            Revision = revision;
        }

        public bool Accepted { get; }
        public DemoCommandRejectionCode Code { get; }
        public string Message { get; }
        public int Revision { get; }

        public static DemoCommandResult Accept(string message, int revision) =>
            new DemoCommandResult(true, DemoCommandRejectionCode.None, message, revision);

        public static DemoCommandResult Reject(DemoCommandRejectionCode code, string message, int revision) =>
            new DemoCommandResult(false, code, message, revision);
    }
}
