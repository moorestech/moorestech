namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    ///     設置対象から今フレームの設置系を選ぶ役割。状態コントローラが具体の設置系群を知らずに済ませるための境界
    ///     Picks this frame's place system from the target; the boundary that keeps the state controller ignorant of the concrete systems
    /// </summary>
    public interface IPlaceSystemSelector
    {
        // 何も選ばれていないときに滞在する設置系
        // The place system occupied while nothing is selected
        IPlaceSystem EmptyPlaceSystem { get; }

        IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context);
    }
}
