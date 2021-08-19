using System.Collections.Generic;
using industrialization.Core.Config.Recipe.Data;
using industrialization.Core.Electric;
using industrialization.Core.GameSystem;
using industrialization.Core.Item;

namespace industrialization.Core.Block.Machine
{
    public class NormalMachine : BlockBase,IBlockInventory,IUpdate,IBlockElectric
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private ProcessState _state = ProcessState.Idle;
        public List<IItemStack> InputSlot => _normalMachineInputInventory.InputSlot;
        public List<IItemStack> OutputSlot => _normalMachineOutputInventory.OutputSlot;
        
        public NormalMachine(int blockId, int intId,
            NormalMachineInputInventory normalMachineInputInventory,
            NormalMachineOutputInventory normalMachineOutputInventory ) : base(blockId, intId)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            intId = intId;
            BlockID = blockId;
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
        public int GetIntId(){return intID;}
    }

    enum ProcessState
    {
        Idle,
        Processing
    }
}