using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 歯車網の区画の詳細を取り出す
    /// Selects the gear-network section's detail
    /// </summary>
    public sealed class GearNetworkDetailSelector : ISpecialDetailSelector<GearNetworkDetail>
    {
        public bool TryGet(SpecialBlockDetailViewModel detail, out GearNetworkDetail value) => detail.TryGetGearNetwork(out value);
    }
}
