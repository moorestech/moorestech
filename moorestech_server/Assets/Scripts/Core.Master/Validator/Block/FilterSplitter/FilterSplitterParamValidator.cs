using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;

namespace Core.Master.Validator.Block.FilterSplitter
{
    /// <summary>
    /// 出口を左・正面・右で見せられないフィルター分岐器を読込時に止める（画面の見出しが番号に戻れないため。ユーザー裁定 2026-11-21）
    /// Stops at load any filter splitter whose exits cannot be shown as left, front and right (the screen has no numbered fallback; user decision 2026-11-21)
    /// </summary>
    internal static class FilterSplitterParamValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not FilterSplitterBlockParam param) continue;
                logs += ValidateConnectors(block.Name, param.InventoryConnectors);
            }
            return logs;

            #region Internal

            string ValidateConnectors(string name, InventoryConnects connectors)
            {
                // 入口は1口で、その向きは水平の1方向（流れる向きが1つに決まる）
                // Exactly one entry whose direction is a single horizontal one (so the flow is unique)
                if (connectors.InputConnects == null || connectors.InputConnects.Length != 1) return $"[BlockMaster] {name} is a FilterSplitter but does not have exactly one input connector\n";
                if (connectors.OutputConnects == null || connectors.OutputConnects.Length == 0) return $"[BlockMaster] {name} is a FilterSplitter but has no output connectors\n";
                var input = connectors.InputConnects[0];
                if (!FilterSplitterOutputSideResolver.HasSingleHorizontalDirection(input)) return $"[BlockMaster] {name} input {input.ConnectorGuid} does not point to a single horizontal direction\n";

                // 側は計算と同じ口で決め、決まらない出口と同じ側の重なりを止める
                // Decide sides through the same calculation and stop undecidable exits and repeated sides
                var result = "";
                var seen = new HashSet<FilterSplitterOutputSide>();
                foreach (var output in connectors.OutputConnects)
                {
                    if (!FilterSplitterOutputSideResolver.TryResolve(input, output, out var side))
                    {
                        result += $"[BlockMaster] {name} output {output.ConnectorGuid} is not left, front or right of its input\n";
                        continue;
                    }
                    if (!seen.Add(side)) result += $"[BlockMaster] {name} has two outputs on the same side ({side})\n";
                }
                return result;
            }

            #endregion
        }
    }
}
