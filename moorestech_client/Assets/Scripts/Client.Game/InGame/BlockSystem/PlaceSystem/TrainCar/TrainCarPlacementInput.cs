using Client.Input;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlacementInput : ITrainCarPlacementInput
    {
        public bool IsPlaceTriggered()
        {
            return InputManager.Playable.ScreenLeftClick.GetKeyUp;
        }
    }
}

