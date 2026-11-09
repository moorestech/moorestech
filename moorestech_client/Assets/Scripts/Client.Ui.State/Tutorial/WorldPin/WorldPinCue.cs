using Client.Ui.State.Text;

namespace Client.Ui.State.Tutorial.WorldPin
{
    public sealed class WorldPinCue
    {
        public readonly string PinId;
        public readonly UiText Label;
        public readonly float ScreenX;
        public readonly float ScreenY;
        public readonly bool OnScreen;
        public readonly float DirectionX;
        public readonly float DirectionY;

        public WorldPinCue(string pinId, UiText label, float screenX, float screenY, bool onScreen, float directionX, float directionY)
        {
            PinId = pinId;
            Label = label;
            ScreenX = screenX;
            ScreenY = screenY;
            OnScreen = onScreen;
            DirectionX = directionX;
            DirectionY = directionY;
        }
    }
}
