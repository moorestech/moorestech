namespace Client.Game.InGame.BlockSystem.PlaceSystem.Empty
{
    public class EmptyPlaceSystem : IPlaceSystem
    {
        public bool OwnsWheelInput => false;

        public void Enable() { }
        public void ManualUpdate(PlaceSystemUpdateContext context) { }
        public void Disable() { }
        public bool TryCancelInProgressOperation() => false;
    }
}