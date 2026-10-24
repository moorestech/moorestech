using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.MachineUpgrade
{
    /// <summary>
    ///     レベル継承レシピの形（継承元の入力番号・継承先の出力番号・両ファミリーの変種列・上限の有無）。継承しないレシピには作らない
    ///     Shape of a level-inheritance recipe: source input index, target output index, both families' variants and whether it is capped; never built for a non-inheritance recipe
    /// </summary>
    public readonly struct LevelInheritanceShape
    {
        public readonly int SourceInputIndex;
        public readonly int TargetOutputIndex;

        // レベル順の変種列（添字0が Lv1）
        // Level-ordered variants (index 0 is Lv1)
        public readonly IReadOnlyList<ItemId> SourceVariants;
        public readonly IReadOnlyList<ItemId> TargetVariants;

        // 継承先ファミリーの基準アイテムGUID。製作上限の鍵にもなる
        // Base item GUID of the target family; also the craft cap key
        public readonly Guid TargetBaseItemGuid;

        // 研究で上げる製作上限を見るか（InheritUpToUnlocked）
        // Whether the research-raised craft cap applies (InheritUpToUnlocked)
        public readonly bool HasCraftCap;

        public int MaxLevel => SourceVariants.Count;

        // 継承レシピにだけ作るので、作れるのは同じアセンブリの MachineRecipeLevelInheritanceUtil.TryGetShape だけにする
        // Built only for inheritance recipes, so only MachineRecipeLevelInheritanceUtil.TryGetShape in this assembly may construct it
        internal LevelInheritanceShape(int sourceInputIndex, int targetOutputIndex, IReadOnlyList<ItemId> sourceVariants, IReadOnlyList<ItemId> targetVariants, Guid targetBaseItemGuid, bool hasCraftCap)
        {
            SourceInputIndex = sourceInputIndex;
            TargetOutputIndex = targetOutputIndex;
            SourceVariants = sourceVariants;
            TargetVariants = targetVariants;
            TargetBaseItemGuid = targetBaseItemGuid;
            HasCraftCap = hasCraftCap;
        }

        // 継承元ファミリーでのレベル（1始まり）。継承元の変種でなければ null
        // Level within the source family (1-based); null when the item is not a source variant
        public int? ResolveSourceLevel(ItemId itemId)
        {
            for (var i = 0; i < SourceVariants.Count; i++)
            {
                if (SourceVariants[i] == itemId) return i + 1;
            }
            return null;
        }

        public ItemId GetSourceVariant(int level)
        {
            return SourceVariants[level - 1];
        }

        public ItemId GetTargetVariant(int level)
        {
            return TargetVariants[level - 1];
        }
    }
}
