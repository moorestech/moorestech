using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction
{
    /// <summary>
    ///     チュートリアルの鉱脈限定中、対象ブロックを対象鉱脈の外に置けなくする（クライアント側の設置制限。サーバーは弾かない）
    ///     While a tutorial restricts placement to a vein, blocks the target block outside that vein (client-side only; the server does not reject it)
    /// </summary>
    public static class VeinRestrictedPlacementReporter
    {
        public static void MarkOutsideTargetVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState state, PlacementFeedback feedback)
        {
            // 制限が無い時と制限対象でないブロックは素通しする
            // Pass through when no restriction is active or the held block is not the restricted one
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            if (!state.IsRestrictedBlock(holdingBlockId)) return;
            var targetVeinGuid = state.VeinGuid.Value;

            // 判定セルは採掘機ならドリル、他は原点。MinerVeinPlacementReporter と同じ導出
            // The judged cell is the drill for miners and the origin otherwise, derived the same way as MinerVeinPlacementReporter
            var lastDirection = (BlockDirection?)null;
            var judgeOffsetFromOrigin = Vector3Int.zero;

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                if (lastDirection != placeInfo.Direction)
                {
                    lastDirection = placeInfo.Direction;
                    judgeOffsetFromOrigin = ResolveJudgeOffset(placeInfo.Direction);
                }

                if (veinAabbRegistry.IsInsideVein(placeInfo.Position + judgeOffsetFromOrigin, targetVeinGuid)) continue;

                placeInfo.Placeable = false;
                if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein));
            }

            #region Internal

            Vector3Int ResolveJudgeOffset(BlockDirection direction)
            {
                if (holdingBlockMaster.BlockParam is not IMinerParam minerParam) return Vector3Int.zero;
                var originPositionInfo = new BlockPositionInfo(Vector3Int.zero, direction, holdingBlockMaster.BlockSize);
                return originPositionInfo.ConvertBlockLocalToWorldCell(minerParam.DrillLocalPosition);
            }

            #endregion
        }
    }
}
