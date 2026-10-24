using Core.Master.Validator.MachineUpgrade;
using Mooresmaster.Model.CleanRoomModule;

namespace Core.Master.CleanRoomRoles
{
    /// <summary>
    ///     ダイシング: 1枚を diesPerWafer 個に切り、損失を失敗ダイへ、残りをグレードのダイ表でレベル別の出力へ振り分ける役割
    ///     Dicing: the role that cuts one wafer into diesPerWafer dies, sends losses to failed dies and splits survivors by level with the grade's die table
    /// </summary>
    public sealed class DicingRecipeRole : CleanRoomRecipeRole
    {
        public DicingsElement Dicing { get; }

        // ダイのレベルファミリーの基準。書き換え器はここから各レベルの変種を引く
        // Base item of the die level family; the rewriter derives each level's variant from it
        public ItemId DieBaseItemId { get; }

        // ダイのレベル数。出力は [ダイLv1..LvN, 失敗ダイ] の順（起動時の検証が保証）なので、失敗ダイの出力の添字もこの値
        // Die level count; outputs run [die Lv1..LvN, failed die] (guaranteed by boot validation), so this is also the failed die's output index
        public int DieLevelCount { get; }

        internal DicingRecipeRole(DicingsElement dicing)
        {
            Dicing = dicing;
            DieBaseItemId = MasterHolder.ItemMaster.GetItemId(dicing.DieItemGuid);
            DieLevelCount = LevelInheritanceRecipeShape.GetFamilyLength(dicing.DieItemGuid);
        }
    }
}
