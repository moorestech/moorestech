using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Core.Master.Validator.Block.FilterSplitter
{
    /// <summary>
    /// 出口を左・正面・右で見せられないフィルター分岐器を読込時に止める（画面の見出しが番号に戻れないため。ユーザー裁定 2026-11-21）
    /// Stops at load any filter splitter whose exits cannot be shown as left, front and right (the screen has no numbered fallback; user decision 2026-11-21)
    /// </summary>
    /// <remarks>
    /// 側の計算（Game.Block.Interface の FilterSplitterOutputSideResolver）は Core.Master から呼べないので、止める条件をここで別に書く。食い違いは FilterSplitterOutputSideTest の表で押さえる。
    /// The side calculation (FilterSplitterOutputSideResolver in Game.Block.Interface) cannot be called from Core.Master, so the stop conditions are written here; FilterSplitterOutputSideTest's table keeps them in agreement.
    /// </remarks>
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
                if (!IsSingleHorizontal(input.Directions)) return $"[BlockMaster] {name} input {input.ConnectorGuid} does not point to a single horizontal direction\n";

                // 水平の出口は入口と同じ向きでなければ左・正面・右のどれか。側は向きと1対1なので、同じ向きが2つなら同じ側が2つ
                // A horizontal exit not facing the entry's way is left, front or right; sides map one-to-one to directions, so a repeated direction is a repeated side
                var entry = input.Directions[0];
                var result = "";
                var seen = new HashSet<Vector3Int>();
                foreach (var output in connectors.OutputConnects)
                {
                    if (!IsSingleHorizontal(output.Directions) || output.Directions[0] == entry)
                    {
                        result += $"[BlockMaster] {name} output {output.ConnectorGuid} is not left, front or right of its input\n";
                        continue;
                    }
                    if (!seen.Add(output.Directions[0])) result += $"[BlockMaster] {name} has two outputs on the same side (direction {output.Directions[0]})\n";
                }
                return result;
            }

            bool IsSingleHorizontal(Vector3Int[] directions)
            {
                if (directions == null || directions.Length != 1) return false;
                var direction = directions[0];
                return direction.y == 0 && System.Math.Abs((long)direction.x) + System.Math.Abs((long)direction.z) == 1;
            }

            #endregion
        }
    }
}
