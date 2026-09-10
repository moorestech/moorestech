using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     入力スロット・入力タンクを選択レシピへ束縛する判定器。未選択は何も受け入れない（ADR 0042）。
    ///     出力側が許可集合を受け取るのに対し入力側がレシピを受け取るのは、素材が「スロットiに素材i」と位置固定で、
    ///     必要数・液体量まで束縛が決めるためである（出力は品質変種やチップで許可集合が機械ごとに広がる）
    ///     Decides the input slot/tank binding against the selected recipe; unselected accepts nothing (ADR 0042).
    ///     Input takes the recipe (not an allowed set like the output side) because inputs are position-fixed - input i in slot i -
    ///     and the binding also owns the required counts and amounts, while the output's allowed set widens per machine
    /// </summary>
    internal class MachineInputSlotBinding
    {
        private MachineRecipeMasterElement _recipe;

        public void SetRecipe(MachineRecipeMasterElement recipe)
        {
            _recipe = recipe;
        }

        // レシピが束縛済みか。未選択の機械は何も受け入れないため、挿入経路の入口判定に使う
        // Whether a recipe is bound; an unselected machine accepts nothing, so insert paths gate on this
        public bool IsBound()
        {
            return _recipe != null;
        }

        // 束縛済みスロットを先頭から走査して列挙する（同一itemGuidが複数スロットに束縛されていても全て試せる）
        // Enumerate bound slots for the item from the front (so a duplicated itemGuid across slots can all be tried)
        public IEnumerable<int> ResolveBoundSlots(ItemId itemId)
        {
            if (_recipe == null) yield break;
            for (var i = 0; i < _recipe.InputItems.Length; i++)
            {
                if (MachineRecipeSlotBindingUtil.IsInputBoundTo(_recipe, i, itemId)) yield return i;
            }
        }

        // スロットiが要求する素材数（第一パスの充填上限）。束縛外は0
        // The input count slot i requires (the first pass's fill limit); unbound slots return 0
        public int RequiredCountAt(int slotIndex)
        {
            if (_recipe == null || slotIndex < 0 || _recipe.InputItems.Length <= slotIndex) return 0;
            return _recipe.InputItems[slotIndex].Count;
        }

        // タンクiが要求する液体量（第一パスの充填上限）。束縛外は0
        // The fluid amount tank i requires (the first pass's fill limit); unbound tanks return 0
        public double RequiredFluidAmountAt(int tankIndex)
        {
            if (_recipe == null || tankIndex < 0 || _recipe.InputFluids.Length <= tankIndex) return 0;
            return _recipe.InputFluids[tankIndex].Amount;
        }

        // 液体fluidIdを受け入れる束縛タンクを先頭から列挙する
        // Enumerate the bound tanks accepting fluidId, from the front
        public IEnumerable<int> ResolveBoundTanks(FluidId fluidId)
        {
            if (_recipe == null) yield break;
            for (var i = 0; i < _recipe.InputFluids.Length; i++)
            {
                if (MachineRecipeSlotBindingUtil.IsInputFluidBoundTo(_recipe, i, fluidId)) yield return i;
            }
        }

        // 空アイテムは取り出しなのでどのスロットでも許す
        // An empty stack means a take-out, so it is allowed on any slot
        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            if (itemStack.Id == ItemMaster.EmptyItemId) return true;
            if (_recipe == null) return false;
            return MachineRecipeSlotBindingUtil.IsInputBoundTo(_recipe, localSlot, itemStack.Id);
        }

        public bool IsFluidAllowedAt(int tankIndex, FluidId fluidId)
        {
            if (_recipe == null) return false;
            return MachineRecipeSlotBindingUtil.IsInputFluidBoundTo(_recipe, tankIndex, fluidId);
        }
    }
}
