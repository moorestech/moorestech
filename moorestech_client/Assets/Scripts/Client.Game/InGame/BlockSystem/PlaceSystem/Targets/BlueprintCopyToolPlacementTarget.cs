namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlueprintCopyToolPlacementTarget : IPlacementTarget
    {
        // 固有データを持たないため同型なら常に等値
        // Carries no data, so any two instances are equal
        public bool Equals(IPlacementTarget other) => other is BlueprintCopyToolPlacementTarget;

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => typeof(BlueprintCopyToolPlacementTarget).GetHashCode();
    }
}
