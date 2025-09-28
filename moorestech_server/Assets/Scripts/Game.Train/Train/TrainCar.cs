using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using System;


namespace Game.Train.Train
{
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
        public TrainCar(int tractionForce, int inventorySlots, int length)
        {
            TractionForce = tractionForce;
            InventorySlots = inventorySlots;
            Length = length;
            dockingblock = null;

            // インベントリー配列を初期化  
            _inventoryItems = new IItemStack[inventorySlots];
            for (int i = 0; i < inventorySlots; i++)
            {
                _inventoryItems[i] = ServerContext.ItemStackFactory.CreatEmpty();
            }
        }


        //重さ、推進力を得る
        public (int,int) GetWeightAndTraction()
        {
            return (DEFAULT_WEIGHT +
                InventorySlots * WHEIGHT_PER_SLOT +
                FuelSlots * FUEL_WEIGHT_PER_SLOT
                , TractionForce * DEFAULT_TRACTION);
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
        }

        // インベントリーサイズ取得  
        public int GetSlotSize()
        {
            return _inventoryItems.Length;
        }

        // インベントリーが満杯かチェック  
        public bool IsInventoryFull()
        {
            for (int i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i].Id == ItemMaster.EmptyItemId)
                    return false;

                if (_inventoryItems[i].Count < MasterHolder.ItemMaster.GetItemMaster(_inventoryItems[i].Id).MaxStack)
                    return false;
            }
            return true;
        }

        // インベントリーが空かチェック  
        public bool IsInventoryEmpty()
        {
            for (int i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i].Id != ItemMaster.EmptyItemId && _inventoryItems[i].Count > 0)
                    return false;
            }
            return true;
        }



    }

}
