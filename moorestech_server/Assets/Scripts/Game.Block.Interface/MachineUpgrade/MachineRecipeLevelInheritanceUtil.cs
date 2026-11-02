using System;
using System.Runtime.CompilerServices;
using Core.Master;
using Core.Master.Validator.MachineUpgrade;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Interface.MachineUpgrade
{
    /// <summary>
    ///     機械レシピの levelInheritance を解釈する。継承レシピの形と、入力・出力を継承レベルの変種へ写す処理を1か所に置く
    ///     Interprets a machine recipe's levelInheritance; keeps the inheritance shape and the mapping of inputs/outputs to the inherited level's variant in one place
    /// </summary>
    public static class MachineRecipeLevelInheritanceUtil
    {
        private static readonly object ShapeLock = new();
        private static ItemMaster _shapeItemMaster;
        private static ConditionalWeakTable<MachineRecipeMasterElement, StrongBox<LevelInheritanceShape>> _shapes = new();

        // 継承レシピなら形を返す。継承しないレシピは false
        // Returns the shape for an inheritance recipe; false for a non-inheritance recipe
        public static bool TryGetShape(MachineRecipeMasterElement recipe, out LevelInheritanceShape shape)
        {
            // サーバーと表示側の同時初回参照を直列化する
            // Serialize concurrent first reads from server and presentation
            lock (ShapeLock)
            {
                // マスタの再ロードで以前の変種配列を捨てる
                // Drop old variant arrays when the master reloads
                if (!ReferenceEquals(_shapeItemMaster, MasterHolder.ItemMaster))
                {
                    _shapeItemMaster = MasterHolder.ItemMaster;
                    _shapes = new ConditionalWeakTable<MachineRecipeMasterElement, StrongBox<LevelInheritanceShape>>();
                }
                if (_shapes.TryGetValue(recipe, out var cached))
                {
                    shape = cached.Value;
                    return true;
                }
                switch (recipe.LevelInheritance)
                {
                    case MachineRecipeMasterElement.LevelInheritanceConst.None:
                        shape = default;
                        return false;
                    case MachineRecipeMasterElement.LevelInheritanceConst.Inherit:
                        shape = BuildShape(recipe, false);
                        _shapes.Add(recipe, new StrongBox<LevelInheritanceShape>(shape));
                        return true;
                    case MachineRecipeMasterElement.LevelInheritanceConst.InheritUpToUnlocked:
                        shape = BuildShape(recipe, true);
                        _shapes.Add(recipe, new StrongBox<LevelInheritanceShape>(shape));
                        return true;
                    default:
                        throw new InvalidOperationException($"未知の levelInheritance です recipe={recipe.MachineRecipeGuid} value={recipe.LevelInheritance}");
                }
            }

            #region Internal

            static LevelInheritanceShape BuildShape(MachineRecipeMasterElement recipe, bool hasCraftCap)
            {
                // 継承元・継承先の探索は検証と同じ LevelInheritanceRecipeShape に委ねる。ちょうど1つずつあることは起動時の検証が保証するので、無ければ検証漏れとして止める
                // Finding the source and target is delegated to the same LevelInheritanceRecipeShape as validation; boot validation guarantees exactly one of each, so a miss is a validation gap and stops here
                if (LevelInheritanceRecipeShape.FindSingleSourceInputIndex(recipe) is not int sourceIndex) throw new InvalidOperationException($"継承レシピの継承元の入力がちょうど1つではありません recipe={recipe.MachineRecipeGuid}");
                if (LevelInheritanceRecipeShape.FindSingleTargetOutputIndex(recipe) is not int targetIndex) throw new InvalidOperationException($"継承レシピの継承先の出力がちょうど1つではありません recipe={recipe.MachineRecipeGuid}");

                var sourceBaseItemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[sourceIndex].ItemGuid);
                var targetBaseGuid = recipe.OutputItems[targetIndex].ItemGuid;
                var targetBaseItemId = MasterHolder.ItemMaster.GetItemId(targetBaseGuid);
                return new LevelInheritanceShape(sourceIndex, targetIndex,
                    MasterHolder.ItemMaster.GetLevelVariants(sourceBaseItemId), MasterHolder.ItemMaster.GetLevelVariants(targetBaseItemId),
                    targetBaseGuid, hasCraftCap);
            }

            #endregion
        }

        // 加工ジョブの継承レベルがレシピと合うか。継承レシピは 1..最大レベル、継承しないレシピは値なし
        // Whether a job's inherited level fits the recipe: 1..max for an inheritance recipe, no value otherwise
        public static bool IsValidInheritedLevel(MachineRecipeMasterElement recipe, int? inheritedLevel)
        {
            if (!TryGetShape(recipe, out var shape)) return !inheritedLevel.HasValue;
            return inheritedLevel.HasValue && 1 <= inheritedLevel.Value && inheritedLevel.Value <= shape.MaxLevel;
        }

        // 継承元として今入れてよい最高レベル。上限なしは最大レベル、上限つきは製作上限との小さい方
        // Highest source level allowed now: the max level when uncapped, otherwise the smaller of it and the craft cap
        public static int GetMaxCraftableLevel(LevelInheritanceShape shape, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            if (!shape.HasCraftCap) return shape.MaxLevel;
            return Math.Min(shape.MaxLevel, craftCapLookup.GetCraftCap(shape.TargetBaseItemGuid));
        }

        // 入力 inputIndex のアイテム。継承元なら継承レベルの変種、それ以外は書いたまま
        // Item of input inputIndex: the inherited level's variant for the source, otherwise as written
        public static ItemId ResolveInputItemId(MachineRecipeMasterElement recipe, int inputIndex, int? inheritedLevel)
        {
            if (TryGetShape(recipe, out var shape) && shape.SourceInputIndex == inputIndex) return shape.GetSourceVariant(RequireLevel(recipe, inheritedLevel));
            return MasterHolder.ItemMaster.GetItemId(recipe.InputItems[inputIndex].ItemGuid);
        }

        // 出力 outputIndex のアイテム。継承先なら継承レベルの変種、それ以外は書いたまま
        // Item of output outputIndex: the inherited level's variant for the target, otherwise as written
        public static ItemId ResolveOutputItemId(MachineRecipeMasterElement recipe, int outputIndex, int? inheritedLevel)
        {
            if (TryGetShape(recipe, out var shape) && shape.TargetOutputIndex == outputIndex) return shape.GetTargetVariant(RequireLevel(recipe, inheritedLevel));
            return MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[outputIndex].ItemGuid);
        }

        private static int RequireLevel(MachineRecipeMasterElement recipe, int? inheritedLevel)
        {
            // 継承レシピのジョブは生成時（入力からの解決・セーブからの復元）に必ず値を持つので、無ければ呼び出し側の誤り
            // An inheritance job always gets its level when created (resolved from input or restored from save), so a missing one is a caller bug
            if (!inheritedLevel.HasValue) throw new InvalidOperationException($"継承レシピのジョブに継承レベルがありません recipe={recipe.MachineRecipeGuid}");
            return inheritedLevel.Value;
        }


    }
}
