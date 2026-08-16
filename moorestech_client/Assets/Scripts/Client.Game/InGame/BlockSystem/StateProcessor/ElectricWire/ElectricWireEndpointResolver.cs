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

            return ResolveAabbTopCenter(block.BlockPosInfo);
        }

        /// <summary>
        /// 未設置ゴーストの端点を解決する。ゴースト内のマーカー→無ければ設置予定AABBの上面中央
        /// Resolve an unplaced ghost's endpoint: the marker inside the ghost, else the planned AABB top center
        /// </summary>
        public static Vector3 ResolveFromGhost(BlockPreviewObject ghost, PlaceInfo placeInfo, BlockMasterElement blockMaster)
        {
            // ghost==nullはTryGetPreviewBlockがfalseを返した正当な「未生成」通知であり、防御的nullチェックではない
            // ghost==null is the legitimate "not yet spawned" signal from TryGetPreviewBlock returning false, not a defensive guard
            var connectionPoint = ghost != null ? ghost.GetComponentInChildren<ElectricWireConnectionPoint>(true) : null;
            if (connectionPoint != null) return connectionPoint.transform.position;

            var ghostInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, blockMaster.BlockSize);
            return ResolveAabbTopCenter(ghostInfo);
        }

        /// <summary>
        /// 設置範囲AABBの上面中央座標を求める唯一の式
        /// The single formula for the placed-range AABB's top center
        /// </summary>
        private static Vector3 ResolveAabbTopCenter(BlockPositionInfo positionInfo)
        {
            var min = positionInfo.MinPos;
            var max = positionInfo.MaxPos + Vector3Int.one;
            return new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
        }
    }
}
