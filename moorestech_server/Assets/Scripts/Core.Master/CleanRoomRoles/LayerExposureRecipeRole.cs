using Mooresmaster.Model.CleanRoomModule;

namespace Core.Master.CleanRoomRoles
{
    /// <summary>
    ///     EUV: 層の成否を1回引き、成功なら次層ウェハ、失敗なら失敗パターンウェハだけを出す役割
    ///     EUV: the role that rolls the layer once, yielding the next-layer wafer on success or only the failed pattern wafer on failure
    /// </summary>
    public sealed class LayerExposureRecipeRole : CleanRoomRecipeRole
    {
        public LayerExposuresElement Exposure { get; }

        // 成否で個数を0にする出力の添字
        // Output indices zeroed out by the outcome
        public int LayeredOutputIndex { get; }
        public int FailedOutputIndex { get; }

        internal LayerExposureRecipeRole(LayerExposuresElement exposure)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(exposure.MachineRecipeGuid);
            Exposure = exposure;
            LayeredOutputIndex = IndexOfOutput(recipe, exposure.LayeredWaferItemGuid);
            FailedOutputIndex = IndexOfOutput(recipe, exposure.FailedWaferItemGuid);
        }
    }
}
