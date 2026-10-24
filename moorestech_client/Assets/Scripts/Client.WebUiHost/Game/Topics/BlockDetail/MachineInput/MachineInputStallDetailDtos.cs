namespace Client.WebUiHost.Game.Topics.BlockDetail.MachineInput
{
    /// <summary>
    ///     機械の搬入が継承元のレベルで止まっている理由と、届いて待っている品
    ///     Why a machine's inflow is stalled on the inheritance source level, and the item that arrived and waits
    /// </summary>
    public class MachineInputStallDetailDto
    {
        // mixedLevel / aboveCraftCap
        public string Reason;
        public int ItemId;
    }
}
