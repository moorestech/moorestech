using System.Collections.Generic;
using Core.Block.RecipeConfig.Data;
using Core.Electric;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.Machine
{
    public class NormalMachine : IBlock,IBlockInventory,IUpdate,IBlockElectric
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private ProcessState _state = ProcessState.Idle;
        public List<IItemStack> InputSlot => _normalMachineInputInventory.InputSlot;
        public List<IItemStack> OutputSlot => _normalMachineOutputInventory.OutputSlot;
        
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
        public IItemStack InsertItem(IItemStack itemStack)
        {
            //アイテムをインプットスロットに入れた後、プロセス開始できるなら開始
            var item = _normalMachineInputInventory.InsertItem(itemStack);
            return item;
        }
        public void ChangeConnector(IBlockInventory blockInventory)
        {
            _normalMachineOutputInventory.ChangeConnectInventory(blockInventory);
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

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            ItemProcessResult result;
            if (slot < _normalMachineInputInventory.InputSlot.Count)
            {
                result = _normalMachineInputInventory.InputSlot[slot].AddItem(itemStack);
                _normalMachineInputInventory.InputSlot[slot] = result.ProcessResultItemStack;
                return result.RemainderItemStack;
            }
            
            slot -= _normalMachineInputInventory.InputSlot.Count;
            result = _normalMachineOutputInventory.OutputSlot[slot].AddItem(itemStack);
            _normalMachineOutputInventory.OutputSlot[slot] = result.ProcessResultItemStack;
            return result.RemainderItemStack;
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

        private IMachineRecipeData _processingRecipeData;
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
            _remainingMillSecond -= GameUpdate.UpdateTime * (_nowPower / (double)requestPower);
            if (_remainingMillSecond <= 0)
            {
                _state = ProcessState.Idle;
                _normalMachineOutputInventory.InsertOutputSlot(_processingRecipeData);
            }
        }

        
        
        //TODO コンフィグに必要電力量を追加
        private const int requestPower = 100;
        private int _nowPower = 0;
        public int RequestPower(){return requestPower;}
        public void SupplyPower(int power){_nowPower = power;}
        public int GetIntId(){return _intId;}
        public int GetBlockId() { return _blockId; }
    }

    enum ProcessState
    {
        Idle,
        Processing
    }
}