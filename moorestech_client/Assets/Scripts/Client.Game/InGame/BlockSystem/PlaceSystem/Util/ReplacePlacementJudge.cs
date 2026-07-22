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
        public static Vector3Int ResolveReplaceCell(BlockGameObjectDataStore store, BlockId holdingBlockId, Vector3Int cell)
        {
            // 天面レイヒットで1段上に浮いた座標を、直下の同一ファミリーブロックへ引き戻す
            // Pull a coordinate floated one step up by a top-face ray hit back down to the same-family block directly below
            if (IsSameFamilyBlockAt(store, holdingBlockId, cell)) return cell;

            var below = cell + Vector3Int.down;
            if (IsSameFamilyBlockAt(store, holdingBlockId, below)) return below;

            return cell;
        }

        public static bool IsReplaceDragStart(BlockGameObjectDataStore store, BlockId holdingBlockId, Vector3Int startCell)
        {
            // 起点セルの既存ブロックが手持ちと同一ファミリーならリプレースドラッグ開始
            // Begin the replace drag when the start cell holds a block in the same family as the held one
            return IsSameFamilyBlockAt(store, holdingBlockId, startCell);
        }

        public static bool TryMarkReplace(BlockGameObjectDataStore store, PlaceInfo info)
        {
            // セルに既存ブロックが無ければ触らない（地面埋没・坂不可等の不可理由を復活させない）
            // Leave the cell untouched when empty (do not revive ground/slope unplaceable reasons)
            if (!store.TryGetBlockGameObject(info.Position, out var existingBlock)) return false;

            // 設置ブロックと別ファミリーの既存ブロックにはリプレース不可
            // An existing block outside the placing block's family cannot be replaced
            if (!BlockReplaceFamilyUtil.IsSameReplaceFamily(existingBlock.BlockId, info.BlockId)) return false;

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

        private static bool IsSameFamilyBlockAt(BlockGameObjectDataStore store, BlockId holdingBlockId, Vector3Int cell)
        {
            // セルに既存ブロックがあり、それが手持ちと同一リプレースファミリーか
            // Whether the cell has an existing block sharing a replace family with the held block
            return store.TryGetBlockGameObject(cell, out var existingBlock) && BlockReplaceFamilyUtil.IsSameReplaceFamily(existingBlock.BlockId, holdingBlockId);
        }
    }
}
