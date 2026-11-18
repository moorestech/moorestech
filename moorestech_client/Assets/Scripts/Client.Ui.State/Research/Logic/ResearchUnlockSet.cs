using System;
using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.Research.Logic
{
    public readonly struct UnlockBlock
    {
        public BlockId BlockId { get; }
        public Guid BlockGuid { get; }

        public UnlockBlock(BlockId blockId, Guid blockGuid)
        {
            BlockId = blockId;
            BlockGuid = blockGuid;
        }
    }

    // 液体は FluidId だけで指す（ItemId と同じ扱い。Web の guid 文字列からの写し替えは画面レーンの 1 か所。判断記録 D-16）
    // A fluid is referred to by FluidId only (like ItemId; mapping the Web guid string happens once in the screen lane, D-16)
    public readonly struct UnlockFluid
    {
        public FluidId FluidId { get; }
        public double Amount { get; }

        public UnlockFluid(FluidId fluidId, double amount)
        {
            FluidId = fluidId;
            Amount = amount;
        }
    }

    public sealed class UnlockMachineRecipe
    {
        public Guid RecipeGuid { get; }
        public IReadOnlyList<ItemId> OutputItemIds { get; }
        public IReadOnlyList<UnlockFluid> OutputFluids { get; }

        public UnlockMachineRecipe(Guid recipeGuid, IReadOnlyList<ItemId> outputItemIds, IReadOnlyList<UnlockFluid> outputFluids)
        {
            RecipeGuid = recipeGuid;
            OutputItemIds = outputItemIds;
            OutputFluids = outputFluids;
        }
    }

    public readonly struct RewardItem
    {
        public ItemId ItemId { get; }
        public int Count { get; }

        public RewardItem(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    public readonly struct LevelInheritanceCraftCap
    {
        public ItemId ItemId { get; }
        public int Level { get; }

        public LevelInheritanceCraftCap(ItemId itemId, int level)
        {
            ItemId = itemId;
            Level = level;
        }
    }

    /// <summary>
    /// 研究ノードの解放物と報酬（Web の ResearchNodeData の unlock*・rewardItems）
    /// A research node's unlocks and rewards (the unlock* and rewardItems of Web ResearchNodeData)
    /// </summary>
    public sealed class ResearchUnlockSet
    {
        public IReadOnlyList<UnlockBlock> Blocks { get; }
        public IReadOnlyList<UnlockMachineRecipe> MachineRecipes { get; }
        public IReadOnlyList<ItemId> ItemRecipeViewItemIds { get; }
        public IReadOnlyList<RewardItem> RewardItems { get; }
        public IReadOnlyList<Guid> ConnectToolGuids { get; }
        public IReadOnlyList<Guid> TrainCarGuids { get; }
        public IReadOnlyList<int> ModuleSlotStackLimits { get; }
        public IReadOnlyList<LevelInheritanceCraftCap> LevelInheritanceCraftCaps { get; }

        public ResearchUnlockSet(IReadOnlyList<UnlockBlock> blocks, IReadOnlyList<UnlockMachineRecipe> machineRecipes, IReadOnlyList<ItemId> itemRecipeViewItemIds, IReadOnlyList<RewardItem> rewardItems, IReadOnlyList<Guid> connectToolGuids, IReadOnlyList<Guid> trainCarGuids, IReadOnlyList<int> moduleSlotStackLimits, IReadOnlyList<LevelInheritanceCraftCap> levelInheritanceCraftCaps)
        {
            Blocks = blocks;
            MachineRecipes = machineRecipes;
            ItemRecipeViewItemIds = itemRecipeViewItemIds;
            RewardItems = rewardItems;
            ConnectToolGuids = connectToolGuids;
            TrainCarGuids = trainCarGuids;
            ModuleSlotStackLimits = moduleSlotStackLimits;
            LevelInheritanceCraftCaps = levelInheritanceCraftCaps;
        }
    }
}
