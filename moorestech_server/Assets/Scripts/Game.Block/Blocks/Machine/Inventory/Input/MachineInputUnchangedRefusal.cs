using System;
using Core.Inventory;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory.Input
{
    /// <summary>
    ///     丸ごと断った直近の搬入と、その答えを決めた入力一式の控え。同じ個体が何も変わっていない機械へまた届いたかを答える
    ///     The latest wholly refused inflow together with every input that decided it; answers whether the same instance has arrived at an unchanged machine again
    ///     詰まったベルトとチェストは同じ個体を毎tick渡し直す。答えが同じと分かっている間は仮想挿入をやり直さない（毎tickの作り直しがGCを呼ぶため）
    ///     Blocked belts and chests re-offer the same instance every tick; while the answer is known to be the same the virtual insert is not redone (redoing it every tick feeds the GC)
    /// </summary>
    internal class MachineInputUnchangedRefusal
    {
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly MachineInputSlotBinding _slotBinding;

        // 仮想挿入が読むものの全部: 届いた個体・各スロットの個体・レシピ・製作上限・関係する品のスタック上限
        // Everything the virtual insert reads: the offered instance, each slot's instance, the recipe, the craft cap and the stack limits of the items involved
        private IItemStack _offered;
        private MachineRecipeMasterElement _recipe;
        private int _craftCapLevel;
        private int _offeredMaxStack;
        private IItemStack[] _slots = Array.Empty<IItemStack>();
        private int[] _slotMaxStacks = Array.Empty<int>();

        public MachineInputUnchangedRefusal(OpenableInventoryItemDataStoreService itemDataStoreService, MachineInputSlotBinding slotBinding)
        {
            _itemDataStoreService = itemDataStoreService;
            _slotBinding = slotBinding;
        }

        public void Remember(IItemStack offered)
        {
            var slots = _itemDataStoreService.InventoryItems;
            if (_slots.Length != slots.Count)
            {
                _slots = new IItemStack[slots.Count];
                _slotMaxStacks = new int[slots.Count];
            }

            _offered = offered;
            _slotBinding.TryGetRecipe(out _recipe);
            _craftCapLevel = _slotBinding.ResolveCraftCapLevel();
            _offeredMaxStack = MaxStackOf(offered);
            for (var i = 0; i < slots.Count; i++)
            {
                _slots[i] = slots[i];
                _slotMaxStacks[i] = MaxStackOf(slots[i]);
            }
        }

        public void Forget()
        {
            _offered = null;
        }

        // ItemStackは不変なので、個体が同じなら中身も同じ。上限は解放で変わるので値そのものを比べる
        // An ItemStack is immutable, so the same instance means the same content; limits change by unlocks, so their values are compared directly
        public bool Matches(IItemStack offered)
        {
            if (_offered == null || !ReferenceEquals(_offered, offered)) return false;

            _slotBinding.TryGetRecipe(out var recipe);
            if (!ReferenceEquals(_recipe, recipe)) return false;
            if (_craftCapLevel != _slotBinding.ResolveCraftCapLevel()) return false;
            if (_offeredMaxStack != MaxStackOf(offered)) return false;

            var slots = _itemDataStoreService.InventoryItems;
            if (_slots.Length != slots.Count) return false;
            for (var i = 0; i < slots.Count; i++)
            {
                if (!ReferenceEquals(_slots[i], slots[i])) return false;
                if (_slotMaxStacks[i] != MaxStackOf(slots[i])) return false;
            }
            return true;
        }

        // 空スロットに上限は無い
        // An empty slot has no limit
        private static int MaxStackOf(IItemStack stack)
        {
            return stack.Id == ItemMaster.EmptyItemId ? 0 : ItemStackLevelDataStore.Instance.GetMaxStack(stack.Id);
        }
    }
}
