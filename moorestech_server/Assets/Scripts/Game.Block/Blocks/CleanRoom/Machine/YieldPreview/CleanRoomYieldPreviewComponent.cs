using Core.Master;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     室内機械の選択工程について、次の抽選の最終の内訳と改善の差分を答える部品。抽選と同じ入力（選択レシピ・今の部屋の効果・装着モジュール）を読む
    ///     Answers the next draw's final odds and the improvement deltas for an interior machine's selected process, reading the draw's own inputs (selected recipe, current room effect, equipped modules)
    /// </summary>
    public class CleanRoomYieldPreviewComponent : IBlockComponent
    {
        private readonly IMachineRecipeSelectorComponent _recipeSelector;
        private readonly MachineModuleEffectComponent _moduleEffectComponent;
        private readonly ICleanRoomEffectLookup _effectLookup;
        private readonly IMachinePendingRealizationReader _pendingRealizationReader;

        internal CleanRoomYieldPreviewComponent(IMachineRecipeSelectorComponent recipeSelector, MachineModuleEffectComponent moduleEffectComponent, ICleanRoomEffectLookup effectLookup, IMachinePendingRealizationReader pendingRealizationReader)
        {
            _recipeSelector = recipeSelector;
            _moduleEffectComponent = moduleEffectComponent;
            _effectLookup = effectLookup;
            _pendingRealizationReader = pendingRealizationReader;
        }

        public CleanRoomYieldPreviewReport GetPreview()
        {
            BlockException.CheckDestroy(this);

            // 未選択と役割表に無いレシピは数字を持たない
            // Unselected recipes and recipes outside the role tables carry no numbers
            if (!_recipeSelector.TryGetSelectedRecipe(out var recipe)) return CleanRoomNoRoleYieldReport.Instance;
            if (!MasterHolder.CleanRoomMaster.TryGetRecipeRole(recipe.MachineRecipeGuid, out var role)) return CleanRoomNoRoleYieldReport.Instance;

            // 部屋の効果は抽選と同じ持ち主（デコレータ）の読み口から読む。止まっている効果はクラス値を持たないので数字も改善も作らない
            // Read the room effect through the draw owner's (decorator's) read view; a halted effect has no class values, so neither numbers nor improvements are built
            if (!_effectLookup.CurrentEffect.TryGetClassYield(out var classYield)) return CleanRoomNotOperableYieldReport.Instance;

            // 装着（上限で切った個数）の集計・次の抽選の率・今の最終の内訳はここで1回だけ出し、候補選びと改善の計算へ渡す
            // Aggregate the loadout (counts clipped to the limit), the next draw's rates and the current final odds once here, and hand them to candidate selection and the improvement calculation
            var equipped = _moduleEffectComponent.CollectEffectiveModules();
            var moduleEffect = MachineModuleEffect.Aggregate(equipped);
            var next = CleanRoomYieldPreview.Evaluate(role, classYield, moduleEffect, null, out _);
            var current = CleanRoomFinalOdds.Of(role, next);
            var candidates = CleanRoomQualityModuleCandidates.Select(_moduleEffectComponent, moduleEffect.HighestQualityModuleTier);
            var improvements = CleanRoomYieldImprovementCalculator.Compute(role, classYield, equipped, current, candidates);
            return new CleanRoomOddsYieldReport(current, improvements, next, FindPendingDraw());

            #region Internal

            // 待機中の確定結果が今の選択レシピのものなら、抽選に使った率を返す（C7 で外す）
            // Return the draw rates when the waiting realization belongs to the selected recipe (removed in C7)
            CleanRoomYieldPreviewResult FindPendingDraw()
            {
                if (!_pendingRealizationReader.TryGetPendingRealization(out var pending) || pending.Recipe != recipe) return null;
                return pending.DrawRecord as CleanRoomYieldPreviewResult;
            }

            #endregion
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
