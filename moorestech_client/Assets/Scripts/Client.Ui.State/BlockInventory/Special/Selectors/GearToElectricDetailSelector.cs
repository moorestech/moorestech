using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 回転発電機の区画の詳細を取り出す
    /// Selects the gear-to-electric section's detail
    /// </summary>
    public sealed class GearToElectricDetailSelector : ISpecialDetailSelector<GearToElectricDetail>
    {
        public bool TryGet(SpecialBlockDetailViewModel detail, out GearToElectricDetail value) => detail.TryGetGearToElectric(out value);
    }
}
