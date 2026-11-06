using System;
using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.Research.Logic
{
    /// <summary>
    /// 解放物の表示行（Web の UnlockEntry の判別 union）。Index は同じ値の行を見分ける通し番号
    /// A display row for an unlock (the discriminated union of Web UnlockEntry); Index tells apart rows with equal values
    /// </summary>
    public abstract class UnlockEntry
    {
        public int Index { get; }

        private protected UnlockEntry(int index)
        {
            Index = index;
        }
    }

    public sealed class BlockUnlockEntry : UnlockEntry
    {
        public UnlockBlock Block { get; }
        internal BlockUnlockEntry(int index, UnlockBlock block) : base(index) { Block = block; }
    }

    public sealed class MachineRecipeOutputUnlockEntry : UnlockEntry
    {
        public UnlockMachineRecipe Recipe { get; }
        internal MachineRecipeOutputUnlockEntry(int index, UnlockMachineRecipe recipe) : base(index) { Recipe = recipe; }
    }

    public sealed class ItemRecipeViewUnlockEntry : UnlockEntry
    {
        public ItemId ItemId { get; }
        internal ItemRecipeViewUnlockEntry(int index, ItemId itemId) : base(index) { ItemId = itemId; }
    }

    public sealed class RewardItemUnlockEntry : UnlockEntry
    {
        public RewardItem Reward { get; }
        internal RewardItemUnlockEntry(int index, RewardItem reward) : base(index) { Reward = reward; }
    }

    public sealed class ConnectToolUnlockEntry : UnlockEntry
    {
        public Guid Guid { get; }
        internal ConnectToolUnlockEntry(int index, Guid guid) : base(index) { Guid = guid; }
    }

    public sealed class TrainCarUnlockEntry : UnlockEntry
    {
        public Guid Guid { get; }
        internal TrainCarUnlockEntry(int index, Guid guid) : base(index) { Guid = guid; }
    }

    public sealed class ModuleSlotStackLimitUnlockEntry : UnlockEntry
    {
        public int Limit { get; }
        internal ModuleSlotStackLimitUnlockEntry(int index, int limit) : base(index) { Limit = limit; }
    }

    public sealed class LevelInheritanceCraftCapUnlockEntry : UnlockEntry
    {
        public LevelInheritanceCraftCap Cap { get; }
        internal LevelInheritanceCraftCapUnlockEntry(int index, LevelInheritanceCraftCap cap) : base(index) { Cap = cap; }
    }
}
