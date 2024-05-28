using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Const;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using UniRx;

namespace Game.Block.Blocks.PowerGenerator
{
    public class VanillaElectricGeneratorComponent : IElectricGenerator, IBlockInventory, IOpenableInventory, IBlockStateChange, IBlockSaveState
    {
        public BlockPositionInfo BlockPositionInfo { get; }

        public int EntityId { get; }
        
        public bool IsDestroy { get; private set; }
        
        
        public ReadOnlyCollection<IItemStack> Items => _itemDataStoreService.Items;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        public IObservable<BlockState> OnChangeBlockState => _onBlockStateChange;
        private readonly Subject<BlockState> _onBlockStateChange = new();
        
        private readonly BlockComponentManager _blockComponentManager = new();
        private readonly Dictionary<int, FuelSetting> _fuelSettings;

        private readonly int _infinityPower;
        private readonly bool _isInfinityPower;
        
        private int _fuelItemId = ItemConst.EmptyItemId;
        private double _remainingFuelTime;
        
        private readonly IDisposable _updateObservable;

        public VanillaElectricGeneratorComponent(VanillaPowerGeneratorProperties data)
        {
            BlockPositionInfo = data.BlockPositionInfo;
            EntityId = data.EntityId;
            _fuelSettings = data.FuelSettings;
            _isInfinityPower = data.IsInfinityPower;
            _infinityPower = data.InfinityPower;

            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, data.FuelItemSlot);
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());

            _blockComponentManager.AddComponent(data.InventoryInputConnectorComponent);
        }

        public VanillaElectricGeneratorComponent(VanillaPowerGeneratorProperties data, string state) : this(data)
        {
            var split = state.Split(',');
            _fuelItemId = int.Parse(split[0]);
            _remainingFuelTime = double.Parse(split[1]);

            var slot = 0;
            for (var i = 2; i < split.Length; i += 2)
            {
                var itemHash = long.Parse(split[i]);
                var count = int.Parse(split[i + 1]);
                var item = ServerContext.ItemStackFactory.Create(itemHash, count);
                _itemDataStoreService.SetItem(slot, item);
                slot++;
            }
        }
        
        public BlockState GetBlockState() { return new BlockState(); }

        public string GetSaveState()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);
            
            //フォーマット
            //_fuelItemId,_remainingFuelTime,_fuelItemId1,_fuelItemCount1,_fuelItemId2,_fuelItemCount2,_fuelItemId3,_fuelItemCount3...
            var saveState = $"{_fuelItemId},{_remainingFuelTime}";
            foreach (var itemStack in _itemDataStoreService.Inventory)
                saveState += $",{itemStack.ItemHash},{itemStack.Count}";

            return saveState;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);
            
            return _itemDataStoreService.InsertItem(itemStack);
        }

        public IItemStack GetItem(int slot)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _itemDataStoreService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            _itemDataStoreService.SetItem(slot, itemStack);
        }

        public int GetSlotSize()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _itemDataStoreService.GetSlotSize();
        }

        public int OutputEnergy()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            if (_isInfinityPower) return _infinityPower;
            if (_fuelSettings.TryGetValue(_fuelItemId, out var fuelSetting)) return fuelSetting.Power;

            return 0;
        }

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _itemDataStoreService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _itemDataStoreService.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _itemDataStoreService.InsertionCheck(itemStacks);
        }

        public void SetItem(int slot, int itemId, int count)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            _itemDataStoreService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }


        private void Update()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            //現在燃料を消費しているか判定
            //燃料が在る場合は燃料残り時間をUpdate時間分減らす
            if (_fuelItemId != ItemConst.EmptyItemId)
            {
                _remainingFuelTime -= GameUpdater.UpdateMillSecondTime;

                //残り時間が0以下の時は燃料の設定をNullItemIdにする
                if (_remainingFuelTime <= 0) _fuelItemId = ItemConst.EmptyItemId;

                return;
            }

            //燃料がない場合はスロットに燃料が在るか判定する
            //スロットに燃料がある場合は燃料の設定し、アイテムを1個減らす
            for (var i = 0; i < _itemDataStoreService.GetSlotSize(); i++)
            {
                //スロットに燃料がある場合
                var slotItemId = _itemDataStoreService.Inventory[i].Id;
                if (!_fuelSettings.ContainsKey(slotItemId)) continue;

                //ID、残り時間を設定
                _fuelItemId = _fuelSettings[slotItemId].ItemId;
                _remainingFuelTime = _fuelSettings[slotItemId].Time;

                //アイテムを1個減らす
                _itemDataStoreService.SetItem(i, _itemDataStoreService.Inventory[i].SubItem(1));
                return;
            }
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var properties = new BlockOpenableInventoryUpdateEventProperties(EntityId, slot, itemStack);
            blockInventoryUpdate.OnInventoryUpdateInvoke(properties);
        }
        
        public void Destroy()
        {
            IsDestroy = true;
            _updateObservable.Dispose();
        }
    }
}