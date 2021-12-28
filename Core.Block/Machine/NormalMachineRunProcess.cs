using Core.Block.Machine.Inventory;
using Core.Block.RecipeConfig.Data;
using Core.Update;

namespace Core.Block.Machine
{
    public class NormalMachineRunProcess : IUpdate
    {
        private IMachineRecipeData _processingRecipeData;
        private ProcessState _state = ProcessState.Idle;

        public ProcessState State => _state;

        public double RemainingMillSecond => _remainingMillSecond;
        public int RecipeDataId => _processingRecipeData.RecipeId;

        private const int RequestPower = 100;
        private int _nowPower = 0;
        
        
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        
        public NormalMachineRunProcess(
            NormalMachineInputInventory normalMachineInputInventory, 
            NormalMachineOutputInventory normalMachineOutputInventory,
            IMachineRecipeData machineRecipeData)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            _processingRecipeData = machineRecipeData;

            GameUpdate.AddUpdateObject(this);
        }
        public NormalMachineRunProcess(
            NormalMachineInputInventory normalMachineInputInventory, 
            NormalMachineOutputInventory normalMachineOutputInventory,
            ProcessState state,double remainingMillSecond,IMachineRecipeData processingRecipeData)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            
            _processingRecipeData = processingRecipeData;
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
        
        public int GetRequestPower(){return RequestPower;}
        public void SupplyPower(int power){_nowPower = power;}
    }

    public enum ProcessState
    {
        Idle,
        Processing
    }
}