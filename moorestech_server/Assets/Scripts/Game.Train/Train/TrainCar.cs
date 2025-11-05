using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using System;
using System.Collections.Generic;
using Game.Train.Event;


namespace Game.Train.Train
{
    /// <summary>
    /// 列車編成を構成する1両を表すクラス
    /// Represents a single car within a train formation.
    /// </summary>
    public class TrainCar
    {
        const int WHEIGHT_PER_SLOT = 40;
        const int FUEL_WEIGHT_PER_SLOT = 40;
        const int DEFAULT_WEIGHT = 120;
        const int DEFAULT_TRACTION = 100;
        private readonly Guid _carId = Guid.NewGuid();
        // 駆動力 (動力車での推進力、貨車では0)
        public int TractionForce { get; private set; }

        // インベントリスロット数 (貨車での容量、動力車では0)
        public int InventorySlots { get; private set; }

        // 燃料のインベントリスロット数 (動力車での燃料容量、貨車では0)
        public int FuelSlots { get; private set; }

        //列車自体の長さ
        public int Length { get; private set; }
        //列車が駅とドッキングしているかどうか
        public bool IsDocked => dockingblock != null; // ドッキングしているかどうかのプロパティ
        public Guid CarId => _carId;
        public IBlock dockingblock { get; set; }// このTrainCarがcargoやstation駅blockでドッキングしているときにのみ非nullになる。前輪を登録

        private readonly IItemStack[] _inventoryItems;
        private readonly IItemStack[] _fuelItems;
        public bool IsFacingForward { get; private set; }
        
        private readonly TrainUpdateEvent _trainUpdateEvent;

        public TrainCar(int tractionForce, int inventorySlots, int length, int fuelSlots = 0, bool isFacingForward = true)
        {
            TractionForce = tractionForce;
            InventorySlots = inventorySlots;
            Length = length;
            IsFacingForward = isFacingForward;
            if (fuelSlots < 0)
            {
                fuelSlots = 0;
            }
            FuelSlots = fuelSlots;
            dockingblock = null;
            
            _trainUpdateEvent = (TrainUpdateEvent)ServerContext.GetService<ITrainUpdateEvent>();

            // インベントリー配列を初期化
            _inventoryItems = new IItemStack[inventorySlots];
            for (int i = 0; i < inventorySlots; i++)
            {
                _inventoryItems[i] = ServerContext.ItemStackFactory.CreatEmpty();
            }

            if (fuelSlots > 0)
            {
                _fuelItems = new IItemStack[fuelSlots];
                for (int i = 0; i < fuelSlots; i++)
                {
                    _fuelItems[i] = ServerContext.ItemStackFactory.CreatEmpty();
                }
            }
            else
            {
                _fuelItems = Array.Empty<IItemStack>();
            }
        }

        //重さ、推進力を得る
        public (int,int) GetWeightAndTraction()
        {
            return (DEFAULT_WEIGHT +
                InventorySlots * WHEIGHT_PER_SLOT +
                FuelSlots * FUEL_WEIGHT_PER_SLOT
                , IsFacingForward ? TractionForce * DEFAULT_TRACTION : 0);
        }

        public void SetFacingForward(bool isFacingForward)
        {
            IsFacingForward = isFacingForward;
        }




        /// <summary>
        /// ///////以下アイテム関連のメソッド////////
        /// </summary>
        // アイテム挿入機能  
        public IItemStack InsertItem(IItemStack itemStack)
        {
            if (itemStack == null || itemStack.Count == 0)
                return itemStack;

            for (int i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i].Id == ItemMaster.EmptyItemId)
                {
                    // 空きスロットに挿入  
                    _inventoryItems[i] = ServerContext.ItemStackFactory.Create(
                        itemStack.Id, 1, itemStack.ItemInstanceId);
                    InvokeInventoryUpdate(i);
                    return itemStack.SubItem(1);
                }
                else if (_inventoryItems[i].Id == itemStack.Id &&
                         _inventoryItems[i].Count < MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).MaxStack)
                {
                    // 同じアイテムでスタック可能  
                    var addCount = Math.Min(itemStack.Count,
                        MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).MaxStack - _inventoryItems[i].Count);
                    _inventoryItems[i] = ServerContext.ItemStackFactory.Create(
                        itemStack.Id, _inventoryItems[i].Count + addCount, itemStack.ItemInstanceId);
                    InvokeInventoryUpdate(i);
                    return itemStack.SubItem(addCount);
                }
            }

            // 挿入できない場合は元のアイテムを返す  
            return itemStack;
        }

        // アイテム取得機能  
        public IItemStack GetItem(int slot)
        {
            if (slot < 0 || slot >= _inventoryItems.Length)
                return ServerContext.ItemStackFactory.CreatEmpty();

            return _inventoryItems[slot];
        }

        // アイテム設定機能  
        public void SetItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= _inventoryItems.Length)
                return;

            _inventoryItems[slot] = itemStack ?? ServerContext.ItemStackFactory.CreatEmpty();
            InvokeInventoryUpdate(slot);
        }

        public void Destroy()
        {
            _trainUpdateEvent.InvokeTrainRemoved(_carId);
        }

        private void InvokeInventoryUpdate(int slot)
        {
            var item = _inventoryItems[slot] ?? ServerContext.ItemStackFactory.CreatEmpty();
            _trainUpdateEvent.InvokeInventoryUpdate(new TrainInventoryUpdateEventProperties(_carId, slot, item));
        }

        // インベントリーサイズ取得  
        public int GetSlotSize()
        {
            return _inventoryItems.Length;
        }

        // インベントリーが満杯かチェック  
        public bool IsInventoryFull()
        {
            foreach (var (_, stack) in EnumerateInventory())
            {
                if (stack.Id == ItemMaster.EmptyItemId)
                    return false;

                if (stack.Count < MasterHolder.ItemMaster.GetItemMaster(stack.Id).MaxStack)
                    return false;
            }
            return true;
        }

        // インベントリーが空かチェック
        public bool IsInventoryEmpty()
        {
            foreach (var (_, stack) in EnumerateInventory())
            {
                if (stack.Id != ItemMaster.EmptyItemId && stack.Count > 0)
                    return false;
            }
            return true;
        }

        public IEnumerable<(int slot, IItemStack item)> EnumerateInventory()
        {
            for (int i = 0; i < _inventoryItems.Length; i++)
            {
                yield return (i, _inventoryItems[i]);
            }
        }

        public IEnumerable<(int slot, IItemStack item)> EnumerateFuelSlots()
        {
            for (int i = 0; i < _fuelItems.Length; i++)
            {
                yield return (i, _fuelItems[i]);
            }
        }

        public IItemStack GetFuelItem(int slot)
        {
            if (slot < 0 || slot >= _fuelItems.Length)
            {
                return ServerContext.ItemStackFactory.CreatEmpty();
            }

            return _fuelItems[slot];
        }

        public void SetFuelItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= _fuelItems.Length)
            {
                return;
            }

            _fuelItems[slot] = itemStack ?? ServerContext.ItemStackFactory.CreatEmpty();
        }



    }

}
