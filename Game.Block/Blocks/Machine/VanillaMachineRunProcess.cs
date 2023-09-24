using System;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.State;
using Core.Block.Blocks.Util;
using Core.Block.RecipeConfig.Data;
using Core.Update;
using Newtonsoft.Json;

namespace Core.Block.Blocks.Machine
{
    public class VanillaMachineRunProcess : IUpdatable
    {
        private MachineRecipeData _processingRecipeData;
        private ProcessState _currentState = ProcessState.Idle;
        private ProcessState _lastState = ProcessState.Idle;

        public ProcessState CurrentState => _currentState;

        public double RemainingMillSecond => _remainingMillSecond;
        public int RecipeDataId => _processingRecipeData.RecipeId;

        public readonly int RequestPower;
        private int _currentPower = 0;

        public event Action<ChangedBlockState> OnChangeState;

        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        public VanillaMachineRunProcess(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            MachineRecipeData machineRecipeData, int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _processingRecipeData = machineRecipeData;
            RequestPower = requestPower;

            GameUpdater.RegisterUpdater(this);
        }

        public VanillaMachineRunProcess(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState currentState, double remainingMillSecond, MachineRecipeData processingRecipeData, int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;

            _processingRecipeData = processingRecipeData;
            RequestPower = requestPower;
            _remainingMillSecond = remainingMillSecond;
            _currentState = currentState;

            GameUpdater.RegisterUpdater(this);
        }

        public void Update()
        {
            switch (_currentState)
            {
                case ProcessState.Idle:
                    Idle();
                    break;
                case ProcessState.Processing:
                    Processing();
                    break;
            }
            
            //ステートの変化を検知した時か、ステートが処理中の時はイベントを発火させる
            if (_lastState != _currentState || _currentState == ProcessState.Processing)
            {
                var processingRate = 1 -  (float)_remainingMillSecond / _processingRecipeData.Time;
                OnChangeState?.Invoke(
                    new ChangedBlockState(_currentState.ToStr(),_lastState.ToStr(),
                JsonConvert.SerializeObject(new CommonMachineBlockStateChangeData(_currentPower, RequestPower, processingRate))));
                _lastState = _currentState;
            }
        }

        private void Idle()
        {
            if (IsAllowedToStartProcess()) StartProcessing();
        }

        private void StartProcessing()
        {
            _currentState = ProcessState.Processing;
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
                _currentState = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipeData);
            }

            //電力を消費する
            _currentPower = 0;
        }

        private bool IsAllowedToStartProcess()
        {
            var recipe = _vanillaMachineInputInventory.GetRecipeData();
            return _currentState == ProcessState.Idle &&
                   _vanillaMachineInputInventory.IsAllowedToStartProcess &&
                   _vanillaMachineOutputInventory.IsAllowedToOutputItem(recipe);
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

    public static class ProcessStateExtension
    {
        /// <summary>
        /// <see cref="ProcessState"/>をStringに変換します。
        /// EnumのToStringを使わない理由はアロケーションによる速度低下をなくすためです。
        /// </summary>
        public static string ToStr(this ProcessState state)
        {
            return state switch
            {
                ProcessState.Idle => VanillaMachineBlockStateConst.IdleState,
                ProcessState.Processing => VanillaMachineBlockStateConst.ProcessingState,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }


}