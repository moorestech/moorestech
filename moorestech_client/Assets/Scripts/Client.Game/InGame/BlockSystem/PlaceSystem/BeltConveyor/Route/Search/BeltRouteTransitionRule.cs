using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     サーバーの接続規則を探索の可否判定へ写した1枚。探索と組み立ての両方がここだけを通る
    ///     The server's connection rules translated into search feasibility; both the search and the recovery go through this alone
    /// </summary>
    public sealed class BeltRouteTransitionRule
    {
        private readonly BeltRouteGrid _grid;
        private readonly BeltRouteQuery _query;
        private readonly BeltRouteObstacleMask _mask;
        private readonly BeltRouteEdge _endOutputEdge;

        public BeltRouteTransitionRule(BeltRouteGrid grid, BeltRouteQuery query, BeltRouteObstacleMask mask)
        {
            _grid = grid;
            _query = query;
            _mask = mask;
            _endOutputEdge = query.EndOutputEdge;
        }

        public BeltRouteQuery Query => _query;

        public bool IsInsideBox(Vector3Int cell) => _grid.Contains(cell);

        private bool IgnoreTerrain => (_mask & BeltRouteObstacleMask.IgnoreTerrain) != 0;
        private bool IgnoreBlocks => (_mask & BeltRouteObstacleMask.IgnoreBlocks) != 0;

        // 箱の中で、地面にも既存ブロックにも既存坂の真上にも当たらないセルか
        // Whether the cell is inside the box and clear of ground, existing blocks and existing slope heads
        public bool IsCellFree(Vector3Int cell)
        {
            if (!_grid.Contains(cell)) return false;
            if (!IgnoreTerrain && _grid.IsBlockedByTerrain(cell)) return false;
            if (!IgnoreBlocks && (_grid.IsBlockedByBlock(cell) || _grid.IsSlopeHead(cell))) return false;
            return true;
        }

        // 坂の真上が空いているか。既存ブロックが乗るとサーバーが上を優先して繋ぎ坂が切れる
        // Whether the cell above a slope is clear; an existing block there is wired first by the server and cuts the slope
        public bool IsSlopeHeadFree(Vector3Int slopeCell)
        {
            var head = slopeCell + Vector3Int.up;
            if (!_grid.Contains(head)) return false;
            return IgnoreBlocks || !_grid.IsBlockedByBlock(head);
        }

        // 始点・終点以外のセルとして置けるか。incomingは手前セルの出る向き、outgoingはこのセルの出る向き
        // Whether the cell may be a mid-route cell; incoming is the previous cell's exit heading, outgoing this cell's
        public bool IsIntermediatePlaceable(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (cell == _query.StartCell || cell == _query.EndCell) return false;
            // 終点の前のセルは、終点が流す先として空けておく
            // The cell in front of the end stays free as the end's delivery target
            if (cell == _query.EndCell + BeltRouteDirection.Forward(_query.EndDirection)) return false;
            if (!IsCellFree(cell)) return false;
            if (kind.IsSlope() && !IsSlopeHeadFree(cell)) return false;
            if (ReceivesForeignOutput(cell, kind, incoming, outgoing)) return false;
            return !FeedsForeignInput(cell, kind, incoming, outgoing);
        }

        // 手前の状態から終点セルへ入れるか。終点は空セルで向きが固定の直線
        // Whether the end cell can be entered from the previous state; the end is a straight cell of fixed facing
        public bool CanEnterGoal(BeltRouteState previous)
        {
            var next = previous.Cell + BeltRouteDirection.Forward(previous.Outgoing);
            if (next.x != _query.EndCell.x || next.z != _query.EndCell.z) return false;
            if (previous.ExitHeight != _query.EndCell.y) return false;
            // 分岐器の終点は背面入力なので入る向きが正面と一致、通常ベルトは背面・左右のどこからでも入れる
            // A splitter end takes input from its back so the entry heading must equal its facing; a normal belt accepts back, left or right
            if (_query.TurnByOutput) return previous.Outgoing == _query.EndDirection;
            return IsTurnAllowed(previous.Outgoing, _query.EndDirection);
        }

        // 次のセルは前・左・右へ出られる。Uターンだけは入口と出口が同じ面になるので不可（分岐器も幾何は同じ）
        // The next cell may exit forward, left or right; only a U-turn is impossible since entry and exit share a face (splitters share the geometry)
        public bool IsTurnAllowed(BlockDirection incoming, BlockDirection outgoing)
        {
            return outgoing != BeltRouteDirection.Opposite(incoming);
        }

        // 同じ辺で隣り合う2状態が経路として繋がるか（高さ・山谷・曲がりの3条件）
        // Whether two states adjacent across a face connect as a route (height, peak/valley, and turn conditions)
        public bool IsConnected(BeltRouteState previous, BeltRouteState next)
        {
            var expected = previous.Cell + BeltRouteDirection.Forward(previous.Outgoing);
            if (expected.x != next.Cell.x || expected.z != next.Cell.z) return false;
            if (previous.ExitHeight != next.EntryHeight) return false;
            if (!next.Kind.CanFollow(previous.Kind)) return false;
            if (next.Kind.IsSlope()) return next.Outgoing == previous.Outgoing;
            return IsTurnAllowed(previous.Outgoing, next.Outgoing);
        }

        // 入口の面が既存の出力口（または終点の出口）に向いていると、そのラインの荷物を横取りする
        // An entry face aimed at by an existing output (or the end's own exit) would steal that line's items
        private bool ReceivesForeignOutput(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (kind == BeltRouteCellKind.Straight && !_query.TurnByOutput)
            {
                return IsForeignOutput(cell, BeltRouteDirection.Opposite(outgoing), cell.y)
                       || IsForeignOutput(cell, BeltRouteDirection.Left(outgoing), cell.y)
                       || IsForeignOutput(cell, BeltRouteDirection.Right(outgoing), cell.y);
            }
            return IsForeignOutput(cell, BeltRouteDirection.Opposite(incoming), cell.y + kind.EntryHeightOffset());
        }

        private bool IsForeignOutput(Vector3Int cell, BlockDirection face, int height)
        {
            var edge = BeltRouteEdge.FromFace(cell, face, height);
            if (edge.Equals(_endOutputEdge)) return true;
            return !IgnoreBlocks && _grid.HasOutputEdge(edge);
        }

        // 分岐器は使わない出口も流すので、その先に既存の受け手がいてはいけない
        // A splitter also emits through its unused exits, so none of them may face an existing receiver
        private bool FeedsForeignInput(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (!_query.TurnByOutput || kind != BeltRouteCellKind.Straight || IgnoreBlocks) return false;
            return FeedsInputUnless(cell, incoming, outgoing) || FeedsInputUnless(cell, BeltRouteDirection.Left(incoming), outgoing) || FeedsInputUnless(cell, BeltRouteDirection.Right(incoming), outgoing);
        }

        private bool FeedsInputUnless(Vector3Int cell, BlockDirection face, BlockDirection used)
        {
            if (face == used) return false;
            return _grid.HasInputEdge(BeltRouteEdge.FromFace(cell, face, cell.y));
        }
    }
}
