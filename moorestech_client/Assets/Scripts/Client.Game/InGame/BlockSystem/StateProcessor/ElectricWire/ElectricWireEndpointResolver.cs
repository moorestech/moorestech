using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 電線の端点座標を解決する唯一の正。実描画・全プレビューがこれを共有する
    /// The single source of truth for wire endpoint positions, shared by rendering and all previews
    /// </summary>
    public static class ElectricWireEndpointResolver
    {
        /// <summary>
        /// 専用接続点があればそこへ、無ければブロック上面中央へ接続する
        /// Connect to the dedicated point when present, otherwise to the block top center
        /// </summary>
        public static Vector3 Resolve(BlockGameObject block)
        {
            var connectionPoint = block.GetComponentInChildren<ElectricWireConnectionPoint>(true);
            if (connectionPoint != null) return connectionPoint.transform.position;

            var min = block.BlockPosInfo.MinPos;
            var max = block.BlockPosInfo.MaxPos + Vector3Int.one;
            return new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
        }

        /// <summary>
        /// 未設置ゴーストの端点を解決する。ゴースト内のマーカー→無ければ設置予定AABBの上面中央
        /// Resolve an unplaced ghost's endpoint: the marker inside the ghost, else the planned AABB top center
        /// </summary>
        public static Vector3 ResolveFromGhost(BlockPreviewObject ghost, PlaceInfo placeInfo, BlockMasterElement blockMaster)
        {
            var connectionPoint = ghost.GetComponentInChildren<ElectricWireConnectionPoint>(true);
            if (connectionPoint != null) return connectionPoint.transform.position;

            var ghostInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, blockMaster.BlockSize);
            var min = ghostInfo.MinPos;
            var max = ghostInfo.MaxPos + Vector3Int.one;
            return new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
        }
    }
}
