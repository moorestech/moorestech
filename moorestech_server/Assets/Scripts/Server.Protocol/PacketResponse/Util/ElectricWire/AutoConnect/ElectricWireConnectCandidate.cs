using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 自動接続選定コアへ渡すワイヤー端点候補。サーバー/クライアントが各自の状態から組み立てる
    /// A wire endpoint candidate fed into the selection core; each side builds it from its own state
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
