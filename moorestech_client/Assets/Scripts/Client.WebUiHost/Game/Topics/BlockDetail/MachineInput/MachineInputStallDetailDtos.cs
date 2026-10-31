using Client.Game.InGame.BlockInfoPolling;
using Client.WebUiHost.Game.Topics.BlockDetail.FetchState;

namespace Client.WebUiHost.Game.Topics.BlockDetail.MachineInput
{
    /// <summary>
    ///     機械の搬入が継承元のレベルで止まっている理由と、届いて待っている品。組み立ては factory だけ
    ///     Why a machine's inflow is stalled on the inheritance source level, and the item that arrived and waits; built only through the factories
    /// </summary>
    public class MachineInputStallDetailDto
    {
        // fetched / notFetched / transportFailed / unreadable / unsupportedTarget / vanished
        public readonly string FetchState;

        // fetched のときだけ: mixedLevel / aboveCraftCap と待っている品
        // Only when fetched: mixedLevel / aboveCraftCap and the waiting item
        public readonly string Reason;
        public readonly int? ItemId;

        private MachineInputStallDetailDto(string fetchState, string reason, int? itemId)
        {
            FetchState = fetchState;
            Reason = reason;
            ItemId = itemId;
        }

        public static MachineInputStallDetailDto CreateStalled(string reason, int itemId)
        {
            return new MachineInputStallDetailDto(BlockInfoFetchStateWireName.Fetched, reason, itemId);
        }

        // 取得できていない間は取得状態だけを載せ、理由と品を作らない
        // While not fetched, carry only the fetch state and make up no reason or item
        public static MachineInputStallDetailDto CreateUnavailable(BlockInfoFetchState fetchState)
        {
            return new MachineInputStallDetailDto(BlockInfoFetchStateWireName.ToUnavailableWireName(fetchState), null, null);
        }
    }
}
