using Client.Ui.State.ModeHud;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// Web の placement と同じ値の表示状態
    /// Display states with the same values as Web placement
    /// </summary>
    public static class ModeHudViewModelSamples
    {
        public static ModeHudViewModel Placement() => new ModeHudPlacement(ModeHudText.Of("Assembler"), 3);
        public static ModeHudViewModel Delete(int height) => new ModeHudDelete(height);
    }
}
