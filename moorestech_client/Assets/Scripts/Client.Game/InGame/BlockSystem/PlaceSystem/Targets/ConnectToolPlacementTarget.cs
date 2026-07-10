namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class ConnectToolPlacementTarget : IPlacementTarget
    {
        // PlaceSystemMasterElement.PlaceModeConst のいずれか
        // One of PlaceSystemMasterElement.PlaceModeConst values
        public readonly string PlaceMode;

        public ConnectToolPlacementTarget(string placeMode)
        {
            PlaceMode = placeMode;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is ConnectToolPlacementTarget target && PlaceMode == target.PlaceMode;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => PlaceMode.GetHashCode();
    }
}
