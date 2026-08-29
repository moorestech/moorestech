using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     出力スロットを選択レシピの生産物順へ束縛する判定器。スロットjは生産物jのレベルファミリー枠（2026-08-30裁定）
    ///     Decides the output slot binding; slot j is the level-family frame of output j (ruling 2026-08-30)
    /// </summary>
    internal class MachineOutputSlotBinding
    {
        private MachineRecipeMasterElement _recipe;

        public void SetRecipe(MachineRecipeMasterElement recipe)
        {
            _recipe = recipe;
        }

        // 実現出力kが積まれるスロット番号。未選択は-1
        // Slot realized output k lands in; -1 when unselected
        public int ResolveSlot(int realizedOutputIndex)
        {
            if (_recipe == null) return -1;
            return MachineRecipeSlotBindingUtil.FindOutputSlotIndex(_recipe, realizedOutputIndex);
        }

        // 空アイテムは取り出しなのでどのスロットでも許す
        // An empty stack means a take-out, so it is allowed on any slot
        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            if (itemStack.Id == ItemMaster.EmptyItemId) return true;
            if (_recipe == null || _recipe.OutputItems.Length <= localSlot) return false;

            var baseItemId = MasterHolder.ItemMaster.GetItemId(_recipe.OutputItems[localSlot].ItemGuid);
            return MachineRecipeSlotBindingUtil.IsOutputVariantOf(baseItemId, itemStack.Id);
        }
    }
}
