using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     室内機械の選択工程について、次の抽選に使う値と出力待ちの確定済み抽選に使った値を答える部品。抽選と同じ入力（選択レシピ・今の部屋の効果・装着モジュールの集計）を読む
    ///     Answers the values the next draw of an interior machine's selected process will use and those a fixed draw waiting for output used, reading the draw's own inputs (selected recipe, current room effect, module aggregate)
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

            // 未選択は役割が無い。部屋の効果は抽選と同じ持ち主（デコレータ）の値を読み口から読む
            // Unselected means no role; read the room effect through the read view of the draw's own owner (the decorator)
            if (!_recipeSelector.TryGetSelectedRecipe(out var recipe)) return new CleanRoomYieldPreviewReport(NoRolePreview.Instance, null);
            var next = CleanRoomYieldPreview.Compute(recipe, _effectLookup.CurrentEffect, _moduleEffectComponent.AggregateCurrent());
            return new CleanRoomYieldPreviewReport(next, FindPendingDraw());

            #region Internal

            // 待機中の確定結果が今の選択レシピのものなら、抽選に使った率を返す。レシピを替えた直後の1tickは次の待機で確定し直すので出さない
            // Return the draw rates when the waiting realization belongs to the selected recipe; right after a recipe change it is re-realized on the next idle tick, so it is not shown
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
