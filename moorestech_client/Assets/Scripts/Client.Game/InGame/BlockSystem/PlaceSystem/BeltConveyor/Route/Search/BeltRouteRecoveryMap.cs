using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     組み立ての作業用地図。置いたセルと自分の坂の真上を、置いた深さ（終点が0）つきで持つ
    ///     The recovery's working map: placed cells and own slope heads, each with the depth it was placed at (the end is 0)
    ///     衝突した相手の深さが分かると「この失敗は自分より先に置いたセルのせいか」が判定でき、死んだ状態の記憶を安全にできる
    ///     Knowing the depth of the colliding cell tells whether a failure stems from cells placed before this state, which makes memoizing dead states safe
    /// </summary>
    public sealed class BeltRouteRecoveryMap
    {
        public const int NoConflict = int.MaxValue;

        private readonly Dictionary<Vector3Int, int> _usedDepth = new();
        private readonly Dictionary<Vector3Int, int> _slopeHeadDepth = new();

        // 残り予算がこの値以下なら組めないと分かった状態。予算が少ないほど組めないので上限値だけ持てばよい
        // States known to fail with a remaining budget at or below the stored value; less budget fails too, so only the largest value is kept
        private readonly Dictionary<BeltRouteState, int> _deadRemaining = new();

        public void Clear()
        {
            _usedDepth.Clear();
            _slopeHeadDepth.Clear();
            _deadRemaining.Clear();
        }

        // 置けるならtrue。置けないときconflictDepthに衝突した置き済みセルの最も浅い深さが入る
        // True when placeable; otherwise conflictDepth holds the shallowest depth among the colliding placed cells
        // 衝突は、同じセルの再使用・既に置いた坂の真上・坂なら真上が使用済み、の3種
        // Collisions are reusing a cell, sitting above an own slope, and a slope whose head is already used
        public bool CanPlace(BeltRouteState state, out int conflictDepth)
        {
            conflictDepth = NoConflict;
            if (_usedDepth.TryGetValue(state.Cell, out var used)) conflictDepth = Mathf.Min(conflictDepth, used);
            if (_slopeHeadDepth.TryGetValue(state.Cell, out var head)) conflictDepth = Mathf.Min(conflictDepth, head);
            if (state.Kind.IsSlope() && _usedDepth.TryGetValue(state.Cell + Vector3Int.up, out var above)) conflictDepth = Mathf.Min(conflictDepth, above);
            return conflictDepth == NoConflict;
        }

        public void Place(BeltRouteState state, int depth)
        {
            _usedDepth[state.Cell] = depth;
            if (state.Kind.IsSlope()) _slopeHeadDepth[state.Cell + Vector3Int.up] = depth;
        }

        public void Unplace(BeltRouteState state)
        {
            _usedDepth.Remove(state.Cell);
            if (state.Kind.IsSlope()) _slopeHeadDepth.Remove(state.Cell + Vector3Int.up);
        }

        public bool IsDead(BeltRouteState state, int remaining)
        {
            return _deadRemaining.TryGetValue(state, out var dead) && remaining <= dead;
        }

        public void MarkDead(BeltRouteState state, int remaining)
        {
            if (_deadRemaining.TryGetValue(state, out var dead) && remaining <= dead) return;
            _deadRemaining[state] = remaining;
        }
    }
}
