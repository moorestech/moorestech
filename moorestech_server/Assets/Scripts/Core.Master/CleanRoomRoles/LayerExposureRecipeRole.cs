using Mooresmaster.Model.CleanRoomModule;

namespace Core.Master.CleanRoomRoles
{
    /// <summary>
    ///     EUV: 層の成否を1回引き、成功なら入力と同じグレードの次層ウェハ、失敗なら失敗パターンウェハだけを出す役割
    ///     EUV: the role that rolls the layer once, yielding the next-layer wafer at the input's grade on success or only the failed pattern wafer on failure
    /// </summary>
    public sealed class LayerExposureRecipeRole : CleanRoomRecipeRole
    {
        public LayerExposuresElement Exposure { get; }

        // 次層ウェハ（継承先）と失敗パターンウェハの出力の添字
        // Output indices of the layered wafer (the inheritance target) and the failed pattern wafer
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
