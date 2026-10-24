using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     室内機械の選択工程について、次の抽選に使う値を答える部品。抽選と同じ入力（選択レシピ・今の部屋の効果・装着モジュールの集計）を読む
    ///     Answers the values the next draw of an interior machine's selected process will use, reading the draw's own inputs (selected recipe, current room effect, module aggregate)
    /// </summary>
    public class CleanRoomYieldPreviewComponent : IBlockComponent
    {
        private readonly IMachineRecipeSelectorComponent _recipeSelector;
        private readonly MachineModuleEffectComponent _moduleEffectComponent;
        private readonly ICleanRoomEffectLookup _effectLookup;

        internal CleanRoomYieldPreviewComponent(IMachineRecipeSelectorComponent recipeSelector, MachineModuleEffectComponent moduleEffectComponent, ICleanRoomEffectLookup effectLookup)
        {
            _recipeSelector = recipeSelector;
            _moduleEffectComponent = moduleEffectComponent;
            _effectLookup = effectLookup;
        }

        public CleanRoomYieldPreviewResult GetPreview()
        {
            BlockException.CheckDestroy(this);

            // 未選択は役割が無い。部屋の効果は抽選と同じ持ち主（デコレータ）の値を読み口から読む
            // Unselected means no role; read the room effect through the read view of the draw's own owner (the decorator)
            if (!_recipeSelector.TryGetSelectedRecipe(out var recipe)) return NoRolePreview.Instance;
            return CleanRoomYieldPreview.Compute(recipe, _effectLookup.CurrentEffect, _moduleEffectComponent.AggregateCurrent());
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
