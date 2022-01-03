using Core.Block.Blocks.Machine.Inventory;
using Core.Block.RecipeConfig.Data;
using Core.Update;

namespace Core.Block.Blocks.Machine
{
    public class VanillaMachineRunProcess : IUpdate
    {
        private IMachineRecipeData _processingRecipeData;
        private ProcessState _state = ProcessState.Idle;

        public ProcessState State => _state;

        public double RemainingMillSecond => _remainingMillSecond;
        public int RecipeDataId => _processingRecipeData.RecipeId;

        private readonly int _requestPower;
        private int _nowPower = 0;
        
        
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        
        public VanillaMachineRunProcess(
            VanillaMachineInputInventory vanillaMachineInputInventory, 
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            IMachineRecipeData machineRecipeData, int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _processingRecipeData = machineRecipeData;
            _requestPower = requestPower;

            GameUpdate.AddUpdateObject(this);
        }
        public VanillaMachineRunProcess(
            VanillaMachineInputInventory vanillaMachineInputInventory, 
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState state,double remainingMillSecond,IMachineRecipeData processingRecipeData, int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            
            _processingRecipeData = processingRecipeData;
            _requestPower = requestPower;
            _state = state;
            _remainingMillSecond = remainingMillSecond;

            GameUpdate.AddUpdateObject(this);
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
            _processingRecipeData = _vanillaMachineInputInventory.GetRecipeData();
            _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipeData);
            _remainingMillSecond = _processingRecipeData.Time;
        }

        private double _remainingMillSecond;
        private void Processing()
        {
            _remainingMillSecond -= GameUpdate.UpdateTime * (_nowPower / (double)_requestPower);
            if (_remainingMillSecond <= 0)
            {
                _state = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipeData);
            }
        }
        private bool IsAllowedToStartProcess
        {
            get
            {
                var recipe = _vanillaMachineInputInventory.GetRecipeData();
                return _state == ProcessState.Idle && 
                       _vanillaMachineInputInventory.IsAllowedToStartProcess && 
                       _vanillaMachineOutputInventory.IsAllowedToOutputItem(recipe);
            }
        }
        
        public int GetRequestPower(){return _requestPower;}
        public void SupplyPower(int power){_nowPower = power;}
    }

    public enum ProcessState
    {
        Idle,
        Processing
    }
}