using System;
using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.Machine
{
    public readonly struct RecipeOutputItem
    {
        public ItemId ItemId { get; }
        public int Count { get; }

        public RecipeOutputItem(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    public readonly struct RecipeOutputFluid
    {
        public FluidId FluidId { get; }
        public double Amount { get; }

        public RecipeOutputFluid(FluidId fluidId, double amount)
        {
            FluidId = fluidId;
            Amount = amount;
        }
    }

    // 選択行が読むレシピの要約（Web の MachineRecipe のうち行の判断に使う部分）
    // The recipe summary selection rows read (the part of Web MachineRecipe the row decision uses)
    public sealed class MachineRecipeSummary
    {
        public Guid RecipeGuid { get; }
        public Guid BlockGuid { get; }
        public IReadOnlyList<RecipeOutputItem> OutputItems { get; }
        public IReadOnlyList<RecipeOutputFluid> OutputFluids { get; }

        public MachineRecipeSummary(Guid recipeGuid, Guid blockGuid, IReadOnlyList<RecipeOutputItem> outputItems, IReadOnlyList<RecipeOutputFluid> outputFluids)
        {
            RecipeGuid = recipeGuid;
            BlockGuid = blockGuid;
            OutputItems = outputItems;
            OutputFluids = outputFluids;
        }
    }

    // レシピの代表表示（行名・ヘッダアイコンの出所）。アイテム優先・無ければ液体（2026-08-30 裁定 D2・ADR 0042 R2/R3）
    // The recipe's representative display (row name / header icon source): item first, else fluid (2026-08-30 ruling D2, ADR 0042 R2/R3)
    public abstract class RecipeDisplaySubject
    {
        private protected RecipeDisplaySubject()
        {
        }

        public static RecipeDisplaySubject Item(ItemId itemId, int count)
        {
            return new ItemRecipeDisplaySubject(itemId, count);
        }

        public static RecipeDisplaySubject Fluid(FluidId fluidId, double amount)
        {
            return new FluidRecipeDisplaySubject(fluidId, amount);
        }
    }

    public sealed class ItemRecipeDisplaySubject : RecipeDisplaySubject
    {
        public ItemId ItemId { get; }
        public int Count { get; }

        internal ItemRecipeDisplaySubject(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    public sealed class FluidRecipeDisplaySubject : RecipeDisplaySubject
    {
        public FluidId FluidId { get; }
        public double Amount { get; }

        internal FluidRecipeDisplaySubject(FluidId fluidId, double amount)
        {
            FluidId = fluidId;
            Amount = amount;
        }
    }

    public sealed class MachineRecipeSelectionRow
    {
        public MachineRecipeSummary Recipe { get; }
        public RecipeDisplaySubject Subject { get; }
        public bool Selected { get; }

        public MachineRecipeSelectionRow(MachineRecipeSummary recipe, RecipeDisplaySubject subject, bool selected)
        {
            Recipe = recipe;
            Subject = subject;
            Selected = selected;
        }
    }
}
