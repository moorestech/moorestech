using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    ///     設置対象とその由来の組。UI遷移をまたいでも両者が離れないよう1値で運ぶ
    ///     A placement target paired with its origin, carried as one value so they cannot separate across a UI transition
    /// </summary>
    public class PlacementSelection
    {
        public readonly IPlacementTarget Target;
        public readonly PlacementOrigin Origin;

        public PlacementSelection(IPlacementTarget target, PlacementOrigin origin)
        {
            Target = target;
            Origin = origin;
        }
    }
}
