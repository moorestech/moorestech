namespace Client.Game.InGame.BlockSystem.PlaceSystem.Empty
{
    public class EmptyPlaceSystem : IPlaceSystem
    {
        public void Enable() { }
        public void ManualUpdate(PlaceSystemUpdateContext context) { }
        public void Disable() { }
    }
}