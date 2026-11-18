using System.Collections.Generic;
using Client.Game.InGame.Tutorial;

namespace Client.Ui.State.Tutorial.WorldPin
{
    /// <summary>
    /// ピンの演出データを ViewModel にする。GUID が読めないピンは 1 回だけ LogError して以後は黙って外す
    /// Projects pin presentation data into a view model; a pin with an unreadable GUID logs once and is then dropped without repeating
    /// </summary>
    public sealed class WorldPinViewModelProjection
    {
        private readonly TutorialTextFactory _textFactory = new();

        public WorldPinViewModel Project(WorldPinPresentationData data, bool blockingSkit)
        {
            // ピンは会話窓より上に来るので、blocking のスキット中は引っ込む（Web WorldPinOverlay）
            // Pins paint above the dialogue window, so they withdraw during a blocking skit (Web WorldPinOverlay)
            if (blockingSkit) return WorldPinViewModel.Empty;
            var pins = new List<WorldPinCue>(data.Pins.Length);
            foreach (var pin in data.Pins)
            {
                // 読めない GUID のピンは外す（LogError は TutorialTextFactory が GUID ごとに 1 回）
                // Drop a pin with an unreadable GUID (TutorialTextFactory logs once per GUID)
                if (!_textFactory.TryCreate(pin.TutorialGuid, $"worldPin {pin.PinId}", out var label)) continue;
                if (pin.OnScreen) pins.Add(new WorldPinCue.OnScreen(pin.PinId, label, pin.ScreenX, pin.ScreenY));
                else pins.Add(new WorldPinCue.OffScreen(pin.PinId, label, pin.DirectionX, pin.DirectionY));
            }
            return new WorldPinViewModel(pins);
        }
    }
}
