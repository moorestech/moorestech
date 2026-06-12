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

        public readonly float RequestPower;

        // 次のエネルギー供給かアップデートがあるまでは_currentPowerを維持しておきたいのでこのフラグを使う
        // Use this flag because you want to keep _currentPower until the next energy supply or update
        private bool _usedPower;
        private float _currentPower;
        private ProcessState _lastState = ProcessState.Idle;
        private MachineRecipeMasterElement _processingRecipe;
        private uint _processingRecipeTicks;

        // モジュール効果の供給源。倍率は毎回その場で集計する（即時反映）
        // Source of module effects; multipliers are aggregated live on every read (applied immediately)
        private readonly MachineModuleEffectComponent _effectComponent;

        // 開始時に抽選を確定した産出予定スタック列。セーブ・ロードで引き継ぎ、欠落時のみ完了時に再抽選する
        // Realized output stacks fixed at start; carried through save/load, re-rolled on completion only when missing
        private List<IItemStack> _pendingOutputs;
        private readonly Random _random = new();

        public IReadOnlyList<IItemStack> PendingOutputs => _pendingOutputs;

        // 加工中のみモジュール倍率を反映した電力倍率と要求電力（Idleは中立1.0）
        // Power multiplier and request power with module effects applied only while processing (neutral 1.0 when idle)
        public float CurrentPowerMultiplier => CurrentState == ProcessState.Processing ? _effectComponent.AggregateCurrent().PowerMultiplier : 1f;
        public float EffectiveRequestPower => RequestPower * CurrentPowerMultiplier;

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

            // 効果適用済みの加工時間は保存しない割り切りのため、レシピ定義から復元する（進捗率表示にのみ影響）
            // Effect-scaled processing time is deliberately not saved; restore from the recipe definition (only affects the progress rate display)
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

                // 今サイクルの抽選（追加出力・品質）を開始時に確定し、実際に産出されるスタック列で容量を確認する
                // Fix this cycle's rolls (extra output and quality) at start and check capacity with the exact realized stacks
                var effect = _effectComponent.AggregateCurrent();
                var realizedOutputs = CreateRealizedOutputs(recipe, effect);
                if (!_vanillaMachineOutputInventory.CanStoreOutputs(recipe, realizedOutputs)) return;

                // 確定した産出物を保持し、速度倍率を適用した加工時間で開始する
                // Hold the realized outputs and start with the speed-scaled processing time
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

                    // 産出予定が無い場合（pendingOutputs未保存の旧セーブ復帰）のみ、現在の効果でその場で再抽選する
                    // Only when the pending outputs are missing (a load from an old save without the key), re-roll with the current effects
                    var outputs = _pendingOutputs ?? CreateRealizedOutputs(_processingRecipe, _effectComponent.AggregateCurrent());
                    _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipe, outputs);
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

            // ベース1セット＋生産性抽選に当選すればもう1セットの産出スタック列を生成する
            // Build the realized stacks: one base set plus one extra set if the productivity roll succeeds
            List<IItemStack> CreateRealizedOutputs(MachineRecipeMasterElement recipe, MachineModuleEffect effect)
            {
                var outputs = CreateQualityAppliedOutputs(recipe, effect.QualityShift);
                if (_random.NextDouble() < effect.ExtraOutputChance) outputs.AddRange(CreateQualityAppliedOutputs(recipe, effect.QualityShift));
                return outputs;
            }

            // レシピのアイテム出力1セットを生成し、各出力へ品質レベル抽選を適用する
            // Build one set of the recipe's item outputs, applying the quality level roll to each
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

            // 出力アイテムを品質分布で上位レベル変種へ差し替える（シフトなし・ファミリー無しは素通し）
            // Replace an output item with an upgraded variant per the quality distribution (pass-through without shift or family)
            IItemStack ApplyQualityLevel(IItemStack output, float qualityShift)
            {
                if (qualityShift <= 0f || !MasterHolder.LevelFamilyMaster.HasFamily(output.Id)) return output;

                // 整数部=確定レベルアップ、小数部=抽選でさらに+1（レベル上限はマスタ側でクランプ）
                // Integer part = guaranteed level-ups; fractional part = one more by roll (max level clamped by the master)
                var guaranteed = (int)Math.Floor(qualityShift);
                var fraction = qualityShift - guaranteed;
                var extra = _random.NextDouble() < fraction ? 1 : 0;
                var level = 1 + guaranteed + extra;

                var variantId = MasterHolder.LevelFamilyMaster.GetVariantItemId(output.Id, level);
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
