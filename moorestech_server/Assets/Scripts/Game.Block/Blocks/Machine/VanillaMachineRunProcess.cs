using System;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Util;
using Game.Block.Interface.RecipeConfig;
using Game.Block.Interface.State;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineRunProcess
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        public readonly int RequestPower;
        private int _currentPower;
        private ProcessState _lastState = ProcessState.Idle;
        private MachineRecipeData _processingRecipeData;

        public VanillaMachineRunProcess(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            MachineRecipeData machineRecipeData, int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _processingRecipeData = machineRecipeData;
            RequestPower = requestPower;

            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        public VanillaMachineRunProcess(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState currentState, double remainingMillSecond, MachineRecipeData processingRecipeData,
            int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;

            _processingRecipeData = processingRecipeData;
            RequestPower = requestPower;
            RemainingMillSecond = remainingMillSecond;
            CurrentState = currentState;

            GameUpdater.RegisterUpdater(this);
        }

        public ProcessState CurrentState { get; private set; } = ProcessState.Idle;

        public double RemainingMillSecond { get; private set; }

        public int RecipeDataId => _processingRecipeData.RecipeId;

        private void Update()
        {
            switch (CurrentState)
            {
                case ProcessState.Idle:
                    Idle();
                    break;
                case ProcessState.Processing:
                    Processing();
                    break;
            }

            //ステートの変化を検知した時か、ステートが処理中の時はイベントを発火させる
            if (_lastState != CurrentState || CurrentState == ProcessState.Processing)
            {
                var processingRate = 1 - (float)RemainingMillSecond / _processingRecipeData.Time;
                OnChangeState?.Invoke(
                    new ChangedBlockState(CurrentState.ToStr(), _lastState.ToStr(),
                        JsonConvert.SerializeObject(
                            new CommonMachineBlockStateChangeData(_currentPower, RequestPower, processingRate))));
                _lastState = CurrentState;
            }
        }

        public event Action<ChangedBlockState> OnChangeState;

        private void Idle()
        {
            if (IsAllowedToStartProcess()) StartProcessing();
        }

        private void StartProcessing()
        {
            CurrentState = ProcessState.Processing;
            _processingRecipeData = _vanillaMachineInputInventory.GetRecipeData();
            _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipeData);
            RemainingMillSecond = _processingRecipeData.Time;
        }

        private void Processing()
        {
            RemainingMillSecond -= MachineCurrentPowerToSubMillSecond.GetSubMillSecond(_currentPower, RequestPower);
            if (RemainingMillSecond <= 0)
            {
                CurrentState = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipeData);
            }

            //電力を消費する
            _currentPower = 0;
        }

        private bool IsAllowedToStartProcess()
        {
            var recipe = _vanillaMachineInputInventory.GetRecipeData();
            return CurrentState == ProcessState.Idle &&
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
        ///     <see cref="ProcessState" />をStringに変換します。
        ///     EnumのToStringを使わない理由はアロケーションによる速度低下をなくすためです。
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