using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     削除で消えた1つの物。種別不問でUndo履歴に載る
    ///     One removed thing; any kind shares one undo shape
    /// </summary>
    public interface IRemovedObject
    {
        // 同じ物を二重に復元しないための論理キー
        // Logical key preventing the same thing from being restored twice
        object RestoreKey { get; }

        // ブロック相: 再設置セルを積む。戻せなければ理由を返す
        // Block phase: append cells to re-place; return a reason if blocked
        BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy);

        // 接続相: 再設置後に線を引き直す
        // Connection phase: re-draw lines after the block re-place
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
        AlreadyPresent,
        SkippedOccupied,
    }
}
