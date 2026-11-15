using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    /// <summary>
    ///     分岐の始点
    ///     既存ベルトを同ファミリー分岐器へ置換
    ///     幾何が選んだ出口から経路
    ///     A branch start
    ///     The existing belt is replaced by the family's splitter
    ///     The route leaves through the exit the geometry chose
    /// </summary>
    public sealed class BeltRouteBranchOrigin
    {
        public readonly BlockGameObject Belt;
        public readonly BlockId SplitterBlockId;
        private readonly BeltRouteBranchShape _shape;

        private BeltRouteBranchOrigin(BlockGameObject belt, BlockId splitterBlockId, BeltRouteBranchShape shape)
        {
            Belt = belt;
            SplitterBlockId = splitterBlockId;
            _shape = shape;
        }

        public Vector3Int Cell => Belt.BlockPosInfo.OriginalPos;
        public BlockDirection SplitterFacing => _shape.SplitterFacing;
        public BlockDirection ExitDirection => _shape.BranchExit;

        // 隣の入出力辺を入れ物へ写す
        // Copies the neighbours' port edges into a holder
        public static BeltRouteBranchBlockReason Judge(BlockGameObjectDataStore blocks, BlockGameObject belt, BeltConveyorHoldingBlock holding, bool useAlternateExit, out BeltRouteBranchOrigin origin)
        {
            origin = null;
            // silent-ok: 理由行「分岐は同じ種類の直線ベルトからだけ作れます」で分岐不可と分かる
            if (holding.IsSplitter || !IsStraightBeltOfFamily(belt, holding)) return BeltRouteBranchBlockReason.NotStraightBelt;
            // silent-ok: 候補0件や曖昧なマスタはBeltSplitterCounterpartがログに残す / 理由行で分岐不可と分かる
            if (!BeltSplitterCounterpart.TryGetSplitterBlockId(belt.BlockId, out var splitterBlockId)) return BeltRouteBranchBlockReason.NoSplitter;

            var cell = belt.BlockPosInfo.OriginalPos;
            var edges = new BeltRouteNeighbourEdges();
            foreach (var neighbour in blocks.BlockGameObjectDictionary.Values)
            {
                if (ReferenceEquals(neighbour, belt) || !IsNeighbour(neighbour, cell)) continue;
                BeltRouteBlockEdgeScanner.Write(neighbour, edges);
            }

            var reason = BeltRouteBranchGeometry.Resolve(edges, cell, belt.BlockPosInfo.BlockDirection, useAlternateExit, out var shape);
            // silent-ok: 幾何が返した理由はそのまま理由行になる
            if (reason != BeltRouteBranchBlockReason.None) return reason;

            origin = new BeltRouteBranchOrigin(belt, splitterBlockId, shape);
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
