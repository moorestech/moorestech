using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.RecipeSelection;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, IMachineRecipeSelectorComponent
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
            : this(input, output, effect, requestPower, idlePowerRate, ProcessState.Idle, 0, null, null, null)
        {
        }

        // セーブからの復元
        // For restoration from save
        public VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect, List<IItemStack> pendingOutputs, MachineRecipeMasterElement selectedRecipe)
            : this(input, output, effect, requestPower, idlePowerRate, currentState, remainingTicks, processingRecipe, pendingOutputs, selectedRecipe)
        {
        }

        private VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, MachineModuleEffectComponent effect, float requestPower, float idlePowerRate, ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe, List<IItemStack> pendingOutputs, MachineRecipeMasterElement selectedRecipe)
        {
            _context = new MachineProcessContext(input, output, effect, requestPower);
            _context.SelectedRecipe = selectedRecipe;
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
            
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, RecipeGuid, SelectedRecipeGuid);
            return new[] { commonMachineBlock, machineBlock };
        }

        public Guid SelectedRecipeGuid => _context.SelectedRecipe?.MachineRecipeGuid ?? Guid.Empty;

        public MachineRecipeSelectionResult SetSelectedRecipe(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
        {
            BlockException.CheckDestroy(this);

            var validation = MachineRecipeSelectionUtil.ValidateSelection(_context.InputInventory, recipe);
            if (validation != MachineRecipeSelectionResult.Success) return validation;

            // 同一レシピの再設定はジョブを中断しないno-op
            // Re-selecting the same recipe is a no-op that never cancels the job
            if (recipe.MachineRecipeGuid == SelectedRecipeGuid) return MachineRecipeSelectionResult.Success;

            return ChangeSelection(recipe, refundOverflowInventory);
        }

        public MachineRecipeSelectionResult ClearSelectedRecipe(IOpenableInventory refundOverflowInventory)
        {
            BlockException.CheckDestroy(this);
            if (_context.SelectedRecipe == null) return MachineRecipeSelectionResult.Success;
            return ChangeSelection(null, refundOverflowInventory);
        }

        private MachineRecipeSelectionResult ChangeSelection(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
        {
            // 進行中ジョブは返却して中断する。返却しきれなければ変更自体を中止する
            // Cancel the running job with refund; abort the whole change when the refund does not fit
            if (!MachineRecipeSelectionUtil.TryCancelRunningJobWithRefund(_context.InputInventory, _processingState, refundOverflowInventory))
            {
                return MachineRecipeSelectionResult.RefundFailed;
            }

            if (CurrentState == ProcessState.Processing) CurrentState = ProcessState.Idle;
            _context.SelectedRecipe = recipe;
            _changeState.OnNext(Unit.Default);
            return MachineRecipeSelectionResult.Success;
        }

        public void SupplyExternalPower(float power)
        {
            BlockException.CheckDestroy(this);

            // 複数の電力セグメントから供給され得るため加算する
            // Accumulate power because multiple electric segments may supply this machine
            _context.SuppliedPower += power;

            // アイドル中はUpdateが状態変化を出さないため、給電時に明示通知しないとidle→加工遷移がクライアントへ届かない
            // While idle, Update emits no state change, so without this explicit notice the idle-to-processing transition never reaches the client
            if (CurrentState == ProcessState.Idle) _changeState.OnNext(Unit.Default);
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 産出スロットの接続先への払い出しをここで駆動する（旧: inventory自身のグローバル購読。破壊後も残るゾンビ購読だった）
            // Drive output insertion into connected inventories here (was a global subscription on the inventory that outlived block destruction)
            _context.OutputInventory.InsertConnectInventory();

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
                SelectedRecipeGuidStr = _context.SelectedRecipe?.MachineRecipeGuid.ToString(),
            };
        }
    }
}
