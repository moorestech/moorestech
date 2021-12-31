using System;
using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.PowerGenerator
{
    //TODO アイテムを挿入して発電するシステムを作る
    public class VanillaPowerGenerator : IBlock,IPowerGenerator,IBlockInventory,IUpdate
    {
        private readonly int _blockId;
        private readonly int _intId;
        private readonly Dictionary<int,FuelSetting> _fuelSettings;

        private int _fuelItemId = ItemConst.NullItemId;
        private readonly List<IItemStack> _fuelItemStacks;

        public VanillaPowerGenerator(int blockId, int intId,int fuelItemSlot,ItemStackFactory itemStackFactory,Dictionary<int,FuelSetting> fuelSettings)
        {
            _blockId = blockId;
            _intId = intId;
            _fuelSettings = fuelSettings;
            _fuelItemStacks = CreateEmptyItemStacksList.Create(fuelItemSlot,itemStackFactory);
        }

        public int OutputPower()
        {
            if (_fuelSettings.ContainsKey(_fuelItemId))
            {
                return _fuelSettings[_fuelItemId].Power;
            }
            return 0;
        }

        //TODO セーブ、ロードのテストを作る
        public string GetSaveState()
        {
            return "";
        }

        //アイテムを挿入するシステムを作る
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _fuelItemStacks.Count; i++)
            {
                if (!_fuelItemStacks[i].IsAllowedToAdd(itemStack)) continue;
                
                //インベントリにアイテムを入れる
                var r = _fuelItemStacks[i].AddItem(itemStack);
                _fuelItemStacks[i] = r.ProcessResultItemStack;
                
                //とった結果のアイテムを返す
                return r.RemainderItemStack;
            }
            return itemStack;
        }

        public void AddConnector(IBlockInventory blockInventory)
        {
            throw new Exception("発電機にアイテム出力スロットはありません");
        }

        public void RemoveConnector(IBlockInventory blockInventory)
        {
            throw new Exception("発電機にアイテム出力スロットはありません");
        }

        public void Update()
        {
            
        }

        public int GetIntId() { return _intId; }
        public int GetBlockId() { return _blockId; }
    }
}