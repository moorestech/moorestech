using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 選定コアへ渡す端点候補。サーバー/クライアント各自組立
    /// An endpoint candidate for the selection core; each side builds it
    /// </summary>
    public readonly struct ElectricWireConnectCandidate
    {
        public readonly BlockInstanceId InstanceId;
        public readonly IBlockParam BlockParam;
        public readonly BlockPositionInfo PositionInfo;
        public readonly int CurrentConnectionCount;

        public ElectricWireConnectCandidate(BlockInstanceId instanceId, IBlockParam blockParam, BlockPositionInfo positionInfo, int currentConnectionCount)
        {
            InstanceId = instanceId;
            BlockParam = blockParam;
            PositionInfo = positionInfo;
            CurrentConnectionCount = currentConnectionCount;
        }
    }
}
