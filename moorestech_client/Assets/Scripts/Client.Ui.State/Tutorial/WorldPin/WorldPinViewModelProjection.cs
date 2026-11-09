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
        private readonly HashSet<string> _invalidGuids = new();

        public WorldPinViewModel Project(WorldPinPresentationData data, bool blockingSkit)
        {
            // ピンは会話窓より上に来るので、blocking のスキット中は引っ込む（Web WorldPinOverlay）
            // Pins paint above the dialogue window, so they withdraw during a blocking skit (Web WorldPinOverlay)
            if (blockingSkit) return WorldPinViewModel.Empty;
            var pins = new List<WorldPinCue>(data.Pins.Length);
            foreach (var pin in data.Pins)
            {
                // silent-ok: 読めないと分かった GUID は LogError を出し直さない（毎フレーム埋まるため） / 同じ GUID は初回に TutorialTextFactory が LogError を出した
                if (_invalidGuids.Contains(pin.TutorialGuid)) continue;
                if (!TutorialTextFactory.TryCreate(pin.TutorialGuid, $"worldPin {pin.PinId}", out var label))
                {
                    _invalidGuids.Add(pin.TutorialGuid);
                    continue;
                }
                if (pin.OnScreen) pins.Add(new WorldPinCue.OnScreen(pin.PinId, label, pin.ScreenX, pin.ScreenY));
                else pins.Add(new WorldPinCue.OffScreen(pin.PinId, label, pin.DirectionX, pin.DirectionY));
            }
            return new WorldPinViewModel(pins);
        }
    }
}
