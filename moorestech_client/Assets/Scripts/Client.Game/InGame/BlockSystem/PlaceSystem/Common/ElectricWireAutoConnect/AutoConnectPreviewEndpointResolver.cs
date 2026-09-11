using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 自動接続プレビュー線の両端を実描画と同じ計算式で解決する
    /// Resolves both ends of auto-connect preview wires with the same calculation as the actual rendering
    /// </summary>
    public static class AutoConnectPreviewEndpointResolver
    {
        // 接続先ブロックの端点。Viewが未生成の接続先は描かない
        // Target block endpoints; targets without a spawned view are skipped
        public static List<Vector3> ResolveTargets(List<(Vector3Int TargetPos, float Distance)> targets, BlockGameObjectDataStore blockDataStore)
        {
            var endpoints = new List<Vector3>(targets.Count);
            foreach (var target in targets)
            {
                if (blockDataStore.TryGetBlockGameObject(target.TargetPos, out var targetBlock))
                    endpoints.Add(ElectricWireEndpointResolver.Resolve(targetBlock));
            }
            return endpoints;
        }

        // 起点（設置予定ブロック自身）のゴースト端点。ゴースト未取得時のフォールバックはResolver内部に一本化されている
        // Origin (the block about to be placed) ghost endpoint; the ghost-unavailable fallback is centralized inside the resolver
        public static Vector3 ResolveOrigin(IPlacementPreviewBlockGameObjectController previewBlockController, int originIndex, PlaceInfo originInfo, BlockMasterElement blockMaster)
        {
            previewBlockController.TryGetPreviewBlock(originIndex, out var ghost);
            return ElectricWireEndpointResolver.ResolveFromGhost(ghost, originInfo, blockMaster);
        }
    }
}
