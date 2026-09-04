using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Game.Block.Blocks.CleanRoom.Machine.RecipeSelection;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.RecipeSelection;
using Game.Block.Blocks.Machine.State;
using Game.Block.Blocks.Machine.State.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    public class CleanRoomMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, IBlockSaveState, IMachineRecipeSelectorComponent
    {
        public Guid RecipeGuid => _processingState.RecipeGuid;
        public float RequestPower => _context.RequestPower;
        public float CurrentPower => _context.CurrentPower;
        public ProcessState CurrentState { get; private set; }
        public bool IsPolluting => CurrentState == ProcessState.Processing;

        // 停止中は0、稼働中は通常機械と同じ倍率（率の導出はcontextが一元管理する）
        // Halted requests no power and operating states share the normal machine multipliers; the context owns the derivation
        public float EffectiveRequestPower => _context.EffectiveRequestPower(CurrentState);

        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        private readonly MachineProcessContext _context;
        private readonly Dictionary<ProcessState, IMachineProcessState> _stateHandlers;
        private readonly ProcessingMachineProcessState _processingState;
        private readonly VanillaMachineModuleInventory _moduleInventory;

        // チップ抽選は実現出力の確定時に適用する。抽選カウンタもこのデコレータが持つ
        // The chip draw is applied when outputs are realized; the decorator owns the draw counter as well
        private readonly CleanRoomChipDrawDecorator _chipDrawDecorator;

        private CleanRoomEffect _cleanRoomEffect = new(false, 0, 0);
        private ProcessState _lastState = ProcessState.Idle;

        public CleanRoomMachineProcessorComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect)
        {
            _moduleInventory = module;
            CleanRoomMachineProcessorSaveState.Restore(componentStates, SaveKey, input, output, module, out var restoredState, out var remainingTicks, out var recipe, out var pendingOutputs, out var cycleCount, out var selectedRecipe);
            _context = new MachineProcessContext(input, output, effect, requestPower, idlePowerRate);
            _chipDrawDecorator = new CleanRoomChipDrawDecorator(blockInstanceId, cycleCount);
            _context.SetRealizedOutputDecorator(_chipDrawDecorator);
            _context.BindSelectedRecipe(selectedRecipe, CleanRoomChipOutputBindingUtil.BuildOutputBinding(selectedRecipe));
            CurrentState = restoredState;
            _processingState = new ProcessingMachineProcessState(_context, remainingTicks, recipe, pendingOutputs);
            _stateHandlers = new IMachineProcessState[]
                {
                    new IdleMachineProcessState(_context, _processingState),
                    _processingState,
                    new OutputBlockedMachineProcessState(_processingState),
                    new HaltedMachineProcessState(_processingState, () => _cleanRoomEffect.CanOperate),
                }.ToDictionary(handler => handler.State);
            // 初回GetBlockStateDetailsがUpdate前に呼ばれても妥当な値を返せるよう初期化する
            // Initialize so GetBlockStateDetails returns a sane value even if called before the first Update
            _context.RelatchPublishedRequestPower(CurrentState);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            return MachineStateDetailFactory.Create(_context, _processingState, CurrentState, _lastState);
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

        public void SetCleanRoomEffect(CleanRoomEffect effect)
        {
            BlockException.CheckDestroy(this);
            _cleanRoomEffect = effect;
            _chipDrawDecorator.SetCleanRoomEffect(effect);
        }

        // tick内限定の内部経路。供給率から導出済みの実効電力を受け取る
        // Tick-scoped internal path receiving the effective power already derived from the supply rate
        public void SupplyExternalPower(float power)
        {
            BlockException.CheckDestroy(this);
            _context.SuppliedPower += power;
            if (CurrentState == ProcessState.Idle) _changeState.OnNext(Unit.Default);
        }

        public string SaveKey { get; } = typeof(CleanRoomMachineProcessorComponent).FullName;

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var saveData = CleanRoomMachineProcessorSaveState.Build(_context.InputInventory, _context.OutputInventory, _moduleInventory, CurrentState, _processingState, _chipDrawDecorator.CycleCount, _context.SelectedRecipe);
            return JsonConvert.SerializeObject(saveData);
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            // 産出スロットの接続先への払い出しをここで駆動する（inventory自身のグローバル購読は廃止済み）
            // Drive output insertion into connected inventories here (the inventory's own global subscription was removed)
            _context.OutputInventory.InsertConnectInventory();
            // 直前tickの給電と同じ状態基準の要求電力を確定してから清浄室条件で状態遷移を判断する
            // Latch the previous tick's power and the matching request power before evaluating clean-room gated transitions
            _context.LatchTickPower(CurrentState);
            if (!_cleanRoomEffect.CanOperate && CurrentState != ProcessState.Halted)
            {
                ForceHaltedWithoutCompletingJob();
            }
            else
            {
                UpdateCurrentState();
            }
            // ステート変化時か処理中はイベントを発火させる
            // Fire the event on a state change or while processing
            if (_lastState != CurrentState || CurrentState == ProcessState.Processing)
            {
                _changeState.OnNext(Unit.Default);
                _lastState = CurrentState;
            }
            #region Internal
            void ForceHaltedWithoutCompletingJob()
            {
                // Processing.OnExitは出力払い出しを行うため、清浄室喪失時は呼ばずに凍結する
                // Processing.OnExit pays outputs, so a clean-room loss freezes without invoking it
                CurrentState = ProcessState.Halted;
                _stateHandlers[ProcessState.Halted].OnEnter();

                // Halted中はSupplyExternalPowerの再通知が無く恒久固着するため、突入時に分子分母を0で確定する
                // Halted never re-notifies via SupplyExternalPower, so pin both current and published power to zero on entry to avoid a permanent stale snapshot
                _context.PinPowerToZero();
            }
            void UpdateCurrentState()
            {
                var current = CurrentState;
                var nextState = _stateHandlers[current].GetNextUpdate();
                if (nextState == current) return;
                _stateHandlers[current].OnExit();
                CurrentState = nextState;
                // HaltedからProcessingへ戻る時だけ入力再消費と残tick初期化を避ける
                // Only Halted-to-Processing skips re-entering Processing to avoid re-consuming inputs and resetting ticks
                if (current == ProcessState.Halted && nextState == ProcessState.Processing) return;
                _stateHandlers[nextState].OnEnter();
            }
            #endregion
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }

        private MachineRecipeSelectionResult ChangeSelection(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
        {
            // 共通フロー（ジョブ返却→束縛差し替え→非束縛スロット返却）はutilへ委譲する
            // Delegate the shared flow (job refund, rebind, unbound-slot refund) to the util
            var result = MachineRecipeSelectionUtil.ApplyRecipeChange(_context, _processingState, recipe, CleanRoomChipOutputBindingUtil.BuildOutputBinding(recipe), refundOverflowInventory);
            if (result != MachineRecipeSelectionResult.Success) return result;

            // Halted含む非IdleはIdleへ戻し、次Updateで清浄室条件が再評価される
            // Non-Idle including Halted returns to Idle so the next Update re-evaluates clean-room conditions
            if (CurrentState != ProcessState.Idle) CurrentState = ProcessState.Idle;

            // 状態を書き換えたので、公開中の分母を新状態基準へ取り直してから通知する
            // The state was rewritten, so re-derive the published denominator on the new state before notifying
            _context.RelatchPublishedRequestPower(CurrentState);
            _changeState.OnNext(Unit.Default);
            return MachineRecipeSelectionResult.Success;
        }
    }
}
