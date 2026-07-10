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
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent
    {
        public Guid RecipeGuid => _processingState.RecipeGuid;
        public float RequestPower => _context.RequestPower;
        public float CurrentPower => _context.CurrentPower;
        public ProcessState CurrentState { get; private set; }

        // 稼働状態に応じてアイドル倍率かモジュール倍率を適用した要求電力
        // Requested power applies the idle rate or module multiplier based on the active state
        public float EffectiveRequestPower => _context.RequestPower *
                                              (CurrentState == ProcessState.Processing ? _context.EffectComponent.AggregateCurrent().PowerMultiplier : _idlePowerRate);

        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        private readonly MachineProcessContext _context;
        private readonly Dictionary<ProcessState, IMachineProcessState> _stateHandlers;
        private readonly ProcessingMachineProcessState _processingState;
        private readonly float _idlePowerRate;
        
        private ProcessState _lastState = ProcessState.Idle;

        // 新規作成
        // For new creation
        public VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect)
            : this(input, output, effect, requestPower, idlePowerRate, ProcessState.Idle, 0, null, null)
        {
        }

        // セーブからの復元
        // For restoration from save
        public VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect, List<IItemStack> pendingOutputs)
            : this(input, output, effect, requestPower, idlePowerRate, currentState, remainingTicks, processingRecipe, pendingOutputs)
        {
        }

        private VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, MachineModuleEffectComponent effect, float requestPower, float idlePowerRate, ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe, List<IItemStack> pendingOutputs)
        {
            _context = new MachineProcessContext(input, output, effect, requestPower);
            _idlePowerRate = idlePowerRate;

            // 加工状態を復元
            // Restore processing state
            CurrentState = currentState;
            _processingState = new ProcessingMachineProcessState(_context, remainingTicks, processingRecipe, pendingOutputs);

            // レシピを復元できないProcessingセーブは破損データのためIdleへ戻す
            // A Processing save without a restorable recipe is corrupt, so fall back to Idle
            if (CurrentState == ProcessState.Processing && processingRecipe == null)
            {
                CurrentState = ProcessState.Idle;
            }

            _stateHandlers = new IMachineProcessState[]
                {
                    new IdleMachineProcessState(_context, _processingState),
                    _processingState,
                }.ToDictionary(handler => handler.State);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var processingRate = Mathf.Clamp01(_processingState.TotalTicks > 0 ? 1f - (float)_processingState.RemainingTicks / _processingState.TotalTicks : 0f);
            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(_context.CurrentPower, _context.RequestPower, processingRate, CurrentState.ToStr(), _lastState.ToStr());
            
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, RecipeGuid);
            return new[] { commonMachineBlock, machineBlock };
        }

        public void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);

            // 複数の電力セグメントから供給され得るため加算する（発電機なしセグメントの0供給で打ち消されない）
            // Accumulate since multiple energy segments may supply power (a generator-less segment's zero must not cancel it)
            _context.SuppliedPower += power;

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

            // 直前tickで蓄積された供給電力を確定し、加算器をリセットする（未供給なら0になり電力を失う）
            // Latch the power accumulated during the previous tick and reset the accumulator (no supply -> 0, power is lost)
            _context.CurrentPower = _context.SuppliedPower;
            _context.SuppliedPower = 0f;

            // ステートのアップデートと変更処理
            // State update and transition handling
            var current = CurrentState;
            var nextState = _stateHandlers[current].GetNextUpdate();
            if (nextState != current)
            {
                _stateHandlers[current].OnExit();
                CurrentState = nextState;
                _stateHandlers[nextState].OnEnter();
            }

            // ステート変化時か処理中はイベントを発火させる
            // Fire the event on a state change or while processing
            if (_lastState != CurrentState || CurrentState == ProcessState.Processing)
            {
                _changeState.OnNext(Unit.Default);
                _lastState = CurrentState;
            }
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        // セーブデータ構築
        // Build save data object
        public VanillaMachineProcessorSaveJsonObject GetSaveJsonObject()
        {
            BlockException.CheckDestroy(this);
            
            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            return new VanillaMachineProcessorSaveJsonObject
            {
                State = (int)CurrentState,
                RemainingSeconds = GameUpdater.TicksToSeconds(_processingState.RemainingTicks),
                RecipeGuidStr = RecipeGuid.ToString(),
                PendingOutputs = _processingState.PendingOutputs?.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
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
                ProcessState.Halted => VanillaMachineBlockStateConst.HaltedState,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }

    public enum ProcessState
    {
        Idle,
        Processing,
        Halted,
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
