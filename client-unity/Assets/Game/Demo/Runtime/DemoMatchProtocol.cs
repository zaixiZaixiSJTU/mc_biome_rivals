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
        public DemoDrawResult(DemoDrawOutcome outcome, string cardId, int fatigueDamage)
        {
            Outcome = outcome;
            CardId = cardId ?? string.Empty;
            FatigueDamage = fatigueDamage;
        }

        public DemoDrawOutcome Outcome { get; }
        public string CardId { get; }
        public int FatigueDamage { get; }
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
        InvalidTarget,
        SlotOccupied,
        WrongPhase,
        AttackerNotReady,
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
