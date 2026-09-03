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
using Game.Block.Blocks.Machine.State.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, IMachineRecipeSelectorComponent
    {
        public Guid RecipeGuid => _processingState.RecipeGuid;
        public float RequestPower => _context.RequestPower;
        public float CurrentPower => _context.CurrentPower;
        public ProcessState CurrentState { get; private set; }

        // 稼働状態に応じた要求電力率。歯車機械の要求トルク率もここから導出する
        // Requested power rate for the active state; the gear machine's requested torque rate also derives from here
        public float EffectiveRequestPowerRate => _context.EffectiveRequestPowerRate(CurrentState);

        public float EffectiveRequestPower => _context.EffectiveRequestPower(CurrentState);

        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        private readonly MachineProcessContext _context;
        private readonly Dictionary<ProcessState, IMachineProcessState> _stateHandlers;
        private readonly ProcessingMachineProcessState _processingState;

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
            _context = new MachineProcessContext(input, output, effect, requestPower, idlePowerRate);
            _context.BindSelectedRecipe(selectedRecipe, MachineRecipeSlotBindingUtil.BuildDefaultOutputBinding(selectedRecipe));
            // 加工状態を復元
            // Restore processing state
            CurrentState = currentState;
            _processingState = new ProcessingMachineProcessState(_context, remainingTicks, processingRecipe, pendingOutputs);

            // 加工中レシピか選択レシピのいずれかが欠けた加工中セーブ（出力詰まり含む）は破損データのためIdleへ戻す
            // A mid-job save (output blockage included) missing either the processing or selected recipe is corrupt, so fall back to Idle
            var isMidJob = CurrentState == ProcessState.Processing || CurrentState == ProcessState.OutputBlocked;
            if (isMidJob && (processingRecipe == null || selectedRecipe == null))
            {
                CurrentState = ProcessState.Idle;
            }

            _stateHandlers = new IMachineProcessState[]
                {
                    new IdleMachineProcessState(_context, _processingState),
                    _processingState,
                    new OutputBlockedMachineProcessState(_processingState),
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

        private MachineRecipeSelectionResult ChangeSelection(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
        {
            // 共通フロー（ジョブ返却→束縛差し替え→非束縛スロット返却）はutilへ委譲する
            // Delegate the shared flow (job refund, rebind, unbound-slot refund) to the util
            var result = MachineRecipeSelectionUtil.ApplyRecipeChange(_context, _processingState, recipe, MachineRecipeSlotBindingUtil.BuildDefaultOutputBinding(recipe), refundOverflowInventory);
            if (result != MachineRecipeSelectionResult.Success) return result;

            if (CurrentState != ProcessState.Idle) CurrentState = ProcessState.Idle;

            // 状態を書き換えたので、公開中の分母を新状態基準へ取り直してから通知する
            // The state was rewritten, so re-derive the published denominator on the new state before notifying
            _context.RelatchPublishedRequestPower(CurrentState);
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

            // 直前tickで蓄積された供給電力と、同じ状態基準の要求電力を同位置で確定する
            // Latch the power accumulated during the previous tick together with the request power on the same state basis
            _context.LatchTickPower(CurrentState);

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
