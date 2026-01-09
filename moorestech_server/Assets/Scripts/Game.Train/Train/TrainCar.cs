using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.Utility;
using Mooresmaster.Model.TrainModule;
using System;
using System.Collections.Generic;


namespace Game.Train.Train
{
    /// <summary>
    /// 列車編成を構成する1両を表すクラス
    /// Represents a single car within a train formation.
    /// </summary>
    public class TrainCar
    {
        private readonly Guid _carId = Guid.NewGuid();
        
        // 列車のマスターデータ
        public TrainCarMasterElement TrainCarMasterElement { get; }
        
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

        //TODO燃料スロット数削除について修正は今後
        public TrainCar(TrainCarMasterElement trainCarMaster, bool isFacingForward = true, int fuelSlots = 0)
        {
            TrainCarMasterElement = trainCarMaster;
            TractionForce = trainCarMaster.TractionForce;
            InventorySlots = trainCarMaster.InventorySlots;
            Length = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            IsFacingForward = isFacingForward;
            if (fuelSlots < 0)
            {
                fuelSlots = 0;
            }
            FuelSlots = fuelSlots;
            dockingblock = null;
            
            _trainUpdateEvent = (TrainUpdateEvent)ServerContext.GetService<ITrainUpdateEvent>();

            // インベントリー配列を初期化
            _inventoryItems = new IItemStack[trainCarMaster.InventorySlots];
            for (int i = 0; i < trainCarMaster.InventorySlots; i++)
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
            return (TrainMotionParameters.DEFAULT_WEIGHT + InventorySlots * TrainMotionParameters.WEIGHT_PER_SLOT
                , IsFacingForward ? TractionForce * TrainMotionParameters.DEFAULT_TRACTION : 0);
        }

        public void SetFacingForward(bool isFacingForward)
        {
            IsFacingForward = isFacingForward;
        }
        public void Reverse()
        {
            IsFacingForward = !IsFacingForward;
        }

        public TrainCarSaveData CreateTrainCarSaveData()
        {
            var inventoryItems = new List<ItemStackSaveJsonObject>(this.InventorySlots);
            for (int i = 0; i < this.InventorySlots; i++)
            {
                inventoryItems.Add(new ItemStackSaveJsonObject(this.GetItem(i)));
            }

            var fuelItems = new List<ItemStackSaveJsonObject>(this.FuelSlots);
            for (int i = 0; i < this.FuelSlots; i++)
            {
                fuelItems.Add(new ItemStackSaveJsonObject(this.GetFuelItem(i)));
            }

            SerializableVector3Int? dockingPosition = null;
            if (this.dockingblock != null)
            {
                var blockPosition = this.dockingblock.BlockPositionInfo.OriginalPos;
                dockingPosition = new SerializableVector3Int(blockPosition.x, blockPosition.y, blockPosition.z);
            }

            return new TrainCarSaveData
            {
                TrainCarGuid = this.TrainCarMasterElement.TrainCarGuid,
                IsFacingForward = this.IsFacingForward,
                DockingBlockPosition = dockingPosition,
                InventoryItems = inventoryItems,
                FuelItems = fuelItems
            };
        }


        public static TrainCar RestoreTrainCar(TrainCarSaveData data)
        {
            if (data == null)
                return null;

            if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(data.TrainCarGuid, out var trainCarMaster)) throw new Exception("trainCarMaster is not found");
            var isFacingForward = data.IsFacingForward;
            var car = new TrainCar(trainCarMaster, isFacingForward);
            var empty = ServerContext.ItemStackFactory.CreatEmpty();

            for (int i = 0; i < car.GetSlotSize(); i++)
            {
                IItemStack item = empty;
                if (data.InventoryItems != null && i < data.InventoryItems.Count)
                {
                    item = data.InventoryItems[i]?.ToItemStack() ?? empty;
                }
                car.SetItem(i, item);
            }

            for (int i = 0; i < car.FuelSlots; i++)
            {
                IItemStack item = empty;
                if (data.FuelItems != null && i < data.FuelItems.Count)
                {
                    item = data.FuelItems[i]?.ToItemStack() ?? empty;
                }
                car.SetFuelItem(i, item);
            }

            if (data.DockingBlockPosition.HasValue)
            {
                var block = ServerContext.WorldBlockDatastore.GetBlock((UnityEngine.Vector3Int)data.DockingBlockPosition.Value);
                if (block != null)
                {
                    car.dockingblock = block;
                }
            }
            return car;
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
