using System;
using System.Collections.Generic;
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
using UniRx;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, IConsumptionMultiplierSource
    {
        public ProcessState CurrentState { get; private set; } = ProcessState.Idle;

        public uint RemainingTicks { get; private set; }

        public Guid RecipeGuid => _processingRecipe?.MachineRecipeGuid ?? Guid.Empty;
        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        public readonly float RequestPower;

        // 次のエネルギー供給かアップデートがあるまでは_currentPowerを維持しておきたいのでこのフラグを使う
        // Use this flag because you want to keep _currentPower until the next energy supply or update
        private bool _usedPower;
        private float _currentPower;
        private ProcessState _lastState = ProcessState.Idle;
        private MachineRecipeMasterElement _processingRecipe;
        private uint _processingRecipeTicks;

        // モジュール効果は毎回その場で集計する
        // Module effects are aggregated live on every read
        private readonly MachineModuleEffectComponent _effectComponent;

        // 開始時に確定した産出予定。セーブで引き継ぐ
        // Outputs fixed at start; carried through saves
        private List<IItemStack> _pendingOutputs;

        // 同tick生成機械の同シード回避のため共有
        // Shared to avoid same-tick identical seeds
        private static readonly Random Random = new();

        public IReadOnlyList<IItemStack> PendingOutputs => _pendingOutputs;

        // 加工中のみ倍率を適用、Idleは中立1.0
        // Multiplier applies only while processing
        public float ConsumptionMultiplier => CurrentState == ProcessState.Processing ? _effectComponent.AggregateCurrent().PowerMultiplier : 1f;
        public float EffectiveRequestPower => RequestPower * ConsumptionMultiplier;

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

            #region Internal

            void Idle()
            {
                var isGetRecipe = _vanillaMachineInputInventory.TryGetRecipeElement(out var recipe);
                if (!isGetRecipe || !_vanillaMachineInputInventory.IsAllowedToStartProcess()) return;

                // 抽選を開始時に確定し実スタックで容量確認
                // Fix rolls at start and check capacity with realized stacks
                var effect = _effectComponent.AggregateCurrent();
                var realizedOutputs = CreateRealizedOutputs(recipe, effect);
                if (!_vanillaMachineOutputInventory.CanStoreOutputs(realizedOutputs, CreateFluidOutputs(recipe))) return;

                // 産出物を保持し短縮済み時間で開始
                // Hold the outputs and start with the scaled time
                CurrentState = ProcessState.Processing;
                _processingRecipe = recipe;
                _pendingOutputs = realizedOutputs;

                var baseTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
                _processingRecipeTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
                _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipe);
                RemainingTicks = _processingRecipeTicks;
            }

            void Processing()
            {
                var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, EffectiveRequestPower);
                if (subTicks >= RemainingTicks)
                {
                    RemainingTicks = 0;
                    CurrentState = ProcessState.Idle;

                    // 旧セーブで産出予定が無い場合のみ再抽選
                    // Re-roll only when an old save lacks pending outputs
                    var outputs = _pendingOutputs ?? CreateRealizedOutputs(_processingRecipe, _effectComponent.AggregateCurrent());
                    _vanillaMachineOutputInventory.InsertOutputSlot(outputs, CreateFluidOutputs(_processingRecipe));
                    _pendingOutputs = null;
                }
                else
                {
                    RemainingTicks -= subTicks;
                }

                // 電力を消費する
                // Consume power
                _usedPower = true;
            }

            // ベース1セットと当選時の追加1セットを生成
            // Build one base set plus one extra set when the roll succeeds
            List<IItemStack> CreateRealizedOutputs(MachineRecipeMasterElement recipe, MachineModuleEffect effect)
            {
                var outputs = CreateQualityAppliedOutputs(recipe, effect.QualityShift);
                if (Random.NextDouble() < effect.ExtraOutputChance) outputs.AddRange(CreateQualityAppliedOutputs(recipe, effect.QualityShift));
                return outputs;
            }

            // レシピの液体出力1セットを生成
            // Build one set of the recipe's fluid outputs
            List<FluidStack> CreateFluidOutputs(MachineRecipeMasterElement recipe)
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
            List<IItemStack> CreateQualityAppliedOutputs(MachineRecipeMasterElement recipe, float qualityShift)
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
            IItemStack ApplyQualityLevel(IItemStack output, float qualityShift)
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

            #endregion
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
