using Core.Master.Validator.MachineUpgrade;
using Mooresmaster.Model.CleanRoomModule;

namespace Core.Master.CleanRoomRoles
{
    /// <summary>
    ///     成膜: グレード付きウェハの出力を、抽選したグレードの変種へ差し替える役割
    ///     Deposition: the role that swaps the graded-wafer output for the variant of the drawn grade
    /// </summary>
    public sealed class WaferGradeRecipeRole : CleanRoomRecipeRole
    {
        public WaferGradeDrawsElement Draw { get; }

        // グレード数は変種数
        // The grade count is the variant count
        public int GradedOutputIndex { get; }
        public ItemId GradedWaferBaseItemId { get; }
        public int GradeCount { get; }

        internal WaferGradeRecipeRole(WaferGradeDrawsElement draw)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(draw.MachineRecipeGuid);
            Draw = draw;
            GradedOutputIndex = IndexOfOutput(recipe, draw.GradedWaferItemGuid);
            GradedWaferBaseItemId = MasterHolder.ItemMaster.GetItemId(draw.GradedWaferItemGuid);
            GradeCount = LevelInheritanceRecipeShape.GetFamilyLength(draw.GradedWaferItemGuid);
        }
    }
}
