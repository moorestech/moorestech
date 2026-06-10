using System;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
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

        // プロセス開始時にスナップショットしたモジュール効果と、追加出力抽選用の累計サイクル数
        // Module effect snapshotted at process start, plus the cumulative cycle count for extra output rolls
        private readonly MachineModuleEffectComponent _effectComponent;
        private readonly BlockInstanceId _blockInstanceId;
        private MachineModuleEffect _currentEffect;
        private int _processedCycleCount;

        // セーブ用の読み取り専用アクセサ群（効果適用済みの加工時間・倍率・サイクル数）
        // Read-only accessors for saving (effect-scaled processing ticks, multipliers, cycle count)
        public uint ProcessingRecipeTicks => _processingRecipeTicks;
        public float CurrentPowerMultiplier => _currentEffect?.PowerMultiplier ?? 1f;
        public float CurrentExtraOutputChance => _currentEffect?.ExtraOutputChance ?? 0f;
        public int ProcessedCycleCount => _processedCycleCount;
        public float EffectiveRequestPower => CurrentState == ProcessState.Processing ? RequestPower * (_currentEffect?.PowerMultiplier ?? 1f) : RequestPower;

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
            double processingTotalSeconds, float powerMultiplier, float extraOutputChance, int processedCycleCount)
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

            // 旧セーブで電力倍率が未保存（0以下）の場合は中立扱いにする
            // Treat power multipliers missing from old saves (zero or less) as neutral
            _currentEffect = MachineModuleEffect.FromSaved(powerMultiplier <= 0 ? 1f : powerMultiplier, extraOutputChance);

            CurrentState = currentState;

            // プロセス途中のロードでは効果スナップショットも復元する
            // When loading mid-process, restore the effect snapshot as well
            if (CurrentState == ProcessState.Processing)
            {
                _effectComponent.SetProcessSnapshot(_currentEffect);
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
            if (!_vanillaMachineOutputInventory.CanStoreOutputs(recipe, maxExtraSets)) return;

            // プロセス開始時に効果をスナップショットし、速度倍率を適用した加工時間を確定する
            // Snapshot the effects at process start and fix the speed-scaled processing time
            CurrentState = ProcessState.Processing;
            _processingRecipe = recipe;
            _currentEffect = effect;
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
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipe);

                // 生産性モジュールの追加出力を、永続化された入力のみから成る決定論的な抽選で判定する
                // Roll the productivity extra output deterministically using only persisted inputs
                if (_currentEffect != null && _currentEffect.ExtraOutputChance > 0f &&
                    DeterministicRoll(_blockInstanceId, _processedCycleCount) < _currentEffect.ExtraOutputChance)
                {
                    _vanillaMachineOutputInventory.InsertItemOutputsOnly(_processingRecipe);
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

        private static double DeterministicRoll(BlockInstanceId blockInstanceId, int cycleCount)
        {
            // splitmix64系のハッシュでブロックIDとサイクル数から[0,1)の乱数を決定論的に生成する
            // Deterministically derive a [0,1) random value from block id and cycle count via a splitmix64-style hash
            var x = (ulong)(uint)blockInstanceId.AsPrimitive() * 0x9E3779B97F4A7C15UL + (ulong)(uint)cycleCount;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
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