using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     入力スロット・入力タンクを選択レシピへ束縛する判定器。未選択は何も受け入れない（ADR 0042）
    ///     Decides the input slot/tank binding against the selected recipe; unselected accepts nothing (ADR 0042)
    /// </summary>
    internal class MachineInputSlotBinding
    {
        private MachineRecipeMasterElement _recipe;

        public void SetRecipe(MachineRecipeMasterElement recipe)
        {
            _recipe = recipe;
        }

        // このアイテムが積まれるスロット番号。未選択・レシピ外は-1
        // Slot the item stacks into; -1 when unselected or the recipe does not use it
        public int ResolveSlot(IItemStack itemStack)
        {
            if (_recipe == null) return -1;
            return MachineRecipeSlotBindingUtil.FindInputSlotIndex(_recipe, itemStack.Id);
        }

        // 空アイテムは取り出しなのでどのスロットでも許す
        // An empty stack means a take-out, so it is allowed on any slot
        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            if (itemStack.Id == ItemMaster.EmptyItemId) return true;
            return ResolveSlot(itemStack) == localSlot;
        }

        public bool IsFluidAllowedAt(int tankIndex, FluidId fluidId)
        {
            if (_recipe == null) return false;
            return MachineRecipeSlotBindingUtil.IsInputFluidBoundTo(_recipe, tankIndex, fluidId);
        }
    }
}
