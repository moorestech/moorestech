using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 歯車の区画の詳細を取り出す
    /// Selects the gear section's detail
    /// </summary>
    public sealed class GearDetailSelector : ISpecialDetailSelector<GearDetail>
    {
        public bool TryGet(SpecialBlockDetailViewModel detail, out GearDetail value) => detail.TryGetGear(out value);
    }
}
