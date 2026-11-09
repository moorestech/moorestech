using Client.Ui.State.Text;

namespace Client.Ui.State.Tutorial.WorldPin
{
    /// <summary>
    /// ワールドピン 1 本の指示。画面内（OnScreen）と画面外（OffScreen）の判別 union
    /// One world pin cue: a discriminated union of OnScreen and OffScreen
    /// </summary>
    public abstract class WorldPinCue
    {
        public readonly string PinId;
        public readonly UiText Label;

        private WorldPinCue(string pinId, UiText label)
        {
            PinId = pinId;
            Label = label;
        }

        public sealed class OnScreen : WorldPinCue
        {
            public readonly float ScreenX;
            public readonly float ScreenY;

            public OnScreen(string pinId, UiText label, float screenX, float screenY) : base(pinId, label)
            {
                ScreenX = screenX;
                ScreenY = screenY;
            }
        }

        public sealed class OffScreen : WorldPinCue
        {
            public readonly float DirectionX;
            public readonly float DirectionY;

            public OffScreen(string pinId, UiText label, float directionX, float directionY) : base(pinId, label)
            {
                DirectionX = directionX;
                DirectionY = directionY;
            }
        }
    }
}
