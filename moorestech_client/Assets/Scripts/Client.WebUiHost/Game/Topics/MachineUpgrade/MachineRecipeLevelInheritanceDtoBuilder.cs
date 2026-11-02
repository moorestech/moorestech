using System;
using System.Collections.Generic;
using Client.WebUiHost.Common;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.WebUiHost.Game.Topics.MachineUpgrade
{
    /// <summary>
    ///     - 継承元・継承先の変種を返す
    ///     - 受け付け可能な最高レベルを返す
    ///     - Returns source and target variants and the highest accepted level
    /// </summary>
    public static class MachineRecipeLevelInheritanceDtoBuilder
    {
        public const string KindNone = "none";
        public const string KindInherit = "inherit";
        public const string KindInheritUpToUnlocked = "inheritUpToUnlocked";

        public static MachineRecipeLevelInheritanceDto Build(MachineRecipeMasterElement recipe, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            // マスタの値を wire の種類へ写すのはこの網羅 switch の1か所
            // This exhaustive switch is the only place mapping the master value to the wire kind
            switch (recipe.LevelInheritance)
            {
                case MachineRecipeMasterElement.LevelInheritanceConst.None:
                    return MachineRecipeLevelInheritanceDto.CreateNone();
                case MachineRecipeMasterElement.LevelInheritanceConst.Inherit:
                {
                    var (sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel) = ReadInheritance();
                    return MachineRecipeLevelInheritanceDto.CreateInherit(sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel);
                }
                case MachineRecipeMasterElement.LevelInheritanceConst.InheritUpToUnlocked:
                {
                    var (sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel) = ReadInheritance();
                    return MachineRecipeLevelInheritanceDto.CreateInheritUpToUnlocked(sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel);
                }
                default:
                    throw new InvalidOperationException($"未知の levelInheritance です recipe={recipe.MachineRecipeGuid} value={recipe.LevelInheritance}");
            }

            #region Internal

            (int sourceInputIndex, List<int> sourceItemIds, int targetOutputIndex, List<int> targetItemIds, int maxCraftableLevel) ReadInheritance()
            {
                // 上の switch で継承レシピと決めたのに形が引けなければ、switch と util の食い違いなので止める
                // If the shape is missing after the switch above decided on inheritance, the switch and the util disagree, so stop
                if (!MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape)) throw new InvalidOperationException($"継承レシピの形を引けません recipe={recipe.MachineRecipeGuid} levelInheritance={recipe.LevelInheritance}");

                // 上限つきは研究済みの製作上限、上限なしは継承元ファミリーの長さまで受け付ける（サーバーのゲートと同じ関数）
                // Capped recipes accept up to the researched craft cap, uncapped ones up to the source family length (the same function as the server gate)
                return (shape.SourceInputIndex, WebUiItemIdListUtil.ToPrimitiveIds(shape.SourceVariants), shape.TargetOutputIndex, WebUiItemIdListUtil.ToPrimitiveIds(shape.TargetVariants), MachineRecipeLevelInheritanceUtil.GetMaxCraftableLevel(shape, craftCapLookup));
            }

            #endregion
        }
    }

    /// <summary>
    ///     レシピのレベル継承の wire。組み立ては種類ごとの factory だけで、継承しないレシピは番号-1・空リスト・0で一様に表す
    ///     Wire for a recipe's level inheritance; built only through per-kind factories, a non-inheritance recipe being uniformly -1 indices, empty lists and 0
    /// </summary>
    public class MachineRecipeLevelInheritanceDto
    {
        // 継承しないレシピの番号の wire 値（zod の契約。サーバー内部では番号の有無を TryGetShape で表す）
        // Wire value of the indices for a non-inheritance recipe (the zod contract; inside the server, TryGetShape says whether an index exists)
        private const int NoInheritanceWireIndex = -1;

        public readonly string Kind;
        public readonly int SourceInputIndex;
        public readonly IReadOnlyList<int> SourceItemIds;
        public readonly int TargetOutputIndex;
        public readonly IReadOnlyList<int> TargetItemIds;
        public readonly int MaxCraftableLevel;

        private MachineRecipeLevelInheritanceDto(string kind, int sourceInputIndex, IReadOnlyList<int> sourceItemIds, int targetOutputIndex, IReadOnlyList<int> targetItemIds, int maxCraftableLevel)
        {
            Kind = kind;
            SourceInputIndex = sourceInputIndex;
            SourceItemIds = sourceItemIds;
            TargetOutputIndex = targetOutputIndex;
            TargetItemIds = targetItemIds;
            MaxCraftableLevel = maxCraftableLevel;
        }

        public static MachineRecipeLevelInheritanceDto CreateNone()
        {
            return new MachineRecipeLevelInheritanceDto(MachineRecipeLevelInheritanceDtoBuilder.KindNone, NoInheritanceWireIndex, new List<int>(), NoInheritanceWireIndex, new List<int>(), 0);
        }

        public static MachineRecipeLevelInheritanceDto CreateInherit(int sourceInputIndex, IReadOnlyList<int> sourceItemIds, int targetOutputIndex, IReadOnlyList<int> targetItemIds, int maxCraftableLevel)
        {
            return new MachineRecipeLevelInheritanceDto(MachineRecipeLevelInheritanceDtoBuilder.KindInherit, sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel);
        }

        public static MachineRecipeLevelInheritanceDto CreateInheritUpToUnlocked(int sourceInputIndex, IReadOnlyList<int> sourceItemIds, int targetOutputIndex, IReadOnlyList<int> targetItemIds, int maxCraftableLevel)
        {
            return new MachineRecipeLevelInheritanceDto(MachineRecipeLevelInheritanceDtoBuilder.KindInheritUpToUnlocked, sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel);
        }
    }
}
