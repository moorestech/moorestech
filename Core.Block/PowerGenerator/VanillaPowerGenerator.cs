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
    public class VanillaPowerGenerator : IBlock,IPowerGenerator,IBlockInventory,IUpdate
    {
        private readonly int _blockId;
        private readonly int _intId;
        private readonly Dictionary<int,FuelSetting> _fuelSettings;

        private int _fuelItemId = ItemConst.NullItemId;
        private double _remainingFuelTime = 0;
        private readonly List<IItemStack> _fuelItemStacks;

        public VanillaPowerGenerator(int blockId, int intId,int fuelItemSlot,ItemStackFactory itemStackFactory,Dictionary<int,FuelSetting> fuelSettings)
        {
            _blockId = blockId;
            _intId = intId;
            _fuelSettings = fuelSettings;
            _fuelItemStacks = CreateEmptyItemStacksList.Create(fuelItemSlot,itemStackFactory);
            GameUpdate.AddUpdateObject(this);
        }
        public VanillaPowerGenerator(int blockId, int intId,string loadString,int fuelItemSlot,ItemStackFactory itemStackFactory,Dictionary<int,FuelSetting> fuelSettings)
        {
            _blockId = blockId;
            _intId = intId;
            _fuelSettings = fuelSettings;
            GameUpdate.AddUpdateObject(this);
            _fuelItemStacks = new List<IItemStack>();
            
            var split = loadString.Split(',');
            _fuelItemId = int.Parse(split[0]);
            _remainingFuelTime = double.Parse(split[1]);
            
            for (var i = 2; i < split.Length; i+=2)
            {
                _fuelItemStacks[i] = itemStackFactory.Create(int.Parse(split[i]),int.Parse(split[i+1]));
            }
        }

        //TODO セーブ、ロードのテストを作る
        public string GetSaveState()
        {
            //フォーマット
            //_fuelItemId,_remainingFuelTime,_fuelItemId1,_fuelItemCount1,_fuelItemId2,_fuelItemCount2,_fuelItemId3,_fuelItemCount3...
            var saveState = $"{_fuelItemId},{_remainingFuelTime}";
            foreach (var itemStack in _fuelItemStacks)
            {
                saveState += $",{itemStack.Id},{itemStack.Count}";
            }
            return saveState;
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


        public void Update()
        {
            //現在燃料を消費しているか判定
            //燃料が在る場合は燃料残り時間をUpdate時間分減らす
            if (_fuelItemId != ItemConst.NullItemId)
            {
                _remainingFuelTime -= GameUpdate.UpdateTime;
                
                //残り時間が0以下の時は燃料の設定をNullItemIdにする
                if (_remainingFuelTime <= 0)
                {
                    _fuelItemId = ItemConst.NullItemId;
                }
                return;
            }
            
            //燃料がない場合はスロットに燃料が在るか判定する
            //スロットに燃料がある場合は燃料の設定し、アイテムを1個減らす
            for (var i = 0; i < _fuelItemStacks.Count; i++)
            {
                //スロットに燃料がある場合
                var slotId = _fuelItemStacks[i].Id;
                if (!_fuelSettings.ContainsKey(slotId)) continue;
                //ID、残り時間を設定
                _fuelItemId = _fuelSettings[slotId].ItemId;
                _remainingFuelTime = _fuelSettings[slotId].Time;
                //アイテムを1個減らす
                _fuelItemStacks[i] = _fuelItemStacks[i].SubItem(1);
                return;
            }
        }
        
        public int OutputPower()
        {
            if (_fuelSettings.ContainsKey(_fuelItemId))
            {
                return _fuelSettings[_fuelItemId].Power;
            }
            return 0;
        }

        public int GetIntId() { return _intId; }
        public int GetBlockId() { return _blockId; }
        public void AddConnector(IBlockInventory blockInventory) { throw new Exception("発電機にアイテム出力スロットはありません"); }
        public void RemoveConnector(IBlockInventory blockInventory) { throw new Exception("発電機にアイテム出力スロットはありません"); }
    }
}