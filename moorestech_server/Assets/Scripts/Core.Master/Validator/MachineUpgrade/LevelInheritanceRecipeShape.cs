using System;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator.MachineUpgrade
{
    /// <summary>
    ///     生のマスタだけで継承レシピの形（levelFamily の基準を指す入力・出力とファミリー長）を引く。起動時の検証と実行時の MachineRecipeLevelInheritanceUtil が共有する
    ///     Reads an inheritance recipe's shape (level-family base inputs/outputs and family lengths) from the raw master only; shared by boot validation and the runtime MachineRecipeLevelInheritanceUtil
    /// </summary>
    public static class LevelInheritanceRecipeShape
    {
        // levelFamily の基準アイテムか（変種や存在しないGUIDは false）
        // Whether the item is a level-family base (variants and unknown GUIDs are false)
        public static bool IsLevelFamilyBase(Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid);
            return itemId.HasValue && MasterHolder.ItemMaster.HasLevelFamily(itemId.Value);
        }

        // 基準を指す入力・出力の数（検証のエラー文が使う）
        // Number of base inputs/outputs (used by validation error messages)
        public static int CountFamilyBaseInputs(MachineRecipeMasterElement recipe)
        {
            var count = 0;
            foreach (var input in recipe.InputItems) if (IsLevelFamilyBase(input.ItemGuid)) count++;
            return count;
        }

        public static int CountFamilyBaseOutputs(MachineRecipeMasterElement recipe)
        {
            var count = 0;
            foreach (var output in recipe.OutputItems) if (IsLevelFamilyBase(output.ItemGuid)) count++;
            return count;
        }

        // 基準を指す入力がちょうど1つならその番号。0個か2個以上なら null
        // Index of the base input when there is exactly one; null for none or several
        public static int? FindSingleSourceInputIndex(MachineRecipeMasterElement recipe)
        {
            int? found = null;
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                if (!IsLevelFamilyBase(recipe.InputItems[i].ItemGuid)) continue;
                if (found.HasValue) return null;
                found = i;
            }
            return found;
        }

        // 基準を指す出力がちょうど1つならその番号。0個か2個以上なら null
        // Index of the base output when there is exactly one; null for none or several
        public static int? FindSingleTargetOutputIndex(MachineRecipeMasterElement recipe)
        {
            int? found = null;
            for (var i = 0; i < recipe.OutputItems.Length; i++)
            {
                if (!IsLevelFamilyBase(recipe.OutputItems[i].ItemGuid)) continue;
                if (found.HasValue) return null;
                found = i;
            }
            return found;
        }

        // 継承元ファミリーの長さ（グレード数）。基準の入力がちょうど1つでなければ null（Part B の役割表の検証も使う）
        // Length of the source family (grade count); null unless there is exactly one base input (Part B's role-table validation uses it too)
        public static int? FindSourceFamilyLength(MachineRecipeMasterElement recipe)
        {
            if (FindSingleSourceInputIndex(recipe) is not int sourceIndex) return null;
            return GetFamilyLength(recipe.InputItems[sourceIndex].ItemGuid);
        }

        // 継承先の基準アイテムGUID（製作上限の鍵）。基準の出力がちょうど1つでなければ null
        // Base item GUID of the target (the craft cap key); null unless there is exactly one base output
        public static Guid? FindTargetBaseGuid(MachineRecipeMasterElement recipe)
        {
            if (FindSingleTargetOutputIndex(recipe) is not int targetIndex) return null;
            return recipe.OutputItems[targetIndex].ItemGuid;
        }

        public static int GetFamilyLength(Guid baseItemGuid)
        {
            return MasterHolder.ItemMaster.GetLevelVariants(MasterHolder.ItemMaster.GetItemId(baseItemGuid)).Count;
        }
    }
}
