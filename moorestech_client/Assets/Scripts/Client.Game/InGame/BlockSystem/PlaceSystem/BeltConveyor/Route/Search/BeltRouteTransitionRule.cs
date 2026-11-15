using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     サーバーの接続規則を探索の可否判定へ写した1枚。探索と組み立ての両方がここだけを通る
    ///     The server's connection rules translated into search feasibility; both the search and the recovery go through this alone
    ///     世界へはセル単位で問い合わせる
    ///     The world is queried per cell
    /// </summary>
    public sealed class BeltRouteTransitionRule
    {
        private readonly IBeltRouteWorld _world;
        private readonly BeltRouteQuery _query;
        private readonly Vector3Int _endFrontCell;

        // 終点自身の出口の辺も既存の出力口と同じ扱い。辺に接するセルで途中になりうるのは、終点と終点の前それぞれの1つ下だけ
        // The end's own exit edge counts like an existing output; of the cells touching it, only the ones below the end and below its front can be mid-route
        private readonly Vector3Int _cellBelowEnd;
        private readonly Vector3Int _cellBelowEndFront;
        private readonly int _faceBitBelowEnd;
        private readonly int _faceBitBelowEndFront;

        public BeltRouteTransitionRule(IBeltRouteWorld world, BeltRouteQuery query)
        {
            _world = world;
            _query = query;
            _endFrontCell = query.EndCell + BeltRouteDirection.Forward(query.EndDirection);
            _cellBelowEnd = query.EndCell + Vector3Int.down;
            _cellBelowEndFront = _endFrontCell + Vector3Int.down;
            _faceBitBelowEnd = BeltRouteFaceBits.FaceBit(query.EndDirection, 1);
            _faceBitBelowEndFront = BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(query.EndDirection), 1);
        }

        public BeltRouteQuery Query => _query;

        // 地面・既存ブロック・既存坂の真上に当たらないセルか
        // Whether the cell avoids ground, blocks and slope heads
        public bool IsCellFree(Vector3Int cell)
        {
            return BeltRouteCellJudge.Judge(_world, cell) == BeltRouteCellBlockReason.None;
        }

        // 坂の真上が空いているか。既存ブロックが乗るとサーバーが上を優先して繋ぎ坂が切れる
        // Whether the cell above a slope is clear; an existing block there is wired first by the server and cuts the slope
        // 始点・終点は必ず置くので、その真下の坂は既存ブロックが乗るのと同じ。探索で弾かないと組み立てだけが弾いて最短コストの経路が組めず経路なしになる
        // The start and end are always placed, so a slope right below them is as cut as one under an existing block; unless the search rejects it, only recovery does and the shortest cost has no assemblable route
        public bool IsSlopeHeadFree(Vector3Int slopeCell)
        {
            var head = slopeCell + Vector3Int.up;
            if (head == _query.StartCell || head == _query.EndCell) return false;
            return !_world.IsBlockedByBlock(head);
        }

        // 始点・終点以外のセルとして置けるか。incomingは手前セルの出る向き、outgoingはこのセルの出る向き
        // Whether the cell may be a mid-route cell; incoming is the previous cell's exit heading, outgoing this cell's
        public bool IsIntermediatePlaceable(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            // 終点の前のセルは、終点が流す先として空けておく
            // The cell in front of the end stays free as the end's delivery target
            if (cell == _query.StartCell || cell == _query.EndCell || cell == _endFrontCell) return false;
            if (!IsCellFree(cell)) return false;
            if (kind.IsSlope() && !IsSlopeHeadFree(cell)) return false;
            if ((OutputFaceBits(cell) & EntryFaceBits(kind, incoming, outgoing)) != 0) return false;
            return !FeedsForeignInput(cell, kind, incoming, outgoing);
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

        // 分岐器の終点は正面一致、通常は背面・左右から入れる
        // Splitter end needs facing match; normal accepts back or sides
        public bool CanEnterGoalFrom(BlockDirection outgoing)
        {
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

        // 既存の出力口の面ビットに、終点自身の出口の辺を重ねる
        // Existing output faces with the end's own exit edge laid over them
        private int OutputFaceBits(Vector3Int cell)
        {
            var bits = _world.OutputFaceBits(cell);
            if (cell == _cellBelowEnd) bits |= _faceBitBelowEnd;
            if (cell == _cellBelowEndFront) bits |= _faceBitBelowEndFront;
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
                return BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(outgoing), 0)
                       | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Left(outgoing), 0)
                       | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Right(outgoing), 0);
            }
            return BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(incoming), kind.EntryHeightOffset());
        }

        // 分岐器は使わない出口も流すので、その先に既存の受け手がいてはいけない
        // A splitter also emits through its unused exits, so none of them may face an existing receiver
        private bool FeedsForeignInput(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (!_query.TurnByOutput || kind != BeltRouteCellKind.Straight) return false;
            var unusedExits = BeltRouteFaceBits.FaceBit(incoming, 0)
                              | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Left(incoming), 0)
                              | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Right(incoming), 0);
            unusedExits &= ~BeltRouteFaceBits.FaceBit(outgoing, 0);
            return (_world.InputFaceBits(cell) & unusedExits) != 0;
        }
    }
}
