using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Blocks.SpaceElevator
{
    // 成果を出口の複製へ前から順に積む唯一の模擬。確認と実挿入がこれを共有する
    // The single simulation stacking results onto a copy of the outlets front to back; the check and the insert share it
    internal static class SpaceElevatorOutputSimulation
    {
        internal static bool TryStack(List<IItemStack> outlets, IReadOnlyList<IItemStack> outputs, out List<IItemStack> stacked)
        {
            stacked = outlets;
            foreach (var output in outputs)
            {
                var remainder = output;
                for (var i = 0; i < stacked.Count && remainder.Count > 0; i++)
                {
                    var result = stacked[i].AddItem(remainder);
                    stacked[i] = result.ProcessResultItemStack;
                    remainder = result.RemainderItemStack;
                }
                if (remainder.Count > 0) return false;
            }
            return true;
        }
    }
}
