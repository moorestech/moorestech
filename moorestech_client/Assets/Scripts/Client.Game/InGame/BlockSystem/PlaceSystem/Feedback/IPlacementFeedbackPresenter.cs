namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     1フレーム分の理由行を表示面へ反映する役割。状態コントローラが表示実体を知らずに済ませるための境界
    ///     Reflects one frame's reason lines onto the view; the boundary that keeps the state controller ignorant of the view
    /// </summary>
    public interface IPlacementFeedbackPresenter
    {
        void Present(PlacementFeedback feedback);

        void Hide();
    }
}
