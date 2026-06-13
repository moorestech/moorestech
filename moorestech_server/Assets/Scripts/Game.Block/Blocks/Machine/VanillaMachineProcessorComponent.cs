using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json;
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

        // ステートごとに1インスタンスを持つ簡易ステートマシン
        // Simple state machine holding one instance per state
        private Dictionary<ProcessState, IProcessStateHandler> _stateHandlers;
        private IProcessStateHandler _currentHandler;

        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        public readonly float RequestPower;

        // 次のエネルギー供給かアップデートがあるまでは_currentPowerを維持しておきたいのでこのフラグを使う
        // Use this flag because you want to keep _currentPower until the next energy supply or update
        private bool _usedPower;
        private float _currentPower;
        private ProcessState _lastState = ProcessState.Idle;
        private MachineRecipeMasterElement _processingRecipe;
        // 開始時に確定した産出予定。セーブで引き継ぐ
        // Outputs fixed at start; carried through saves
        private List<IItemStack> _pendingOutputs;
        private uint _processingRecipeTicks;

        // モジュール効果は毎回その場で集計する
        // Module effects are aggregated live on every read
        private readonly MachineModuleEffectComponent _effectComponent;

        // 同tick生成機械の同シード回避のため共有
        // Shared to avoid same-tick identical seeds
        private static readonly Random Random = new();

        public float EffectiveRequestPower => RequestPower *
                                              (CurrentState == ProcessState.Processing ? _effectComponent.AggregateCurrent().PowerMultiplier : 1f);
        
        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            MachineRecipeMasterElement machineRecipe, float requestPower,
            MachineModuleEffectComponent effectComponent)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _processingRecipe = machineRecipe;
            RequestPower = requestPower;
            _effectComponent = effectComponent;

            InitializeStateHandlers();
        }

        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe,
            float requestPower,
            MachineModuleEffectComponent effectComponent, List<IItemStack> pendingOutputs)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;

            _processingRecipe = processingRecipe;
            RequestPower = requestPower;
            RemainingTicks = remainingTicks;
            _effectComponent = effectComponent;
            _pendingOutputs = pendingOutputs;

            // 加工時間はレシピ定義から復元（進捗表示のみに影響）
            // Restore ticks from the recipe; affects only the progress display
            _processingRecipeTicks = processingRecipe != null ? GameUpdater.SecondsToTicks(processingRecipe.Time) : 0;

            CurrentState = currentState;

            // セーブ復元時は途中状態のためOnEnterは呼ばずハンドラのみ合わせる
            // On save restore we are mid-state, so just align the handler without OnEnter
            InitializeStateHandlers();
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
                State = (int)CurrentState,
                RemainingSeconds = GameUpdater.TicksToSeconds(RemainingTicks),
                RecipeGuidStr = RecipeGuid.ToString(),
                // 産出予定も保存する（Idle時はnull）
                // Also save the pending outputs (null while idle)
                PendingOutputs = _pendingOutputs?.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            // 処理率を計算し、0〜1の範囲にクランプ
            // Calculate processing rate and clamp to 0-1 range
            var processingRate = _processingRecipeTicks > 0 ? 1f - (float)RemainingTicks / _processingRecipeTicks : 0f;
            if (processingRate < 0f) processingRate = 0f;
            else if (processingRate > 1f) processingRate = 1f;

            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(_currentPower, RequestPower, processingRate, CurrentState.ToStr(), _lastState.ToStr());
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, _processingRecipe?.MachineRecipeGuid ?? Guid.Empty);

            return new[] { commonMachineBlock, machineBlock };
        }

        public void SupplyPower(float power)
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
                _currentPower = 0f;
            }

            // 現ステートを更新し、遷移が起きた時のみOnExit→OnEnterを実行
            // Update the current state; run OnExit→OnEnter only when a transition happens
            var nextState = _currentHandler.GetNextUpdate();
            if (nextState != CurrentState)
            {
                _currentHandler.OnExit();
                CurrentState = nextState;
                _currentHandler = _stateHandlers[nextState];
                _currentHandler.OnEnter();
            }

            // ステート変化時か処理中はイベントを発火させる
            // Fire the event on a state change or while processing
            if (_lastState != CurrentState || CurrentState == ProcessState.Processing)
            {
                _changeState.OnNext(Unit.Default);
                _lastState = CurrentState;
            }
        }

        // ステートごとに1インスタンスを生成して保持する
        // Create and hold one instance per state
        private void InitializeStateHandlers()
        {
            var handlers = new IProcessStateHandler[] { new IdleState(this), new ProcessingState(this) };
            _stateHandlers = handlers.ToDictionary(handler => handler.State);
            _currentHandler = _stateHandlers[CurrentState];
        }

        // ベース1セットと当選時の追加1セットを生成
        // Build one base set plus one extra set when the roll succeeds
        private List<IItemStack> CreateRealizedOutputs(MachineRecipeMasterElement recipe, MachineModuleEffect effect)
        {
            var outputs = CreateQualityAppliedOutputs(recipe, effect.QualityShift);
            if (Random.NextDouble() < effect.ExtraOutputChance) outputs.AddRange(CreateQualityAppliedOutputs(recipe, effect.QualityShift));
            return outputs;
        }

        // レシピの液体出力1セットを生成
        // Build one set of the recipe's fluid outputs
        private List<FluidStack> CreateFluidOutputs(MachineRecipeMasterElement recipe)
        {
            var outputs = new List<FluidStack>(recipe.OutputFluids.Length);
            foreach (var outputFluid in recipe.OutputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(outputFluid.FluidGuid);
                outputs.Add(new FluidStack(outputFluid.Amount, fluidId));
            }
            return outputs;
        }

        // アイテム出力1セットに品質抽選を適用して生成
        // Build one output set with quality rolls applied
        private List<IItemStack> CreateQualityAppliedOutputs(MachineRecipeMasterElement recipe, float qualityShift)
        {
            var outputs = new List<IItemStack>(recipe.OutputItems.Length);
            foreach (var outputItem in recipe.OutputItems)
            {
                var stack = ServerContext.ItemStackFactory.Create(outputItem.ItemGuid, outputItem.Count);
                outputs.Add(ApplyQualityLevel(stack, qualityShift));
            }
            return outputs;
        }

        // 品質シフトで上位レベル変種へ差し替える
        // Swap to a higher-level variant per the quality shift
        private IItemStack ApplyQualityLevel(IItemStack output, float qualityShift)
        {
            if (qualityShift <= 0f || !MasterHolder.ItemMaster.HasLevelFamily(output.Id)) return output;

            // 整数部=確定、小数部=抽選で+1
            // Integer part guaranteed; the fraction rolls one more
            var guaranteed = (int)Math.Floor(qualityShift);
            var fraction = qualityShift - guaranteed;
            var extra = Random.NextDouble() < fraction ? 1 : 0;
            var level = 1 + guaranteed + extra;

            var variantId = MasterHolder.ItemMaster.GetLevelVariantItemId(output.Id, level);
            if (variantId == output.Id) return output;
            return ServerContext.ItemStackFactory.Create(variantId, output.Count);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }

        // 各加工ステートの共通インターフェース
        // Common interface for each processing state
        private interface IProcessStateHandler
        {
            ProcessState State { get; }
            void OnEnter();
            ProcessState GetNextUpdate();
            void OnExit();
        }

        // 待機ステート。レシピが揃えば開始情報を確定し加工へ遷移する
        // Idle state: fixes the start info and transitions to processing once a recipe is ready
        private class IdleState : IProcessStateHandler
        {
            private readonly VanillaMachineProcessorComponent _owner;
            public IdleState(VanillaMachineProcessorComponent owner) { _owner = owner; }

            public ProcessState State => ProcessState.Idle;
            public void OnEnter() { }
            public void OnExit() { }

            public ProcessState GetNextUpdate()
            {
                // レシピの有無と開始可否を確認
                // Check the recipe presence and whether processing may start
                var isGetRecipe = _owner._vanillaMachineInputInventory.TryGetRecipeElement(out var recipe);
                if (!isGetRecipe || !_owner._vanillaMachineInputInventory.IsAllowedToStartProcess()) return ProcessState.Idle;

                // 抽選を開始時に確定し実スタックで容量確認
                // Fix rolls at start and check capacity with realized stacks
                var effect = _owner._effectComponent.AggregateCurrent();
                var realizedOutputs = _owner.CreateRealizedOutputs(recipe, effect);
                if (!_owner._vanillaMachineOutputInventory.CanStoreOutputs(realizedOutputs, _owner.CreateFluidOutputs(recipe))) return ProcessState.Idle;

                // 産出物と短縮済み時間を確定して加工へ遷移
                // Fix the outputs and the scaled time, then transition to processing
                _owner._processingRecipe = recipe;
                _owner._pendingOutputs = realizedOutputs;
                var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
                _owner._processingRecipeTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
                return ProcessState.Processing;
            }
        }

        // 加工ステート。電力に応じて進行し、完了で待機へ戻る
        // Processing state: advances with power and returns to idle on completion
        private class ProcessingState : IProcessStateHandler
        {
            private readonly VanillaMachineProcessorComponent _owner;
            public ProcessingState(VanillaMachineProcessorComponent owner) { _owner = owner; }

            public ProcessState State => ProcessState.Processing;

            // 開始時に入力を消費し残りtickを設定する
            // Consume inputs and set remaining ticks on start
            public void OnEnter()
            {
                _owner._vanillaMachineInputInventory.ReduceInputSlot(_owner._processingRecipe);
                _owner.RemainingTicks = _owner._processingRecipeTicks;
            }

            public ProcessState GetNextUpdate()
            {
                var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_owner._currentPower, _owner.EffectiveRequestPower);

                // 電力を消費する
                // Consume power
                _owner._usedPower = true;

                // 残りtickを使い切ったら完了して待機へ
                // Once remaining ticks are exhausted, finish and return to idle
                if (subTicks >= _owner.RemainingTicks)
                {
                    _owner.RemainingTicks = 0;
                    return ProcessState.Idle;
                }

                _owner.RemainingTicks -= subTicks;
                return ProcessState.Processing;
            }

            // 完了時に産出物を払い出す（旧セーブは産出予定が無いため再抽選）
            // Output the produced items on completion (re-roll for old saves that lack pending outputs)
            public void OnExit()
            {
                var outputs = _owner._pendingOutputs ?? _owner.CreateRealizedOutputs(_owner._processingRecipe, _owner._effectComponent.AggregateCurrent());
                _owner._vanillaMachineOutputInventory.InsertOutputSlot(outputs, _owner.CreateFluidOutputs(_owner._processingRecipe));
                _owner._pendingOutputs = null;
            }
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
