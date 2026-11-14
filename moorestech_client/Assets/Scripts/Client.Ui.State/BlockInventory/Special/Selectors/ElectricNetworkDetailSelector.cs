using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 電力網の区画の詳細を取り出す
    /// Selects the electric-network section's detail
    /// </summary>
    public sealed class ElectricNetworkDetailSelector : ISpecialDetailSelector<ElectricNetworkDetail>
    {
        public bool TryGet(SpecialBlockDetailViewModel detail, out ElectricNetworkDetail value) => detail.TryGetElectricNetwork(out value);
    }
}
