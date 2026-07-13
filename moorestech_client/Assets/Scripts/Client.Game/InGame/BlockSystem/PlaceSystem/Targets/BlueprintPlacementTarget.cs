namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlueprintPlacementTarget : IPlacementTarget
    {
        public readonly string BlueprintName;

        public BlueprintPlacementTarget(string blueprintName)
        {
            BlueprintName = blueprintName;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BlueprintPlacementTarget target && BlueprintName == target.BlueprintName;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => BlueprintName.GetHashCode();
    }
}
