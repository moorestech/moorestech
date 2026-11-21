using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// フィルター分岐器の出口が入口から見て左・正面・右のどれかを、マスタの接続の向き（ブロックの座標系）から決める
    /// Decides whether each filter splitter exit is left, front or right of the entry from the master connector directions (block-local)
    /// </summary>
    /// <remarks>
    /// 設置の向きは入口と出口の向きを同じ回転で回すだけなので、ブロックの座標系で決めた側はどの向きに置いても変わらない（BlockDirectionExtension.ConvertLocalCell）。
    /// 止める条件は Core.Master の FilterSplitterParamValidator が別に書く（Core.Master からこの層は呼べない）。食い違いは FilterSplitterOutputSideTest の表で押さえる。
    /// Placement rotates the entry and exit directions by the same rotation, so a side decided in block-local space holds for every facing (BlockDirectionExtension.ConvertLocalCell).
    /// FilterSplitterParamValidator in Core.Master writes the stop conditions separately (Core.Master cannot call this layer); FilterSplitterOutputSideTest's table keeps them in agreement.
    /// </remarks>
    public static class FilterSplitterOutputSideResolver
    {
        public static bool TryResolve(IBlockConnector input, IBlockConnector output, out FilterSplitterOutputSide side)
        {
            // silent-ok: ResolveAll は false を例外にし、不正な出口の GUID を呼び出し側へ伝える / UI を開いた開発者が例外で気づく
            // ResolveAll reports a false result through an exception that identifies the invalid output GUID.
            side = default;
            if (!TryGetSingleHorizontalDirection(input, out var entry)) return false;
            if (!TryGetSingleHorizontalDirection(output, out var exit)) return false;

            // 入口の向きは外を指すので、流れる向きはその逆。上から見て流れる向きを時計回りに 90 度回すと右手、反時計回りで左手
            // The entry direction points outward, so the flow is its opposite; turning the flow 90° clockwise seen from above gives the right hand, counter-clockwise the left
            var flow = -entry;
            if (exit == flow) { side = FilterSplitterOutputSide.Front; return true; }
            if (exit == new Vector3Int(flow.z, 0, -flow.x)) { side = FilterSplitterOutputSide.Right; return true; }
            if (exit == new Vector3Int(-flow.z, 0, flow.x)) { side = FilterSplitterOutputSide.Left; return true; }
            return false;
        }

        // 出口の並び（＝方向の番号。VanillaFilterSplitterTemplate）どおりに側を返す
        // Returns the sides in exit order (= the direction index, see VanillaFilterSplitterTemplate)
        public static IReadOnlyList<FilterSplitterOutputSide> ResolveAll(InventoryConnects connectors)
        {
            // 入口1つ・出口1つ以上・どの出口も側が決まることは FilterSplitterParamValidator が読込時に保証する。ここで外れたら契約違反なので止める
            // FilterSplitterParamValidator guarantees one entry, at least one exit and resolvable exits at load; anything else here is a broken contract, so stop
            if (connectors.InputConnects == null || connectors.InputConnects.Length != 1) throw new InvalidOperationException("FilterSplitter does not have exactly one input connector; FilterSplitterParamValidator should have stopped the master load");
            if (connectors.OutputConnects == null || connectors.OutputConnects.Length == 0) throw new InvalidOperationException("FilterSplitter has no output connectors; FilterSplitterParamValidator should have stopped the master load");
            var input = connectors.InputConnects[0];
            var sides = new List<FilterSplitterOutputSide>(connectors.OutputConnects.Length);
            foreach (var output in connectors.OutputConnects)
            {
                if (!TryResolve(input, output, out var side)) throw new InvalidOperationException($"FilterSplitter output {output.ConnectorGuid} is not left, front or right of the entry; FilterSplitterParamValidator should have stopped the master load");
                sides.Add(side);
            }
            return sides;
        }

        private static bool TryGetSingleHorizontalDirection(IBlockConnector connector, out Vector3Int direction)
        {
            // silent-ok: TryResolve が不正な向きを false で伝える / ResolveAll の例外で開発者が気づく
            // TryResolve propagates a malformed direction to the exception in ResolveAll.
            direction = Vector3Int.zero;
            if (connector.Directions == null || connector.Directions.Length != 1) return false;
            direction = connector.Directions[0];
            return direction.y == 0 && Math.Abs((long)direction.x) + Math.Abs((long)direction.z) == 1;
        }
    }
}
