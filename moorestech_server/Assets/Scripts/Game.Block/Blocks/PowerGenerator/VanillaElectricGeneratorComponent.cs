using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.PowerGenerator
{
    public class VanillaElectricGeneratorComponent : IElectricGenerator, IBlockInventory, IOpenableInventory, IBlockSaveState, IUpdatableBlockComponent, IFluidInventory
    {
        private readonly BlockComponentManager _blockComponentManager = new();
        private readonly ElectricPower _infinityPower;
        private readonly bool _isInfinityPower;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly VanillaElectricGeneratorFuelService _fuelService;
        
        public VanillaElectricGeneratorComponent(VanillaPowerGeneratorProperties data)
        {
            BlockPositionInfo = data.BlockPositionInfo;
            BlockInstanceId = data.BlockInstanceId;
            _isInfinityPower = data.IsInfinityPower;
            _infinityPower = data.InfinityPower;
            
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, data.FuelItemSlot);
            _fuelService = new VanillaElectricGeneratorFuelService(
                data.FuelSettings,
                data.FluidFuelSettings,
                _itemDataStoreService,
                data.FuelFluidTankCapacity);
            
            if (data.InventoryInputConnectorComponent != null)
            {
                _blockComponentManager.AddComponent(data.InventoryInputConnectorComponent);
            }

            if (data.FluidInputConnectorComponent != null)
            {
                _blockComponentManager.AddComponent(data.FluidInputConnectorComponent);
            }
        }
        
        public VanillaElectricGeneratorComponent(Dictionary<string, string> componentStates, VanillaPowerGeneratorProperties data) : this(data)
        {
            if (!componentStates.TryGetValue(SaveKey, out var stateRaw)) return;

            var saveData = JsonConvert.DeserializeObject<VanillaElectricGeneratorSaveJsonObject>(stateRaw);
            if (saveData == null) return;

            _fuelService.Restore(saveData);
            RestoreInventory(saveData);

            #region Internal

            void RestoreInventory(VanillaElectricGeneratorSaveJsonObject dataObject)
            {
                if (dataObject.Items == null) return;

                for (var i = 0; i < dataObject.Items.Count; i++)
                {
                    _itemDataStoreService.SetItem(i, dataObject.Items[i].ToItemStack());
                }
            }

            #endregion
        }
        public BlockPositionInfo BlockPositionInfo { get; }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemStack);
        }
        
        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.GetItem(slot);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            _itemDataStoreService.SetItem(slot, itemStack);
        }
        
        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _itemDataStoreService.GetSlotSize();
        }
        
        public string SaveKey { get; } = typeof(VanillaElectricGeneratorComponent).FullName;
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            var saveData = new VanillaElectricGeneratorSaveJsonObject
            {
                Items = _itemDataStoreService.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
            _fuelService.WritSaveData(saveData);
            return JsonConvert.SerializeObject(saveData);
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        
        public bool IsDestroy { get; private set; }
        
        public ElectricPower OutputEnergy()
        {
            BlockException.CheckDestroy(this);
            
            if (_isInfinityPower) return _infinityPower;
            return _fuelService.GetOutputEnergy();
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            return _itemDataStoreService.CreateCopiedItems();
        }
        
        
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }
        
        public IItemStack InsertItem(ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemId, count);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemStacks);
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }
        
        public void SetItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            _itemDataStoreService.SetItem(slot, itemId, count);
        }
        
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }
        
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_isInfinityPower) return;

            _fuelService.Update();
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            BlockException.CheckDestroy(this);
            return _fuelService.AddLiquid(fluidStack, source);
        }

        public List<FluidStack> GetFluidInventory()
        {
            BlockException.CheckDestroy(this);
            return _fuelService.GetFluidInventory();
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var properties = new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack);
            blockInventoryUpdate.OnInventoryUpdateInvoke(properties);
        }
    }
    
    public class VanillaElectricGeneratorSaveJsonObject
    {
        [JsonProperty("currentFuelItemGuid")]
        public string CurrentFuelItemGuidStr;
        [JsonIgnore]
        public Guid? CurrentFuelItemGuid => Guid.TryParse(CurrentFuelItemGuidStr, out var guid) ? guid : null;

        [JsonProperty("currentFuelFluidGuid")]
        public string CurrentFuelFluidGuidStr;
        [JsonIgnore]
        public Guid? CurrentFuelFluidGuid => Guid.TryParse(CurrentFuelFluidGuidStr, out var guid) ? guid : null;

        [JsonProperty("inventory")]
        public List<ItemStackSaveJsonObject> Items;

        [JsonProperty("remainingFuelTime")]
        public double RemainingFuelTime;

        [JsonProperty("activeFuelType")]
        public string ActiveFuelType;

        [JsonProperty("fluidTank")]
        public VanillaElectricGeneratorFluidSaveJsonObject FluidTank;
    }

    public class VanillaElectricGeneratorFluidSaveJsonObject
    {
        [JsonProperty("fluidGuid")]
        public string FluidGuidStr;
        [JsonIgnore]
        public Guid? FluidGuid => Guid.TryParse(FluidGuidStr, out var guid) ? guid : null;

        [JsonProperty("amount")]
        public double Amount;
    }
}
