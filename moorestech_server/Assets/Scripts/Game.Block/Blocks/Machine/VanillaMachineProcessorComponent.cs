using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent
    {
        public ProcessState CurrentState => _context.CurrentState;
        public uint RemainingTicks => _context.RemainingTicks;
        public Guid RecipeGuid => _context.RecipeGuid;
        public float RequestPower => _context.RequestPower;
        public float EffectiveRequestPower => _context.EffectiveRequestPower;

        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        // 加工状態の共有ブラックボード。各ステートはこれを操作する
        // Shared blackboard for processing state; each state operates on it
        private readonly MachineProcessContext _context;

        // ステートごとに1インスタンスを持つ簡易ステートマシン
        // Simple state machine holding one instance per state
        private readonly Dictionary<ProcessState, IMachineProcessState> _stateHandlers;
        private IMachineProcessState _currentHandler;

        private ProcessState _lastState = ProcessState.Idle;

        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            MachineRecipeMasterElement machineRecipe, float requestPower,
            MachineModuleEffectComponent effectComponent)
        {
            _context = new MachineProcessContext(vanillaMachineInputInventory, vanillaMachineOutputInventory, effectComponent, requestPower)
            {
                ProcessingRecipe = machineRecipe,
            };

            _stateHandlers = CreateStateHandlers();
            _currentHandler = _stateHandlers[_context.CurrentState];
        }

        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe,
            float requestPower,
            MachineModuleEffectComponent effectComponent, List<IItemStack> pendingOutputs)
        {
            _context = new MachineProcessContext(vanillaMachineInputInventory, vanillaMachineOutputInventory, effectComponent, requestPower)
            {
                CurrentState = currentState,
                RemainingTicks = remainingTicks,
                ProcessingRecipe = processingRecipe,
                PendingOutputs = pendingOutputs,
                // 加工時間はレシピ定義から復元（進捗表示のみに影響）
                // Restore ticks from the recipe; affects only the progress display
                ProcessingRecipeTicks = processingRecipe != null ? GameUpdater.SecondsToTicks(processingRecipe.Time) : 0,
            };

            // セーブ復元時は途中状態のためOnEnterは呼ばずハンドラのみ合わせる
            // On save restore we are mid-state, so just align the handler without OnEnter
            _stateHandlers = CreateStateHandlers();
            _currentHandler = _stateHandlers[_context.CurrentState];
        }

        // 自身のセーブデータを単独で構築する
        // Build this component's save data on its own
        public VanillaMachineProcessorSaveJsonObject GetSaveJsonObject()
        {
            BlockException.CheckDestroy(this);

            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            return new VanillaMachineProcessorSaveJsonObject
            {
                State = (int)_context.CurrentState,
                RemainingSeconds = GameUpdater.TicksToSeconds(_context.RemainingTicks),
                RecipeGuidStr = _context.RecipeGuid.ToString(),
                // 産出予定も保存する（Idle時はnull）
                // Also save the pending outputs (null while idle)
                PendingOutputs = _context.PendingOutputs?.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            // 処理率を計算し、0〜1の範囲にクランプ
            // Calculate processing rate and clamp to 0-1 range
            var processingRate = _context.ProcessingRecipeTicks > 0 ? 1f - (float)_context.RemainingTicks / _context.ProcessingRecipeTicks : 0f;
            if (processingRate < 0f) processingRate = 0f;
            else if (processingRate > 1f) processingRate = 1f;

            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(_context.CurrentPower, _context.RequestPower, processingRate, _context.CurrentState.ToStr(), _lastState.ToStr());
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, _context.RecipeGuid);

            return new[] { commonMachineBlock, machineBlock };
        }

        public void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);
            _context.UsedPower = false;
            _context.CurrentPower = power;

            // アイドル中はエネルギーの供給を受けてもその情報がクライアントに伝わらないため、明示的に通知を行う
            // During idle, even if energy is supplied, the information is not transmitted to the client, so the client is notified explicitly.
            if (_context.CurrentState == ProcessState.Idle)
            {
                _changeState.OnNext(Unit.Default);
            }
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_context.UsedPower)
            {
                _context.UsedPower = false;
                _context.CurrentPower = 0f;
            }

            // 現ステートを更新し、遷移が起きた時のみOnExit→OnEnterを実行
            // Update the current state; run OnExit→OnEnter only when a transition happens
            var nextState = _currentHandler.GetNextUpdate();
            if (nextState != _context.CurrentState)
            {
                _currentHandler.OnExit();
                _context.CurrentState = nextState;
                _currentHandler = _stateHandlers[nextState];
                _currentHandler.OnEnter();
            }

            // ステート変化時か処理中はイベントを発火させる
            // Fire the event on a state change or while processing
            if (_lastState != _context.CurrentState || _context.CurrentState == ProcessState.Processing)
            {
                _changeState.OnNext(Unit.Default);
                _lastState = _context.CurrentState;
            }
        }

        // ステートごとに1インスタンスを生成して保持する
        // Create and hold one instance per state
        private Dictionary<ProcessState, IMachineProcessState> CreateStateHandlers()
        {
            var handlers = new IMachineProcessState[]
            {
                new IdleMachineProcessState(_context),
                new ProcessingMachineProcessState(_context),
            };
            return handlers.ToDictionary(handler => handler.State);
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

    public class VanillaMachineProcessorSaveJsonObject
    {
        [JsonProperty("state")]
        public int State;

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        [JsonProperty("remainingSeconds")]
        public double RemainingSeconds;

        [JsonProperty("recipeGuid")]
        public string RecipeGuidStr;

        [JsonIgnore]
        public Guid RecipeGuid => Guid.Parse(RecipeGuidStr);

        // 産出予定。Idle時や過去セーブではnull
        // Pending outputs; null while idle or in old saves
        [JsonProperty("pendingOutputs")]
        public List<ItemStackSaveJsonObject> PendingOutputs;
    }
}
