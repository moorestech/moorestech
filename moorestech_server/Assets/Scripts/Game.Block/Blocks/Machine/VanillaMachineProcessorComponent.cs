using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Core.Master;
using Game.Block.Blocks.Machine.State.Save;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
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
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, IMachineRecipeSelectorComponent, IMachineProcessStateReader
    {
        public Guid RecipeGuid => _processingState.RecipeGuid;
        public float RequestPower => _context.RequestPower;
        public float CurrentPower => _context.CurrentPower;
        public ProcessState CurrentState { get; private set; }
        // 稼働状態に応じた要求電力率。歯車機械の要求トルク率もここから導出する
        // Requested power rate for the active state; the gear machine's requested torque rate also derives from here
        public float EffectiveRequestPowerRate => _context.EffectiveRequestPowerRate(CurrentState);
        public float EffectiveRequestPower => _context.EffectiveRequestPower(CurrentState);
        public IObservable<Unit> OnChangeBlockState => _statePublisher.OnChangeBlockState;
        private readonly MachineStateChangePublisher _statePublisher = new();
        private readonly MachineProcessContext _context;
        private readonly Dictionary<ProcessState, IMachineProcessState> _stateHandlers;
        private readonly ProcessingMachineProcessState _processingState;
        private readonly IdleMachineProcessState _idleState;
        // 新規作成
        // For new creation
        public VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect)
            : this(input, output, effect, requestPower, idlePowerRate, ProcessState.Idle, null, null)
        {
        }
        // セーブからの復元
        // For restoration from save
        public VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, ProcessState currentState, MachineProcessingJob processingJob, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect, MachineRecipeMasterElement selectedRecipe)
            : this(input, output, effect, requestPower, idlePowerRate, currentState, processingJob, selectedRecipe)
        {
        }
        private VanillaMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, MachineModuleEffectComponent effect, float requestPower, float idlePowerRate, ProcessState currentState, MachineProcessingJob processingJob, MachineRecipeMasterElement selectedRecipe)
        {
            _context = new MachineProcessContext(input, output, effect, requestPower, idlePowerRate);
            _context.BindSelectedRecipe(selectedRecipe, MachineRecipeSlotBindingUtil.BuildDefaultOutputBinding(selectedRecipe));
            // 加工中レシピか選択レシピが欠けた加工中セーブ（出力詰まり含む）は破損のため、ジョブも捨ててIdleへ戻す
            // A mid-job save (output blockage included) missing either the processing or selected recipe is corrupt, so drop the job too and fall back to Idle
            var isMidJob = currentState == ProcessState.Processing || currentState == ProcessState.OutputBlocked;
            var isCorrupt = isMidJob && (processingJob == null || selectedRecipe == null);
            if (isCorrupt) UnityEngine.Debug.LogWarning($"[VanillaMachineProcessorComponent] 加工中セーブのジョブか選択レシピが欠けているためIdleへ戻します 状態:{currentState}");
            processingJob = isCorrupt ? null : processingJob;
            CurrentState = isCorrupt ? ProcessState.Idle : currentState;
            _processingState = new ProcessingMachineProcessState(_context, processingJob);
            _idleState = new IdleMachineProcessState(_context, _processingState);
            // Idleに留まったままの空き不足の変化は、tick内で判定が確定した時点の状態通知にする
            // An output-space change while staying idle becomes the tick's state notice once the check settles
            _idleState.OnOutputSpaceWaitChanged.Subscribe(_ => _statePublisher.PublishTickState(CurrentState));
            _stateHandlers = new IMachineProcessState[]
                {
                    _idleState,
                    _processingState,
                    new OutputBlockedMachineProcessState(_processingState),
                }.ToDictionary(handler => handler.State);
            // 初回GetBlockStateDetailsがUpdate前に呼ばれても妥当な値を返せるよう初期化する
            // Initialize so GetBlockStateDetails returns a sane value even if called before the first Update
            _context.RelatchPublishedRequestPower(CurrentState);
        }
        internal void RestorePendingRealization(MachineRealizationSaveJsonObject saved)
        {
            if (saved?.DrawRecord != null) throw new InvalidOperationException("通常機械の確定結果に未対応の抽選記録があります");
            _idleState.RestorePending(saved?.Restore(null, _context.EffectComponent));
        }
        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            return MachineStateDetailFactory.Create(_context, _processingState, _idleState, CurrentState, _statePublisher.LastState);
        }
        public Guid SelectedRecipeGuid => _context.SelectedRecipe?.MachineRecipeGuid ?? Guid.Empty;
        public bool TryGetSelectedRecipe(out MachineRecipeMasterElement recipe) => (recipe = _context.SelectedRecipe) != null;
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
            // 状態を書き換えたので、公開中の分母と出力空き不足を新しい選択で取り直してから通知する（旧レシピの理由を残さない）
            // The state was rewritten, so re-derive the published denominator and output-space reason under the new selection before notifying
            _context.RelatchPublishedRequestPower(CurrentState);
            _idleState.RefreshOutputSpaceWait();
            _statePublisher.Publish();
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
            if (CurrentState == ProcessState.Idle) _statePublisher.Publish();
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
            // 状態変化時と加工中に通知（Idle中の空き不足変化はIdleの通知が運ぶ）
            // Notify on a state change or while processing (an idle wait change rides the idle state's notice)
            _statePublisher.PublishIfStateChanged(CurrentState);
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
                TotalTicks = _processingState.TotalTicks,
                PowerMultiplier = _processingState.PowerMultiplier,
                ConsumedInputItemGuids = _processingState.ConsumedInputItemIds?.Select(id => MasterHolder.ItemMaster.GetItemMaster(id).ItemGuid.ToString()).ToList(),
                PendingRealization = MachineRealizationSaveJsonObject.Capture(_idleState, null),
                State = (int)CurrentState,
                RemainingSeconds = GameUpdater.TicksToSeconds(_processingState.RemainingTicks),
                RecipeGuidStr = RecipeGuid.ToString(),
                PendingOutputs = _processingState.PendingOutputs?.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                SelectedRecipeGuidStr = _context.SelectedRecipe?.MachineRecipeGuid.ToString(),
                InheritedLevel = _processingState.InheritedLevel,
            };
        }
    }
}
