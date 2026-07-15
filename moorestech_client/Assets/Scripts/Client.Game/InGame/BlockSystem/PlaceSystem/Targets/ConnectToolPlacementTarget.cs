using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class ConnectToolPlacementTarget : IPlacementTarget
    {
        public readonly ConnectToolType ToolType;

        public ConnectToolPlacementTarget(ConnectToolType toolType)
        {
            ToolType = toolType;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is ConnectToolPlacementTarget target && ToolType == target.ToolType;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => ToolType.GetHashCode();
    }
}
