using System;
using System.Collections.Generic;
using Client.WebUiHost.Common;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.WebUiHost.Game.Topics.MachineUpgrade
{
    /// <summary>
    ///     機械レシピのレベル継承をDTOへ組み立てる。継承元・継承先の変種と、今受け付ける最高レベルを返す
    ///     Builds a machine recipe's level inheritance for the DTO: source/target variants and the highest level accepted now
    /// </summary>
    public static class MachineRecipeLevelInheritanceDtoBuilder
    {
        public const string KindNone = "none";
        public const string KindInherit = "inherit";
        public const string KindInheritUpToUnlocked = "inheritUpToUnlocked";

        // 継承しないレシピの番号の wire 値（zod の契約。サーバー内部では番号の有無を TryGetShape で表す）
        // Wire value of the indices for a non-inheritance recipe (the zod contract; inside the server, TryGetShape says whether an index exists)
        private const int NoInheritanceWireIndex = -1;

        public static MachineRecipeLevelInheritanceDto Build(MachineRecipeMasterElement recipe, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            // マスタの値を wire の種類へ写すのはこの網羅 switch の1か所
            // This exhaustive switch is the only place mapping the master value to the wire kind
            switch (recipe.LevelInheritance)
            {
                case MachineRecipeMasterElement.LevelInheritanceConst.None:
                    return BuildNone();
                case MachineRecipeMasterElement.LevelInheritanceConst.Inherit:
                    return BuildInheritance(KindInherit);
                case MachineRecipeMasterElement.LevelInheritanceConst.InheritUpToUnlocked:
                    return BuildInheritance(KindInheritUpToUnlocked);
                default:
                    throw new InvalidOperationException($"未知の levelInheritance です recipe={recipe.MachineRecipeGuid} value={recipe.LevelInheritance}");
            }

            #region Internal

            MachineRecipeLevelInheritanceDto BuildNone()
            {
                // 継承しないレシピは番号-1・空リスト・0で一様に表す
                // A non-inheritance recipe is uniformly -1 indices, empty lists and 0
                return new MachineRecipeLevelInheritanceDto
                {
                    Kind = KindNone,
                    SourceInputIndex = NoInheritanceWireIndex,
                    SourceItemIds = new List<int>(),
                    TargetOutputIndex = NoInheritanceWireIndex,
                    TargetItemIds = new List<int>(),
                    MaxCraftableLevel = 0,
                };
            }

            MachineRecipeLevelInheritanceDto BuildInheritance(string kind)
            {
                // 上の switch で継承レシピと決めたのに形が引けなければ、switch と util の食い違いなので止める
                // If the shape is missing after the switch above decided on inheritance, the switch and the util disagree, so stop
                if (!MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape)) throw new InvalidOperationException($"継承レシピの形を引けません recipe={recipe.MachineRecipeGuid} levelInheritance={recipe.LevelInheritance}");

                // 上限つきは研究済みの製作上限、上限なしは継承元ファミリーの長さまで受け付ける（サーバーのゲートと同じ関数）
                // Capped recipes accept up to the researched craft cap, uncapped ones up to the source family length (the same function as the server gate)
                return new MachineRecipeLevelInheritanceDto
                {
                    Kind = kind,
                    SourceInputIndex = shape.SourceInputIndex,
                    SourceItemIds = WebUiItemIdListUtil.ToPrimitiveIds(shape.SourceVariants),
                    TargetOutputIndex = shape.TargetOutputIndex,
                    TargetItemIds = WebUiItemIdListUtil.ToPrimitiveIds(shape.TargetVariants),
                    MaxCraftableLevel = MachineRecipeLevelInheritanceUtil.GetMaxCraftableLevel(shape, craftCapLookup),
                };
            }

            #endregion
        }
    }

    public class MachineRecipeLevelInheritanceDto
    {
        public string Kind;
        public int SourceInputIndex;
        public List<int> SourceItemIds;
        public int TargetOutputIndex;
        public List<int> TargetItemIds;
        public int MaxCraftableLevel;
    }
}
