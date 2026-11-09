using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial;

namespace Client.Ui.State.Tutorial.WorldPin
{
    public sealed class WorldPinViewModel
    {
        public static readonly WorldPinViewModel Empty = new(Array.Empty<WorldPinCue>());

        public readonly IReadOnlyList<WorldPinCue> Pins;

        public WorldPinViewModel(IReadOnlyList<WorldPinCue> pins)
        {
            Pins = pins;
        }

        public static WorldPinViewModel From(WorldPinPresentationData data, bool blockingSkit)
        {
            // ピンは会話窓より上に来るので、blocking のスキット中は引っ込む（Web WorldPinOverlay）
            // Pins paint above the dialogue window, so they withdraw during a blocking skit (Web WorldPinOverlay)
            if (blockingSkit) return Empty;
            var pins = new List<WorldPinCue>(data.Pins.Length);
            foreach (var pin in data.Pins)
            {
                if (!TutorialTextFactory.TryCreate(pin.TutorialGuid, $"worldPin {pin.PinId}", out var label)) continue;
                pins.Add(new WorldPinCue(pin.PinId, label, pin.ScreenX, pin.ScreenY, pin.OnScreen, pin.DirectionX, pin.DirectionY));
            }
            return new WorldPinViewModel(pins);
        }
    }
}
