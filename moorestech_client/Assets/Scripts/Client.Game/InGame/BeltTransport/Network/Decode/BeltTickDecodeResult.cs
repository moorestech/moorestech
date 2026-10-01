using Core.BeltTransport;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltTickDecodeResult
    {
        public BeltTickDifference Difference { get; }
        public string FailureReason { get; }
        public bool Succeeded => FailureReason == null;
        internal BeltTickDecodeResult(BeltTickDifference difference, string failureReason)
        { Difference = difference; FailureReason = failureReason; }
    }
}
