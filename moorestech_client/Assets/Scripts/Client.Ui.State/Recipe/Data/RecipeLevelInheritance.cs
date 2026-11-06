using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.Recipe.Data
{
    /// <summary>
    /// レシピのレベル継承（Web の "none"・"inherit"・"inheritUpToUnlocked"）。継承するときだけ番号と並びを持つ union
    /// A recipe's level inheritance (Web "none", "inherit", "inheritUpToUnlocked"); a union where only inheriting carries indices and families
    /// </summary>
    public abstract class RecipeLevelInheritance
    {
        public static readonly RecipeLevelInheritance None = new NoLevelInheritance();

        private protected RecipeLevelInheritance()
        {
        }

        public static RecipeLevelInheritance Inherit(int sourceInputIndex, IReadOnlyList<ItemId> sourceItemIds, int targetOutputIndex, IReadOnlyList<ItemId> targetItemIds, int maxCraftableLevel)
        {
            return new LevelInheritanceMapping(false, sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel);
        }

        public static RecipeLevelInheritance InheritUpToUnlocked(int sourceInputIndex, IReadOnlyList<ItemId> sourceItemIds, int targetOutputIndex, IReadOnlyList<ItemId> targetItemIds, int maxCraftableLevel)
        {
            return new LevelInheritanceMapping(true, sourceInputIndex, sourceItemIds, targetOutputIndex, targetItemIds, maxCraftableLevel);
        }
    }

    // 継承しない。番号も並びも持たない
    // Does not inherit; carries no indices or families
    public sealed class NoLevelInheritance : RecipeLevelInheritance
    {
        internal NoLevelInheritance()
        {
        }
    }

    // 継承元の入力のレベルを継承先の出力へ写す
    // Maps the level of a source input onto a target output
    public sealed class LevelInheritanceMapping : RecipeLevelInheritance
    {
        // true は研究済みの上限まで（Web の inheritUpToUnlocked）、false は上限なし（inherit）
        // True caps at the researched level (Web inheritUpToUnlocked); false is uncapped (inherit)
        public bool CappedAtUnlockedLevel { get; }
        public int SourceInputIndex { get; }
        public IReadOnlyList<ItemId> SourceItemIds { get; }
        public int TargetOutputIndex { get; }
        public IReadOnlyList<ItemId> TargetItemIds { get; }

        // 今作れる最高レベル（上限つきは研究済みの上限、上限なしは継承元の並びの長さ）
        // Highest craftable level now (the researched cap when capped, the source family length otherwise)
        public int MaxCraftableLevel { get; }

        internal LevelInheritanceMapping(bool cappedAtUnlockedLevel, int sourceInputIndex, IReadOnlyList<ItemId> sourceItemIds, int targetOutputIndex, IReadOnlyList<ItemId> targetItemIds, int maxCraftableLevel)
        {
            CappedAtUnlockedLevel = cappedAtUnlockedLevel;
            SourceInputIndex = sourceInputIndex;
            SourceItemIds = sourceItemIds;
            TargetOutputIndex = targetOutputIndex;
            TargetItemIds = targetItemIds;
            MaxCraftableLevel = maxCraftableLevel;
        }
    }
}
