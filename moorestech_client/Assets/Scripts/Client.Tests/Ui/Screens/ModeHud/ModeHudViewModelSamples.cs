using Client.Ui.State.ModeHud;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// Web の mock-host の topic シナリオ（placement）と同じ値の表示状態。テストは View.Render へ直に渡す
    /// Display states with the same values as the Web mock-host topic scenario (placement); tests pass them straight to View.Render
    /// </summary>
    public static class ModeHudViewModelSamples
    {
        public static ModeHudViewModel Placement() => new ModeHudPlacement(ModeHudText.Of("Assembler"), 3);
        public static ModeHudViewModel Delete(int height) => new ModeHudDelete(height);
    }
}
