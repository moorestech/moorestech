using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    /// <summary>
    ///     工場が作った出力1セットの並びのまま実現出力を組み直す。出力kはスロット(k % 出力数)へ積まれるので並びを崩さない
    ///     Rebuilds realized outputs in the order of the factory's output set; output k lands in slot (k % output count), so the order must hold
    /// </summary>
    internal static class RecipeOutputSetBuilder
    {
        // 工場の1セットをアイテムIDと個数の写しにする。書き換え器は写しの必要な所だけを書き換える
        // Copy the factory's set into item ids and counts; rewriters change only the entries they own
        public static (ItemId[] itemIds, int[] counts) CopySet(IReadOnlyList<IItemStack> factorySet)
        {
            var itemIds = new ItemId[factorySet.Count];
            var counts = new int[factorySet.Count];
            for (var i = 0; i < factorySet.Count; i++)
            {
                itemIds[i] = factorySet[i].Id;
                counts[i] = factorySet[i].Count;
            }
            return (itemIds, counts);
        }

        // 1セットを setCount 回並べる。個数0は空スタックになり、スロットへは何も積まれない
        // Repeat one set setCount times; a zero count becomes an empty stack that adds nothing to its slot
        public static List<IItemStack> Repeat(IReadOnlyList<ItemId> itemIds, IReadOnlyList<int> counts, int setCount)
        {
            var outputs = new List<IItemStack>(itemIds.Count * setCount);
            for (var set = 0; set < setCount; set++)
            {
                for (var i = 0; i < itemIds.Count; i++) outputs.Add(ServerContext.ItemStackFactory.Create(itemIds[i], counts[i]));
            }
            return outputs;
        }
    }
}
