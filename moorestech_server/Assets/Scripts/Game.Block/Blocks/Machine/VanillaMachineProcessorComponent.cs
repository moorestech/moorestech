using System;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.EnergySystem;
using MessagePack;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent
    {
        public ProcessState CurrentState { get; private set; } = ProcessState.Idle;

        public uint RemainingTicks { get; private set; }

        public Guid RecipeGuid => _processingRecipe?.MachineRecipeGuid ?? Guid.Empty;
        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        public readonly ElectricPower RequestPower;

        // 次のエネルギー供給かアップデートがあるまでは_currentPowerを維持しておきたいのでこのフラグを使う
        // Use this flag because you want to keep _currentPower until the next energy supply or update
        private bool _usedPower;
        private ElectricPower _currentPower;
        private ProcessState _lastState = ProcessState.Idle;
        private MachineRecipeMasterElement _processingRecipe;
        private uint _processingRecipeTicks;
        
        
        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            MachineRecipeMasterElement machineRecipe, ElectricPower requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _processingRecipe = machineRecipe;
            RequestPower = requestPower;
        }
        
        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe,
            ElectricPower requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;

            _processingRecipe = processingRecipe;
            _processingRecipeTicks = processingRecipe != null ? GameUpdater.SecondsToTicks(processingRecipe.Time) : 0;
            RequestPower = requestPower;
            RemainingTicks = remainingTicks;

            CurrentState = currentState;
        }
        
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            // 処理率を計算し、0〜1の範囲にクランプ
            // Calculate processing rate and clamp to 0-1 range
            var processingRate = _processingRecipeTicks > 0 ? 1f - (float)RemainingTicks / _processingRecipeTicks : 0f;
            if (processingRate < 0f) processingRate = 0f;
            else if (processingRate > 1f) processingRate = 1f;

            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(_currentPower.AsPrimitive(), RequestPower.AsPrimitive(), processingRate, CurrentState.ToStr(), _lastState.ToStr());
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, _processingRecipe?.MachineRecipeGuid ?? Guid.Empty);

            return new[] { commonMachineBlock, machineBlock };
        }
        
        public void SupplyPower(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _usedPower = false;
            _currentPower = power;
            
            // アイドル中はエネルギーの供給を受けてもその情報がクライアントに伝わらないため、明示的に通知を行う
            // During idle, even if energy is supplied, the information is not transmitted to the client, so the client is notified explicitly.
            if (CurrentState == ProcessState.Idle)
            {
                _changeState.OnNext(Unit.Default);
            }
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_usedPower)
            {
                _usedPower = false;
                _currentPower = new ElectricPower(0);
            }
            
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
                _changeState.OnNext(Unit.Default);
                _lastState = CurrentState;
            }
        }
        
        private void Idle()
        {
            var isGetRecipe = _vanillaMachineInputInventory.TryGetRecipeElement(out var recipe);
            var isStartProcess = CurrentState == ProcessState.Idle && isGetRecipe &&
                   _vanillaMachineInputInventory.IsAllowedToStartProcess() &&
                   _vanillaMachineOutputInventory.IsAllowedToOutputItem(recipe);

            if (isStartProcess)
            {
                CurrentState = ProcessState.Processing;
                _processingRecipe = recipe;
                _processingRecipeTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
                _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipe);
                RemainingTicks = _processingRecipeTicks;
            }
        }

        private void Processing()
        {
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, RequestPower);
            if (subTicks >= RemainingTicks)
            {
                RemainingTicks = 0;
                CurrentState = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipe);
            }
            else
            {
                RemainingTicks -= subTicks;
            }

            // 電力を消費する
            // Consume power
            _usedPower = true;
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
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
    
    public enum ProcessState
    {
        Idle,
        Processing,
    }
}