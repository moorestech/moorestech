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

        // モジュール効果の単一供給源コンポーネント。倍率はすべてここのスナップショットから読む
        // The single-source module effect component. Every multiplier is read from its snapshot
        private readonly MachineModuleEffectComponent _effectComponent;
        private readonly BlockInstanceId _blockInstanceId;
        private int _processedCycleCount;

        // セーブ用の読み取り専用アクセサ群（効果コンポーネントへ委譲。Idle時はスナップショットが無く中立値になる）
        // Read-only accessors for saving (delegated to the effect component; neutral while idle without a snapshot)
        public uint ProcessingRecipeTicks => _processingRecipeTicks;
        public float CurrentPowerMultiplier => _effectComponent.CurrentEffect.PowerMultiplier;
        public float CurrentExtraOutputChance => _effectComponent.CurrentEffect.ExtraOutputChance;
        public float CurrentQualityShift => _effectComponent.CurrentEffect.QualityShift;
        public int ProcessedCycleCount => _processedCycleCount;
        public float EffectiveRequestPower => CurrentState == ProcessState.Processing ? RequestPower * _effectComponent.CurrentEffect.PowerMultiplier : RequestPower;

        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            MachineRecipeMasterElement machineRecipe, float requestPower,
            MachineModuleEffectComponent effectComponent, BlockInstanceId blockInstanceId)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _processingRecipe = machineRecipe;
            RequestPower = requestPower;
            _effectComponent = effectComponent;
            _blockInstanceId = blockInstanceId;
        }

        public VanillaMachineProcessorComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe,
            float requestPower,
            MachineModuleEffectComponent effectComponent, BlockInstanceId blockInstanceId,
            double processingTotalSeconds, float powerMultiplier, float extraOutputChance, float qualityShift, int processedCycleCount)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;

            _processingRecipe = processingRecipe;
            RequestPower = requestPower;
            RemainingTicks = remainingTicks;
            _effectComponent = effectComponent;
            _blockInstanceId = blockInstanceId;
            _processedCycleCount = processedCycleCount;

            // 効果適用済みの加工時間を秒から復元する。旧セーブ（0秒）はレシピ定義から再計算する
            // Restore the effect-scaled processing time from seconds. Old saves (0 seconds) recompute from the recipe definition
            _processingRecipeTicks = processingTotalSeconds > 0
                ? GameUpdater.SecondsToTicks(processingTotalSeconds)
                : processingRecipe != null ? GameUpdater.SecondsToTicks(processingRecipe.Time) : 0;

            CurrentState = currentState;

            // プロセス途中のロードでは効果スナップショットを復元する。旧セーブの未保存倍率（0以下）は中立扱い
            // When loading mid-process, restore the effect snapshot. Multipliers missing from old saves (zero or less) are neutral
            if (CurrentState == ProcessState.Processing)
            {
                var savedEffect = MachineModuleEffect.FromSaved(powerMultiplier <= 0 ? 1f : powerMultiplier, extraOutputChance, qualityShift);
                _effectComponent.SetProcessSnapshot(savedEffect);
            }
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
        }
        
        private void Idle()
        {
            var isGetRecipe = _vanillaMachineInputInventory.TryGetRecipeElement(out var recipe);
            if (!isGetRecipe || !_vanillaMachineInputInventory.IsAllowedToStartProcess()) return;

            // 現在のモジュール効果を集計し、追加出力分の空きも含めて出力可否を判定する
            // Aggregate the current module effects and check output capacity including potential extra output
            var effect = _effectComponent.AggregateCurrent();
            var maxExtraSets = effect.ExtraOutputChance > 0f ? 1 : 0;

            // 品質変種は独立スタックになるため、確定レベルと任意の+1レベルの両方で容量を予約する（保守的予約。実産出がどちらでも収まる）
            // Quality variants stack separately, so reserve capacity at both the guaranteed level and the optional +1 level (conservative; either realized outcome fits)
            var guaranteedLevelUps = (int)Math.Floor(effect.QualityShift);
            var fractionalChance = effect.QualityShift - guaranteedLevelUps;
            if (!_vanillaMachineOutputInventory.CanStoreOutputs(recipe, maxExtraSets, stack => TransformToQualityLevel(stack, effect.QualityShift, 1 + guaranteedLevelUps))) return;
            if (fractionalChance > 0f && !_vanillaMachineOutputInventory.CanStoreOutputs(recipe, maxExtraSets, stack => TransformToQualityLevel(stack, effect.QualityShift, 2 + guaranteedLevelUps))) return;

            // プロセス開始時に効果をスナップショットし、速度倍率を適用した加工時間を確定する
            // Snapshot the effects at process start and fix the speed-scaled processing time
            CurrentState = ProcessState.Processing;
            _processingRecipe = recipe;
            _effectComponent.SetProcessSnapshot(effect);

            var baseTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
            _processingRecipeTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
            _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipe);
            RemainingTicks = _processingRecipeTicks;
        }

        private void Processing()
        {
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, EffectiveRequestPower);
            if (subTicks >= RemainingTicks)
            {
                RemainingTicks = 0;
                CurrentState = ProcessState.Idle;

                // 1サイクル内の抽選順序は固定: ソルト0=生産性、ソルト1..=品質（ベースセットの出力順→追加セットの出力順）（§7.2）
                // Fixed roll order per cycle: salt 0 = productivity, salts 1.. = quality per output (base set order, then extra set order) (§7.2)
                var outputCount = _processingRecipe.OutputItems.Length;
                var baseOutputs = CreateQualityAppliedOutputs(1);
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipe, baseOutputs);

                // 生産性モジュールの追加出力を、スナップショットをクリアする前に決定論的な抽選で判定する
                // Roll the productivity extra output deterministically, reading the snapshot before clearing it
                var effect = _effectComponent.CurrentEffect;
                if (effect.ExtraOutputChance > 0f &&
                    DeterministicRoll(_blockInstanceId, _processedCycleCount, 0) < effect.ExtraOutputChance)
                {
                    var extraOutputs = CreateQualityAppliedOutputs(1 + outputCount);
                    _vanillaMachineOutputInventory.InsertItemOutputsOnly(extraOutputs);
                }
                _processedCycleCount++;
                _effectComponent.ClearProcessSnapshot();
            }
            else
            {
                RemainingTicks -= subTicks;
            }

            // 電力を消費する
            // Consume power
            _usedPower = true;
        }

        private static double DeterministicRoll(BlockInstanceId blockInstanceId, int cycleCount, int salt)
        {
            // splitmix64系のハッシュでブロックID・サイクル数・ソルトから[0,1)の乱数を決定論的に生成する
            // Deterministically derive a [0,1) random value from block id, cycle count, and salt via a splitmix64-style hash
            var x = (ulong)(uint)blockInstanceId.AsPrimitive() * 0x9E3779B97F4A7C15UL + (ulong)(uint)cycleCount;

            // ソルトを別定数で混ぜ、同一サイクル内の複数抽選を脱相関させる（ソルト0は従来の生産性抽選と同一の値になる）
            // Mix the salt with a distinct constant to decorrelate multiple rolls within one cycle (salt 0 matches the legacy productivity roll)
            x ^= (ulong)(uint)salt * 0xD1B54A32D192ED03UL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
        }

        // レシピのアイテム出力1セットを生成し、各出力へ品質レベル抽選を適用する（saltOffsetから出力順にソルトを割り当てる）
        // Build one set of the recipe's item outputs, applying the quality level roll to each (salts assigned in output order from saltOffset)
        private List<IItemStack> CreateQualityAppliedOutputs(int saltOffset)
        {
            var outputs = new List<IItemStack>(_processingRecipe.OutputItems.Length);
            for (var i = 0; i < _processingRecipe.OutputItems.Length; i++)
            {
                var outputItem = _processingRecipe.OutputItems[i];
                var stack = ServerContext.ItemStackFactory.Create(outputItem.ItemGuid, outputItem.Count);
                outputs.Add(ApplyQualityLevel(stack, saltOffset + i));
            }
            return outputs;
        }

        // 出力アイテムを品質分布で上位レベル変種へ差し替える（ファミリーが無ければ素通し）
        // Replace an output item with an upgraded variant per the quality distribution (pass-through if no family)
        private IItemStack ApplyQualityLevel(IItemStack output, int salt)
        {
            var shift = _effectComponent.CurrentEffect.QualityShift;
            if (shift <= 0f || !MasterHolder.LevelFamilyMaster.HasFamily(output.Id)) return output;

            // 整数部=確定レベルアップ、小数部=決定的抽選でさらに+1（レベル上限はマスタ側でクランプ）
            // Integer part = guaranteed level-ups; fractional part = one more via deterministic roll (max level clamped by the master)
            var guaranteed = (int)Math.Floor(shift);
            var fraction = shift - guaranteed;
            var extra = fraction > 0f && DeterministicRoll(_blockInstanceId, _processedCycleCount, salt) < fraction ? 1 : 0;
            var level = 1 + guaranteed + extra;

            var variantId = MasterHolder.LevelFamilyMaster.GetVariantItemId(output.Id, level);
            if (variantId == output.Id) return output;
            return ServerContext.ItemStackFactory.Create(variantId, output.Count);
        }

        // 容量予約用に、出力アイテムを指定レベルの品質変種へ差し替える（シフトなし・ファミリー無しは素通し）
        // For capacity reservation, replace an output item with the variant at the given level (pass-through without shift or family)
        private static IItemStack TransformToQualityLevel(IItemStack output, float qualityShift, int level)
        {
            if (qualityShift <= 0f || !MasterHolder.LevelFamilyMaster.HasFamily(output.Id)) return output;

            var variantId = MasterHolder.LevelFamilyMaster.GetVariantItemId(output.Id, level);
            if (variantId == output.Id) return output;
            return ServerContext.ItemStackFactory.Create(variantId, output.Count);
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