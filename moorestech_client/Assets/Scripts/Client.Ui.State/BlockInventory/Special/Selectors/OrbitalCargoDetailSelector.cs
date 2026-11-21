using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 発射台・宇宙エレベーター拠点の積荷の詳細を取り出す（どちらでもなければ取れない）。積荷の本文の Presenter が一覧のモードを決めるのに使う
    /// Selects the cargo detail of a launch pad or space elevator base (none for other kinds); the cargo body presenter uses it to decide the list mode
    /// </summary>
    public sealed class OrbitalCargoDetailSelector : ISpecialDetailSelector<OrbitalCargoDetail>
    {
        public bool TryGet(SpecialBlockDetailViewModel detail, out OrbitalCargoDetail value)
        {
            if (detail.TryGetRocketLaunchPad(out var rocket))
            {
                value = rocket.Cargo;
                return true;
            }
            if (detail.TryGetSpaceElevator(out var elevator))
            {
                value = elevator.Cargo;
                return true;
            }
            value = null;
            return false;
        }
    }
}
