using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     既存の直線ベルトを分岐の始点にできない理由
    ///     Why an existing straight belt cannot become a branch start
    /// </summary>
    public enum BeltRouteBranchBlockReason
    {
        None,
        NotStraightBelt,
        NoSplitter,
        SideInputOccupied,
        OtherSideHasReceiver,
    }

    /// <summary>
    ///     分岐の始点。既存ベルトは同じファミリーの分岐器へ置き換わり、経路は選んだ横の出口から出る
    ///     A branch start: the existing belt becomes the family's splitter and the route leaves through the chosen side exit
    /// </summary>
    public sealed class BeltRouteBranchOrigin
    {
        public readonly BlockGameObject Belt;
        public readonly BlockId SplitterBlockId;
        public readonly bool SideIsRight;

        private BeltRouteBranchOrigin(BlockGameObject belt, BlockId splitterBlockId, bool sideIsRight)
        {
            Belt = belt;
            SplitterBlockId = splitterBlockId;
            SideIsRight = sideIsRight;
        }

        public Vector3Int Cell => Belt.BlockPosInfo.OriginalPos;
        public BlockDirection BeltFacing => Belt.BlockPosInfo.BlockDirection;
        public BlockDirection SideDirection => SideIsRight ? BeltRouteDirection.Right(BeltFacing) : BeltRouteDirection.Left(BeltFacing);
        public BlockDirection OtherSideDirection => SideIsRight ? BeltRouteDirection.Left(BeltFacing) : BeltRouteDirection.Right(BeltFacing);

        // 周囲の既存ブロックの入出力辺だけを小さな箱に写して、横入力と反対側の受け手を調べる
        // Copies only the surrounding blocks' port edges into a tiny box to check side inputs and a receiver on the other side
        public static BeltRouteBranchBlockReason Judge(BlockGameObjectDataStore blocks, BlockGameObject belt, BeltConveyorHoldingBlock holding, bool sideIsRight, out BeltRouteBranchOrigin origin)
        {
            origin = null;
            if (holding.IsSplitter || !IsStraightBeltOfFamily(belt, holding)) return BeltRouteBranchBlockReason.NotStraightBelt;
            // silent-ok: 候補0件や曖昧なマスタはBeltSplitterCounterpartがログに残す / 理由行で分岐不可と分かる
            if (!BeltSplitterCounterpart.TryGetSplitterBlockId(belt.BlockId, out var splitterBlockId)) return BeltRouteBranchBlockReason.NoSplitter;

            var candidate = new BeltRouteBranchOrigin(belt, splitterBlockId, sideIsRight);
            var cell = candidate.Cell;
            var edges = new BeltRouteGrid(cell, Vector3Int.one);
            foreach (var neighbour in blocks.BlockGameObjectDictionary.Values)
            {
                if (ReferenceEquals(neighbour, belt) || !IsNeighbour(neighbour, cell)) continue;
                BeltRouteBlockEdgeScanner.Write(neighbour, edges);
            }

            var leftFace = BeltRouteEdge.FromFace(cell, BeltRouteDirection.Left(candidate.BeltFacing), cell.y);
            var rightFace = BeltRouteEdge.FromFace(cell, BeltRouteDirection.Right(candidate.BeltFacing), cell.y);
            if (edges.HasOutputEdge(leftFace) || edges.HasOutputEdge(rightFace)) return BeltRouteBranchBlockReason.SideInputOccupied;
            if (edges.HasInputEdge(BeltRouteEdge.FromFace(cell, candidate.OtherSideDirection, cell.y))) return BeltRouteBranchBlockReason.OtherSideHasReceiver;

            origin = candidate;
            return BeltRouteBranchBlockReason.None;
        }

        private static bool IsStraightBeltOfFamily(BlockGameObject belt, BeltConveyorHoldingBlock holding)
        {
            if (!BeltRouteBlockEdgeScanner.IsBelt(belt.BlockMasterElement)) return false;
            if (!holding.IsSameFamily(belt.BlockId)) return false;
            return belt.BlockId == holding.Family.StraightBlockId;
        }

        // 大きな機械の口も拾えるよう、原点ではなく占有範囲が2セル以内に掛かるものを隣とみなす
        // Treat a block as a neighbour when its footprint, not only its origin, comes within two cells, so large machines' ports are caught
        private static bool IsNeighbour(BlockGameObject block, Vector3Int cell)
        {
            const int reach = 2;
            var min = block.BlockPosInfo.MinPos;
            var max = block.BlockPosInfo.MaxPos;
            return min.x - reach <= cell.x && cell.x <= max.x + reach &&
                   min.y - reach <= cell.y && cell.y <= max.y + reach &&
                   min.z - reach <= cell.z && cell.z <= max.z + reach;
        }
    }
}
