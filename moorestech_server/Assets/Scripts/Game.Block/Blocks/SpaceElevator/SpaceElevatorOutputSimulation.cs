using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Blocks.SpaceElevator
{
    // 成果を既存スタック優先で出口の複製へ積む。確認と実挿入がこの模擬を共有する
    // Stack results onto copied outlets, preferring existing stacks; checks and insertion share this simulation
    internal static class SpaceElevatorOutputSimulation
    {
        internal static bool TryStack(List<IItemStack> outlets, IReadOnlyList<IItemStack> outputs, out List<IItemStack> stacked)
        {
            stacked = outlets;
            foreach (var output in outputs)
            {
                var remainder = output;
                // 同属性への加算はAddItemに任せ、空き枠は既存スタックを埋めた後に使う
                // Let AddItem enforce metadata compatibility and use empty slots only after existing stacks
                for (var pass = 0; pass < 2 && 0 < remainder.Count; pass++)
                {
                    for (var i = 0; i < stacked.Count && 0 < remainder.Count; i++)
                    {
                        // silent-ok: 各段で対象外の枠は次の候補へ進む / 呼び出し元が最終判定を待機状態とログへ反映する
                        if ((stacked[i].Count == 0) != (pass == 1)) continue;
                        var result = stacked[i].AddItem(remainder);
                        stacked[i] = result.ProcessResultItemStack;
                        remainder = result.RemainderItemStack;
                    }
                }
                // silent-ok: 模擬失敗は在庫を確定しない / 呼び出し元が出口待ちの表示とログ、または格納失敗の例外で通知する
                if (0 < remainder.Count) return false;
            }
            return true;
        }
    }
}
