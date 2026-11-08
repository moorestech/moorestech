using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     サーバーの接続規則を探索の可否判定へ写した1枚。探索と組み立ての両方がここだけを通る
    ///     The server's connection rules translated into search feasibility; both the search and the recovery go through this alone
    ///     途中セルの判定はセル添字と面ビットで行う。Vector3Intの演算はMonoでは遅く、毎フレーム百万回単位で通るため
    ///     Mid-route checks work on cell indices and face bits; Vector3Int math is slow under Mono and runs millions of times per frame
    /// </summary>
    public sealed class BeltRouteTransitionRule
    {
        private readonly BeltRouteGrid _grid;
        private readonly BeltRouteQuery _query;
        private readonly Vector3Int _endFrontCell;
        private readonly bool _ignoreTerrain;
        private readonly bool _ignoreBlocks;

        // 終点自身の出口の辺も既存の出力口と同じ扱い。辺に接するセルで途中になりうるのは、終点と終点の前それぞれの1つ下だけ
        // The end's own exit edge counts like an existing output; of the cells touching it, only the ones below the end and below its front can be mid-route
        private readonly int _cellBelowEnd;
        private readonly int _cellBelowEndFront;
        private readonly int _faceBitBelowEnd;
        private readonly int _faceBitBelowEndFront;

        // 途中に使えない3セルの添字。箱の外なら-1で、どの添字にも一致しない
        // Indices of the three cells never used mid-route; -1 outside the box, which matches no index
        private readonly int _startIndex;
        private readonly int _endIndex;
        private readonly int _endFrontIndex;

        public BeltRouteTransitionRule(BeltRouteGrid grid, BeltRouteQuery query, BeltRouteObstacleMask mask)
        {
            _grid = grid;
            _query = query;
            _endFrontCell = query.EndCell + BeltRouteDirection.Forward(query.EndDirection);
            _ignoreTerrain = (mask & BeltRouteObstacleMask.IgnoreTerrain) != 0;
            _ignoreBlocks = (mask & BeltRouteObstacleMask.IgnoreBlocks) != 0;
            _startIndex = grid.CellIndexOrOutside(query.StartCell);
            _endIndex = grid.CellIndexOrOutside(query.EndCell);
            _endFrontIndex = grid.CellIndexOrOutside(_endFrontCell);
            _cellBelowEnd = grid.CellIndexOrOutside(query.EndCell + Vector3Int.down);
            _cellBelowEndFront = grid.CellIndexOrOutside(_endFrontCell + Vector3Int.down);
            _faceBitBelowEnd = BeltRouteEdgeTable.FaceBit(query.EndDirection, 1);
            _faceBitBelowEndFront = BeltRouteEdgeTable.FaceBit(BeltRouteDirection.Opposite(query.EndDirection), 1);
        }

        public BeltRouteQuery Query => _query;

        public bool IsInsideBox(Vector3Int cell) => _grid.Contains(cell);

        // 箱の中で、地面にも既存ブロックにも既存坂の真上にも当たらないセルか
        // Whether the cell is inside the box and clear of ground, existing blocks and existing slope heads
        public bool IsCellFree(Vector3Int cell)
        {
            var index = _grid.CellIndexOrOutside(cell);
            return 0 <= index && IsCellFreeAt(index);
        }

        // 添字は箱の中のもの
        // The index must lie inside the box
        public bool IsCellFreeAt(int cellIndex)
        {
            if (!_ignoreTerrain && _grid.IsBlockedByTerrainAt(cellIndex)) return false;
            if (!_ignoreBlocks && (_grid.IsBlockedByBlockAt(cellIndex) || _grid.IsSlopeHeadAt(cellIndex))) return false;
            return true;
        }

        // 坂の真上が空いているか。既存ブロックが乗るとサーバーが上を優先して繋ぎ坂が切れる
        // Whether the cell above a slope is clear; an existing block there is wired first by the server and cuts the slope
        public bool IsSlopeHeadFree(Vector3Int slopeCell)
        {
            var head = _grid.CellIndexOrOutside(slopeCell + Vector3Int.up);
            return 0 <= head && IsSlopeHeadFreeAt(head);
        }

        // 始点・終点以外のセルとして置けるか。incomingは手前セルの出る向き、outgoingはこのセルの出る向き
        // Whether the cell may be a mid-route cell; incoming is the previous cell's exit heading, outgoing this cell's
        public bool IsIntermediatePlaceable(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            var index = _grid.CellIndexOrOutside(cell);
            return 0 <= index && IsIntermediatePlaceableAt(index, kind, incoming, outgoing);
        }

        // 添字は箱の中のもの
        // The index must lie inside the box
        public bool IsIntermediatePlaceableAt(int cellIndex, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            // 終点の前のセルは、終点が流す先として空けておく
            // The cell in front of the end stays free as the end's delivery target
            if (cellIndex == _startIndex || cellIndex == _endIndex || cellIndex == _endFrontIndex) return false;
            if (!IsCellFreeAt(cellIndex)) return false;
            if (kind.IsSlope())
            {
                var head = cellIndex + _grid.LayerCellCount;
                if (_grid.CellCount <= head || !IsSlopeHeadFreeAt(head)) return false;
            }
            if ((OutputFaceBits(cellIndex) & EntryFaceBits(kind, incoming, outgoing)) != 0) return false;
            return !FeedsForeignInput(cellIndex, kind, incoming, outgoing);
        }

        // 手前の状態から終点セルへ入れるか。終点は空セルで向きが固定の直線
        // Whether the end cell can be entered from the previous state; the end is a straight cell of fixed facing
        public bool CanEnterGoal(BeltRouteState previous)
        {
            var next = previous.Cell + BeltRouteDirection.Forward(previous.Outgoing);
            if (next.x != _query.EndCell.x || next.z != _query.EndCell.z) return false;
            if (previous.ExitHeight != _query.EndCell.y) return false;
            return CanEnterGoalFrom(previous.Outgoing);
        }

        // 終点の手前まで来ている状態が、その出る向きで終点へ入れるか（位置と高さは呼び出し側が合わせている）
        // Whether a state already adjacent to the end may enter it with that exit heading (position and height are matched by the caller)
        public bool CanEnterGoalFrom(BlockDirection outgoing)
        {
            // 分岐器の終点は背面入力なので入る向きが正面と一致、通常ベルトは背面・左右のどこからでも入れる
            // A splitter end takes input from its back so the entry heading must equal its facing; a normal belt accepts back, left or right
            if (_query.TurnByOutput) return outgoing == _query.EndDirection;
            return IsTurnAllowed(outgoing, _query.EndDirection);
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

        // 始点・終点は必ず置くので、その真下の坂は既存ブロックが乗るのと同じ。探索で弾かないと組み立てだけが弾いて最短コストの経路が組めず経路なしになる
        // The start and end are always placed, so a slope right below them is as cut as one under an existing block; unless the search rejects it, only recovery does and the shortest cost has no assemblable route
        private bool IsSlopeHeadFreeAt(int headIndex)
        {
            if (headIndex == _startIndex || headIndex == _endIndex) return false;
            return _ignoreBlocks || !_grid.IsBlockedByBlockAt(headIndex);
        }

        // 既存の出力口の面ビットに、終点自身の出口の辺を重ねる
        // Existing output faces with the end's own exit edge laid over them
        private int OutputFaceBits(int cellIndex)
        {
            var bits = _ignoreBlocks ? 0 : _grid.OutputFaceBits(cellIndex);
            if (cellIndex == _cellBelowEnd) bits |= _faceBitBelowEnd;
            if (cellIndex == _cellBelowEndFront) bits |= _faceBitBelowEndFront;
            return bits;
        }

        // 入口の面が既存の出力口に向いていると、そのラインの荷物を横取りする
        // An entry face aimed at by an existing output would steal that line's items
        // 坂なしの直線ベルトは背面・左右の3面が入口。分岐器と坂は手前セルへ向く1面だけ（下り坂は1つ上の高さ）
        // A flat straight belt takes input on back, left and right; splitters and slopes only on the face toward the previous cell (a down slope one level up)
        private int EntryFaceBits(BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (kind == BeltRouteCellKind.Straight && !_query.TurnByOutput)
            {
                return BeltRouteEdgeTable.FaceBit(BeltRouteDirection.Opposite(outgoing), 0)
                       | BeltRouteEdgeTable.FaceBit(BeltRouteDirection.Left(outgoing), 0)
                       | BeltRouteEdgeTable.FaceBit(BeltRouteDirection.Right(outgoing), 0);
            }
            return BeltRouteEdgeTable.FaceBit(BeltRouteDirection.Opposite(incoming), kind.EntryHeightOffset());
        }

        // 分岐器は使わない出口も流すので、その先に既存の受け手がいてはいけない
        // A splitter also emits through its unused exits, so none of them may face an existing receiver
        private bool FeedsForeignInput(int cellIndex, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (!_query.TurnByOutput || kind != BeltRouteCellKind.Straight || _ignoreBlocks) return false;
            var unusedExits = BeltRouteEdgeTable.FaceBit(incoming, 0)
                              | BeltRouteEdgeTable.FaceBit(BeltRouteDirection.Left(incoming), 0)
                              | BeltRouteEdgeTable.FaceBit(BeltRouteDirection.Right(incoming), 0);
            unusedExits &= ~BeltRouteEdgeTable.FaceBit(outgoing, 0);
            return (_grid.InputFaceBits(cellIndex) & unusedExits) != 0;
        }
    }
}
