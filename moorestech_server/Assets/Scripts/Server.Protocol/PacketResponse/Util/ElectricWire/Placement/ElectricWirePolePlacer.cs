using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.Placement
{
    /// <summary>
    /// 電線ツール経由の電柱設置。設置とワイヤー端点の解決だけを行い、支払いは呼び出し側が持つ
    /// Places a pole for the wire tool; only places and resolves the wire endpoint, leaving payment to the caller
    /// </summary>
    public static class ElectricWirePolePlacer
    {
        public static bool TryPlace(PlaceInfoMessagePack polePlaceInfo, BlockId blockId, out IElectricWireConnector selfConnector)
        {
            // ブロックを設置しワイヤー端点を解決する
            // Place the block and resolve its wire connector component
            selfConnector = null;
            var createParams = polePlaceInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, polePlaceInfo.Position, polePlaceInfo.Direction, createParams, out var placedBlock)) return false;

            selfConnector = placedBlock.GetComponent<IElectricWireConnector>();
            return true;
        }
    }
}
