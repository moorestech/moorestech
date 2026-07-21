using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface.Extension;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// リプレースドラッグの開始判定と、各セルのリプレース設置マーキングを行う
    /// Judges replace-drag start and marks each cell for replace placement
    /// </summary>
    public static class ReplacePlacementJudge
    {
        public static bool IsReplaceDragStart(BlockGameObjectDataStore store, BlockId holdingBlockId, Vector3Int startCell)
        {
            // 手持ちがリプレースファミリーでなければ通常設置のまま
            // Stay in normal placement when the held block is not a replace family
            if (!BlockReplaceFamilyUtil.IsReplaceFamily(holdingBlockId)) return false;

            // 起点セルに既存ブロックが無ければリプレースドラッグではない
            // Not a replace drag when the start cell has no existing block
            if (!store.TryGetBlockGameObject(startCell, out var existingBlock)) return false;

            // 既存ブロックもファミリーならリプレースドラッグ開始
            // Begin the replace drag when the existing block is also a replace family
            return BlockReplaceFamilyUtil.IsReplaceFamily(existingBlock.BlockId);
        }

        public static bool TryMarkReplace(BlockGameObjectDataStore store, PlaceInfo info)
        {
            // セルに既存ブロックが無ければ触らない（地面埋没・坂不可等の不可理由を復活させない）
            // Leave the cell untouched when empty (do not revive ground/slope unplaceable reasons)
            if (!store.TryGetBlockGameObject(info.Position, out var existingBlock)) return false;

            // 非ファミリーの既存ブロックにはリプレース不可
            // A non-family existing block cannot be replaced
            if (!BlockReplaceFamilyUtil.IsReplaceFamily(existingBlock.BlockId)) return false;

            // 同BlockID・同向きは無変化のためno-op（送信されないようPlaceable=false）
            // Same block id and direction is a no-op (keep Placeable=false so it is not sent)
            if (existingBlock.BlockId == info.BlockId && existingBlock.BlockPosInfo.BlockDirection == info.Direction)
            {
                info.Placeable = false;
                return true;
            }

            // リプレース対象として設置可へ復活させる
            // Revive the cell as a replace target
            info.IsReplace = true;
            info.Placeable = true;
            return true;
        }
    }
}
