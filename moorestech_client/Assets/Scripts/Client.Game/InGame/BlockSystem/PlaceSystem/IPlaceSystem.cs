using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IPlaceSystem
    {
        public void Enable();

        public void ManualUpdate(PlaceSystemUpdateContext context);

        public void Disable();
    }

    public readonly struct PlaceSystemUpdateContext
    {
        // 設置対象（null = 未選択）。具体型を知るのはSelectorと各システムのみ
        // The placement target (null = nothing selected); only the selector and each system know concrete types
        public readonly IPlacementTarget Target;
        public readonly bool IsSelectionChanged;

        public PlaceSystemUpdateContext(IPlacementTarget target, bool isSelectionChanged)
        {
            Target = target;
            IsSelectionChanged = isSelectionChanged;
        }
    }
}
