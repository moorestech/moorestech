using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     削除ツールで消えた1つの物（ブロック・接続線・レール）。種類を問わず同じ形でUndo履歴に載る
    ///     One thing removed by the delete tool (block, connection line, rail), held in undo history in the same shape regardless of kind
    /// </summary>
    public interface IRemovedObject
    {
        // 同じ物を二重に復元しないための論理キー
        // Logical key preventing the same thing from being restored twice
        object RestoreKey { get; }

        // ブロック相: 再設置すべきセルを積む。ブロック以外はNotABlock、占有で戻せなければSkippedOccupied
        // Block phase: append the cell to re-place; non-blocks return NotABlock, an occupied footprint returns SkippedOccupied
        BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy);

        // 接続相: ブロック再設置の送信後に線の引き直しを送る（ブロックは何もしない）
        // Connection phase: send the line restore after the block re-place has been sent (blocks do nothing)
        void SendConnectionRestore(IRemovalRestoreSender sender);
    }

    /// <summary>
    ///     ブロック相1件の結果
    ///     Outcome of one block-phase append
    /// </summary>
    public enum BlockRestoreOutcome
    {
        NotABlock,
        Appended,
        SkippedOccupied,
    }
}
