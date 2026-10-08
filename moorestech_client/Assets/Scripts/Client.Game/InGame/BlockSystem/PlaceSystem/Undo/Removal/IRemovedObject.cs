using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Core.Master;
using UnityEngine;

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

        // ブロック相: 再設置セルを積む。戻せなければ位置を skippedBlockPositions へ足して理由を返す
        // Block phase: append cells to re-place; if blocked, add the position to skippedBlockPositions and return a reason
        BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy, HashSet<Vector3Int> skippedBlockPositions);

        // 接続相: 再設置後に線を引き直す。戻らなかったブロックの端点を含む線は送らず false
        // Connection phase: re-draw lines after the re-place; returns false without sending when an endpoint block was not restored
        bool TrySendConnectionRestore(IRemovalRestoreSender sender, HashSet<Vector3Int> skippedBlockPositions);
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
