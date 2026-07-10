using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;
namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     インプットのインベントリとアウトプットのインベントリを同じように扱う
    ///     Insertなどの処理は基本的にInputのインベントリにのみ行う
    /// </summary>
    public class VanillaMachineInputInventory : IVanillaMachineSubInventory
    {
        public IReadOnlyList<IItemStack> InputSlot => _itemDataStoreService.InventoryItems;
        IReadOnlyList<IItemStack> IVanillaMachineSubInventory.Items => InputSlot;
        public IReadOnlyList<FluidContainer> FluidInputSlot => _fluidContainers;
        private readonly BlockId _blockId;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly FluidContainer[] _fluidContainers;
        private readonly IGameUnlockStateData _gameUnlockStateData;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        public VanillaMachineInputInventory(
            BlockId blockId,
            int inputSlot,
            int innerTankCount,
            float innerTankCapacity,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate,
            BlockInstanceId blockInstanceId,
            IGameUnlockStateData gameUnlockStateData)
        {
            _blockId = blockId;
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _gameUnlockStateData = gameUnlockStateData;
            var option = new OpenableInventoryItemDataStoreServiceOption()
            {
                AllowMultipleStacksPerItemOnInsert = false,
            };
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, inputSlot, option);
            _fluidContainers = new FluidContainer[innerTankCount];
            for (var i = 0; i < innerTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }
        }
        public bool IsRecipeForThisMachine(MachineRecipeMasterElement recipe)
        {
            return MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid) == _blockId;
        }
        public bool IsRecipeUnlocked(MachineRecipeMasterElement recipe)
        {
            return _gameUnlockStateData.MachineRecipeUnlockStateInfos.TryGetValue(recipe.MachineRecipeGuid, out var unlockInfo) && unlockInfo.IsUnlocked;
        }
        public bool IsAllowedToStartProcess(MachineRecipeMasterElement recipe)
        {
            if (!IsRecipeForThisMachine(recipe)) return false;
            // 必要数のアイテムが揃うか確認
            // Confirm required item amounts are available
            var itemRequirements = new Dictionary<ItemId, (int consumed, int remained)>();
            foreach (var inputItem in recipe.InputItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                itemRequirements.TryGetValue(itemId, out var requirement);
                if (inputItem.IsRemain.HasValue && inputItem.IsRemain.Value)
                    requirement.remained = System.Math.Max(requirement.remained, inputItem.Count);
                else
                    requirement.consumed += inputItem.Count;
                itemRequirements[itemId] = requirement;
            }
            foreach (var (itemId, requirement) in itemRequirements)
            {
                var found = false;
                foreach (var slot in InputSlot)
                {
                    if (slot.Id != itemId || slot.Count < requirement.consumed + requirement.remained) continue;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            // 必要量の液体が揃うか確認
            // Confirm required fluid amounts are available
            var fluidRequirements = new Dictionary<FluidId, double>();
            foreach (var inputFluid in recipe.InputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                fluidRequirements.TryGetValue(fluidId, out var amount);
                fluidRequirements[fluidId] = amount + inputFluid.Amount;
            }
            foreach (var (fluidId, amount) in fluidRequirements)
            {
                var found = false;
                foreach (var container in FluidInputSlot)
                {
                    if (container.FluidId != fluidId || container.Amount < amount) continue;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }
        public List<IItemStack> ConsumeInputs(MachineRecipeMasterElement recipe)
        {
            var consumedItems = new List<IItemStack>();
            // 触媒以外の消費分を返却用に記録
            // Record consumed non-catalyst stacks for refund
            foreach (var inputItem in recipe.InputItems)
            {
                if (inputItem.IsRemain.HasValue && inputItem.IsRemain.Value) continue;
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                for (var i = 0; i < InputSlot.Count; i++)
                {
                    var source = InputSlot[i];
                    if (source.Id != itemId || source.Count < inputItem.Count) continue;
                    consumedItems.Add(source.SubItem(source.Count - inputItem.Count));
                    _itemDataStoreService.SetItem(i, source.SubItem(inputItem.Count));
                    break;
                }
            }
            // 液体は消費するが返却対象には含めない
            // Consume fluids without adding them to the refundable snapshot
            foreach (var inputFluid in recipe.InputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                for (var i = 0; i < _fluidContainers.Length; i++)
                {
                    var container = _fluidContainers[i];
                    if (container.FluidId != fluidId || container.Amount < inputFluid.Amount) continue;
                    container.Amount -= inputFluid.Amount;
                    if (container.Amount <= 0) container.FluidId = FluidMaster.EmptyFluidId;
                    break;
                }
            }
            return consumedItems;
        }
        public bool TryRefundConsumedItems(List<IItemStack> consumedItems, IOpenableInventory playerMainInventory)
        {
            // 機械入力への返却後の余りを仮計算する
            // Simulate machine-input refunds and collect the remaining stacks
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var simulatedMachineInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, InputSlot.Count, option);
            for (var i = 0; i < InputSlot.Count; i++) simulatedMachineInventory.SetItemWithoutEvent(i, InputSlot[i]);
            var playerRefunds = simulatedMachineInventory.InsertItem(consumedItems);
            // 全量返却できる場合だけ確定する
            // Mutate real inventories only when the player can accept every remainder
            if (!playerMainInventory.InsertionCheck(playerRefunds)) return false;
            var actualPlayerRefunds = _itemDataStoreService.InsertItem(consumedItems);
            playerMainInventory.InsertItem(actualPlayerRefunds);
            return true;
        }
        public IItemStack InsertItem(IItemStack itemStack) => _itemDataStoreService.InsertItem(itemStack);
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) => _itemDataStoreService.InsertItem(itemStacks);
        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }
        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItemWithoutEvent(slot, itemStack);
        }
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot, itemStack));
        }
    }
}
