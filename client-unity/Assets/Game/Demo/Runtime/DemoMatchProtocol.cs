namespace BiomeRivals.Demo
{
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
        SlotOccupied
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
