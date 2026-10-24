using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Mooresmaster.Model.ItemsModule;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     機械のモジュールスロット（統合スロットの第3レンジ）
    ///     The machine's module slots (third range in unified numbering)
    /// </summary>
    public class VanillaMachineModuleInventory : IVanillaMachineSubInventory
    {
        public IReadOnlyList<IItemStack> ModuleSlot => _itemDataStoreService.InventoryItems;
        IReadOnlyList<IItemStack> IVanillaMachineSubInventory.Items => ModuleSlot;

        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly bool _allowQualityModule;

        public VanillaMachineModuleInventory(
            int moduleSlotCount,
            bool allowQualityModule,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate,
            BlockInstanceId blockInstanceId,
            int inputSlotSize,
            int outputSlotSize)
        {
            _allowQualityModule = allowQualityModule;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, moduleSlotCount);

            #region Internal

            void InvokeEvent(int slot, IItemStack itemStack)
            {
                // 第3レンジのためオフセットを加算して通知
                // Offset by the first two ranges when notifying
                blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                    blockInstanceId, slot + inputSlotSize + outputSlotSize, itemStack));
            }

            #endregion
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }

        // 不許可の品質モジュールだけを拒否する。非モジュールは従来どおり置ける（効果集計が無視する）
        // Refuse only a disallowed quality module; non-module items stay placeable as before (the aggregation ignores them)
        public MachineSlotPlacementCheck CheckPlacement(int localSlot, IItemStack itemStack)
        {
            if (itemStack.Id == ItemMaster.EmptyItemId) return MachineSlotPlacementCheck.Allowed;
            var module = MasterHolder.ItemMaster.GetModuleByItemIdOrNull(itemStack.Id);
            if (module == null) return MachineSlotPlacementCheck.Allowed;
            return IsModuleEffective(module) ? MachineSlotPlacementCheck.Allowed : MachineSlotPlacementCheck.ModuleNotAllowedOnMachine;
        }

        // この機械で効くモジュールか。品質は許可された機械だけで効く
        // Whether the module works on this machine; quality works only where allowed
        public bool IsModuleEffective(ModuleMasterElement module)
        {
            return _allowQualityModule || module.EffectAxis != ModuleMasterElement.EffectAxisConst.Quality;
        }

        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItemWithoutEvent(slot, itemStack);
        }
    }
}
