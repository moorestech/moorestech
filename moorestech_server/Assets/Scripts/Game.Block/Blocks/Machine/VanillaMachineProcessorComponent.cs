using System;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.RecipeConfig;
using Game.Block.Interface.State;
using Game.EnergySystem;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorComponent : IBlockStateChange
    {
        private readonly Subject<BlockState> _changeState = new();
        
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        
        public readonly ElectricPower RequestPower;
        
        private readonly IDisposable UpdateObservable;
        
        private ElectricPower _currentPower;
        private ProcessState _lastState = ProcessState.Idle;
        private MachineRecipeData _processingRecipeData;
        
        
        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            MachineRecipeData machineRecipeData, ElectricPower requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _processingRecipeData = machineRecipeData;
            RequestPower = requestPower;
            
            //TODO コンポーネント化する
            UpdateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState currentState, double remainingSecond, MachineRecipeData processingRecipeData,
            ElectricPower requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            
            _processingRecipeData = processingRecipeData;
            RequestPower = requestPower;
            RemainingSecond = remainingSecond;
            
            CurrentState = currentState;
            
            UpdateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public ProcessState CurrentState { get; private set; } = ProcessState.Idle;
        // public double RemainingMillSecond { get; private set; }
        public double RemainingSecond { get; private set; }
        
        public int RecipeDataId => _processingRecipeData.RecipeId;
        public IObservable<BlockState> OnChangeBlockState => _changeState;
        
        public BlockState GetBlockState()
        {
            BlockException.CheckDestroy(this);
            
            var processingRate = 1 - (float)RemainingSecond / _processingRecipeData.Time;
            return new BlockState(CurrentState.ToStr(), _lastState.ToStr(),
                MessagePackSerializer.Serialize(
                    new CommonMachineBlockStateChangeData(_currentPower.AsPrimitive(), RequestPower.AsPrimitive(), processingRate)));
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            UpdateObservable.Dispose();
            IsDestroy = true;
        }
        
        public void SupplyPower(ElectricPower power)
        {
            _currentPower = power;
        }
        
        private void Update()
        {
            BlockException.CheckDestroy(this);
            
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
                var state = GetBlockState();
                _changeState.OnNext(state);
                _lastState = CurrentState;
            }
        }
        
        private void Idle()
        {
            var isStartProcess = IsAllowedToStartProcess();
            if (isStartProcess)
            {
                CurrentState = ProcessState.Processing;
                _processingRecipeData = _vanillaMachineInputInventory.GetRecipeData();
                _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipeData);
                RemainingSecond = _processingRecipeData.Time;
            }
        }
        
        private void Processing()
        {
            RemainingSecond -= MachineCurrentPowerToSubMillSecond.GetSubSecond(_currentPower, RequestPower);
            if (RemainingSecond <= 0)
            {
                CurrentState = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipeData);
            }
            
            //電力を消費する
            _currentPower = new ElectricPower(0);
        }
        
        private bool IsAllowedToStartProcess()
        {
            var recipe = _vanillaMachineInputInventory.GetRecipeData();
            return CurrentState == ProcessState.Idle &&
                   _vanillaMachineInputInventory.IsAllowedToStartProcess &&
                   _vanillaMachineOutputInventory.IsAllowedToOutputItem(recipe);
        }
    }
    
    public enum ProcessState
    {
        Idle,
        Processing,
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
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}