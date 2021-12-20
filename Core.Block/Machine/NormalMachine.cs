using System;
using System.Collections.Generic;
using System.Text;
using Core.Block.BlockInventory;
using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.Electric;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.Machine
{
    /// <summary>
    /// 「機械」というオブジェクト(ドメイン)事態の責務が大きすぎて、クラス自体の責務も大きくなってしまっているう
    /// 単純に別のクラスに分けるのも手かも知れないが、本質的な解決になるのだろうか？
    /// 現状箱のままにしておくが、今後機械関係のクラスに修正をする場合、この機械のクラス全体をリファクタする必要があるような気がする
    ///
    /// TODO それぞれの要素（ブロックインベントリ、プロセスなど）をコンポーネントにする
    /// </summary>
    public class NormalMachine : IBlock,IBlockInventory,IUpdate,IBlockElectric,IInventory
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private ProcessState _state = ProcessState.Idle;
        public List<IItemStack> InputSlotWithoutEmptyItemStack => _normalMachineInputInventory.InputSlotWithoutEmptyItemStack;
        public List<IItemStack> OutputSlotWithoutEmptyItemStack => _normalMachineOutputInventory.OutputSlotWithoutEmptyItemStack;
        
        private IMachineRecipeData _processingRecipeData;
        
        private readonly int _blockId;
        private readonly int _intId;

        public NormalMachine(int blockId, int intId,
            NormalMachineInputInventory normalMachineInputInventory,
            NormalMachineOutputInventory normalMachineOutputInventory )
        {
            
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            _blockId = blockId;
            _intId = intId;
            GameUpdate.AddUpdateObject(this);
        }
        public NormalMachine(int blockId, int intId,string loadState,
            ItemStackFactory itemStackFactory,
            IMachineRecipeConfig machineRecipeConfig,
            NormalMachineInputInventory normalMachineInputInventory,
            NormalMachineOutputInventory normalMachineOutputInventory )
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            _blockId = blockId;
            _intId = intId;
            LoadString(loadState,itemStackFactory,machineRecipeConfig);
            GameUpdate.AddUpdateObject(this);
        }

        private void LoadString(string loadString,ItemStackFactory itemStackFactory,IMachineRecipeConfig machineRecipeConfig)
        {
            var split = loadString.Split(',');
            int index = 1;
            int inventorySlot = 0;
            for (; split[index] != "outputSlot"; index+=2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                _normalMachineInputInventory.SetItem(inventorySlot,itemStackFactory.Create(id, count));
                inventorySlot++;
            }
            
            inventorySlot = 0;
            for (index++; split[index] != "state"; index+=2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                _normalMachineOutputInventory.SetItem(inventorySlot,itemStackFactory.Create(id, count));
                inventorySlot++;
            }
            index++;
            _state = (ProcessState) int.Parse(split[index]);
            index+=2;
            _remainingMillSecond = Double.Parse(split[index]);
            index+=2;
            int recipeId = int.Parse(split[index]);
            _processingRecipeData = machineRecipeConfig.GetRecipeData(recipeId);
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            //アイテムをインプットスロットに入れた後、プロセス開始できるなら開始
            var item = _normalMachineInputInventory.InsertItem(itemStack);
            return item;
        }
        public void AddConnector(IBlockInventory blockInventory)
        {
            _normalMachineOutputInventory.AddConnectInventory(blockInventory);
        }
        public void RemoveConnector(IBlockInventory blockInventory)
        {
            _normalMachineOutputInventory.RemoveConnectInventory(blockInventory);
        }

        /// <summary>
        /// インプットスロットが0から始まり、アウトプットスロットが続く
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public IItemStack GetItem(int slot)
        {
            if (slot < _normalMachineInputInventory.InputSlot.Count)
            {
                return _normalMachineInputInventory.InputSlot[slot];
            }
            slot -= _normalMachineInputInventory.InputSlot.Count;
            return _normalMachineOutputInventory.OutputSlot[slot];
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (slot < _normalMachineInputInventory.InputSlot.Count)
            {
                _normalMachineInputInventory.SetItem(slot,itemStack);
            }
            else
            {
                slot -= _normalMachineInputInventory.InputSlot.Count;
                _normalMachineOutputInventory.SetItem(slot, itemStack);
            }
        }

        /// <summary>
        /// アイテムの置き換えを実行しますが、同じアイテムIDの場合はそのまま現在のアイテムにスタックされ、スタックしきらなかったらその分を返します。
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="itemStack"></param>
        /// <returns></returns>
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            ItemProcessResult result;
            if (slot < _normalMachineInputInventory.InputSlot.Count)
            {
                //アイテムIDが同じの時はスタックして余ったものを返す
                var item = _normalMachineInputInventory.InputSlot[slot];
                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _normalMachineInputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }

                //違う場合はそのまま入れ替える
                _normalMachineInputInventory.SetItem(slot, itemStack);
                return item;
            }
            else
            {
                //アウトプットスロットのインデックスに変換する
                slot -= _normalMachineInputInventory.InputSlot.Count;

                var item = _normalMachineOutputInventory.OutputSlot[slot];

                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _normalMachineOutputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }
                _normalMachineOutputInventory.SetItem(slot, itemStack);
                return item;
            }
        }

        private bool IsAllowedToStartProcess
        {
            get
            {
                var recipe = _normalMachineInputInventory.GetRecipeData();
                return _state == ProcessState.Idle && 
                       _normalMachineInputInventory.IsAllowedToStartProcess && 
                       _normalMachineOutputInventory.IsAllowedToOutputItem(recipe);
            }
        }
            

        public void Update()
        {
            switch (_state)
            {
                case ProcessState.Idle :
                    Idle();
                    break;
                case ProcessState.Processing :
                    Processing();
                    break;
            }
        }
        private void Idle()
        {
            if (IsAllowedToStartProcess) StartProcessing();
        }
        private void StartProcessing()
        {
            _state = ProcessState.Processing;
            _processingRecipeData = _normalMachineInputInventory.GetRecipeData();
            _normalMachineInputInventory.ReduceInputSlot(_processingRecipeData);
            _remainingMillSecond = _processingRecipeData.Time;
        }

        private double _remainingMillSecond;
        private void Processing()
        {
            _remainingMillSecond -= GameUpdate.UpdateTime * (_nowPower / (double)RequestPower);
            if (_remainingMillSecond <= 0)
            {
                _state = ProcessState.Idle;
                _normalMachineOutputInventory.InsertOutputSlot(_processingRecipeData);
            }
        }

        
        
        private const int RequestPower = 100;
        private int _nowPower = 0;
        public int GetRequestPower(){return RequestPower;}
        public void SupplyPower(int power){_nowPower = power;}
        public int GetIntId(){return _intId;}
        public int GetBlockId() { return _blockId; }
        public string GetSaveState()
        {
            //フォーマット
            //inputSlot,item1 id,item1 count,item2 id,item2 count,outputSlot,item1 id,item1 count,item2 id,item2 count,state,0 or 1,remainingTime,500
            StringBuilder saveState = new StringBuilder("inputSlot,");
            //インプットスロットを保存
            foreach (var item in _normalMachineInputInventory.InputSlot)
            {
                saveState.Append(item.Id + "," + item.Count + ",");
            }
            saveState.Append("outputSlot,");
            //アウトプットスロットを保存
            foreach (var item in _normalMachineOutputInventory.OutputSlot)
            {
                saveState.Append(item.Id + "," + item.Count + ",");
            }
            //状態を保存
            saveState.Append("state,"+(int)_state + ",");
            //現在の残り時間を保存
            saveState.Append("remainingTime,"+_remainingMillSecond + ",");
            //レシピIDを保存
            saveState.Append("recipeId,"+_processingRecipeData.RecipeId);
            
            return saveState.ToString();
        }
    }

    public enum ProcessState
    {
        Idle,
        Processing
    }
}