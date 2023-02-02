using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Util;
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

        public readonly int RequestPower;
        private int _currentPower = 0;


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
            RequestPower = requestPower;

            GameUpdate.AddUpdateObject(this);
        }

        public VanillaMachineRunProcess(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState state, double remainingMillSecond, IMachineRecipeData processingRecipeData, int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;

            _processingRecipeData = processingRecipeData;
            RequestPower = requestPower;
            _state = state;
            _remainingMillSecond = remainingMillSecond;

            GameUpdate.AddUpdateObject(this);
        }

        public void Update()
        {
            switch (_state)
            {
                case ProcessState.Idle:
                    Idle();
                    break;
                case ProcessState.Processing:
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
            _remainingMillSecond -= MachineCurrentPowerToSubMillSecond.GetSubMillSecond(_currentPower, RequestPower);
            if (_remainingMillSecond <= 0)
            {
                _state = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipeData);
            }

            //電力を消費する
            _currentPower = 0;
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

        public void SupplyPower(int power)
        {
            _currentPower = power;
        }
    }

    public enum ProcessState
    {
        Idle,
        Processing
    }
}