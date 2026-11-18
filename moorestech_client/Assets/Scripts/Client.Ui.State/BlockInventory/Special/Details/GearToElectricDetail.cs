using Client.Ui.State.BlockInventory.Logic.Detail;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 回転発電機の詳細（発電状態・供給電力・マスタの最大）
    /// GearToElectric generator detail: generation state, supplied power and the master maximum
    /// </summary>
    public sealed class GearToElectricDetail
    {
        public GearToElectricDisplayState State { get; }
        public double SuppliedPower { get; }
        public double MaxGeneratedPower { get; }

        public GearToElectricDetail(GearToElectricDisplayState state, double suppliedPower, double maxGeneratedPower)
        {
            State = state;
            SuppliedPower = suppliedPower;
            MaxGeneratedPower = maxGeneratedPower;
        }
    }
}
