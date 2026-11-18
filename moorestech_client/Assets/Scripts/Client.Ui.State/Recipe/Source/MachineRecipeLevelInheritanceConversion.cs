using System;
using Client.Ui.State.Recipe.Data;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// マスタの levelInheritance を State の継承 union へ写す唯一の場所（ホストの DtoBuilder と同じ網羅 switch）
    /// The single place mapping the master levelInheritance onto the State union (same exhaustive switch as the host DtoBuilder)
    /// </summary>
    public static class MachineRecipeLevelInheritanceConversion
    {
        public static RecipeLevelInheritance Convert(MachineRecipeMasterElement recipe, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            switch (recipe.LevelInheritance)
            {
                case MachineRecipeMasterElement.LevelInheritanceConst.None:
                    return RecipeLevelInheritance.None;
                case MachineRecipeMasterElement.LevelInheritanceConst.Inherit:
                {
                    var shape = ReadShape();
                    return RecipeLevelInheritance.Inherit(shape.SourceInputIndex, shape.SourceVariants, shape.TargetOutputIndex, shape.TargetVariants, MachineRecipeLevelInheritanceUtil.GetMaxCraftableLevel(shape, craftCapLookup));
                }
                case MachineRecipeMasterElement.LevelInheritanceConst.InheritUpToUnlocked:
                {
                    var shape = ReadShape();
                    return RecipeLevelInheritance.InheritUpToUnlocked(shape.SourceInputIndex, shape.SourceVariants, shape.TargetOutputIndex, shape.TargetVariants, MachineRecipeLevelInheritanceUtil.GetMaxCraftableLevel(shape, craftCapLookup));
                }
                default:
                    throw new InvalidOperationException($"未知の levelInheritance です recipe={recipe.MachineRecipeGuid} value={recipe.LevelInheritance}");
            }

            #region Internal

            LevelInheritanceShape ReadShape()
            {
                // switch が継承と決めたのに形が引けなければ util との食い違いなので止める
                // Stop when the switch chose inheritance but the util has no shape: the two disagree
                if (!MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape)) throw new InvalidOperationException($"継承レシピの形を引けません recipe={recipe.MachineRecipeGuid}");
                return shape;
            }

            #endregion
        }
    }
}
