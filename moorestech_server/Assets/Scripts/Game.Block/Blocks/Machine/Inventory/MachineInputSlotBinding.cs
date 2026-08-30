using System;
using System.Collections.Generic;
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
        private int _slotCount;

        // 物理スロット数を固定する（コンストラクタ相当。マスタ超過の素材数を境界内へ切り詰めるため）
        // Fix the physical slot count once (guards against a recipe with more inputs than the machine has slots)
        public void SetSlotCount(int slotCount)
        {
            _slotCount = slotCount;
        }

        public void SetRecipe(MachineRecipeMasterElement recipe)
        {
            _recipe = recipe;
        }

        // 束縛済みスロットを先頭から走査して列挙する（同一itemGuidが複数スロットに束縛されていても全て試せる）
        // Enumerate bound slots for the item from the front (so a duplicated itemGuid across slots can all be tried)
        public IEnumerable<int> ResolveBoundSlots(ItemId itemId)
        {
            if (_recipe == null) yield break;
            var boundCount = Math.Min(_recipe.InputItems.Length, _slotCount);
            for (var i = 0; i < boundCount; i++)
            {
                if (MachineRecipeSlotBindingUtil.IsInputBoundTo(_recipe, i, itemId)) yield return i;
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
